using System;
using System.Collections.Generic;

namespace ProjectJianghu.Core.Domain.Config
{
    /// <summary>One localization row: a stable key and its text per language.</summary>
    /// <remarks>
    /// Player-facing text lives only in the data table (AGENTS rule 11); code holds keys, never
    /// sentences. A key that is missing at runtime is a data bug and throws.
    /// </remarks>
    public sealed class LocalizationEntry
    {
        /// <summary>The stable key, e.g. <c>ui.title</c>.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Thai text.</summary>
        public string Th { get; set; } = string.Empty;

        /// <summary>English text.</summary>
        public string En { get; set; } = string.Empty;

        /// <summary>The text for a language.</summary>
        public string For(Language language) => language == Language.En ? En : Th;
    }

    /// <summary>
    /// One attribute bonus, carried by a row that is its source.
    /// </summary>
    /// <remarks>
    /// The owning row is the source, so a tooltip reads "+3 from &lt;the gear row's name&gt;"
    /// without a second column repeating the owner's id in every cell.
    /// </remarks>
    public sealed class AttrMod
    {
        /// <summary>Which attribute the bonus applies to.</summary>
        public EAttribute Attribute { get; set; }

        /// <summary>How much it adds. Negative values are penalties and are legal.</summary>
        public int Value { get; set; }
    }

    /// <summary>
    /// One term of a derived stat: <c>clamp(attribute * coefficient, min, max)</c>.
    /// </summary>
    /// <remarks>
    /// The coefficient is an integer on purpose: every value that reaches the simulation is
    /// hashed, and a hashed state never carries a float (AGENTS rule 5).
    /// </remarks>
    public sealed class StatTerm
    {
        /// <summary>The attribute this term reads.</summary>
        public EAttribute Attribute { get; set; }

        /// <summary>Multiplied with the attribute's value before clamping.</summary>
        public int Coefficient { get; set; }

        /// <summary>Lower clamp of this term's contribution.</summary>
        public int Min { get; set; }

        /// <summary>Upper clamp of this term's contribution.</summary>
        public int Max { get; set; }
    }

    /// <summary>An attribute bonus a skill grants once it reaches a level.</summary>
    public sealed class SkillLevelBonus
    {
        /// <summary>The mastery level that unlocks the bonus, 1..10.</summary>
        public int Level { get; set; }

        /// <summary>Which attribute the bonus applies to.</summary>
        public EAttribute Attribute { get; set; }

        /// <summary>How much it adds.</summary>
        public int Value { get; set; }
    }

    /// <summary>Display metadata for one attribute (the <c>attribute</c> table).</summary>
    public sealed class AttributeConfig
    {
        /// <summary>Stable id, e.g. <c>attribute.toughness</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>The enum value this row describes.</summary>
        public EAttribute Attribute { get; set; }

        /// <summary>The radar-chart category.</summary>
        public EAttrCategory Category { get; set; }

        /// <summary>True when the attribute is shown but changes no combat formula yet.</summary>
        public bool DisplayOnly { get; set; }

        /// <summary>Display order inside the category; ties are allowed.</summary>
        public int SortOrder { get; set; }

        /// <summary>Contribution weight for PowerScore.</summary>
        public int PowerWeight { get; set; }

        /// <summary>Weight when averaging this value into its radar category.</summary>
        public int RadarWeight { get; set; }

        /// <summary>Minimum character-creation value.</summary>
        public int CreationMin { get; set; }

        /// <summary>Maximum character-creation value.</summary>
        public int CreationMax { get; set; }

        /// <summary>Localization key of the attribute's name.</summary>
        public string NameKey { get; set; } = string.Empty;

        /// <summary>Optional localization key of the attribute's description.</summary>
        public string DescKey { get; set; } = string.Empty;
    }

    /// <summary>One derived combat stat (the <c>stat_formula</c> table).</summary>
    /// <remarks>
    /// This is the only place a stat formula lives (AGENTS rule 6):
    /// <c>clamp(base_value + sum(terms), clamp_min, clamp_max)</c>.
    /// </remarks>
    public sealed class StatFormulaConfig
    {
        /// <summary>Stable id, e.g. <c>stat.max_hp</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>The constant part of the formula.</summary>
        public int BaseValue { get; set; }

        /// <summary>The attribute terms, in data order.</summary>
        public IReadOnlyList<StatTerm> Terms { get; set; } = Array.Empty<StatTerm>();

        /// <summary>Lower clamp of the final value.</summary>
        public int ClampMin { get; set; }

        /// <summary>Upper clamp of the final value.</summary>
        public int ClampMax { get; set; }

