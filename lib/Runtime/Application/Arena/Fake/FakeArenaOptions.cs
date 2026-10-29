using ProjectJianghu.Core.Domain.Arena;

namespace ProjectJianghu.Core.Application.Arena.Fake
{
    /// <summary>
    /// The knobs <see cref="FakeArenaSession"/> adds on top of the contract. They exist
    /// only so the contract suite can stage a situation in a few ticks; the real
    /// simulation takes everything from the setup and the data tables.
    /// </summary>
    /// <remarks>
    /// Values are copied into a session when it is created, so changing an options
    /// instance later never changes a running match.
    /// </remarks>
    public sealed class FakeArenaOptions
    {
        /// <summary>Intro countdown length. Defaults to <see cref="ArenaConstants.IntroTicks"/>.</summary>
        public int IntroTicks = ArenaConstants.IntroTicks;

        /// <summary>Width of the empty grid the fake query reports. The prototype map is 12×12.</summary>
        public int Width = 12;

        /// <summary>Height of the empty grid the fake query reports.</summary>
        public int Height = 12;

        /// <summary>
        /// When the elapsed-tick count reaches this value, the match ends as a victory
        /// for the player. -1 disables the trigger.
        /// </summary>
        public int WinAtTick = -1;

        /// <summary>Same as <see cref="WinAtTick"/>, but the player loses.</summary>
        public int LoseAtTick = -1;

        /// <summary>Same as <see cref="WinAtTick"/>, but resolves by HP percentage.</summary>
        public int TimeoutAtTick = -1;
    }
}
