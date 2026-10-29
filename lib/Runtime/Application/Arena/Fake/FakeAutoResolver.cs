using System;
using System.Collections.Generic;
using ProjectJianghu.Core.Domain.Arena;

namespace ProjectJianghu.Core.Application.Arena.Fake
{
    /// <summary>
    /// Resolves a background tournament match with the fake session: every fighter is
    /// forced onto AI, the match runs headless until it finishes, and the result comes
    /// back exactly as a played match would.
    /// </summary>
    /// <remarks>
    /// The setup's seed is the only input that varies between matches (the session
    /// derives each fighter's head start from it), so "resolve the same seed twice" is
    /// reproducible while a bracket of different seeds produces different matches.
    /// </remarks>
    public sealed class FakeAutoResolver : IArenaAutoResolver
    {
        private const int DefaultMaxTicks = 30 * 120;

        private readonly FakeArenaSessionFactory _factory;

        /// <summary>Creates a resolver over a default fake factory.</summary>
        public FakeAutoResolver() : this(new FakeArenaSessionFactory())
        {
        }

        /// <summary>Creates a resolver that starts sessions from <paramref name="factory"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is null.</exception>
        public FakeAutoResolver(FakeArenaSessionFactory factory)
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

            ArenaMatchSetup allAi = MakeAllAi(setup);
            int budget = maxTicks > 0 ? maxTicks : DefaultMaxTicks;

            using IArenaSession session = _factory.Create(allAi);
            while (session.Phase != ArenaPhase.Finished && session.Tick < budget)
            {
                session.Step();
            }

            if (session.TryGetResult(out ArenaMatchResult result))
            {
                return result;
            }

            // The budget ran out before a natural ending. Background matches must still
            // return something, and the contract's timeout rule is the honest fallback.
            return BuildBudgetResult(session);
        }

        private static ArenaMatchSetup MakeAllAi(ArenaMatchSetup setup)
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

            return new ArenaMatchSetup
            {
                Seed = setup.Seed,
                ArenaMapId = setup.ArenaMapId,
                Mode = AtbMode.Active,
                TimeLimitTicks = setup.TimeLimitTicks,
                Fighters = fighters,
                DevCheatsAllowed = false,
            };
        }

        private static ArenaMatchResult BuildBudgetResult(IArenaSession session)
        {
            IArenaFrame frame = session.Frame;
            ReadOnlySpan<FighterView> views = frame.Fighters;

            var stats = new FighterMatchStat[views.Length];
            long firstTeamHp = 0;
            long firstTeamMax = 0;
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
                    DamageDealt = 0,
                    DamageTaken = 0,
                    Evades = 0,
                    Counters = 0,
                    SkillUses = Array.Empty<SkillUseStat>(),
                };

                if (view.Team == firstTeam)
                {
                    firstTeamHp += view.Hp;
                    firstTeamMax += view.MaxHp;
                }
                else
                {
                    otherHp += view.Hp;
                    otherMax += view.MaxHp;
                }
            }

            ArenaOutcome outcome;
            ArenaEndReason reason;
            byte winner;

            if (firstTeamMax <= 0 || otherMax <= 0)
            {
                outcome = ArenaOutcome.Draw;
                reason = ArenaEndReason.TimeoutDraw;
                winner = ArenaConstants.NoFighter;
            }
            else
            {
                long left = firstTeamHp * otherMax;
                long right = otherHp * firstTeamMax;
                if (left > right)
                {
                    outcome = ArenaOutcome.Victory;
                    reason = ArenaEndReason.TimeoutHpRatio;
                    winner = firstTeam;
                }
                else if (left < right)
                {
                    outcome = ArenaOutcome.Defeat;
                    reason = ArenaEndReason.TimeoutHpRatio;
                    winner = FindOtherTeam(views, firstTeam);
                }
                else
                {
                    outcome = ArenaOutcome.Draw;
                    reason = ArenaEndReason.TimeoutDraw;
                    winner = ArenaConstants.NoFighter;
                }
            }

            return new ArenaMatchResult
            {
                Outcome = outcome,
                Reason = reason,
                WinnerTeam = winner,
                DurationTicks = frame.ElapsedTicks,
                Fighters = stats,
            };
        }

        private static byte FindOtherTeam(ReadOnlySpan<FighterView> views, byte team)
        {
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i].Team != team)
                {
                    return views[i].Team;
                }
            }

            return ArenaConstants.NoFighter;
        }
    }
}
