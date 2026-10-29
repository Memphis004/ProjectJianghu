namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>Where a match is in its lifecycle.</summary>
    /// <remarks>
    /// An enum rather than a set of flags on purpose: a match is in exactly one place,
    /// and every transition is one assignment the contract suite can assert.
    /// </remarks>
    public enum ArenaPhase : byte
    {
        /// <summary>Countdown before the match; ATB does not fill and commands are rejected.</summary>
        Intro = 0,

        /// <summary>The match is playing.</summary>
        Running = 1,

        /// <summary>
        /// <see cref="AtbMode.Wait"/> only: a player-controlled fighter is ready and the
        /// simulation is stopped until that fighter's command is accepted.
        /// </summary>
        AwaitingPlayer = 2,

        /// <summary>The match has a result; <see cref="IArenaSession.Step"/> does nothing.</summary>
        Finished = 3,
    }

    /// <summary>Whether the simulation stops for the player's decision.</summary>
    public enum AtbMode : byte
    {
        /// <summary>Time keeps running while the player decides.</summary>
        Active = 0,

        /// <summary>The simulation pauses at <see cref="ArenaPhase.AwaitingPlayer"/>.</summary>
        Wait = 1,
    }

    /// <summary>What a fighter is doing.</summary>
    /// <remarks>
    /// The presentation maps this to an animation; the rules map it to what commands are
    /// legal. Only <see cref="Ready"/> accepts a normal command.
    /// </remarks>
    public enum FighterState : byte
    {
        /// <summary>ATB is filling.</summary>
        Charging,

        /// <summary>ATB is full and the fighter waits for its controller's command.</summary>
        Ready,

        /// <summary>Walking to a tile.</summary>
        Moving,

        /// <summary>In the air.</summary>
        Jumping,

        /// <summary>Casting a skill, before it resolves.</summary>
        WindUp,

        /// <summary>After a skill resolves, before the ATB starts again.</summary>
        Recovery,

        /// <summary>Under a status that stops the ATB.</summary>
        Stunned,

        /// <summary>Out of the match.</summary>
        Defeated,
    }

    /// <summary>The four isometric facings. Two are drawn and two are mirrored (ADR 0102).</summary>
    public enum IsoDir : byte
    {
        /// <summary>Up-right on screen.</summary>
        NE = 0,

        /// <summary>Down-right on screen.</summary>
        SE = 1,

        /// <summary>Down-left on screen.</summary>
        SW = 2,

        /// <summary>Up-left on screen.</summary>
        NW = 3,
    }
}
