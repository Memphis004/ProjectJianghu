using System;
using System.Collections.Generic;
using ProjectJianghu.Core.Application.Arena;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Shared;
using Xunit;

namespace ProjectJianghu.Core.Tests.Arena
{
    /// <summary>
    /// The ArenaBattle contract suite. Every implementation of
    /// <see cref="IArenaSessionFactory"/> must pass exactly these tests; Stage 07 adds
    /// <c>ArenaSessionContractTests&lt;ArenaSessionFactory&gt;</c> and must not need to
    /// change one line here.
    /// </summary>
    /// <remarks>
    /// The suite is generic so a new factory only supplies
    /// <see cref="CreateFactory"/> and, if it needs different timing, its intro length.
    /// Facts are inherited by a concrete class, which is what xUnit discovers; the
    /// abstract generic class itself is never run.
    /// </remarks>
    public abstract class ArenaSessionContractTests<TFactory> where TFactory : IArenaSessionFactory
    {
        /// <summary>The factory under test.</summary>
        protected abstract TFactory CreateFactory();

        /// <summary>How many ticks this implementation's intro lasts.</summary>
        protected virtual int IntroTicks => ArenaConstants.IntroTicks;

        /// <summary>Creates one session from <paramref name="setup"/>.</summary>
        protected IArenaSession CreateSession(ArenaMatchSetup setup) => CreateFactory().Create(setup);

        [Fact]
        public void Intro_does_not_fill_atb_and_rejects_commands()
        {
            using IArenaSession session = CreateSession(ArenaTestSetup.Create());

            Assert.Equal(ArenaPhase.Intro, session.Phase);

            for (int i = 0; i < IntroTicks - 1; i++)
            {
                session.Step();
            }

            Assert.Equal(ArenaPhase.Intro, session.Phase);
            foreach (FighterView fighter in session.Frame.Fighters)
            {
                Assert.Equal(0, fighter.Atb);
            }

            byte player = session.Frame.PlayerFighter;
            Assert.NotEqual(ArenaConstants.NoFighter, player);

            Assert.Equal(
                CommandResult.RejectedWrongPhase,
                session.Submit(ArenaCommand.Move(player, new GridCoord(2, 1))));

            Assert.Equal(
                CommandResult.RejectedWrongPhase,
                session.Submit(new ArenaCommand(ArenaCommandKind.Surrender, player, GridCoord.Invalid)));
        }

        [Fact]
        public void Commands_before_atb_is_full_are_not_ready_and_ai_fighters_are_not_controlled()
        {
            using IArenaSession session = CreateSession(ArenaTestSetup.Create(playerFillTicks: 20, enemyFillTicks: 20));
            RunToRunning(session);

            byte player = session.Frame.PlayerFighter;
            byte enemy = FindEnemyId(session, player);

            Assert.Equal(
                CommandResult.RejectedNotReady,
                session.Submit(ArenaCommand.Move(player, new GridCoord(2, 1))));

            Assert.Equal(
                CommandResult.RejectedNotControlled,
                session.Submit(ArenaCommand.Move(enemy, new GridCoord(3, 1))));
        }

        [Fact]
        public void Wait_mode_stops_at_the_player_and_step_does_not_advance_the_tick()
        {
            using IArenaSession session = CreateSession(
                ArenaTestSetup.Create(mode: AtbMode.Wait, playerFillTicks: 4, enemyFillTicks: 60));

            RunToRunning(session);
            StepUntilPhase(session, ArenaPhase.AwaitingPlayer, 400);

            Assert.Equal(ArenaPhase.AwaitingPlayer, session.Phase);
            int waitingTick = session.Tick;

            for (int i = 0; i < 10; i++)
            {
                session.Step();
            }

            Assert.Equal(waitingTick, session.Tick);

            byte player = session.Frame.PlayerFighter;
            var tiles = new GridCoord[ArenaConstants.MaxGridTiles];
            int count = session.Query.GetReachableTiles(player, tiles);
            Assert.True(count > 0);

            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Move(player, tiles[0])));
            Assert.Equal(ArenaPhase.Running, session.Phase);

