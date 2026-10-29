using ProjectJianghu.Core.Application.Arena.Fake;

namespace ProjectJianghu.Core.Tests.Arena
{
    /// <summary>Runs the whole ArenaBattle contract suite against the fake session.</summary>
    /// <remarks>
    /// Stage 07 adds a sibling for the real simulation. If that class needs a change
    /// here, the contract changed and <see cref="ArenaSessionContractTests{TFactory}"/>
    /// is where it must be made — not in the fake.
    /// </remarks>
    public sealed class FakeArenaSessionContractTests : ArenaSessionContractTests<FakeArenaSessionFactory>
    {
        /// <inheritdoc />
        protected override FakeArenaSessionFactory CreateFactory() => new FakeArenaSessionFactory();
    }
}
