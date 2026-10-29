using ProjectJianghu.Core.Domain.Arena;

namespace ProjectJianghu.Core.Application.Arena.Fake
{
    /// <summary>
    /// Creates <see cref="FakeArenaSession"/>s. The same factory is handed to the
    /// contract suite, the replay runner and the auto-resolver, so a test never
    /// accidentally tests a different implementation in each place.
    /// </summary>
    public sealed class FakeArenaSessionFactory : IArenaSessionFactory
    {
        private readonly FakeArenaOptions _options;

        /// <summary>Creates a factory with the default <see cref="FakeArenaOptions"/>.</summary>
        public FakeArenaSessionFactory() : this(null)
        {
        }

        /// <summary>Creates a factory with explicit knobs. <paramref name="options"/> may be null.</summary>
        public FakeArenaSessionFactory(FakeArenaOptions? options)
        {
            _options = options ?? new FakeArenaOptions();
        }

        /// <summary>The knobs every session starts with.</summary>
        public FakeArenaOptions Options => _options;

        /// <inheritdoc />
        public IArenaSession Create(ArenaMatchSetup setup) => new FakeArenaSession(setup, _options);
    }
}
