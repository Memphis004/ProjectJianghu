using System;

namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>What happened, for VFX, audio and the UI — never for rules.</summary>
    /// <remarks>
    /// The field meanings per event are tabulated in
    /// <c>llmwiki/systems/arena-contract.md</c>; that table is the contract.
    /// </remarks>
    public enum ArenaEventType : ushort
    {
        /// <summary>The countdown started. <c>Value</c> is its length in ticks.</summary>
        MatchIntro,

        /// <summary>The countdown finished and ATB began to fill.</summary>
        MatchStarted,

        /// <summary>A fighter's ATB reached full.</summary>
        AtbReady,

        /// <summary>A controller's command was accepted. <c>Value</c> is the <see cref="ArenaCommandKind"/>.</summary>
        CommandAccepted,

        /// <summary>A walk started. <c>Tile</c> is the destination.</summary>
        MoveStarted,

        /// <summary>A walk finished. <c>Tile</c> is the destination.</summary>
        MoveEnded,

        /// <summary>A jump started. <c>Tile</c> is the landing tile.</summary>
        JumpStarted,

        /// <summary>A jump finished. <c>Tile</c> is the landing tile.</summary>
        Landed,

        /// <summary>
        /// A skill began its wind-up. <c>Tile</c> is the centre of the area; the full
        /// area comes from <see cref="IArenaQuery.PreviewSkillArea"/>.
        /// </summary>
        SkillWindUp,

        /// <summary>A skill resolved. Used for the cut-in and the impact.</summary>
        SkillCast,

        /// <summary>An attack landed. <c>Value</c> is damage, <c>Value2</c> the target's HP after it.</summary>
        Hit,

        /// <summary>An attack missed.</summary>
        Evaded,

        /// <summary>A counter-attack resolved.</summary>
        Countered,

        /// <summary>The target was pushed. <c>Value</c> is tiles, <c>Tile</c> the landing tile.</summary>
        KnockedBack,

        /// <summary>A status was applied. <c>DefId</c> is the status id, <c>Value</c> its duration.</summary>
        StatusApplied,

        /// <summary>A status ran out.</summary>
        StatusExpired,

        /// <summary>Inner power was recovered. <c>Value</c> is the gain, <c>Value2</c> the new total.</summary>
        NeiliRecovered,

        /// <summary>A fighter left the match.</summary>
        FighterDefeated,

        /// <summary>Thirty seconds remain. Emitted once per match.</summary>
        TimeWarning,

        /// <summary>The match ended. <c>Value</c> is <see cref="ArenaEndReason"/>.</summary>
        MatchEnded,
    }

    /// <summary>Modifiers of one event, used by the presentation to pick a stronger reaction.</summary>
    [Flags]
    public enum ArenaEventFlags : byte
    {
        /// <summary>No modifier.</summary>
        None = 0,

        /// <summary>The hit was critical.</summary>
        Critical = 1,

        /// <summary>The hit was in the target's back arc.</summary>
        Backstab = 2,

        /// <summary>The hit was in the target's side arc.</summary>
        Flank = 4,

        /// <summary>The damage is inner (internal) rather than external.</summary>
        Inner = 8,

        /// <summary>The skill carries the ultimate flag.</summary>
        Ultimate = 16,
    }

    /// <summary>
    /// One thing that happened, in order. A flat struct: it goes into
    /// <c>EventRing&lt;ArenaEvent&gt;</c> without allocation and a reader can copy it out.
    /// </summary>
    /// <remarks>
    /// See <c>llmwiki/systems/arena-contract.md</c> for the meaning of
    /// <c>DefId</c>/<c>Value</c>/<c>Value2</c> per event type and for the span's lifetime.
    /// </remarks>
    public struct ArenaEvent
    {
        /// <summary>Ring-assigned sequence number, strictly increasing and never reused.</summary>
        public uint Seq;

        /// <summary>The simulation tick that produced the event.</summary>
        public int Tick;

        /// <summary>What happened.</summary>
        public ArenaEventType Type;

        /// <summary>Why it happened that way.</summary>
        public ArenaEventFlags Flags;

        /// <summary>Fighter id of the actor, or <see cref="ArenaConstants.NoFighter"/>.</summary>
        public byte Source;

        /// <summary>Fighter id of the target, or <see cref="ArenaConstants.NoFighter"/>.</summary>
        public byte Target;

        /// <summary>Skill or status id when the event names one; 0 otherwise.</summary>
        public int DefId;

        /// <summary>Damage, healing, duration, reason or command kind — see the table.</summary>
        public int Value;

        /// <summary>The secondary number (remaining HP, new inner power, team) — see the table.</summary>
        public int Value2;

        /// <summary>The tile the event is drawn at, or <see cref="GridCoord.Invalid"/>.</summary>
        public GridCoord Tile;
    }

    /// <summary>
    /// A reader's end of the match's event ring. Each reader owns its cursor; a reader
    /// that falls behind by more than the capacity is told how many it lost.
    /// </summary>
    public interface IArenaEventSource
    {
        /// <summary>
        /// The sequence number of the most recently written event. Not for emptiness
        /// checks: compare a read cursor instead.
        /// </summary>
        uint LatestSeq { get; }

        /// <summary>
        /// Copies unread events into <paramref name="destination"/>, advances
        /// <paramref name="cursor"/>, and reports events lost to overwrite in
        /// <paramref name="dropped"/>. Allocation-free.
        /// </summary>
        int Read(ref uint cursor, Span<ArenaEvent> destination, out int dropped);
    }
}
