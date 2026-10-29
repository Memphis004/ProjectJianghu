using System;
using ProjectJianghu.Core.Domain.Arena;

namespace ProjectJianghu.Core.Simulation.Arena
{
    /// <summary>
    /// Plays a match headless with every fighter on AI — the background matches the
    /// tournament needs (Stage 10) and the balance sweep <c>tools/sim.ps1 --matches</c> runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tick budget becomes the match's own time limit, so the result comes from the
    /// session's ordinary timeout rule (HP percentage, draw on a tie) with full per-fighter
    /// statistics, instead of a second result builder that could disagree with the first.
    /// An authored limit shorter than the budget wins; a non-positive budget keeps the
    /// factory's <c>constant.match_ticks</c>.
    /// </para>
    /// <para>
    /// Nothing here reads the clock, so a seed always resolves to the same match. Timing
    /// the sweep is the caller's job (the Sim balance mode).
    /// </para>
    /// </remarks>
    public sealed class ArenaAutoResolver : IArenaAutoResolver
    {
        private readonly IArenaSessionFactory _factory;

        /// <summary>Creates a resolver over <paramref name="factory"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is null.</exception>
        public ArenaAutoResolver(IArenaSessionFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <inheritdoc />
        public ArenaMatchResult Resolve(ArenaMatchSetup setup, int maxTicks)
        {
            if (setup is null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            ArenaMatchSetup allAi = MakeAllAi(setup, maxTicks);

            // The session ends at its time limit; the guard is a belt for a rule bug, never
            // a normal exit.
            int guard = (maxTicks > 0 ? maxTicks : setup.TimeLimitTicks) + ArenaConstants.IntroTicks + 4;

            using IArenaSession session = _factory.Create(allAi);
            while (session.Phase != ArenaPhase.Finished && session.Tick < guard)
            {
                session.Step();
            }

            if (session.TryGetResult(out ArenaMatchResult result))
            {
                return result;
            }

            return BuildGuardResult(session);
        }

        private static ArenaMatchSetup MakeAllAi(ArenaMatchSetup setup, int maxTicks)
        {
            var fighters = new ArenaFighterSetup[setup.Fighters.Count];
            for (int i = 0; i < fighters.Length; i++)
            {
                ArenaFighterSetup source = setup.Fighters[i];
                fighters[i] = new ArenaFighterSetup
                {
                    Id = source.Id,
                    Team = source.Team,
                    DefId = source.DefId,
                    PlayerControlled = false,
                    AiProfileId = source.AiProfileId,
                    AiReactionTicks = source.AiReactionTicks,
                    PowerRank = source.PowerRank,
                    Decisiveness = source.Decisiveness,
                    StartTile = source.StartTile,
                    StartFacing = source.StartFacing,
                    StartHp = source.StartHp,
                    Stats = source.Stats,
                    Skills = source.Skills,
                    PassiveIds = source.PassiveIds,
                };
            }

            int limit = setup.TimeLimitTicks;
            if (maxTicks > 0 && (limit <= 0 || maxTicks < limit))
            {
                limit = maxTicks;
            }

            return new ArenaMatchSetup
            {
                Seed = setup.Seed,
                ArenaMapId = setup.ArenaMapId,
                Mode = AtbMode.Active,
                TimeLimitTicks = limit,
                Fighters = fighters,
                DevCheatsAllowed = false,
            };
        }

        /// <summary>
        /// The answer when the loop stopped without a result — unreachable while the session
        /// honours its time limit. It reports what the frame says, honestly, rather than
        /// pretending the match resolved.
        /// </summary>
        private static ArenaMatchResult BuildGuardResult(IArenaSession session)
        {
            IArenaFrame frame = session.Frame;
            ReadOnlySpan<FighterView> views = frame.Fighters;

            var stats = new FighterMatchStat[views.Length];
            long firstHp = 0;
            long firstMax = 0;
            long otherHp = 0;
            long otherMax = 0;
            byte firstTeam = views.Length > 0 ? views[0].Team : ArenaConstants.NoFighter;

            for (int i = 0; i < views.Length; i++)
            {
                FighterView view = views[i];
                stats[i] = new FighterMatchStat
                {
                    Id = view.Id,
                    FinalHp = view.Hp,
                    SkillUses = Array.Empty<SkillUseStat>(),
                };

                if (view.Team == firstTeam)
                {
                    firstHp += view.Hp;
                    firstMax += view.MaxHp;
                }
                else
                {
                    otherHp += view.Hp;
                    otherMax += view.MaxHp;
                }
            }

            ArenaOutcome outcome = ArenaOutcome.Draw;
            byte winner = ArenaConstants.NoFighter;
            if (firstMax > 0 && otherMax > 0)
            {
                long left = firstHp * otherMax;
                long right = otherHp * firstMax;
                if (left > right)
                {
                    outcome = ArenaOutcome.Victory;
                    winner = firstTeam;
                }
                else if (left < right)
                {
                    outcome = ArenaOutcome.Defeat;
                    winner = firstTeam == views[0].Team ? views[views.Length - 1].Team : views[0].Team;
                }
            }

            return new ArenaMatchResult
            {
                Outcome = outcome,
                Reason = outcome == ArenaOutcome.Draw ? ArenaEndReason.TimeoutDraw : ArenaEndReason.TimeoutHpRatio,
                WinnerTeam = winner,
                DurationTicks = frame.ElapsedTicks,
                Fighters = stats,
            };
        }
    }
}
