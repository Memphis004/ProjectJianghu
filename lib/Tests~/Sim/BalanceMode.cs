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
            if (options.Mode == SimMode.Stratified)
            {
                return RunStratified(options, config);
            }

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

        private static int RunStratified(SimOptions options, LubanGameConfig config)
        {
            if (config.Fighters.Count < 2 || config.ArenaMaps.Count == 0)
            {
                Console.Error.WriteLine("stratified: requires at least two fighters and one arena map");
                return 2;
            }

            var builder = new ArenaFighterBuilder(config, new StatResolver(config));
            var templates = new ArenaFighterSetup[config.Fighters.Count];
            for (int i = 0; i < config.Fighters.Count; i++)
            {
                FighterConfig row = config.Fighters[i];
                templates[i] = BuildFighter(config, builder, row, i);
            }

            var profiles = new List<string>();
            for (int i = 0; i < config.AiProfiles.Count; i++)
            {
                profiles.Add(config.AiProfiles[i].Id);
            }
            profiles.Sort(StringComparer.Ordinal);
            var rankValues = new List<int>();
            for (int i = 0; i < config.Fighters.Count; i++)
            {
                int rank = config.Fighters[i].PowerRank;
                if (!rankValues.Contains(rank)) rankValues.Add(rank);
            }
            rankValues.Sort();

            var rankPairs = new List<(int Low, int High)>();
            for (int lowIndex = 0; lowIndex < rankValues.Count; lowIndex++)
            {
                for (int highIndex = lowIndex + 1; highIndex < rankValues.Count; highIndex++)
                {
                    int low = rankValues[lowIndex];
                    int high = rankValues[highIndex];
                    if (high - low >= 2) rankPairs.Add((low, high));
                }
            }

            var resolver = new ArenaAutoResolver(new ArenaSimulationFactory(config));
            var map = config.ArenaMaps[0];
            GridCoord startA = new GridCoord(1, 1);
            GridCoord startB = new GridCoord(map.Width - 2, map.Height - 2);
            var results = new List<StratifiedCell>();
            long totalTicks = 0;
            int totalMatches = 0;
            ulong trialIndex = 0;
            var stopwatch = Stopwatch.StartNew();

            for (int rankIndex = 0; rankIndex < rankPairs.Count; rankIndex++)
            {
                (int lowRank, int highRank) = rankPairs[rankIndex];
                for (int lowProfile = 0; lowProfile < profiles.Count; lowProfile++)
                {
                    for (int highProfile = 0; highProfile < profiles.Count; highProfile++)
                    {
                        string profileA = profiles[lowProfile];
                        string profileB = profiles[highProfile];
                        var pairings = new List<(int Low, int High)>();
                        for (int lowIndex = 0; lowIndex < templates.Length; lowIndex++)
                        {
                            FighterConfig low = config.Fighters[lowIndex];
                            if (low.PowerRank != lowRank) continue;
                            for (int highIndex = 0; highIndex < templates.Length; highIndex++)
                            {
                                FighterConfig high = config.Fighters[highIndex];
                                if (high.PowerRank != highRank) continue;
                                if (low.AiProfileId == profileA && high.AiProfileId == profileB)
                                {
                                    pairings.Add((lowIndex, highIndex));
                                }
                            }
                        }

                        if (pairings.Count == 0) continue;
                        var cell = new StratifiedCell(lowRank, highRank, profileA, profileB)
                        {
                            Pairings = pairings.Count,
                        };
                        for (int pairing = 0; pairing < pairings.Count; pairing++)
                        {
                            (int lowIndex, int highIndex) = pairings[pairing];
                            for (int repeat = 0; repeat < options.Matches; repeat++)
                            {
                                ulong seed = unchecked(((ulong)options.Seed * 1000003UL) + trialIndex++);
                                for (int side = 0; side < 2; side++)
                                {
                                    bool lowFirst = side == 0;
                                    ArenaFighterSetup first = templates[lowFirst ? lowIndex : highIndex];
                                    ArenaFighterSetup second = templates[lowFirst ? highIndex : lowIndex];
                                    ArenaMatchSetup setup = Compose(first, second, startA, startB, seed);
                                    ArenaMatchResult result = resolver.Resolve(setup, config.Constant.MatchTicks);
                                    totalTicks += result.DurationTicks;
                                    totalMatches++;
                                    cell.Add(lowFirst ? result.Outcome : Swap(result.Outcome), result.DurationTicks,
                                        result.Reason == ArenaEndReason.TimeoutHpRatio
                                            || result.Reason == ArenaEndReason.TimeoutDraw);
                                }
                            }
                        }

                        results.Add(cell);
                    }
                }
            }

            stopwatch.Stop();
            double totalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            string output = SimEnvironment.OutputPath(options.BalanceOut);
            string? directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            WriteStratifiedCsv(output, results);

            int lowWins = 0, highWins = 0, draws = 0, timeouts = 0;
            for (int i = 0; i < results.Count; i++)
            {
                lowWins += results[i].LowWins;
                highWins += results[i].HighWins;
                draws += results[i].Draws;
                timeouts += results[i].Timeouts;
            }

            double nonDrawRate = lowWins + highWins == 0 ? 0 : (double)highWins / (lowWins + highWins);
            double allMatchRate = totalMatches == 0 ? 0 : (double)highWins / totalMatches;
            Console.WriteLine("stratified: seed=" + options.Seed.ToString(CultureInfo.InvariantCulture)
                + " rankProfileCells=" + results.Count.ToString(CultureInfo.InvariantCulture)
                + " matches=" + totalMatches.ToString(CultureInfo.InvariantCulture)
                + " pairings=" + PairingCount(results).ToString(CultureInfo.InvariantCulture)
                + " lowWins=" + lowWins.ToString(CultureInfo.InvariantCulture)
                + " highWins=" + highWins.ToString(CultureInfo.InvariantCulture)
                + " draws=" + draws.ToString(CultureInfo.InvariantCulture)
                + " timeouts=" + timeouts.ToString(CultureInfo.InvariantCulture)
                + " highWinRateIncludingDraws=" + Format(allMatchRate)
                + " highWinRateExcludingDraws=" + Format(nonDrawRate));
            Console.WriteLine("stratified: avgMsPerMatch=" + Format(totalMatches == 0 ? 0 : (double)totalMilliseconds / totalMatches)
                + " avgTicks=" + Format(totalMatches == 0 ? 0 : (double)totalTicks / totalMatches)
                + " csv=" + output);
            return 0;
        }

        private static int PairingCount(List<StratifiedCell> cells)
        {
            int count = 0;
            for (int i = 0; i < cells.Count; i++) count += cells[i].Pairings;
            return count;
        }

        private static ArenaOutcome Swap(ArenaOutcome outcome) => outcome switch
        {
            ArenaOutcome.Victory => ArenaOutcome.Defeat,
            ArenaOutcome.Defeat => ArenaOutcome.Victory,
            _ => outcome,
        };

        private static void WriteStratifiedCsv(string path, List<StratifiedCell> cells)
        {
            var text = new StringBuilder(8192);
            text.Append("low_rank,high_rank,low_profile,high_profile,pairings,matches,low_wins,high_wins,draws,timeouts,high_win_rate_including_draws,high_win_rate_excluding_draws,avg_duration_ticks\n");
            long allLow = 0, allHigh = 0, allDraws = 0, allTimeouts = 0, allMatches = 0, allTicks = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                StratifiedCell cell = cells[i];
                text.Append(Num(cell.LowRank)).Append(',').Append(Num(cell.HighRank)).Append(',')
                    .Append(cell.LowProfile).Append(',').Append(cell.HighProfile).Append(',')
                    .Append(Num(cell.Pairings)).Append(',').Append(Num(cell.Matches)).Append(',')
                    .Append(Num(cell.LowWins)).Append(',').Append(Num(cell.HighWins)).Append(',')
                    .Append(Num(cell.Draws)).Append(',').Append(Num(cell.Timeouts)).Append(',')
                    .Append(Frac(cell.Matches == 0 ? 0 : (double)cell.HighWins / cell.Matches)).Append(',')
                    .Append(Frac(cell.Matches == cell.Draws ? 0 : (double)cell.HighWins / (cell.Matches - cell.Draws))).Append(',')
                    .Append(Frac(cell.Matches == 0 ? 0 : (double)cell.Ticks / cell.Matches)).Append('\n');
                allLow += cell.LowWins; allHigh += cell.HighWins; allDraws += cell.Draws;
                allTimeouts += cell.Timeouts; allMatches += cell.Matches; allTicks += cell.Ticks;
            }

            text.Append("TOTAL,0,all,all,0,").Append(allMatches).Append(',').Append(allLow).Append(',')
                .Append(allHigh).Append(',').Append(allDraws).Append(',').Append(allTimeouts).Append(',')
                .Append(Frac(allMatches == 0 ? 0 : (double)allHigh / allMatches)).Append(',')
                .Append(Frac(allMatches == allDraws ? 0 : (double)allHigh / (allMatches - allDraws))).Append(',')
                .Append(Frac(allMatches == 0 ? 0 : (double)allTicks / allMatches)).Append('\n');
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        }

        private sealed class StratifiedCell
        {
            internal readonly int LowRank, HighRank;
            internal readonly string LowProfile, HighProfile;
            internal int Pairings, Matches, LowWins, HighWins, Draws, Timeouts;
            internal long Ticks;
            internal StratifiedCell(int lowRank, int highRank, string lowProfile, string highProfile)
            { LowRank=lowRank; HighRank=highRank; LowProfile=lowProfile; HighProfile=highProfile; }
            internal void Add(ArenaOutcome outcome, int ticks, bool timeout)
            {
                Matches++; Ticks += ticks; if (timeout) Timeouts++;
                if (outcome == ArenaOutcome.Victory) LowWins++;
                else if (outcome == ArenaOutcome.Defeat) HighWins++;
                else Draws++;
            }
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
            PowerRank = source.PowerRank,
            Decisiveness = source.Decisiveness,
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
