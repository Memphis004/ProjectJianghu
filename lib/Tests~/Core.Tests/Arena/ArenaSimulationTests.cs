using System;
using System.Collections.Generic;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Domain.Character;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Core.Infrastructure.Config;
using ProjectJianghu.Core.Simulation.Arena;
using Xunit;

namespace ProjectJianghu.Core.Tests.Arena
{
    /// <summary>
    /// The rules of the real simulation: the damage pipeline golden, the positional
    /// bonuses, the counter, the knockback stop, the jump i-frame, the telegraph, a status
    /// and the endings.
    /// </summary>
    /// <remarks>
    /// Every expected number below is derived by hand from the pipeline in
    /// [ADR 0114](../../../../docs/adr/0114-arena-simulation-tick-resolve-and-damage.md) §4
    /// and the authored <c>constant</c> row (mitigation K 1,000, back +50 %, flank +15 %),
    /// not copied from a run: a golden that was read off the code would accept a regression.
    /// </remarks>
    public sealed class ArenaSimulationTests
    {
        // ---------------------------------------------------------------- damage

        /// <summary>
        /// 1,400 flat + 50 power × 45 % = 1,422 raw; mitigation 100 defense / K 1,000 keeps
        /// 1,422 × 1,000 / 1,100 = 1,292. The position decides the multiplier.
        /// </summary>
        [Theory]
        [InlineData(1, 1, IsoDir.NW, 1292)]
        [InlineData(2, 3, IsoDir.NW, 1486)]
        [InlineData(3, 3, IsoDir.NW, 1939)]
        public void Damage_pipeline_is_power_mastery_position_crit_mitigation(
            int attackerX, int attackerY, IsoDir defenderFacing, int expectedDamage)
        {
            ArenaFighterSetup player = Fighter(0, 0, true, new GridCoord(attackerX, attackerY));
            player.Stats.ExternalPower = 50;
            player.Skills = new[] { Skill(101, 1400, ratioX100: 4500) };

            ArenaFighterSetup enemy = Fighter(1, 1, false, new GridCoord(2, 2), defenderFacing);
            enemy.Stats.Defense = 100;

            using IArenaSession session = Create(Match(player, enemy));
            RunToRunning(session);

            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, 0)));
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Skill(0, 0, new GridCoord(2, 2))));

            Assert.Equal(5000 - expectedDamage, Find(session, 1).Hp);
        }

        [Fact]
        public void A_critical_hit_multiplies_before_mitigation()
        {
            ArenaFighterSetup player = Fighter(0, 0, true, new GridCoord(1, 1));
            player.Stats.ExternalPower = 50;
            player.Stats.CritChanceX100 = 10000;
            player.Skills = new[] { Skill(101, 1400, ratioX100: 4500) };

            ArenaFighterSetup enemy = Fighter(1, 1, false, new GridCoord(2, 2), IsoDir.NW);
            enemy.Stats.Defense = 100;

            using IArenaSession session = Create(Match(player, enemy));
            RunToRunning(session);

            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, 0)));
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Skill(0, 0, new GridCoord(2, 2))));

            // 1,422 × 1.5 = 2,133 raw; × 1,000 / 1,100 = 1,939.
            Assert.Equal(5000 - 1939, Find(session, 1).Hp);
        }

        // ---------------------------------------------------------------- counter

        [Fact]
        public void A_counter_hits_back_with_the_defenders_first_skill()
        {
            ArenaFighterSetup player = Fighter(0, 0, true, new GridCoord(1, 1));
            player.Skills = new[] { Skill(101, 1400) };

            ArenaFighterSetup enemy = Fighter(1, 1, false, new GridCoord(2, 2), IsoDir.NW);
            enemy.Stats.CounterChanceX100 = 10000;
            enemy.AiReactionTicks = 1000;
            enemy.Skills = new[] { Skill(202, 300) };

            using IArenaSession session = Create(Match(player, enemy));
            RunToRunning(session);

            uint cursor = 0;
            Drain(session, ref cursor);

            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, 0)));
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Skill(0, 0, new GridCoord(2, 2))));

            List<ArenaEvent> events = Read(session, ref cursor);
            ArenaEvent countered = Assert.Single(events, e => e.Type == ArenaEventType.Countered);

            Assert.Equal((byte)1, countered.Source);
            Assert.Equal((byte)0, countered.Target);
            Assert.Equal(300, countered.Value);
            Assert.Equal(5000 - 300, Find(session, 0).Hp);
        }

        // ---------------------------------------------------------------- knockback

        [Fact]
        public void Knockback_stops_at_a_blocked_tile()
        {
            ArenaFighterSetup player = Fighter(0, 0, true, new GridCoord(1, 1));
            player.Stats.ExternalPower = 0;
            player.Skills = new[] { Skill(101, 1000, knockback: 2) };

            ArenaFighterSetup enemy = Fighter(1, 1, false, new GridCoord(2, 2), IsoDir.NW);

            using IArenaSession session = Create(Match(player, enemy));
            RunToRunning(session);

            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, 0)));
            uint cursor = 0;
            Drain(session, ref cursor);
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Skill(0, 0, new GridCoord(2, 2))));

            List<ArenaEvent> events = Read(session, ref cursor);
            ArenaEvent pushed = Assert.Single(events, e => e.Type == ArenaEventType.KnockedBack);

            // Away from the caster is +x here; (3,2) is walkable and (4,2) is a wall on the
            // authored map.open_court mask, so the second tile of the push never happens.
            Assert.Equal(1, pushed.Value);
            Assert.Equal(new GridCoord(3, 2), pushed.Tile);
            Assert.Equal(new GridCoord(3, 2), Find(session, 1).Tile);
        }

        // ---------------------------------------------------------------- jump

        [Fact]
        public void A_jumping_fighter_is_invulnerable_while_airborne()
        {
            (string outcome, FighterView enemyAtResolve) = CastAtAJumpingEnemy(windUpTicks: 6, landing: new GridCoord(11, 1));

            Assert.Equal("evaded", outcome);

            // Still in the air: its tile never moved, so the miss is the i-frame and not a
            // positional dodge.
            Assert.Equal(new GridCoord(4, 1), enemyAtResolve.Tile);
            Assert.Equal(5000, enemyAtResolve.Hp);
        }

        [Fact]
        public void Landing_outside_the_telegraph_is_a_dodge()
        {
            (string outcome, FighterView enemyAtResolve) = CastAtAJumpingEnemy(windUpTicks: 14, landing: new GridCoord(11, 1));

            Assert.Equal("evaded", outcome);
            Assert.Equal(new GridCoord(11, 1), enemyAtResolve.Tile);
            Assert.Equal(5000, enemyAtResolve.Hp);
        }

        // ---------------------------------------------------------------- area

        [Fact]
        public void The_area_that_was_previewed_is_the_area_that_is_hit()
        {
            ArenaFighterSetup player = Fighter(0, 0, true, new GridCoord(1, 1));
            player.Skills = new[] { Skill(101, 700, rangeMax: 3, area: AreaShape.Diamond, areaSize: 2) };

            ArenaFighterSetup first = Fighter(1, 1, false, new GridCoord(3, 1));
            ArenaFighterSetup second = Fighter(2, 1, false, new GridCoord(2, 2));

            using IArenaSession session = Create(Match(player, first, second));
            RunToRunning(session);

            // Aimed at the first enemy; the diamond reaches the second one two tiles away,
            // which is the case a single-tile skill could not show.
            var aim = new GridCoord(3, 1);
            var preview = new GridCoord[ArenaConstants.MaxGridTiles];
            int previewCount = session.Query.PreviewSkillArea(0, 0, aim, preview);

            uint cursor = 0;
            Drain(session, ref cursor);
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, 0)));
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Skill(0, 0, aim)));

            List<ArenaEvent> events = Read(session, ref cursor);
            var hits = new List<ArenaEvent>();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == ArenaEventType.Hit)
                {
                    hits.Add(events[i]);
                }
            }

            Assert.Equal(2, hits.Count);
            for (int i = 0; i < hits.Count; i++)
            {
                bool inside = false;
                for (int tile = 0; tile < previewCount; tile++)
                {
                    if (preview[tile] == hits[i].Tile)
                    {
                        inside = true;
                        break;
                    }
                }

                Assert.True(inside, "a hit landed outside the previewed area");
            }

            Assert.True(Find(session, 1).Hp < 5000);
            Assert.True(Find(session, 2).Hp < 5000);
        }

        /// <summary>
        /// A Line skill's size comes from <c>skill.area_size</c> (ADR 0113: total tiles,
        /// anchor included), and the line grows away from the caster, so a size of 2 aimed
        /// at the near enemy reaches the one behind it.
        /// </summary>
        [Fact]
        public void A_line_skill_covers_every_tile_away_from_the_caster()
        {
            ArenaFighterSetup player = Fighter(0, 0, true, new GridCoord(1, 1));
            player.Skills = new[] { Skill(101, 1000, rangeMax: 3, area: AreaShape.Line, areaSize: 2) };

            ArenaFighterSetup near = Fighter(1, 1, false, new GridCoord(2, 1), IsoDir.NW);
            ArenaFighterSetup far = Fighter(2, 1, false, new GridCoord(3, 1), IsoDir.NW);

            using IArenaSession session = Create(Match(player, near, far));
            RunToRunning(session);

            uint cursor = 0;
            Drain(session, ref cursor);
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, 0)));
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Skill(0, 0, new GridCoord(2, 1))));

            List<ArenaEvent> events = Read(session, ref cursor);
            var hits = new List<ArenaEvent>();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == ArenaEventType.Hit)
                {
                    hits.Add(events[i]);
                }
            }

            Assert.Equal(2, hits.Count);
            Assert.Equal(4000, Find(session, 1).Hp);
            Assert.Equal(4000, Find(session, 2).Hp);
        }

        /// <summary>
        /// <c>skill.hits</c> is how many separate blows one cast lands: every blow is its own
        /// accuracy, crit and mitigation, while the cast still counts as one use for mastery.
        /// </summary>
        [Fact]
        public void A_two_hit_skill_rolls_every_blow_and_counts_one_use()
        {
            ArenaFighterSetup player = Fighter(0, 0, true, new GridCoord(1, 1));
            player.Skills = new[] { Skill(101, 1000, hits: 2) };

            ArenaFighterSetup enemy = Fighter(1, 1, false, new GridCoord(2, 2), IsoDir.NW);

            using IArenaSession session = Create(Match(player, enemy));
            RunToRunning(session);

            uint cursor = 0;
            Drain(session, ref cursor);
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, 0)));
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Skill(0, 0, new GridCoord(2, 2))));

            List<ArenaEvent> events = Read(session, ref cursor);
            int landed = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == ArenaEventType.Hit)
                {
                    landed++;
                }
            }

            Assert.Equal(2, landed);
            Assert.Equal(5000 - 2000, Find(session, 1).Hp);

            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevKillEnemies, 0)));
            Assert.True(session.TryGetResult(out ArenaMatchResult result));

            SkillUseStat use = Assert.Single(result.Fighters[0].SkillUses);
            Assert.Equal(1, use.Uses);
            Assert.Equal(2, use.Hits);
        }

        // ---------------------------------------------------------------- statuses

        [Fact]
        public void The_acupoint_seal_stops_the_victims_atb_bar()
        {
            int sealId = StatGrowth.StableId("status.acupoint_seal");

            ArenaFighterSetup player = Fighter(0, 0, true, new GridCoord(1, 1));
            player.Skills = new[] { Skill(101, 100, statusId: sealId, statusChanceX100: 10000) };

            ArenaFighterSetup enemy = Fighter(1, 1, false, new GridCoord(2, 2), IsoDir.NW);
            enemy.Stats.AtbFillTicks = 100;
            enemy.AiReactionTicks = 1000;

            using IArenaSession session = Create(Match(player, enemy));
            RunToRunning(session);

            // Let the victim bank some ATB first, so "it stopped" is measurable.
            for (int i = 0; i < 5; i++)
            {
                session.Step();
            }

            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, 0)));
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Skill(0, 0, new GridCoord(2, 2))));

            FighterView sealedEnemy = Find(session, 1);
            Assert.NotEqual(0, sealedEnemy.StatusMask);
            int frozenAtb = sealedEnemy.Atb;

            for (int i = 0; i < 20; i++)
            {
                session.Step();
            }

            FighterView later = Find(session, 1);
            Assert.True(frozenAtb > 0, "the victim had no ATB to freeze");
            Assert.Equal(frozenAtb, later.Atb);
            Assert.NotEqual(0, later.StatusMask);
        }

        // ---------------------------------------------------------------- endings

        [Fact]
        public void Timeout_reports_the_hp_ratio_and_the_duration()
        {
            ArenaFighterSetup player = Fighter(0, 0, true, new GridCoord(1, 1));
            player.StartHp = 800;

            ArenaFighterSetup enemy = Fighter(1, 1, false, new GridCoord(2, 2));
            enemy.StartHp = 400;

            ArenaMatchSetup setup = Match(player, enemy);
            setup.TimeLimitTicks = 4;

            using IArenaSession session = Create(setup);
            RunToRunning(session);

            int guard = 0;
            while (session.Phase != ArenaPhase.Finished && guard++ < 50)
            {
                session.Step();
            }

            Assert.True(session.TryGetResult(out ArenaMatchResult result));
            Assert.Equal(ArenaOutcome.Victory, result.Outcome);
            Assert.Equal(ArenaEndReason.TimeoutHpRatio, result.Reason);
            Assert.Equal(4, result.DurationTicks);
            Assert.Equal(2, result.Fighters.Count);
            Assert.Equal(800, result.Fighters[0].FinalHp);
            Assert.Equal(400, result.Fighters[1].FinalHp);
        }

        [Fact]
        public void The_auto_resolver_finishes_inside_its_budget()
        {
            ArenaFighterSetup player = Fighter(0, 0, true, new GridCoord(1, 1));
            player.Stats.MaxHp = 900;
            player.Stats.AtbFillTicks = 10;
            player.Skills = new[] { Skill(101, 300, rangeMax: 30) };

            ArenaFighterSetup enemy = Fighter(1, 1, false, new GridCoord(4, 1));
            enemy.Stats.MaxHp = 900;
            enemy.Stats.AtbFillTicks = 10;
            enemy.Skills = new[] { Skill(202, 250, rangeMax: 30) };

            var factory = new ArenaSimulationFactory(Config);
            var resolver = new ArenaAutoResolver(factory);

            ArenaMatchResult result = resolver.Resolve(Match(player, enemy), maxTicks: 600);

            Assert.NotNull(result);
            Assert.True(result.DurationTicks <= 600);
            Assert.NotEqual(ArenaConstants.NoFighter, result.WinnerTeam);
            Assert.Equal(2, result.Fighters.Count);
        }

        [Fact]
        public void Event_ring_overflow_does_not_change_real_simulation_state()
        {
            LubanGameConfig config = Config;
            AiProfileConfig profile = config.AiProfiles[0];
            profile.DecisionDelayTicks = 0;

            ArenaFighterSetup player = Fighter(0, 0, true, new GridCoord(1, 1));
            player.Stats.AtbFillTicks = 1;
            ArenaFighterSetup enemy = Fighter(1, 1, false, new GridCoord(4, 1));
            enemy.AiProfileId = StatGrowth.StableId(profile.Id);
            enemy.Stats.AtbFillTicks = 1;
            enemy.Stats.MaxHp = 1_000_000;
            enemy.StartHp = 1_000_000;
            ArenaMatchSetup setup = Match(player, enemy);

            var factory = new ArenaSimulationFactory(config, new MeditateHook());
            using IArenaSession reading = factory.Create(setup);
            using IArenaSession control = factory.Create(setup);
            for (int i = 0; i < 800; i++)
            {
                reading.Step();
                control.Step();
            }

            var buffer = new ArenaEvent[64];
            uint cursor = 0;
            reading.Events.Read(ref cursor, buffer, out int dropped);

            Assert.True(dropped > 0, "the simulation did not generate enough events to overflow its ring");
            Assert.Equal(control.ComputeStateHash(), reading.ComputeStateHash());
            Assert.Equal(control.Phase, reading.Phase);
        }

        [Fact]
        public void Utility_ai_uses_a_legal_available_skill()
        {
            ArenaMatchSetup setup = Match(
                Fighter(0, 0, true, new GridCoord(1, 1)),
                Fighter(1, 1, false, new GridCoord(2, 1)));
            setup.Fighters[0].Stats.AtbFillTicks = 1000;
            setup.Fighters[1].Stats.AtbFillTicks = 1;
            setup.Fighters[1].AiProfileId = StatGrowth.StableId("ai.melee");
            setup.Fighters[1].Skills = new[] { Skill(202, 50) };

            (int readyTick, int commandTick, ArenaCommandKind kind) = WaitForAiCommand(setup, 1);

            Assert.True(commandTick > readyTick);
            Assert.Equal(ArenaCommandKind.UseSkill, kind);
        }

        [Fact]
        public void Decisiveness_and_power_rank_reduce_profile_reaction_delay()
        {
            ArenaMatchSetup baseline = CreateDelaySetup(powerRank: 1, decisiveness: 0);
            ArenaMatchSetup experienced = CreateDelaySetup(powerRank: 9, decisiveness: 100);

            (int baselineReady, int baselineCommand, _) = WaitForAiCommand(baseline, 1);
            (int experiencedReady, int experiencedCommand, _) = WaitForAiCommand(experienced, 1);

            Assert.True(baselineCommand - baselineReady > experiencedCommand - experiencedReady,
                "higher decisiveness and power rank should shorten the configured AI delay");
        }

        [Fact]
        public void Evader_jumps_to_a_tile_outside_an_incoming_telegraph()
        {
            var player = Fighter(0, 0, true, new GridCoord(1, 1));
            player.Stats.AtbFillTicks = 1000;
            player.Skills = new[] { Skill(101, 100, windUp: 20) };

            var evader = Fighter(1, 1, false, new GridCoord(4, 1), IsoDir.NW);
            evader.AiProfileId = StatGrowth.StableId("ai.evader");
            evader.Stats.AtbFillTicks = 1;
            evader.Stats.JumpRange = 7;

            using IArenaSession session = new ArenaSimulationFactory(Config).Create(Match(player, evader));
            RunToRunning(session);
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, 0)));
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Skill(0, 0, evader.StartTile)));

            uint cursor = 0;
            var buffer = new ArenaEvent[128];
            bool jumped = false;
            bool evaded = false;
            for (int tick = 0; tick < 40 && !evaded; tick++)
            {
                session.Step();
                int count;
                do
                {
                    count = session.Events.Read(ref cursor, buffer, out _);
                    for (int i = 0; i < count; i++)
                    {
                        ArenaEvent arenaEvent = buffer[i];
                        if (arenaEvent.Type == ArenaEventType.JumpStarted && arenaEvent.Source == 1)
                        {
                            jumped = true;
                            Assert.NotEqual(evader.StartTile, arenaEvent.Tile);
                        }

                        if (arenaEvent.Type == ArenaEventType.Evaded && arenaEvent.Target == 1)
                        {
                            evaded = true;
                        }
                    }
                }
                while (count > 0);
            }

            Assert.True(jumped, "the evader did not leave the telegraphed tile");
            Assert.True(evaded, "the incoming skill did not resolve as evaded");
            Assert.Equal(5000, Find(session, 1).Hp);
        }

        // ---------------------------------------------------------------- helpers

        private static LubanGameConfig Config => ConfigFixture.Load();

        private static ArenaMatchSetup CreateDelaySetup(int powerRank, int decisiveness)
        {
            ArenaMatchSetup setup = Match(
                Fighter(0, 0, true, new GridCoord(1, 1)),
                Fighter(1, 1, false, new GridCoord(2, 1)));
            setup.Fighters[0].Stats.AtbFillTicks = 1000;
            setup.Fighters[1].Stats.AtbFillTicks = 1;
            setup.Fighters[1].AiProfileId = StatGrowth.StableId("ai.melee");
            setup.Fighters[1].PowerRank = powerRank;
            setup.Fighters[1].Decisiveness = decisiveness;
            return setup;
        }

        private static (int ReadyTick, int CommandTick, ArenaCommandKind Kind) WaitForAiCommand(
            ArenaMatchSetup setup, byte aiFighter)
        {
            using IArenaSession session = new ArenaSimulationFactory(Config).Create(setup);
            RunToRunning(session);

            uint cursor = 0;
            var buffer = new ArenaEvent[128];
            int readyTick = -1;
            for (int tick = 0; tick < 300; tick++)
            {
                session.Step();
                int count;
                do
                {
                    count = session.Events.Read(ref cursor, buffer, out _);
                    for (int i = 0; i < count; i++)
                    {
                        ArenaEvent arenaEvent = buffer[i];
                        if (arenaEvent.Type == ArenaEventType.AtbReady && arenaEvent.Source == aiFighter)
                        {
                            readyTick = arenaEvent.Tick;
                        }

                        if (arenaEvent.Type == ArenaEventType.CommandAccepted && arenaEvent.Source == aiFighter)
                        {
                            return (readyTick, arenaEvent.Tick, (ArenaCommandKind)arenaEvent.Value);
                        }
                    }
                }
                while (count > 0);
            }

            throw new InvalidOperationException("AI did not issue a command within the test budget.");
        }

        private static (string Outcome, FighterView Enemy) CastAtAJumpingEnemy(int windUpTicks, GridCoord landing)
        {
            ArenaFighterSetup player = Fighter(0, 0, true, new GridCoord(1, 1));
            player.Skills = new[] { Skill(101, 900, windUp: windUpTicks) };

            ArenaFighterSetup enemy = Fighter(1, 1, false, new GridCoord(4, 1), IsoDir.NW);
            enemy.Stats.JumpRange = 7;
            enemy.Stats.AtbFillTicks = 1;
            enemy.AiReactionTicks = 1;

            var factory = new ArenaSimulationFactory(Config, new JumpHook(landing));
            using IArenaSession session = factory.Create(Match(player, enemy));
            RunToRunning(session);

            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, 0)));

            uint cursor = 0;
            Drain(session, ref cursor);
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Skill(0, 0, new GridCoord(4, 1))));

            for (int step = 0; step < 40; step++)
            {
                session.Step();

                List<ArenaEvent> events = Read(session, ref cursor);
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i].Type == ArenaEventType.Hit && events[i].Target == 1)
                    {
                        return ("hit", Find(session, 1));
                    }

                    if (events[i].Type == ArenaEventType.Evaded && events[i].Target == 1)
                    {
                        return ("evaded", Find(session, 1));
                    }
                }
            }

            return ("never", Find(session, 1));
        }

        private static IArenaSession Create(ArenaMatchSetup setup) =>
            new ArenaSimulationFactory(Config, new PassiveHook()).Create(setup);

        private static ArenaMatchSetup Match(params ArenaFighterSetup[] fighters) => new ArenaMatchSetup
        {
            Seed = 11,
            ArenaMapId = 1,
            Mode = AtbMode.Active,
            TimeLimitTicks = 0,
            Fighters = fighters,
            DevCheatsAllowed = true,
        };

        private static ArenaFighterSetup Fighter(byte id, byte team, bool playerControlled, GridCoord tile,
            IsoDir facing = IsoDir.SE) => new ArenaFighterSetup
        {
            Id = id,
            Team = team,
            DefId = 1,
            PlayerControlled = playerControlled,
            AiProfileId = playerControlled ? 0 : 1,
            AiReactionTicks = 0,
            StartTile = tile,
            StartFacing = facing,
            StartHp = -1,
            Stats = new CombatStatBlock
            {
                MaxHp = 5000,
                MaxNeili = 100,
                NeiliRegenPerSecX100 = 0,
                ExternalPower = 0,
                InnerPower = 0,
                Defense = 0,
                InnerDefense = 0,
                AccuracyX100 = 10000,
                EvasionX100 = 0,
                CritChanceX100 = 0,
                CritDamageX100 = 15000,
                CounterChanceX100 = 0,
                StatusResistX100 = 0,
                KnockbackResist = 0,
                AtbFillTicks = 500,
                ActionSpeedX100 = 10000,
                MoveRange = 0,
                JumpRange = 0,
                JumpCooldownTicks = 5,
            },
            Skills = Array.Empty<ArenaSkillLoadout>(),
            PassiveIds = Array.Empty<int>(),
        };

        private static ArenaSkillLoadout Skill(
            int skillId,
            int baseDamage,
            int ratioX100 = 0,
            int rangeMax = 3,
            AreaShape area = AreaShape.Single,
            int areaSize = 0,
            int windUp = 0,
            int recovery = 0,
            int knockback = 0,
            int statusId = 0,
            int statusChanceX100 = 0,
            int hits = 1) => new ArenaSkillLoadout
        {
            SkillId = skillId,
            Slot = 0,
            Level = 1,
            WeaponType = 1,
            Targeting = SkillTargeting.Enemy,
            RangeMin = 1,
            RangeMax = (byte)rangeMax,
            Area = area,
            AreaSize = (byte)areaSize,
            Kind = DamageKind.External,
            BaseDamage = baseDamage,
            RatioX100 = ratioX100,
            MasteryX100 = 10000,
            Hits = hits,
            WindUpTicks = windUp,
            RecoveryTicks = recovery,
            CooldownTicks = 0,
            NeiliCost = 0,
            StatusId = statusId,
            StatusChanceX100 = statusChanceX100,
            StatusDurationTicks = 0,
            KnockbackTiles = (byte)knockback,
            Flags = SkillFlags.None,
        };

        private static void RunToRunning(IArenaSession session)
        {
            int guard = 0;
            while (session.Phase == ArenaPhase.Intro && guard++ < ArenaConstants.IntroTicks + 10)
            {
                session.Step();
            }

            Assert.Equal(ArenaPhase.Running, session.Phase);
        }

        private static FighterView Find(IArenaSession session, byte id)
        {
            foreach (FighterView fighter in session.Frame.Fighters)
            {
                if (fighter.Id == id)
                {
                    return fighter;
                }
            }

            throw new InvalidOperationException("No fighter view with id " + id + ".");
        }

        private static void Drain(IArenaSession session, ref uint cursor)
        {
            var buffer = new ArenaEvent[512];
            while (session.Events.Read(ref cursor, buffer, out _) > 0)
            {
            }
        }

        private static List<ArenaEvent> Read(IArenaSession session, ref uint cursor)
        {
            var events = new List<ArenaEvent>();
            var buffer = new ArenaEvent[512];
            while (true)
            {
                int count = session.Events.Read(ref cursor, buffer, out _);
                if (count == 0)
                {
                    return events;
                }

                for (int i = 0; i < count; i++)
                {
                    events.Add(buffer[i]);
                }
            }
        }

        private sealed class MeditateHook : IArenaAiHook
        {
            public bool TryChoose(IArenaFrame frame, IArenaQuery query,
                ProjectJianghu.Core.Shared.IRandomSource random, byte fighterId, out ArenaCommand command)
            {
                command = ArenaCommand.Meditate(fighterId);
                return query.Validate(in command) == CommandResult.Ok;
            }
        }

        /// <summary>An AI hook that never wants to act, so a test controls every decision.</summary>
        private sealed class PassiveHook : IArenaAiHook
        {
            public bool TryChoose(IArenaFrame frame, IArenaQuery query, ProjectJianghu.Core.Shared.IRandomSource random, byte fighterId, out ArenaCommand command)
            {
                command = ArenaCommand.Meditate(fighterId);
                return false;
            }
        }

        /// <summary>An AI hook that leaves the ground at a fixed tile, for the jump tests.</summary>
        private sealed class JumpHook : IArenaAiHook
        {
            private readonly GridCoord _landing;

            public JumpHook(GridCoord landing) => _landing = landing;

            public bool TryChoose(IArenaFrame frame, IArenaQuery query, ProjectJianghu.Core.Shared.IRandomSource random, byte fighterId, out ArenaCommand command)
            {
                command = ArenaCommand.Jump(fighterId, _landing);
                return query.Validate(in command) == CommandResult.Ok;
            }
        }
    }
}