        /// <summary>Localization key of the stat's name.</summary>
        public string NameKey { get; set; } = string.Empty;
    }

    /// <summary>One skill row (the <c>skill</c> table).</summary>
    public sealed class SkillConfig
    {
        /// <summary>Stable id, e.g. <c>skill.falling_rain_slash</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>External, inner or movement.</summary>
        public ESkillKind Kind { get; set; }

        /// <summary>The weapon a skill needs; <see cref="EWeaponType.None"/> for passives.</summary>
        public EWeaponType WeaponType { get; set; }

        /// <summary>Enemy, ground or self.</summary>
        public ESkillTarget Target { get; set; }

        /// <summary>The tiles the skill covers.</summary>
        public EAreaShape AreaShape { get; set; }

        /// <summary>
        /// What <see cref="AreaShape"/> measures — Line: tiles including the anchor,
        /// Cone: rows, Cross: arm length, Diamond: Manhattan radius, Square: half extent,
        /// Single: ignored (ADR 0113). 0 is the smallest area, one tile.
        /// </summary>
        public int AreaSize { get; set; }

        /// <summary>How many separate blows one cast lands; each rolls hit and crit on its own.</summary>
        public int Hits { get; set; }

        /// <summary>Minimum Manhattan range from the caster.</summary>
        public int RangeMin { get; set; }

        /// <summary>Maximum Manhattan range from the caster.</summary>
        public int RangeMax { get; set; }

        /// <summary>The flat part of the damage.</summary>
        public int Power { get; set; }

        /// <summary>Power ratio, times 100.</summary>
        public int RatioX100 { get; set; }

        /// <summary>ATB spent to use it, in ATB units (a full bar is <c>constant.atb_full</c>).</summary>
        public int AtbCost { get; set; }

        /// <summary>Telegraph length before the effect lands.</summary>
        public int WindUpTicks { get; set; }

        /// <summary>Length after the effect lands.</summary>
        public int RecoveryTicks { get; set; }

        /// <summary>Optional status id applied on a hit.</summary>
        public string StatusId { get; set; } = string.Empty;

        /// <summary>Chance the status lands, times 100.</summary>
        public int StatusChanceX100 { get; set; }

        /// <summary>How many tiles the target is pushed back.</summary>
        public int KnockbackTiles { get; set; }

        /// <summary>Attribute bonuses granted at specific mastery levels.</summary>
        public IReadOnlyList<SkillLevelBonus> LevelBonus { get; set; } = Array.Empty<SkillLevelBonus>();

        /// <summary>Localized special effect unlocked at mastery level 5.</summary>
        public string Level5SpecialKey { get; set; } = string.Empty;

        /// <summary>Localized special effect unlocked at mastery level 10.</summary>
        public string Level10SpecialKey { get; set; } = string.Empty;

        /// <summary>Localization key of the skill's name.</summary>
        public string NameKey { get; set; } = string.Empty;

        /// <summary>Optional localization key of the skill's description.</summary>
        public string DescKey { get; set; } = string.Empty;
    }

    /// <summary>One rung of the global mastery ladder (the <c>skill_level</c> table).</summary>
    public sealed class SkillLevelConfig
    {
        /// <summary>Stable id, e.g. <c>mastery.level_2</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>The level this rung unlocks, 1..10.</summary>
        public int Level { get; set; }

        /// <summary>Mastery XP needed to reach it from the level below.</summary>
        public int MasteryXp { get; set; }

        /// <summary>Combat damage mastery multiplier, times 100.</summary>
        public int CombatMasteryX100 { get; set; }
    }

    /// <summary>One piece of gear (the <c>gear</c> table).</summary>
    public sealed class GearConfig
    {
        /// <summary>Stable id, e.g. <c>gear.iron_sword</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Weapon, armour or accessory.</summary>
        public EGearSlot Slot { get; set; }

        /// <summary>For a weapon, the skills it enables.</summary>
        public EWeaponType WeaponType { get; set; }

        /// <summary>Base power a weapon adds.</summary>
        public int Power { get; set; }

        /// <summary>Attribute bonuses the piece carries.</summary>
        public IReadOnlyList<AttrMod> Mods { get; set; } = Array.Empty<AttrMod>();

        /// <summary>Localization key of the piece's name.</summary>
        public string NameKey { get; set; } = string.Empty;

        /// <summary>Optional localization key of the piece's description.</summary>
        public string DescKey { get; set; } = string.Empty;
    }

    /// <summary>One abnormal state (the <c>status</c> table).</summary>
    public sealed class StatusConfig
    {
        /// <summary>Stable id, e.g. <c>status.poison</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Which status this row tunes.</summary>
        public EStatus Kind { get; set; }

