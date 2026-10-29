using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Domain.Character;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Core.Infrastructure.Config;
using ProjectJianghu.Core.Shared;
using ProjectJianghu.Core.Simulation.Arena;

namespace ProjectJianghu.Sim
{
    /// <summary>
    /// Stage 07's balance sweep: build every authored fighter from its <c>fighter</c> row,
    /// auto-resolve a batch of matches between them, and write what happened.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The point is the loop Stage 07 was built for — a match with every side on AI, finished
    /// without presentation, in under 20 ms (the stage's DoD) — and the three numbers balance
    /// work needs first: win rate per power rank, match length, and which skills are used.
    /// The CSV is the artifact; the console line is the timing.
    /// </para>
    /// <para>
    /// Fighters come from <c>data/</c> through the same <see cref="ArenaFighterBuilder"/>
    /// the game uses, so a data edit shows up here after <c>tools/gen.ps1</c>. Nothing in
    /// this file decides a number: the match length is <c>constant.match_ticks</c>, the
    /// fighters' power comes from their rows.
    /// </para>
    /// </remarks>
    internal static class BalanceMode
    {
        /// <summary>Rank buckets 1..9 plus a slot for a row without a rank.</summary>
        private const int RankBuckets = 10;

        internal static int Run(SimOptions options, LubanGameConfig config)
        {
            int fighterCount = config.Fighters.Count;
            if (fighterCount < 2)
            {
                Console.Error.WriteLine("balance: the fighter table needs at least two rows");
                return 2;
            }

            var builder = new ArenaFighterBuilder(config, new StatResolver(config));
            var templates = new ArenaFighterSetup[fighterCount];
            var ranks = new int[fighterCount];
            for (int i = 0; i < fighterCount; i++)
            {
                FighterConfig row = config.Fighters[i];
                if (row is null)
                {
                    throw new InvalidOperationException("sim: fighter row " + i + " is missing");
                }

                templates[i] = BuildFighter(config, builder, row, i);
                ranks[i] = row.PowerRank;
            }

            if (config.ArenaMaps.Count == 0)
            {
                Console.Error.WriteLine("balance: the arena_map table is empty");
                return 2;
            }

            ArenaMapConfig map = config.ArenaMaps[0];

            var startA = new GridCoord(1, 1);
            var startB = new GridCoord(map.Width - 2, map.Height - 2);

            var resolver = new ArenaAutoResolver(new ArenaSimulationFactory(config));
            int budget = config.Constant.MatchTicks;

            var rankMatches = new int[RankBuckets];
            var rankWins = new int[RankBuckets];
            var rankDraws = new int[RankBuckets];
            var rankKnockouts = new int[RankBuckets];
            var rankTimeouts = new int[RankBuckets];
            var rankDurations = new long[RankBuckets];
            var skillUse = new Dictionary<int, int>();
            var skillHit = new Dictionary<int, int>();

            long totalTicks = 0;
            long totalMilliseconds = 0;
            int wins = 0;
            int draws = 0;
            int knockouts = 0;
            int timeouts = 0;

            var stopwatch = new Stopwatch();

            for (int match = 0; match < options.Matches; match++)
            {
                int first = match % fighterCount;
                int second = (int)((uint)((match * 7) + 3) % (uint)fighterCount);
                if (second == first)
                {
                    second = (second + 1) % fighterCount;
                }

                ArenaMatchSetup setup = Compose(
                    templates[first], templates[second], startA, startB,
                    unchecked(((ulong)options.Seed * 1000003UL) + (ulong)match));

                stopwatch.Restart();
                ArenaMatchResult result = resolver.Resolve(setup, budget);
                stopwatch.Stop();
                totalMilliseconds += stopwatch.ElapsedMilliseconds;

                totalTicks += result.DurationTicks;
                rankMatches[ranks[first]]++;
                rankDurations[ranks[first]] += result.DurationTicks;
                rankMatches[ranks[second]]++;
                rankDurations[ranks[second]] += result.DurationTicks;

                // How the match ended is the number a balance pass tunes against: a limit
                // reached by HP percentage is a match that could not finish what it started.
                bool knockedOut = result.Reason == ArenaEndReason.AllEnemiesDefeated
                    || result.Reason == ArenaEndReason.AllAlliesDefeated;
                if (knockedOut)
                {
                    knockouts++;
                }
                else if (result.Reason == ArenaEndReason.TimeoutHpRatio || result.Reason == ArenaEndReason.TimeoutDraw)
                {
                    timeouts++;
                }

                if (knockedOut)
                {
                    rankKnockouts[ranks[first]]++;
                    rankKnockouts[ranks[second]]++;
                }
                else if (result.Reason == ArenaEndReason.TimeoutHpRatio || result.Reason == ArenaEndReason.TimeoutDraw)
                {
                    rankTimeouts[ranks[first]]++;
                    rankTimeouts[ranks[second]]++;
                }

                if (result.Outcome == ArenaOutcome.Victory)
                {
                    wins++;
                    rankWins[ranks[first]]++;
                }
                else if (result.Outcome == ArenaOutcome.Defeat)
                {
                    wins++;
                    rankWins[ranks[second]]++;
                }
                else
                {
                    draws++;
                    rankDraws[ranks[first]]++;
                    rankDraws[ranks[second]]++;
                }

                int casts = 0;
                int landed = 0;
                for (int i = 0; i < result.Fighters.Count; i++)
                {
                    FighterMatchStat stat = result.Fighters[i];
                    for (int use = 0; use < stat.SkillUses.Count; use++)
                    {
                        SkillUseStat entry = stat.SkillUses[use];
                        skillUse[entry.SkillId] = (skillUse.TryGetValue(entry.SkillId, out int uses) ? uses : 0) + entry.Uses;
                        skillHit[entry.SkillId] = (skillHit.TryGetValue(entry.SkillId, out int hits) ? hits : 0) + entry.Hits;
                        casts += entry.Uses;
                        landed += entry.Hits;
                    }
                }

                if (options.Trace > 0 && match < options.Trace)
                {
                    Console.WriteLine("balance: match=" + Num(match)
                        + " a=" + config.Fighters[first].Id
                        + " rank=" + Num(ranks[first])
                        + " b=" + config.Fighters[second].Id
                        + " rank=" + Num(ranks[second])
                        + " ticks=" + Num(result.DurationTicks)
                        + " reason=" + result.Reason
                        + " winnerTeam=" + Num(result.WinnerTeam)
                        + " casts=" + Num(casts)
                        + " hits=" + Num(landed));
                }
            }

            string output = SimEnvironment.OutputPath(options.BalanceOut);
            string? directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            WriteCsv(output, config, options.Matches, wins, draws, knockouts, timeouts, totalTicks,
                rankMatches, rankWins, rankDraws, rankKnockouts, rankTimeouts, rankDurations, skillUse, skillHit);

            double perMatch = options.Matches == 0 ? 0 : (double)totalMilliseconds / options.Matches;
            double secondsPerMatch = options.Matches == 0 ? 0 : totalTicks / (double)options.Matches / ArenaConstants.TickRate;

            Console.WriteLine("balance: matches=" + options.Matches.ToString(CultureInfo.InvariantCulture)
                + " wins=" + wins.ToString(CultureInfo.InvariantCulture)
                + " draws=" + draws.ToString(CultureInfo.InvariantCulture)
                + " knockouts=" + knockouts.ToString(CultureInfo.InvariantCulture)
                + " timeouts=" + timeouts.ToString(CultureInfo.InvariantCulture)
                + " avgTicks=" + Format(options.Matches == 0 ? 0 : totalTicks / (double)options.Matches)
                + " avgSeconds=" + Format(secondsPerMatch));
            Console.WriteLine("balance: avgMsPerMatch=" + Format(perMatch)
                + " totalMs=" + totalMilliseconds.ToString(CultureInfo.InvariantCulture)
                + " budgetMs=20"
                + " verdict=" + (perMatch < 20.0 ? "PASS" : "OVER"));
            Console.WriteLine("balance: csv=" + output);

            return 0;
        }

