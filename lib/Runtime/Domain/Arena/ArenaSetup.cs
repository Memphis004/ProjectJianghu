using System;
using System.Collections.Generic;

namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>
    /// The resolved combat numbers of one fighter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Produced by <c>Domain.Character.StatResolver</c> (Stage 05) from attributes,
    /// skills and gear. The arena <b>never</b> recomputes it from attributes: the data
    /// formulas live in one place, and a replay that only carries the block is enough
    /// to reproduce a match.
    /// </para>
    /// <para>
    /// Values ending in <c>X100</c> are percentages in hundredths (1234 = 12.34%), and
    /// <c>Permille</c> values are thousandths, so the whole block is integer arithmetic
    /// and deterministic.
    /// </para>
    /// </remarks>
    public struct CombatStatBlock
    {
        /// <summary>Maximum hit points.</summary>
        public int MaxHp;

        /// <summary>Maximum inner power.</summary>
        public int MaxNeili;

        /// <summary>Inner power regenerated per second, in hundredths.</summary>
        public int NeiliRegenPerSecX100;

        /// <summary>Base external (physical) power.</summary>
        public int ExternalPower;

        /// <summary>Base inner power.</summary>
        public int InnerPower;

        /// <summary>External damage reduction.</summary>
        public int Defense;

        /// <summary>Inner damage reduction.</summary>
        public int InnerDefense;

        /// <summary>Accuracy, in hundredths of a percent.</summary>
        public int AccuracyX100;

        /// <summary>Evasion, in hundredths of a percent.</summary>
        public int EvasionX100;

        /// <summary>Critical chance, in hundredths of a percent.</summary>
        public int CritChanceX100;

        /// <summary>Critical damage multiplier, in hundredths of a percent.</summary>
        public int CritDamageX100;

        /// <summary>Counter-attack chance, in hundredths of a percent.</summary>
        public int CounterChanceX100;

        /// <summary>Status resistance, in hundredths of a percent.</summary>
        public int StatusResistX100;

        /// <summary>Knockback resistance in tiles.</summary>
        public int KnockbackResist;

        /// <summary>Ticks for ATB to fill from empty to full.</summary>
        public int AtbFillTicks;

        /// <summary>Action-speed multiplier, in hundredths of a percent.</summary>
        public int ActionSpeedX100;

        /// <summary>How many tiles a walk may cover.</summary>
        public byte MoveRange;

        /// <summary>How far a jump may reach, in Manhattan tiles.</summary>
        public byte JumpRange;

        /// <summary>Jump cooldown in ticks.</summary>
        public int JumpCooldownTicks;
    }

    /// <summary>Who a skill is pointed at.</summary>
    /// <remarks>
    /// Commands always carry a target tile, so <see cref="Ground"/> means "this tile,
    /// possibly empty" and <see cref="Self"/> means "the caster's own tile".
    /// </remarks>
    public enum SkillTargeting : byte
    {
        /// <summary>The target tile must hold an enemy.</summary>
        Enemy,

        /// <summary>Any walkable tile, empty or occupied.</summary>
        Ground,

        /// <summary>The caster's own tile.</summary>
        Self,
    }

    /// <summary>The shape a skill covers around its target tile (Stage 06 implements the geometry).</summary>
    public enum AreaShape : byte
    {
        /// <summary>One tile.</summary>
        Single,

        /// <summary>A straight line away from the caster.</summary>
        Line,

        /// <summary>A plus shape.</summary>
        Cross,

        /// <summary>A Manhattan-radius diamond.</summary>
        Diamond,

        /// <summary>A filled square.</summary>
        Square,

        /// <summary>A cone widening away from the caster.</summary>
        Cone,
    }

    /// <summary>Which defense a skill's damage is checked against.</summary>
    public enum DamageKind : byte
    {
        /// <summary>Physical damage, reduced by <see cref="CombatStatBlock.Defense"/>.</summary>
        External,

        /// <summary>Internal damage, reduced by <see cref="CombatStatBlock.InnerDefense"/>.</summary>
        Inner,
    }

    /// <summary>Modifiers a skill carries besides its numbers.</summary>
    [System.Flags]
    public enum SkillFlags : ushort
    {
        /// <summary>No modifier.</summary>
        None = 0,

        /// <summary>The skill passes through other fighters.</summary>
        Pierce = 1,

        /// <summary>Guard does not apply.</summary>
        IgnoreGuard = 2,

        /// <summary>The target cannot counter.</summary>
        Uncounterable = 4,

        /// <summary>Show the cut-in when it resolves.</summary>
        Ultimate = 8,

        /// <summary>Refund part of the ATB after the cast.</summary>
        AtbRefund = 16,
    }

    /// <summary>
    /// One skill in a fighter's arena loadout: the resolved numbers, not the data row.
    /// </summary>
    /// <remarks>
    /// <see cref="Slot"/> is the index the HUD and <see cref="ArenaCommand.Slot"/> use;
    /// <see cref="WeaponType"/> is the Luban enum number and is checked against the
    /// loadout by Stage 05, not by the arena.
    /// </remarks>
    public struct ArenaSkillLoadout
    {
        /// <summary>The data row's skill id, carried into events and stats.</summary>
        public int SkillId;

        /// <summary>0-based slot; keys 1–4 on the command bar.</summary>
        public byte Slot;

        /// <summary>Skill level, 1–10.</summary>
        public byte Level;

        /// <summary>The weapon type the skill requires, as the Luban enum's number.</summary>
        public byte WeaponType;

        /// <summary>What the skill targets.</summary>
        public SkillTargeting Targeting;

        /// <summary>Minimum Manhattan range.</summary>
        public byte RangeMin;

        /// <summary>Maximum Manhattan range.</summary>
        public byte RangeMax;

        /// <summary>Area shape around the target.</summary>
        public AreaShape Area;

        /// <summary>Area size, in tiles; the shape decides what it measures.</summary>
        public byte AreaSize;

        /// <summary>Which defense the damage uses.</summary>
        public DamageKind Kind;

        /// <summary>Flat damage.</summary>
        public int BaseDamage;

        /// <summary>Attribute scaling, in hundredths of a percent.</summary>
        public int RatioX100;

        /// <summary>Mastery bonus, in hundredths of a percent.</summary>
        public int MasteryX100;

        /// <summary>How many hits a resolve delivers.</summary>
        public int Hits;

        /// <summary>Wind-up length in ticks.</summary>
        public int WindUpTicks;

        /// <summary>Recovery length in ticks.</summary>
        public int RecoveryTicks;

        /// <summary>Cooldown length in ticks after the cast.</summary>
        public int CooldownTicks;

        /// <summary>Inner power cost.</summary>
        public int NeiliCost;

        /// <summary>Fraction of the ATB refunded, in per-mille.</summary>
        public int AtbRefundPermille;

        /// <summary>Status applied on hit, or 0.</summary>
        public int StatusId;

        /// <summary>Chance to apply the status, in hundredths of a percent.</summary>
        public int StatusChanceX100;

        /// <summary>Status duration in ticks.</summary>
        public int StatusDurationTicks;

        /// <summary>Knockback distance in tiles.</summary>
        public byte KnockbackTiles;

        /// <summary>Skill modifiers.</summary>
        public SkillFlags Flags;
    }

    /// <summary>
    /// One fighter entering the arena. Built by
    /// <c>Domain.Character.ArenaFighterBuilder</c> (Stage 05); the arena does not read
    /// character sheets.
    /// </summary>
    /// <remarks>
    /// A class with public fields, matching the data-shaped DTO style of the project.
    /// The session copies every value it needs at construction, so a caller may reuse
    /// or mutate the setup afterwards without changing a running match.
    /// </remarks>
    public sealed class ArenaFighterSetup
    {
        /// <summary>Fighter id, unique within the match; <see cref="ArenaCommand.Actor"/> uses it.</summary>
        public byte Id;

        /// <summary>Team id.</summary>
        public byte Team;

        /// <summary>Appearance / roster row id.</summary>
        public int DefId;

        /// <summary>True when the player's input drives this fighter.</summary>
        public bool PlayerControlled;

        /// <summary>AI profile row id for an AI-controlled fighter; 0 otherwise.</summary>
        public int AiProfileId;

        /// <summary>Fallback reaction delay for an AI without a matching data profile.</summary>
        public int AiReactionTicks;

        /// <summary>Power rank used by profile difficulty delay scaling; 1 is the baseline.</summary>
        public int PowerRank = 1;

        /// <summary>Decisiveness attribute used by profile reaction delay scaling.</summary>
        public int Decisiveness;

        /// <summary>Where the fighter starts.</summary>
        public GridCoord StartTile;

        /// <summary>Which way the fighter faces at the start.</summary>
        public IsoDir StartFacing;

        /// <summary>Starting HP, or -1 for full.</summary>
        public int StartHp = -1;

        /// <summary>The resolved combat numbers.</summary>
        public CombatStatBlock Stats;

        /// <summary>Equipped skills, at most <see cref="ArenaConstants.MaxSkillSlots"/>.</summary>
        public IReadOnlyList<ArenaSkillLoadout> Skills = Array.Empty<ArenaSkillLoadout>();

        /// <summary>Passive ids from inner/movement skills and accessories.</summary>
        public IReadOnlyList<int> PassiveIds = Array.Empty<int>();
    }

    /// <summary>Everything needed to start one arena match deterministically.</summary>
    /// <remarks>
    /// The same setup plus the same command stream must produce the same
    /// <see cref="IArenaSession.ComputeStateHash"/> on any factory — that is what makes
    /// a saved tournament and a bug report replayable.
    /// </remarks>
    public sealed class ArenaMatchSetup
    {
        /// <summary>The match seed. Each session derives its random stream from it.</summary>
        public ulong Seed;

        /// <summary>The <c>arena_map</c> row id.</summary>
        public int ArenaMapId;

        /// <summary>Whether time keeps running while the player decides.</summary>
        public AtbMode Mode;

        /// <summary>Match length in ticks; 0 or less means no limit.</summary>
        public int TimeLimitTicks;

        /// <summary>The fighters, in display order.</summary>
        public IReadOnlyList<ArenaFighterSetup> Fighters = Array.Empty<ArenaFighterSetup>();

        /// <summary>Whether <see cref="ArenaCommandKind.DevKillEnemies"/> and its siblings are legal.</summary>
        public bool DevCheatsAllowed;
    }
}