        /// <summary>How long it lasts.</summary>
        public int DurationTicks { get; set; }

        /// <summary>How often a damage-over-time tick lands.</summary>
        public int TickIntervalTicks { get; set; }

        /// <summary>Damage per tick; 0 for a status that only slows the bar.</summary>
        public int DamagePerTick { get; set; }

        /// <summary>ATB fill multiplier, times 100. 100 leaves it unchanged.</summary>
        public int AtbMultiplierX100 { get; set; }

        /// <summary>Inner power regeneration multiplier, times 100.</summary>
        public int NeiliRegenMultiplierX100 { get; set; }

        /// <summary>Localization key of the status's name.</summary>
        public string NameKey { get; set; } = string.Empty;

        /// <summary>Optional localization key of the status's description.</summary>
        public string DescKey { get; set; } = string.Empty;
    }

    /// <summary>One tournament opponent (the <c>fighter</c> table).</summary>
    public sealed class FighterConfig
    {
        /// <summary>Stable id, e.g. <c>fighter.rival_01</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Mastery rank 1..9, used to seed the bracket.</summary>
        public int PowerRank { get; set; }

        /// <summary>The AI profile that drives this fighter.</summary>
        public string AiProfileId { get; set; } = string.Empty;

        /// <summary>Optional background the fighter was built from.</summary>
        public string BackgroundId { get; set; } = string.Empty;

        /// <summary>Optional weapon id.</summary>
        public string WeaponId { get; set; } = string.Empty;

        /// <summary>Optional armour id.</summary>
        public string ArmorId { get; set; } = string.Empty;

        /// <summary>Accessory ids, in slot order.</summary>
        public IReadOnlyList<string> AccessoryIds { get; set; } = Array.Empty<string>();

        /// <summary>Skill ids the fighter knows.</summary>
        public IReadOnlyList<string> SkillIds { get; set; } = Array.Empty<string>();

        /// <summary>The fighter's attribute values.</summary>
        public IReadOnlyList<AttrMod> Attributes { get; set; } = Array.Empty<AttrMod>();

        /// <summary>Localization key of the fighter's name.</summary>
        public string NameKey { get; set; } = string.Empty;

        /// <summary>Optional localization key of the fighter's blurb.</summary>
        public string DescKey { get; set; } = string.Empty;
    }

    /// <summary>One AI behaviour profile (the <c>ai_profile</c> table).</summary>
    public sealed class AiProfileConfig
    {
        /// <summary>Stable id, e.g. <c>ai.melee</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>The archetype this profile implements.</summary>
        public EAiArchetype Archetype { get; set; }

        /// <summary>The range the AI tries to fight at.</summary>
        public int EngageRange { get; set; }

        /// <summary>HP ratio (times 100) below which a kiter retreats.</summary>
        public int RetreatHpX100 { get; set; }

        /// <summary>Weight of attacking over repositioning, times 100.</summary>
        public int AggressionX100 { get; set; }

        /// <summary>How long the AI waits between decisions.</summary>
        public int DecisionDelayTicks { get; set; }

        /// <summary>Localization key of the profile's name.</summary>
        public string NameKey { get; set; } = string.Empty;
    }

    /// <summary>One arena layout (the <c>arena_map</c> table).</summary>
    /// <remarks>
    /// <see cref="Mask"/> holds one string per row: <c>.</c> is walkable, <c>#</c> is blocked.
    /// <c>Data.Validator</c> owns the size rule, because Luban cannot express "this list must be
    /// <c>Height</c> long and every element <c>Width</c> characters".
    /// </remarks>
    public sealed class ArenaMapConfig
    {
        /// <summary>Stable id, e.g. <c>map.open_court</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Tiles across.</summary>
        public int Width { get; set; }

        /// <summary>Tiles down.</summary>
        public int Height { get; set; }

        /// <summary>One string per row, <c>.</c> walkable and <c>#</c> blocked.</summary>
        public IReadOnlyList<string> Mask { get; set; } = Array.Empty<string>();

        /// <summary>Localization key of the map's name.</summary>
        public string NameKey { get; set; } = string.Empty;
    }

    /// <summary>One tournament configuration (the <c>tournament</c> table).</summary>
    public sealed class TournamentConfig
    {
        /// <summary>Stable id, e.g. <c>tournament.arena_open</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>How many entrants the bracket has.</summary>
        public int Seats { get; set; }

        /// <summary>How many rewards are offered after a round.</summary>
        public int RewardChoices { get; set; }

        /// <summary>Attribute points awarded for entering.</summary>
        public int AttributePoints { get; set; }