            session.Step();
            Assert.Equal(waitingTick + 1, session.Tick);
        }

        [Fact]
        public void Active_mode_keeps_ticking_and_the_ai_keeps_acting_after_the_player_is_ready()
        {
            using IArenaSession session = CreateSession(ArenaTestSetup.Create(
                mode: AtbMode.Active,
                playerFillTicks: 6,
                enemyFillTicks: 3,
                damage: 10,
                playerHp: 500,
                timeLimitTicks: 0));

            RunToRunning(session);

            byte player = session.Frame.PlayerFighter;
            byte enemy = FindEnemyId(session, player);

            int guard = 0;
            while (!IsReady(session.Frame, player) && guard++ < 300 && session.Phase == ArenaPhase.Running)
            {
                session.Step();
            }

            Assert.True(IsReady(session.Frame, player), "the player never became ready");
            int tickBefore = session.Tick;

            // Discard the events so far: what is checked is that the AI still acts from
            // the moment the player is waiting, not that it once acted during the intro.
            var buffer = new ArenaEvent[256];
            uint cursor = 0;
            DrainEvents(session, buffer, ref cursor);

            for (int i = 0; i < 60 && session.Phase != ArenaPhase.Finished; i++)
            {
                session.Step();
            }

            Assert.True(session.Tick > tickBefore, "Active mode stopped ticking while the player was ready");

            var since = new List<ArenaEvent>();
            ReadInto(session, buffer, ref cursor, since);
            Assert.Contains(since, e => e.Source == enemy
                && (e.Type == ArenaEventType.CommandAccepted
                    || e.Type == ArenaEventType.MoveStarted
                    || e.Type == ArenaEventType.JumpStarted
                    || e.Type == ArenaEventType.SkillWindUp
                    || e.Type == ArenaEventType.SkillCast
                    || e.Type == ArenaEventType.Hit
                    || e.Type == ArenaEventType.Evaded
                    || e.Type == ArenaEventType.NeiliRecovered
                    || e.Type == ArenaEventType.StatusApplied));
        }

        [Fact]
        public void Validate_matches_submit_over_five_thousand_random_commands()
        {
            ArenaMatchSetup setup = ArenaTestSetup.Create(
                mode: AtbMode.Active,
                playerFillTicks: 3,
                enemyFillTicks: 5,
                damage: 12,
                playerHp: 2000,
                enemyHp: 2000,
                timeLimitTicks: 0,
                devCheats: false);

            var rng = new Xoshiro128(1234);
            ArenaCommandKind[] kinds =
            {
                ArenaCommandKind.Move,
                ArenaCommandKind.UseSkill,
                ArenaCommandKind.Jump,
                ArenaCommandKind.Meditate,
                ArenaCommandKind.Surrender,
                ArenaCommandKind.DevFillAtb,
                ArenaCommandKind.DevKillEnemies,
                ArenaCommandKind.DevWinNow,
            };

            IArenaSession session = CreateSession(setup);
            int accepted = 0;
            int rejected = 0;

            try
            {
                RunToRunning(session);

                for (int i = 0; i < 5000; i++)
                {
                    if (session.Phase == ArenaPhase.Finished)
                    {
                        session.Dispose();
                        session = CreateSession(setup);
                        RunToRunning(session);
                    }

                    var command = new ArenaCommand(
                        kinds[rng.NextInt(0, kinds.Length)],
                        (byte)rng.NextInt(0, 4),
                        new GridCoord(rng.NextInt(-1, 13), rng.NextInt(-1, 13)),
                        (byte)rng.NextInt(0, 5),
                        rng.NextInt(0, 3));

                    CommandResult expected = session.Query.Validate(in command);
                    Assert.Equal(expected, session.Query.Validate(in command));

                    CommandResult actual = session.Submit(in command);
                    Assert.Equal(expected, actual);

                    if (actual == CommandResult.Ok)
                    {
                        accepted++;
                    }
                    else
                    {
                        rejected++;
                    }

                    int steps = rng.NextInt(1, 3);
                    for (int step = 0; step < steps; step++)
                    {
                        session.Step();
                    }
                }
            }
            finally
            {
                session.Dispose();
            }

            Assert.True(accepted > 0, "the random run never accepted a command");
            Assert.True(rejected > 0, "the random run never rejected a command");
        }

        [Fact]
        public void Every_tile_the_query_reports_is_accepted_and_tiles_outside_are_not()
        {
            ArenaMatchSetup setup = ArenaTestSetup.Create(moveRange: 2, devCheats: true);

            var tiles = new GridCoord[ArenaConstants.MaxGridTiles];
            var reachable = new List<GridCoord>();

            using (IArenaSession session = CreateSession(setup))
            {
                RunToRunning(session);
                byte player = session.Frame.PlayerFighter;
                Assert.Equal(
                    CommandResult.Ok,
                    session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, player)));

                int count = session.Query.GetReachableTiles(player, tiles);
                Assert.True(count > 0);
                for (int i = 0; i < count; i++)
                {
                    reachable.Add(tiles[i]);
                }
            }

            foreach (GridCoord tile in reachable)
            {
                using IArenaSession probe = CreateSession(setup);
                RunToRunning(probe);
                byte player = probe.Frame.PlayerFighter;

                Assert.Equal(
                    CommandResult.Ok,
                    probe.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, player)));

                Assert.Equal(CommandResult.Ok, probe.Submit(ArenaCommand.Move(player, tile)));
                Assert.Equal(tile, FindView(probe.Frame, player).Tile);
            }

            using (IArenaSession outside = CreateSession(setup))
            {
                RunToRunning(outside);
                byte player = outside.Frame.PlayerFighter;
                Assert.Equal(
                    CommandResult.Ok,
                    outside.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, player)));

                var tooFar = new GridCoord(5, 1);
                Assert.True(outside.Query.IsWalkable(tooFar));
                Assert.DoesNotContain(tooFar, reachable);
                Assert.NotEqual(CommandResult.Ok, outside.Submit(ArenaCommand.Move(player, tooFar)));
            }
        }

        [Fact]
        public void Timeout_compares_hp_percentage_and_ties_are_a_draw()
        {
            AssertTimeout(800, 400, ArenaOutcome.Victory, ArenaEndReason.TimeoutHpRatio);
            AssertTimeout(400, 800, ArenaOutcome.Defeat, ArenaEndReason.TimeoutHpRatio);
            AssertTimeout(800, 800, ArenaOutcome.Draw, ArenaEndReason.TimeoutDraw);
        }

        [Fact]
        public void Surrender_aborts_and_dev_commands_are_refused_without_cheats()
        {
            using (IArenaSession session = CreateSession(ArenaTestSetup.Create(devCheats: false)))
            {
                RunToRunning(session);
                byte player = session.Frame.PlayerFighter;

                ArenaCommand cheat = ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, player);
                Assert.Equal(CommandResult.RejectedNotAllowed, session.Query.Validate(in cheat));
                Assert.Equal(CommandResult.RejectedNotAllowed, session.Submit(in cheat));

                Assert.Equal(
                    CommandResult.Ok,
                    session.Submit(new ArenaCommand(ArenaCommandKind.Surrender, player, GridCoord.Invalid)));

                Assert.Equal(ArenaPhase.Finished, session.Phase);
                Assert.Equal(ArenaPhase.Finished, session.Phase);
                Assert.True(session.TryGetResult(out ArenaMatchResult result));
                Assert.Equal(ArenaOutcome.Aborted, result.Outcome);
                Assert.Equal(ArenaEndReason.Surrendered, result.Reason);
                Assert.Equal((byte)1, result.WinnerTeam);
            }
        }

        [Fact]
        public void Replay_reproduces_the_state_hash_and_event_sequence_is_continuous()
        {
            ArenaMatchSetup setup = ArenaTestSetup.Create(
                mode: AtbMode.Active,
                playerFillTicks: 7,
                enemyFillTicks: 9,
                damage: 5,
                playerHp: 5000,
                enemyHp: 5000,
                timeLimitTicks: 0,
                devCheats: true,
                seed: 4242);

            TFactory factory = CreateFactory();
            var recorded = new List<ArenaReplayCommand>();
            var buffer = new ArenaEvent[256];
            var tileBuffer = new GridCoord[ArenaConstants.MaxGridTiles];
            uint cursor = 0;
            int lastSeq = -1;

            IArenaSession original = factory.Create(setup);
            try
            {
                for (int i = 0; i < 120 && original.Phase != ArenaPhase.Finished; i++)
                {
                    ReadContinuous(original, buffer, ref cursor, ref lastSeq);

                    if (TryPatternCommand(original, i, tileBuffer, out ArenaCommand command))
                    {
                        CommandResult result = original.Submit(in command);
                        if (result == CommandResult.Ok)
                        {
                            recorded.Add(new ArenaReplayCommand(original.Tick, command, result));
                        }
                    }

                    original.Step();
                }

                ReadContinuous(original, buffer, ref cursor, ref lastSeq);

                Assert.True(recorded.Count > 0, "the replay run never accepted a command");
                Assert.True(lastSeq >= 0);

                ulong hash = original.ComputeStateHash();
                int stopAtTick = original.Tick;

                ulong replayed = ArenaReplay.Play(factory, setup, recorded, stopAtTick);

                Assert.Equal(hash, replayed);
            }
            finally
            {
                original.Dispose();
            }
        }

        [Fact]
        public void Dropped_events_do_not_change_the_state()
        {
            ArenaMatchSetup setup = ArenaTestSetup.Create(
                mode: AtbMode.Active,
                playerFillTicks: 1,
                enemyFillTicks: 1,
                damage: 0,
                playerHp: 1_000_000,
                enemyHp: 1_000_000,
                skillRange: 12,
                timeLimitTicks: 0);

            using IArenaSession reading = CreateSession(setup);
            using IArenaSession control = CreateSession(setup);

            // Enough AI actions to wrap the ring several times over.
            for (int i = 0; i < 600; i++)
            {
                reading.Step();
                control.Step();
            }

            var buffer = new ArenaEvent[64];
            uint cursor = 0;
            reading.Events.Read(ref cursor, buffer, out int dropped);

            Assert.True(dropped > 0, "the ring never overflowed, so the test proves nothing");
            Assert.Equal(control.ComputeStateHash(), reading.ComputeStateHash());
            Assert.Equal(control.Phase, reading.Phase);
        }

        [Fact]
        public void Step_submit_and_query_do_not_allocate_after_warm_up()
        {
            ArenaMatchSetup setup = ArenaTestSetup.Create(
                mode: AtbMode.Active,
                playerFillTicks: 3,
                enemyFillTicks: 4,
                damage: 0,
                playerHp: 1_000_000,
                enemyHp: 1_000_000,
                timeLimitTicks: 0,
                devCheats: true);

            using IArenaSession session = CreateSession(setup);
            byte player = session.Frame.PlayerFighter;
            var tiles = new GridCoord[ArenaConstants.MaxGridTiles];

            for (int i = 0; i < 600; i++)
            {
                Exercise(session, player, tiles, i);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 600; i++)
            {
                Exercise(session, player, tiles, i);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0L, after - before);
        }

        // ---------------------------------------------------------------- helpers

        private static void Exercise(IArenaSession session, byte player, GridCoord[] tiles, int iteration)
        {
            session.Step();

            int reachable = session.Query.GetReachableTiles(player, tiles);
            GridCoord moveTarget = reachable > 0 ? tiles[0] : new GridCoord(1, 2);

            session.Query.GetJumpTiles(player, tiles);
            session.Query.GetSkillRangeTiles(player, 0, tiles);
            session.Query.PreviewSkillArea(player, 0, new GridCoord(4, 1), tiles);

            ArenaCommand command;
            switch (iteration % 4)
            {
                case 0:
                    command = ArenaCommand.Move(player, moveTarget);
                    break;
                case 1:
                    command = ArenaCommand.Skill(player, 0, new GridCoord(4, 1));
                    break;
                case 2:
                    command = ArenaCommand.Jump(player, new GridCoord(2, 1));
                    break;
                default:
                    command = ArenaCommand.Meditate(player);
                    break;
            }

            session.Query.Validate(in command);
            session.Submit(in command);
        }

        private void AssertTimeout(int playerStartHp, int enemyStartHp, ArenaOutcome outcome, ArenaEndReason reason)
        {
            using IArenaSession session = CreateSession(ArenaTestSetup.Create(
                timeLimitTicks: 4,
                playerFillTicks: 500,
                enemyFillTicks: 500,
                playerHp: 1000,
                enemyHp: 1000,
                playerStartHp: playerStartHp,
                enemyStartHp: enemyStartHp));

            RunToRunning(session);

            int guard = 0;
            while (session.Phase != ArenaPhase.Finished && guard++ < 100)
            {
                session.Step();
            }

            Assert.Equal(ArenaPhase.Finished, session.Phase);
            Assert.True(session.TryGetResult(out ArenaMatchResult result));
            Assert.Equal(outcome, result.Outcome);
            Assert.Equal(reason, result.Reason);
        }

        private void RunToRunning(IArenaSession session)
        {
            int guard = 0;
            while (session.Phase == ArenaPhase.Intro && guard++ < IntroTicks + 10)
            {
                session.Step();
            }

            Assert.Equal(ArenaPhase.Running, session.Phase);
        }

        private static void StepUntilPhase(IArenaSession session, ArenaPhase phase, int maxSteps)
        {
            for (int i = 0; i < maxSteps && session.Phase != phase; i++)
            {
                session.Step();
            }

            Assert.Equal(phase, session.Phase);
        }

        private static FighterView FindView(IArenaFrame frame, byte id)
        {
            foreach (FighterView fighter in frame.Fighters)
            {
                if (fighter.Id == id)
                {
                    return fighter;
                }
            }

            throw new InvalidOperationException("No fighter view with id " + id + ".");
        }

        private static bool IsReady(IArenaFrame frame, byte id) =>
            FindView(frame, id).State == FighterState.Ready;

        private static byte FindEnemyId(IArenaSession session, byte player)
        {
            FighterView own = FindView(session.Frame, player);
            foreach (FighterView fighter in session.Frame.Fighters)
            {
                if (fighter.Team != own.Team)
                {
                    return fighter.Id;
                }
            }

            return ArenaConstants.NoFighter;
        }

        private static void DrainEvents(IArenaSession session, ArenaEvent[] buffer, ref uint cursor)
        {
            while (session.Events.Read(ref cursor, buffer, out _) > 0)
            {
            }
        }

        private static void ReadInto(IArenaSession session, ArenaEvent[] buffer, ref uint cursor, List<ArenaEvent> destination)
        {
            while (true)
            {
                int count = session.Events.Read(ref cursor, buffer, out _);
                if (count == 0)
                {
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    destination.Add(buffer[i]);
                }
            }
        }

        private static void ReadContinuous(IArenaSession session, ArenaEvent[] buffer, ref uint cursor, ref int lastSeq)
        {
            while (true)
            {
                int count = session.Events.Read(ref cursor, buffer, out _);
                if (count == 0)
                {
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    int seq = (int)buffer[i].Seq;
                    if (lastSeq >= 0)
                    {
                        Assert.Equal(lastSeq + 1, seq);
                    }

                    lastSeq = seq;
                }
            }
        }

        private static bool TryPatternCommand(
            IArenaSession session, int iteration, GridCoord[] tiles, out ArenaCommand command)
        {
            command = ArenaCommand.Meditate(ArenaConstants.NoFighter);

            if (session.Phase != ArenaPhase.Running)
            {
                return false;
            }

            byte player = session.Frame.PlayerFighter;
            if (player == ArenaConstants.NoFighter)
            {
                return false;
            }

            switch (iteration % 4)
            {
                case 0:
                    command = ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, player);
                    return true;

                case 1:
                    int count = session.Query.GetReachableTiles(player, tiles);
                    if (count == 0)
                    {
                        return false;
                    }

                    command = ArenaCommand.Move(player, tiles[0]);
                    return true;

                case 2:
                    byte enemy = FindEnemyId(session, player);
                    if (enemy == ArenaConstants.NoFighter || FindView(session.Frame, enemy).Hp <= 0)
                    {
                        return false;
                    }

                    command = ArenaCommand.Skill(player, 0, FindView(session.Frame, enemy).Tile);
                    return true;

                default:
                    command = ArenaCommand.Meditate(player);
                    return true;
            }
        }
    }
}
