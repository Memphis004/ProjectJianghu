using ProjectJianghu.Core.Simulation.Arena;

namespace ProjectJianghu.Core.Tests.Arena
{
    /// <summary>
    /// Runs the whole ArenaBattle contract suite against the real simulation (Stage 07).
    /// </summary>
    /// <remarks>
    /// No fact is overridden and none is disabled: the same eleven facts the fake passes are
    /// the gate for <see cref="ArenaSimulation"/>. The factory reads the real generated
    /// tables, so the match also runs on the authored <c>constant</c>, <c>status</c> and
    /// <c>arena_map</c> rows.
    /// </remarks>
    public sealed class ArenaSimulationContractTests : ArenaSessionContractTests<ArenaSimulationFactory>
    {
        /// <inheritdoc />
        protected override ArenaSimulationFactory CreateFactory() => new ArenaSimulationFactory(ConfigFixture.Load());
    }
}