        private static ArenaFighterSetup BuildFighter(
            LubanGameConfig config, ArenaFighterBuilder builder, FighterConfig row, int index)
        {
            var character = new CharacterState("sim." + row.Id);
            var rules = new LoadoutRules(config);

            for (int i = 0; i < row.Attributes.Count; i++)
            {
                character.SetBaseAttribute(row.Attributes[i].Attribute, row.Attributes[i].Value);
            }

            int instance = 1;
            if (!string.IsNullOrEmpty(row.WeaponId))
            {
                character.AddGear(new GearInventoryEntry(row.WeaponId, instance));
                rules.EquipWeapon(character, instance);
                instance++;
            }

            if (!string.IsNullOrEmpty(row.ArmorId))
            {
                character.AddGear(new GearInventoryEntry(row.ArmorId, instance));
                rules.EquipArmor(character, instance);
                instance++;
            }

            for (int slot = 0; slot < row.AccessoryIds.Count && slot < 2; slot++)
            {
                string accessory = row.AccessoryIds[slot];
                if (string.IsNullOrEmpty(accessory))
                {
                    continue;
                }

                character.AddGear(new GearInventoryEntry(accessory, instance));
                rules.EquipAccessory(character, slot, instance);
                instance++;
            }

            for (int i = 0; i < row.SkillIds.Count; i++)
            {
                string skillId = row.SkillIds[i];
                if (!string.IsNullOrEmpty(skillId) && character.FindSkill(skillId) is null)
                {
                    character.LearnSkill(new LearnedSkill(skillId));
                }
            }

            int externalSlot = 0;
            for (int i = 0; i < row.SkillIds.Count; i++)
            {
                SkillConfig skill = FindSkill(config, row.SkillIds[i]);
                if (skill is null)
                {
                    continue;
                }

                if (skill.Kind == ESkillKind.External)
                {
                    if (externalSlot >= 4 || !rules.EquipSkill(character, ESkillKind.External, externalSlot, skill.Id).IsSuccess)
                    {
                        continue;
                    }

                    externalSlot++;
                }
                else
                {
                    rules.EquipSkill(character, skill.Kind, 0, skill.Id);
                }
            }

            int profileId = StatGrowth.StableId(row.AiProfileId);
            Result<ArenaFighterSetup> built = builder.Build(
                character, 0, 0, false, profileId, ReactionTicks(config, row.AiProfileId),
                new GridCoord(1, 1), IsoDir.SE, index + 1);

            if (built.IsFailure)
            {
                throw new InvalidOperationException(
                    "sim: fighter '" + row.Id + "' cannot be built: " + built.Error.Code);
            }

            return built.Value;
        }

