using System;

namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>
    /// One fighter as the view sees it: an immutable-per-frame snapshot with enough
    /// state to draw and to interpolate between ticks.
    /// </summary>
    /// <remarks>
    /// Tile coordinates are authored on the grid; <c>X</c>/<c>Y</c> are the continuous
    /// position the presentation interpolates from <c>PrevX</c>/<c>PrevY</c>. A fake
    /// session teleports (Prev equals the tile at the start of the tick), the real
    /// simulation walks one tile at a time.
    /// </remarks>
    public struct FighterView
    {
        /// <summary>Fighter id, stable for the match.</summary>
        public byte Id;

        /// <summary>Team id; fighters on the same team cannot target each other.</summary>
        public byte Team;

        /// <summary>Appearance / roster row id from data.</summary>
        public int DefId;

        /// <summary>Public AI profile identity (0 for player-controlled fighters).</summary>
        public int AiProfileId;

        /// <summary>Skill slot currently creating the visible telegraph.</summary>
        public byte ActiveSkillSlot;

        /// <summary>Tile currently shown as the skill telegraph's aim point.</summary>
        public GridCoord ActionTarget;

        /// <summary>What the fighter is doing.</summary>
        public FighterState State;

        /// <summary>Which way the sprite faces.</summary>
        public IsoDir Facing;

        /// <summary>The tile the fighter occupies.</summary>
        public GridCoord Tile;

        /// <summary>Continuous X at the previous frame, for interpolation.</summary>
        public float PrevX;

        /// <summary>Continuous Y at the previous frame, for interpolation.</summary>
        public float PrevY;

        /// <summary>Continuous X now.</summary>
        public float X;

        /// <summary>Continuous Y now.</summary>
        public float Y;

        /// <summary>Height above the ground while jumping.</summary>
        public float Height;

        /// <summary>Current hit points.</summary>
        public int Hp;

        /// <summary>Maximum hit points.</summary>
        public int MaxHp;

        /// <summary>Current inner power.</summary>
        public int Neili;

        /// <summary>Maximum inner power.</summary>
        public int MaxNeili;

        /// <summary>ATB, 0 to <see cref="ArenaConstants.AtbFull"/>.</summary>
        public int Atb;

        /// <summary>Bit mask of active statuses (see <c>EStatus</c> in data).</summary>
        public ushort StatusMask;

        /// <summary>The skill being cast, or 0.</summary>
        public int ActiveSkillId;

        /// <summary>Progress of the current action, in per-mille.</summary>
        public ushort ActionProgressPermille;

        /// <summary>Remaining jump cooldown, in ticks.</summary>
        public ushort JumpCooldownTicks;
    }

    /// <summary>
    /// The published state of the match. A span read from this interface stays valid
    /// until the next <see cref="IArenaSession.Step"/> or <see cref="IArenaSession.Submit"/>.
    /// </summary>
    /// <remarks>
    /// The presentation must copy what it needs before stepping again
    /// (<c>llmwiki/gotchas/copy-frame-before-step.md</c>): the frame buffer reuses its
    /// slots, so a live span after another step reads memory being overwritten.
    /// </remarks>
    public interface IArenaFrame
    {
        /// <summary>The current simulation tick (intro included).</summary>
        int Tick { get; }

        /// <summary>Ticks since the intro finished; the clock the time limit uses.</summary>
        int ElapsedTicks { get; }

        /// <summary>The match's time limit in ticks; 0 or less means no limit.</summary>
        int TimeLimitTicks { get; }

        /// <summary>The player-controlled fighter id, or <see cref="ArenaConstants.NoFighter"/>.</summary>
        byte PlayerFighter { get; }

        /// <summary>Every fighter, in setup order.</summary>
        ReadOnlySpan<FighterView> Fighters { get; }

        /// <summary>
        /// The fighter's skill cooldowns, exactly <see cref="ArenaConstants.MaxSkillSlots"/>
        /// entries (0 = ready).
        /// </summary>
        ReadOnlySpan<ushort> SkillCooldowns(byte fighter);
    }

    /// <summary>
    /// Pure questions about the match: highlight tiles, preview areas, and check whether
    /// a command would be accepted. No method changes state, and every result is written
    /// into the caller's span (AGENTS rule 7).
    /// </summary>
    public interface IArenaQuery
    {
        /// <summary>Writes the tiles <paramref name="fighter"/> can walk to; returns the count.</summary>
        int GetReachableTiles(byte fighter, Span<GridCoord> destination);

        /// <summary>Writes the tiles <paramref name="fighter"/> can jump to; returns the count.</summary>
        int GetJumpTiles(byte fighter, Span<GridCoord> destination);

        /// <summary>Writes the tiles in skill <paramref name="slot"/>'s range; returns the count.</summary>
        int GetSkillRangeTiles(byte fighter, byte slot, Span<GridCoord> destination);

        /// <summary>
        /// Writes the tiles the skill would cover at <paramref name="target"/>; returns the
        /// count. This is what draws the red preview and the enemy telegraph.
        /// </summary>
        int PreviewSkillArea(byte fighter, byte slot, GridCoord target, Span<GridCoord> destination);

        /// <summary>
        /// Exactly what <see cref="IArenaSession.Submit"/> would return at this state, with
        /// the same state afterwards. The contract suite checks it.
        /// </summary>
        CommandResult Validate(in ArenaCommand command);

        /// <summary>Whether a tile can be entered at all (bounds and static map data).</summary>
        bool IsWalkable(GridCoord tile);

        /// <summary>Grid width in tiles.</summary>
        int Width { get; }

        /// <summary>Grid height in tiles.</summary>
        int Height { get; }
    }
}
