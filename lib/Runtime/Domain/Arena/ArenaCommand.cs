namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>Everything a controller can ask a fighter to do.</summary>
    /// <remarks>
    /// Values below 100 are normal play; 100+ are development commands that
    /// <see cref="ArenaMatchSetup.DevCheatsAllowed"/> gates. The numbers are part of the
    /// replay format — never renumber them.
    /// </remarks>
    public enum ArenaCommandKind : byte
    {
        /// <summary>Walk to <see cref="ArenaCommand.Target"/>, which must be reachable.</summary>
        Move = 0,

        /// <summary>Use <see cref="ArenaCommand.Slot"/> at <see cref="ArenaCommand.Target"/>.</summary>
        UseSkill = 1,

        /// <summary>Jump to <see cref="ArenaCommand.Target"/>.</summary>
        Jump = 2,

        /// <summary>Recover inner power instead of acting.</summary>
        Meditate = 3,

        /// <summary>Forfeit the match; the player's side loses, the match is <c>Aborted</c>.</summary>
        Surrender = 10,

        /// <summary>Development only: defeat every enemy of the actor's team.</summary>
        DevKillEnemies = 100,

        /// <summary>Development only: the actor takes no damage for the rest of the match.</summary>
        DevGodMode = 101,

        /// <summary>Development only: fill the actor's ATB immediately.</summary>
        DevFillAtb = 102,

        /// <summary>Development only: end the match as a victory.</summary>
        DevWinNow = 103,

        /// <summary>Development only: end the match as a defeat.</summary>
        DevLoseNow = 104,
    }

    /// <summary>
    /// One player or AI request. A command is self-contained: actor, target tile, skill
    /// slot and a free integer for the dev commands. Submitting one is atomic.
    /// </summary>
    /// <remarks>
    /// A readonly struct so a command can be copied into a replay entry without
    /// allocation. <see cref="Actor"/> is a fighter <b>id</b>(<see cref="ArenaFighterSetup.Id"/>),
    /// not an array index.
    /// </remarks>
    public readonly struct ArenaCommand
    {
        /// <summary>What is being asked for.</summary>
        public readonly ArenaCommandKind Kind;

        /// <summary>The fighter id that acts.</summary>
        public readonly byte Actor;

        /// <summary>Skill slot for <see cref="ArenaCommandKind.UseSkill"/>; 0 otherwise.</summary>
        public readonly byte Slot;

        /// <summary>The target tile, or <see cref="GridCoord.Invalid"/> when the command has none.</summary>
        public readonly GridCoord Target;

        /// <summary>Free argument for development commands.</summary>
        public readonly int Arg;

        /// <summary>Creates a command. Prefer the named factories.</summary>
        public ArenaCommand(ArenaCommandKind kind, byte actor, GridCoord target, byte slot = 0, int arg = 0)
        {
            Kind = kind;
            Actor = actor;
            Target = target;
            Slot = slot;
            Arg = arg;
        }

        /// <summary>A walk order.</summary>
        public static ArenaCommand Move(byte actor, GridCoord target) =>
            new ArenaCommand(ArenaCommandKind.Move, actor, target);

        /// <summary>A skill order.</summary>
        public static ArenaCommand Skill(byte actor, byte slot, GridCoord target) =>
            new ArenaCommand(ArenaCommandKind.UseSkill, actor, target, slot);

        /// <summary>A jump order.</summary>
        public static ArenaCommand Jump(byte actor, GridCoord target) =>
            new ArenaCommand(ArenaCommandKind.Jump, actor, target);

        /// <summary>A meditate order.</summary>
        public static ArenaCommand Meditate(byte actor) =>
            new ArenaCommand(ArenaCommandKind.Meditate, actor, GridCoord.Invalid);

        /// <summary>A development command, gated by <see cref="ArenaMatchSetup.DevCheatsAllowed"/>.</summary>
        public static ArenaCommand Dev(ArenaCommandKind kind, byte actor, int arg = 0) =>
            new ArenaCommand(kind, actor, GridCoord.Invalid, 0, arg);
    }

    /// <summary>
    /// The typed outcome of <see cref="IArenaSession.Submit"/> and
    /// <see cref="IArenaQuery.Validate"/>. <c>Ok</c> is 0 so a default result is not a
    /// rejection by accident.
    /// </summary>
    /// <remarks>
    /// The UI maps a value to a localization key (<c>command.&lt;name&gt;</c>); renaming a
    /// member is therefore a localization change too. <see cref="IArenaQuery.Validate"/>
    /// must return exactly what <see cref="IArenaSession.Submit"/> would, at the same
    /// state — the contract suite checks that over thousands of random commands.
    /// </remarks>
    public enum CommandResult : byte
    {
        /// <summary>The command was accepted.</summary>
        Ok = 0,

        /// <summary>Wrong match phase: the intro is running, the match is over, or another fighter is awaited.</summary>
        RejectedWrongPhase,

        /// <summary>The fighter's ATB is not full, or it is busy with an action.</summary>
        RejectedNotReady,

        /// <summary>The command names a fighter the player does not control.</summary>
        RejectedNotControlled,

        /// <summary>A tile or a fighter lies outside the allowed range.</summary>
        RejectedOutOfRange,

        /// <summary>The destination is occupied, not walkable, or unreachable.</summary>
        RejectedBlocked,

        /// <summary>The skill costs more inner power than the actor has.</summary>
        RejectedNoNeili,

        /// <summary>The skill slot is still cooling down.</summary>
        RejectedOnCooldown,

        /// <summary>The skill's weapon does not match the actor's loadout.</summary>
        RejectedWeaponMismatch,

        /// <summary>The command's shape is invalid (slot, actor or target).</summary>
        RejectedInvalidArg,

        /// <summary>A development command arrived in a build where cheats are off.</summary>
        RejectedNotAllowed,
    }
}