        private static int ReactionTicks(LubanGameConfig config, string profileId)
        {
            for (int i = 0; i < config.AiProfiles.Count; i++)
            {
                if (string.Equals(config.AiProfiles[i].Id, profileId, StringComparison.Ordinal))
                {
                    return config.AiProfiles[i].DecisionDelayTicks;
                }
            }

            return 0;
        }

        private static SkillConfig FindSkill(LubanGameConfig config, string id)
        {
            for (int i = 0; i < config.Skills.Count; i++)
            {
                if (string.Equals(config.Skills[i].Id, id, StringComparison.Ordinal))
                {
                    return config.Skills[i];
                }
            }

            return null!;
        }

        private static ArenaMatchSetup Compose(
            ArenaFighterSetup first, ArenaFighterSetup second, GridCoord startA, GridCoord startB, ulong seed)
        {
            return new ArenaMatchSetup
            {
                Seed = seed,
                ArenaMapId = 1,
                Mode = AtbMode.Active,
                TimeLimitTicks = 0,
                DevCheatsAllowed = false,
                Fighters = new[]
                {
                    Fork(first, 0, 0, startA, IsoDir.SE),
                    Fork(second, 1, 1, startB, IsoDir.NW),
                },
            };
        }

        private static ArenaFighterSetup Fork(
            ArenaFighterSetup source, byte id, byte team, GridCoord tile, IsoDir facing) => new ArenaFighterSetup
        {
            Id = id,
            Team = team,
            DefId = source.DefId,
            PlayerControlled = false,
            AiProfileId = source.AiProfileId,
            AiReactionTicks = source.AiReactionTicks,
            StartTile = tile,
            StartFacing = facing,
            StartHp = -1,
            Stats = source.Stats,
            Skills = source.Skills,
            PassiveIds = source.PassiveIds,
        };

