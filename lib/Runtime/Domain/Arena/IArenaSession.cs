using System;

namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>
    /// One arena match. The simulation advances on a fixed 30 Hz tick; input never
    /// enters through <see cref="Step"/> but only through <see cref="Submit"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The arena equivalent of the template's <c>IBattleSession</c>, with two deliberate
    /// differences (GDD part 2): <see cref="Step"/> takes no input, and
    /// <see cref="Query"/> answers "would this command be accepted?" without touching
    /// state so the UI can highlight tiles.
    /// </para>
    /// <para>
    /// <see cref="Submit"/> is atomic: an accepted command is applied completely (or
    /// ends the match), a rejected one changes nothing. A contract factory is expected
    /// to be deterministic for one setup + one command stream.
    /// </para>
    /// </remarks>
    public interface IArenaSession : IDisposable
    {
        /// <summary>Where the match is in its lifecycle.</summary>
        ArenaPhase Phase { get; }

        /// <summary>The current tick; the intro counts up from 0.</summary>
        int Tick { get; }

        /// <summary>Whether the match pauses for the player.</summary>
        AtbMode Mode { get; }

        /// <summary>
        /// Advances one fixed tick. Does nothing when the match is
        /// <see cref="ArenaPhase.Finished"/> or <see cref="ArenaPhase.AwaitingPlayer"/>.
        /// </summary>
        void Step();

        /// <summary>
        /// Applies one command atomically and records it with the current tick. Returns
        /// <see cref="CommandResult.Ok"/> when the state changed.
        /// </summary>
        CommandResult Submit(in ArenaCommand command);

        /// <summary>The published frame; valid until the next <see cref="Step"/> or <see cref="Submit"/>.</summary>
        IArenaFrame Frame { get; }

        /// <summary>Pure questions over the current state.</summary>
        IArenaQuery Query { get; }

        /// <summary>The match's event ring.</summary>
        IArenaEventSource Events { get; }

        /// <summary>Reports the result once the match is finished.</summary>
        bool TryGetResult(out ArenaMatchResult result);

        /// <summary>
        /// A hash of the rules-relevant state. Two sessions with the same setup and the
        /// same command stream must produce the same value — this is the replay check
        /// and the save/replay integrity check.
        /// </summary>
        ulong ComputeStateHash();
    }

    /// <summary>Creates sessions. The fake and the real simulation both implement it.</summary>
    public interface IArenaSessionFactory
    {
        /// <summary>Starts a match from <paramref name="setup"/>.</summary>
        IArenaSession Create(ArenaMatchSetup setup);
    }

    /// <summary>
    /// Plays a match headless with every fighter on AI, for background tournament
    /// matches (Stage 10). Must not depend on presentation or real time.
    /// </summary>
    public interface IArenaAutoResolver
    {
        /// <summary>
        /// Resolves the match within <paramref name="maxTicks"/> ticks. If it does not
        /// finish in time, the result is the HP-ratio timeout resolution.
        /// </summary>
        ArenaMatchResult Resolve(ArenaMatchSetup setup, int maxTicks);
    }
}
