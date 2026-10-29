namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>
    /// The fixed numbers of the arena contract (ArenaBattle Contract v1, GDD part 2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Balance numbers live in <c>data/</c> (AGENTS rule 6). This class holds only the
    /// shape of the contract: tick rate, buffer sizes, identifier sentinels and the
    /// intro countdown. Every number here is identical for the fake session and the
    /// real simulation, so a replay can cross between them without a translation step.
    /// </para>
    /// <para>
    /// <see cref="IntroTicks"/> is not in the GDD's original listing: the contract as
    /// attached gave <see cref="IArenaSession"/> an <c>Intro</c> phase but no length,
    /// and the contract suite has to know how long to step. It is a shared default
    /// (30 ticks = 1 s at 30 Hz) rather than a per-session knob so the fake and the
    /// simulation cannot disagree about what "intro" means. Stage 07 may read the
    /// value from the <c>constant</c> table once that row exists; the contract shape
    /// does not change.
    /// </para>
    /// </remarks>
    public static class ArenaConstants
    {
        /// <summary>Simulation frequency, in ticks per second.</summary>
        public const int TickRate = 30;

        /// <summary>Seconds per tick — the accumulator step of <c>ArenaRunner</c>.</summary>
        public const float TickDelta = 1f / TickRate;

        /// <summary>Upper bound of <see cref="IArenaSession.Step"/> calls in one rendered frame.</summary>
        public const int MaxStepsPerFrame = 4;

        /// <summary>The ATB bar is fixed-point: 0 is empty, this value is full.</summary>
        public const int AtbFull = 10_000;

        /// <summary>
        /// How many ticks the intro countdown lasts. ATB does not fill during the intro and
        /// every command is rejected with <see cref="CommandResult.RejectedWrongPhase"/>.
        /// </summary>
        public const int IntroTicks = 30;

        /// <summary>Fighter capacity of a match. The prototype uses 2; teams of up to 8 fit.</summary>
        public const int MaxFighters = 8;

        /// <summary>Skill slots per fighter (keys 1–4 in the HUD).</summary>
        public const int MaxSkillSlots = 4;

        /// <summary>Largest grid edge the contract can carry.</summary>
        public const int MaxGridSize = 32;

        /// <summary>Largest number of tiles a query can write into a caller span.</summary>
        public const int MaxGridTiles = MaxGridSize * MaxGridSize;

        /// <summary>Capacity of <see cref="IArenaEventSource"/>'s ring.</summary>
        public const int EventRingCapacity = 2048;

        /// <summary>
        /// The "no fighter" sentinel for ids and <see cref="ArenaEvent.Target"/>.
        /// Fighter ids are setup data and always below this value.
        /// </summary>
        public const byte NoFighter = 255;
    }
}