        /// <summary>Localization key of the tournament's name.</summary>
        public string NameKey { get; set; } = string.Empty;
    }

    /// <summary>One weighted reward offer (the <c>reward_pool</c> table).</summary>
    public sealed class RewardPoolConfig
    {
        /// <summary>Stable id, e.g. <c>reward.round_1_gear</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>The first round the offer can appear in.</summary>
        public int RoundMin { get; set; }

        /// <summary>The last round the offer can appear in.</summary>
        public int RoundMax { get; set; }

        /// <summary>Relative weight among the offers of a round.</summary>
        public int Weight { get; set; }

        /// <summary>Optional gear id; exactly one of <see cref="GearId"/> and <see cref="SkillId"/> is set.</summary>
        public string GearId { get; set; } = string.Empty;

        /// <summary>Optional skill manual id.</summary>
        public string SkillId { get; set; } = string.Empty;

        /// <summary>Optional localization key for the offer's flavour text.</summary>
        public string NoteKey { get; set; } = string.Empty;
    }

    /// <summary>One character-creation background (the <c>background</c> table).</summary>
    /// <remarks>
    /// Not to be confused with a background <i>match</i>, which is a bracket term
    /// (glossary section 7).
    /// </remarks>
    public sealed class BackgroundConfig
    {
        /// <summary>Stable id, e.g. <c>background.sword_academy</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Attribute points this background grants on top of the pool.</summary>
        public int AttributePoints { get; set; }

        /// <summary>The attribute values the background starts with.</summary>
        public IReadOnlyList<AttrMod> Attributes { get; set; } = Array.Empty<AttrMod>();

        /// <summary>Skills the character starts knowing.</summary>
        public IReadOnlyList<string> StartSkillIds { get; set; } = Array.Empty<string>();

        /// <summary>Gear the character starts owning.</summary>
        public IReadOnlyList<string> StartGearIds { get; set; } = Array.Empty<string>();

        /// <summary>Localization key of the background's name.</summary>
        public string NameKey { get; set; } = string.Empty;

        /// <summary>Optional localization key of the background's description.</summary>
        public string DescKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// The singleton row of tuned numbers every build needs (the <c>constant</c> table).
    /// </summary>
    /// <remarks>
    /// The prototype's ATB, timing and mitigation numbers live here and nowhere else (AGENTS
    /// rule 6). Ticks are 30 Hz; a "x100" field is a times-100 integer.
    /// </remarks>
    public sealed class ConstantConfig
    {
        /// <summary>ATB units in a full bar.</summary>
        public int AtbFull { get; set; }

        /// <summary>Length of one match, in ticks (3,600 is 120 seconds).</summary>
        public int MatchTicks { get; set; }

        /// <summary>ATB spent on a move.</summary>
        public int MoveAtbCost { get; set; }

        /// <summary>ATB spent on a jump.</summary>
        public int JumpAtbCost { get; set; }

        /// <summary>ATB spent on meditating.</summary>
        public int MeditateAtbCost { get; set; }

        /// <summary>How long a jump is on cooldown.</summary>
        public int JumpCooldownTicks { get; set; }

        /// <summary>Inner power restored by meditating.</summary>
        public int MeditateNeiliRestore { get; set; }

        /// <summary>The K of the <c>Defense / (Defense + K)</c> mitigation formula.</summary>
        public int DamageMitigationK { get; set; }

        /// <summary>Damage multiplier bonus when striking from behind, times 100.</summary>
        public int BackDamageBonusX100 { get; set; }

        /// <summary>Damage multiplier bonus when striking from the flank, times 100.</summary>
        public int FlankDamageBonusX100 { get; set; }

        /// <summary>Mastery XP granted for each skill use.</summary>
        public int MasteryXpPerUse { get; set; }

        /// <summary>The highest mastery level a skill can reach.</summary>
        public int MaxMasteryLevel { get; set; }

        /// <summary>Attribute points a character starts with.</summary>
        public int StartingAttributePoints { get; set; }

        /// <summary>Intellect mastery XP bonus per point, times 100.</summary>
        public int MasteryIntellectBonusX100PerPoint { get; set; }

        /// <summary>Minimum PowerScore for rank two; later ranks use their corresponding thresholds.</summary>
        public int PowerRank2Min { get; set; }
        public int PowerRank3Min { get; set; }
        public int PowerRank4Min { get; set; }
        public int PowerRank5Min { get; set; }
        public int PowerRank6Min { get; set; }
        public int PowerRank7Min { get; set; }
        public int PowerRank8Min { get; set; }
        public int PowerRank9Min { get; set; }
    }
}
