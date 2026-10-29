using System;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Domain.Config;

namespace ProjectJianghu.Core.Simulation.Arena
{
    /// <summary>
    /// Creates the real <see cref="ArenaSimulation"/>s. The one place a match picks up the
    /// <c>constant</c> row, so every session in a process runs on the same numbers.
    /// </summary>
    /// <remarks>
    /// The factory, not the session, fills a missing time limit from
    /// <see cref="ConstantConfig.MatchTicks"/>: the contract's <c>0 = no limit</c> stays
    /// exactly as promised inside the session, while a caller that does not care about time
    /// limits (a background match, a test) still gets the authored match length instead of
    /// an endless one. AGENTS rule 6 — the number comes from data, not from here.
    /// </remarks>
    public sealed class ArenaSimulationFactory : IArenaSessionFactory
    {
        private readonly IGameConfig _config;
        private readonly IArenaAiHook _ai;

        /// <summary>Creates a factory with the default <see cref="GreedyArenaAiHook"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public ArenaSimulationFactory(IGameConfig config) : this(config, new GreedyArenaAiHook())
        {
        }

        /// <summary>Creates a factory with an explicit AI hook.</summary>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public ArenaSimulationFactory(IGameConfig config, IArenaAiHook ai)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _ai = ai ?? throw new ArgumentNullException(nameof(ai));
        }

        /// <summary>The configuration every session is created with.</summary>
        public IGameConfig Config => _config;

        /// <inheritdoc />
        public IArenaSession Create(ArenaMatchSetup setup)
        {
            if (setup is null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            return new ArenaSimulation(WithDefaultTimeLimit(setup, _config.Constant.MatchTicks), _config, _ai);
        }

        private static ArenaMatchSetup WithDefaultTimeLimit(ArenaMatchSetup setup, int matchTicks)
        {
            if (setup.TimeLimitTicks > 0 || matchTicks <= 0)
            {
                return setup;
            }

            return new ArenaMatchSetup
            {
                Seed = setup.Seed,
                ArenaMapId = setup.ArenaMapId,
                Mode = setup.Mode,
                TimeLimitTicks = matchTicks,
                Fighters = setup.Fighters,
                DevCheatsAllowed = setup.DevCheatsAllowed,
            };
        }
    }
}