        private static void WriteCsv(
            string path,
            LubanGameConfig config,
            int matches,
            int wins,
            int draws,
            int knockouts,
            int timeouts,
            long totalTicks,
            int[] rankMatches,
            int[] rankWins,
            int[] rankDraws,
            int[] rankKnockouts,
            int[] rankTimeouts,
            long[] rankDurations,
            Dictionary<int, int> skillUse,
            Dictionary<int, int> skillHit)
        {
            var text = new StringBuilder(4096);
            text.Append("type,name,power_rank,matches,wins,draws,knockouts,timeouts,win_rate,"
                + "avg_duration_ticks,avg_duration_seconds,uses,hits\n");

            for (int rank = 0; rank < RankBuckets; rank++)
            {
                if (rankMatches[rank] == 0)
                {
                    continue;
                }

                double rate = (double)rankWins[rank] / rankMatches[rank];
                double ticks = (double)rankDurations[rank] / rankMatches[rank];
                text.Append("rank,").Append("rank_").Append(Num(rank))
                    .Append(',').Append(Num(rank))
                    .Append(',').Append(Num(rankMatches[rank]))
                    .Append(',').Append(Num(rankWins[rank]))
                    .Append(',').Append(Num(rankDraws[rank]))
                    .Append(',').Append(Num(rankKnockouts[rank]))
                    .Append(',').Append(Num(rankTimeouts[rank]))
                    .Append(',').Append(Frac(rate))
                    .Append(',').Append(Frac(ticks))
                    .Append(',').Append(Frac(ticks / ArenaConstants.TickRate))
                    .Append(",0,0\n");
            }

            var byName = new List<(string Name, int Uses, int Hits)>();
            for (int i = 0; i < config.Skills.Count; i++)
            {
                SkillConfig skill = config.Skills[i];
                int id = StatGrowth.StableId(skill.Id);
                int uses = skillUse.TryGetValue(id, out int value) ? value : 0;
                int hits = skillHit.TryGetValue(id, out int landed) ? landed : 0;
                if (uses > 0)
                {
                    byName.Add((skill.Id, uses, hits));
                }
            }

            byName.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            for (int i = 0; i < byName.Count; i++)
            {
                text.Append("skill,").Append(byName[i].Name)
                    .Append(",0,0,0,0,0,0,0,0,0,")
                    .Append(Num(byName[i].Uses)).Append(',').Append(Num(byName[i].Hits)).Append('\n');
            }

            double averageTicks = matches == 0 ? 0 : (double)totalTicks / matches;
            text.Append("total,total,0,").Append(Num(matches))
                .Append(',').Append(Num(wins))
                .Append(',').Append(Num(draws))
                .Append(',').Append(Num(knockouts))
                .Append(',').Append(Num(timeouts))
                .Append(",0,").Append(Frac(averageTicks))
                .Append(',').Append(Frac(averageTicks / ArenaConstants.TickRate))
                .Append(",0,0\n");

            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        }

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Frac(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
