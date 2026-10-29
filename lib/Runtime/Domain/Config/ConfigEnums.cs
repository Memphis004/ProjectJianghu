namespace ProjectJianghu.Core.Domain.Config
{
    /// <summary>
    /// A language the localization table carries.
    /// </summary>
    /// <remarks>
    /// A hand-written mirror of the Luban enum with the same names and values, converted by a
    /// plain cast in the Infrastructure adapter — Domain must not reference generated code
    /// (ADR 0006). <c>ConfigEnumParityTests</c> is what keeps the two copies honest.
    /// </remarks>
    public enum Language
    {
        /// <summary>Thai.</summary>
        Th = 1,

        /// <summary>English.</summary>
        En = 2,
    }

    /// <summary>The five combat categories on the character sheet's radar chart, plus life skills.</summary>
    /// <remarks>Mirrors <c>EAttrCategory</c> in <c>data/Defines/schema.xml</c>; see ADR 0103.</remarks>
    public enum EAttrCategory
    {
        /// <summary>Weapon mastery: the martial values.</summary>
        Martial = 1,

        /// <summary>Inner power: pool, flow, force and lightness.</summary>
        Neili = 2,

        /// <summary>The body: waist power, lower body, toughness, sinew.</summary>
        Body = 3,

        /// <summary>The mind: resolve, focus, composure, decisiveness, wit.</summary>
        Mind = 4,

        /// <summary>The meridians: the du and ren pulses.</summary>
        Meridian = 5,

        /// <summary>Life skills. Display only in the prototype, except intellect.</summary>
        LifeSkill = 6,
    }

    /// <summary>Every attribute a character carries.</summary>
    /// <remarks>
    /// <para>
    /// Mirrors <c>EAttribute</c>. The numbers are stable identifiers (they appear in replays and
    /// saves), and they are grouped in blocks so a future attribute takes the next free number
    /// instead of renumbering a save: Martial 1..9, Neili 10..19, Body 20..29, Mind 30..39,
    /// Meridian 40..49, LifeSkill 50..69.
    /// </para>
    /// <para>
    /// The enum says which attribute code can name; the <c>attribute</c> table says how it is
    /// shown and whether it does anything yet.
    /// </para>
    /// </remarks>
    public enum EAttribute
    {
        /// <summary>Fist and palm techniques.</summary>
        Fist = 1,

        /// <summary>Sword techniques.</summary>
        Sword = 2,

        /// <summary>Saber techniques.</summary>
        Saber = 3,

        /// <summary>Hidden weapon techniques.</summary>
        HiddenWeapon = 4,

        /// <summary>Finger techniques. Placeholder: no prototype skill uses it.</summary>
        Finger = 5,

        /// <summary>Spear techniques. Placeholder: no prototype skill uses it.</summary>
        Spear = 6,

        /// <summary>Staff techniques. Placeholder: no prototype skill uses it.</summary>
        Staff = 7,

        /// <summary>How large the inner power pool is.</summary>
        NeiliPool = 10,

        /// <summary>How fast inner power regenerates.</summary>
        NeiliFlow = 11,

        /// <summary>The base of inner damage.</summary>
        NeiliPower = 12,

        /// <summary>Lightness: walk and jump range, ATB and evasion.</summary>
        Lightness = 13,

        /// <summary>Waist power: the base of external damage and of critical damage.</summary>
        WaistPower = 20,

        /// <summary>Lower body: knockback resistance.</summary>
        LowerBody = 21,

        /// <summary>Toughness: the main term of maximum HP.</summary>
        Toughness = 22,

        /// <summary>Sinew: HP and defense.</summary>
        Sinew = 23,

        /// <summary>Resolve: status resistance.</summary>
        Resolve = 30,

        /// <summary>Focus: accuracy.</summary>
        Focus = 31,

        /// <summary>Composure: counter chance and inner defense.</summary>
        Composure = 32,

        /// <summary>Decisiveness: ATB fill time and how fast an AI decides.</summary>
        Decisiveness = 33,

        /// <summary>Wit: critical chance.</summary>
        Wit = 34,

        /// <summary>The du pulse: the ceiling of the inner power pool.</summary>
        DuPulse = 40,

        /// <summary>The ren pulse: inner power regeneration and status resistance.</summary>
        RenPulse = 41,

        /// <summary>Intellect: the one life skill with an effect (mastery XP rate).</summary>
        Intellect = 50,

        /// <summary>Lore. Display only.</summary>
        Lore = 51,

        /// <summary>Virtue. Display only.</summary>
        Virtue = 52,

        /// <summary>Charm. Display only.</summary>
        Charm = 53,

        /// <summary>Fortune. Display only.</summary>
        Fortune = 54,

        /// <summary>Killing intent. Display only.</summary>
        KillingIntent = 55,

        /// <summary>Medicine. Display only.</summary>
        Medicine = 56,

        /// <summary>Toxicology. Display only.</summary>
        Toxicology = 57,

        /// <summary>Cooking. Display only.</summary>
        Cooking = 58,

        /// <summary>Wine. Display only.</summary>
        Wine = 59,

        /// <summary>Music. Display only.</summary>
        Music = 60,

        /// <summary>Brush. Display only.</summary>
        Brush = 61,
    }

    /// <summary>Which weapon a skill or a piece of gear belongs to.</summary>
    /// <remarks>
    /// <see cref="None"/> is 0 so an empty data cell is legal: an inner or movement skill carries
    /// no weapon, and neither does a non-weapon gear row.
    /// </remarks>
    public enum EWeaponType
    {
        /// <summary>No weapon required.</summary>
        None = 0,

        /// <summary>Fist and palm, including a gauntlet.</summary>
        Fist = 1,

        /// <summary>Sword.</summary>
        Sword = 2,

        /// <summary>Saber.</summary>
        Saber = 3,

        /// <summary>Hidden weapon.</summary>
        HiddenWeapon = 4,
    }

    /// <summary>External, inner or movement.</summary>
    public enum ESkillKind
    {
        /// <summary>A skill pressed in the arena; must match the held weapon.</summary>
        External = 1,

        /// <summary>A passive that adds attributes and sets the inner element.</summary>
        Inner = 2,

        /// <summary>A passive that adds range, ATB, evasion and a jump effect.</summary>
        Movement = 3,
    }

    /// <summary>Who a skill is pointed at.</summary>
    /// <remarks>
    /// An <c>ArenaCommand</c> always carries a target tile, so <see cref="Ground"/> means "this
    /// tile, possibly empty" and <see cref="Self"/> means "the caster's own tile".
    /// </remarks>
    public enum ESkillTarget
    {
        /// <summary>The target tile must hold an enemy.</summary>
        Enemy = 1,

        /// <summary>Any tile, empty or occupied.</summary>
        Ground = 2,

        /// <summary>The caster's own tile.</summary>
        Self = 3,
    }

    /// <summary>The shape a skill covers around its target tile.</summary>
    public enum EAreaShape
    {
        /// <summary>One tile.</summary>
        Single = 1,

        /// <summary>A straight line away from the caster.</summary>
        Line = 2,

        /// <summary>A plus shape.</summary>
        Cross = 3,

        /// <summary>A diamond (Manhattan radius).</summary>
        Diamond = 4,

        /// <summary>A filled square.</summary>
        Square = 5,

        /// <summary>A cone widening away from the caster.</summary>
        Cone = 6,
    }

    /// <summary>The abnormal states a skill can apply.</summary>
    public enum EStatus
    {
        /// <summary>The ATB bar stops.</summary>
        AcupointSeal = 1,

        /// <summary>Inner power regenerates slower and the fighter takes damage over time.</summary>
        InternalInjury = 2,

        /// <summary>Damage over time.</summary>
        Poison = 3,

        /// <summary>The ATB bar fills slower.</summary>
        Stagger = 4,
    }

    /// <summary>Where a piece of gear is worn.</summary>
    public enum EGearSlot
    {
        /// <summary>One weapon, which decides the external skills that can be used.</summary>
        Weapon = 1,

        /// <summary>One armour piece.</summary>
        Armor = 2,

        /// <summary>Two accessory slots.</summary>
        Accessory = 3,
    }

    /// <summary>How an AI opponent behaves.</summary>
    public enum EAiArchetype
    {
        /// <summary>Closes in and stays in melee.</summary>
        Melee = 1,

        /// <summary>Fires from range and backs off.</summary>
        Kiter = 2,

        /// <summary>Soaks damage with inner power.</summary>
        InnerTank = 3,

        /// <summary>Dodges telegraphs by jumping.</summary>
        Evader = 4,
    }
}
