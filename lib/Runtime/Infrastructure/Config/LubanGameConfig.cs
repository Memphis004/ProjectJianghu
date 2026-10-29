using System;
using System.Collections.Generic;
using Luban;
using ProjectJianghu.Core.Domain.Config;
using GeneratedTables = ProjectJianghu.Config.Tables;

// The domain enums are hand-written mirrors of the generated ones (ConfigEnums.cs), and every cast
// below is safe only while the two copies agree; ConfigEnumParityTests is what keeps them in step.

namespace ProjectJianghu.Core.Infrastructure.Config
{
    /// <summary>
    /// <see cref="IGameConfig"/> over the Luban-generated <c>ProjectJianghu.Config</c>
    /// tables. This is the single place where generated rows are read; everything
    /// upstream of it works with domain types (AGENTS rule 2, ADR 0006).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The adapter never invents a value. A table that cannot be read, a missing
    /// singleton row, or a duplicate localization key raises
    /// <see cref="ConfigLoadException"/> - there is no "return a constant instead"
    /// path, which is what the data gate forbids.
    /// </para>
    /// <para>
    /// It is engine-free on purpose: the caller supplies
    /// <paramref name="readTable"/>, which maps a Luban table name ("tblocalization") to
    /// its raw bytes. In Unity that comes from <c>Resources/Config/*.bytes</c>; in
    /// the dotnet harness and the data validator it comes from
    /// <c>lib/Tests~/data/</c> on disk. Nothing here touches
    /// <c>Application.streamingAssetsPath</c>.
    /// </para>
    /// </remarks>
    public sealed class LubanGameConfig : IGameConfig
    {
        /// <summary>Luban's generated loader key for each table, in report order.</summary>
        /// <remarks>
        /// Kept in step with <c>ProjectJianghu.Config.Tables</c>: the validator, the Sim and the
        /// Unity loaders all iterate this list, so a table missing from it is a table nothing
        /// loads and nothing checks.
        /// </remarks>
        public static readonly string[] TableNames =
        {
            "tblocalization",
            "tbconstant",
            "tbattribute",
            "tbstatformula",
            "tbskill",
            "tbskilllevel",
            "tbgear",
            "tbstatus",
            "tbfighter",
            "tbaiprofile",
            "tbarenamap",
            "tbtournament",
            "tbrewardpool",
            "tbbackground",
        };

        private readonly Dictionary<string, LocalizationEntry> _localization =
            new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);

        private readonly List<LocalizationEntry> _localizationList = new List<LocalizationEntry>();
        private readonly List<AttributeConfig> _attributes = new List<AttributeConfig>();
        private readonly List<StatFormulaConfig> _statFormulas = new List<StatFormulaConfig>();
        private readonly List<SkillConfig> _skills = new List<SkillConfig>();
        private readonly List<SkillLevelConfig> _skillLevels = new List<SkillLevelConfig>();
        private readonly List<GearConfig> _gear = new List<GearConfig>();
        private readonly List<StatusConfig> _statuses = new List<StatusConfig>();
        private readonly List<FighterConfig> _fighters = new List<FighterConfig>();
        private readonly List<AiProfileConfig> _aiProfiles = new List<AiProfileConfig>();
        private readonly List<ArenaMapConfig> _arenaMaps = new List<ArenaMapConfig>();
        private readonly List<TournamentConfig> _tournaments = new List<TournamentConfig>();
        private readonly List<RewardPoolConfig> _rewardPools = new List<RewardPoolConfig>();
        private readonly List<BackgroundConfig> _backgrounds = new List<BackgroundConfig>();

        private LubanGameConfig(string configHash, GeneratedTables tables)
        {
            ConfigHash = configHash;

            foreach (ProjectJianghu.Config.Localization row in tables.TbLocalization.DataList)
            {
                var entry = new LocalizationEntry { Key = row.Key, Th = row.Th, En = row.En };
                if (_localization.ContainsKey(entry.Key))
                {
                    // Luban's table index already makes a duplicate key an error;
                    // this guard exists so the adapter can never silently drop a
                    // row if that ever stops being true.
                    throw new ConfigLoadException(ConfigError.DuplicateLocalizationKey, "tblocalization",
                        "duplicate localization key '" + entry.Key + "'");
                }

                _localization.Add(entry.Key, entry);
                _localizationList.Add(entry);
            }

            ProjectJianghu.Config.ConstantDef constant = tables.TbConstant.Data;
            Constant = new ConstantConfig
            {
                AtbFull = constant.AtbFull,
                MatchTicks = constant.MatchTicks,
                MoveAtbCost = constant.MoveAtbCost,
                JumpAtbCost = constant.JumpAtbCost,
                MeditateAtbCost = constant.MeditateAtbCost,
                JumpCooldownTicks = constant.JumpCooldownTicks,
                MeditateNeiliRestore = constant.MeditateNeiliRestore,
                DamageMitigationK = constant.DamageMitigationK,
                BackDamageBonusX100 = constant.BackDamageBonusX100,
                FlankDamageBonusX100 = constant.FlankDamageBonusX100,
                MasteryXpPerUse = constant.MasteryXpPerUse,
                MaxMasteryLevel = constant.MaxMasteryLevel,
                StartingAttributePoints = constant.StartingAttributePoints,
                MasteryIntellectBonusX100PerPoint = constant.MasteryIntellectBonusX100PerPoint,
                PowerRank2Min = constant.PowerRank2Min,
                PowerRank3Min = constant.PowerRank3Min,
                PowerRank4Min = constant.PowerRank4Min,
                PowerRank5Min = constant.PowerRank5Min,
                PowerRank6Min = constant.PowerRank6Min,
                PowerRank7Min = constant.PowerRank7Min,
                PowerRank8Min = constant.PowerRank8Min,
                PowerRank9Min = constant.PowerRank9Min,
            };

            foreach (ProjectJianghu.Config.AttributeDef row in tables.TbAttribute.DataList)
            {
                _attributes.Add(new AttributeConfig
                {
                    Id = row.Id,
                    Attribute = (EAttribute)row.Attribute,
                    Category = (EAttrCategory)row.Category,
                    DisplayOnly = row.DisplayOnly,
                    SortOrder = row.SortOrder,
                    PowerWeight = row.PowerWeight,
                    RadarWeight = row.RadarWeight,
                    CreationMin = row.CreationMin,
                    CreationMax = row.CreationMax,
                    NameKey = row.NameKey,
                    DescKey = row.DescKey,
                });
            }

            foreach (ProjectJianghu.Config.StatFormulaDef row in tables.TbStatFormula.DataList)
            {
                _statFormulas.Add(new StatFormulaConfig
                {
                    Id = row.Id,
                    BaseValue = row.BaseValue,
                    Terms = MapStatTerms(row.Terms),
                    ClampMin = row.ClampMin,
                    ClampMax = row.ClampMax,
                    NameKey = row.NameKey,
                });
            }

            foreach (ProjectJianghu.Config.SkillDef row in tables.TbSkill.DataList)
            {
                _skills.Add(new SkillConfig
                {
                    Id = row.Id,
                    Kind = (ESkillKind)row.Kind,
                    WeaponType = (EWeaponType)row.WeaponType,
                    Target = (ESkillTarget)row.Target,
                    AreaShape = (EAreaShape)row.AreaShape,
                    RangeMin = row.RangeMin,
                    RangeMax = row.RangeMax,
                    Power = row.Power,
                    RatioX100 = row.RatioX100,
                    AtbCost = row.AtbCost,
                    WindUpTicks = row.WindUpTicks,
                    RecoveryTicks = row.RecoveryTicks,
                    StatusId = row.StatusId,
                    StatusChanceX100 = row.StatusChanceX100,
                    KnockbackTiles = row.KnockbackTiles,
                    LevelBonus = MapLevelBonuses(row.LevelBonus),
                    Level5SpecialKey = row.Level5SpecialKey,
                    Level10SpecialKey = row.Level10SpecialKey,
                    NameKey = row.NameKey,
                    DescKey = row.DescKey,
                });
            }

            foreach (ProjectJianghu.Config.SkillLevelDef row in tables.TbSkillLevel.DataList)
            {
                _skillLevels.Add(new SkillLevelConfig
                {
                    Id = row.Id,
                    Level = row.Level,
                    MasteryXp = row.MasteryXp,
                    CombatMasteryX100 = row.CombatMasteryX100,
                });
            }

            foreach (ProjectJianghu.Config.GearDef row in tables.TbGear.DataList)
            {
                _gear.Add(new GearConfig
                {
                    Id = row.Id,
                    Slot = (EGearSlot)row.Slot,
                    WeaponType = (EWeaponType)row.WeaponType,
                    Power = row.Power,
                    Mods = MapAttrMods(row.Mods),
                    NameKey = row.NameKey,
                    DescKey = row.DescKey,
                });
            }

            foreach (ProjectJianghu.Config.StatusDef row in tables.TbStatus.DataList)
            {
                _statuses.Add(new StatusConfig
                {
                    Id = row.Id,
                    Kind = (EStatus)row.Kind,
                    DurationTicks = row.DurationTicks,
                    TickIntervalTicks = row.TickIntervalTicks,
                    DamagePerTick = row.DamagePerTick,
                    AtbMultiplierX100 = row.AtbMultiplierX100,
                    NeiliRegenMultiplierX100 = row.NeiliRegenMultiplierX100,
                    NameKey = row.NameKey,
                    DescKey = row.DescKey,
                });
            }

            foreach (ProjectJianghu.Config.FighterDef row in tables.TbFighter.DataList)
            {
                _fighters.Add(new FighterConfig
                {
                    Id = row.Id,
                    PowerRank = row.PowerRank,
                    AiProfileId = row.AiProfileId,
                    BackgroundId = row.BackgroundId,
                    WeaponId = row.WeaponId,
                    ArmorId = row.ArmorId,
                    AccessoryIds = Copy(row.AccessoryIds),
                    SkillIds = Copy(row.SkillIds),
                    Attributes = MapAttrMods(row.Attributes),
                    NameKey = row.NameKey,
                    DescKey = row.DescKey,
                });
            }

            foreach (ProjectJianghu.Config.AiProfileDef row in tables.TbAiProfile.DataList)
            {
                _aiProfiles.Add(new AiProfileConfig
                {
                    Id = row.Id,
                    Archetype = (EAiArchetype)row.Archetype,
                    EngageRange = row.EngageRange,
                    RetreatHpX100 = row.RetreatHpX100,
                    AggressionX100 = row.AggressionX100,
                    DecisionDelayTicks = row.DecisionDelayTicks,
                    NameKey = row.NameKey,
                });
            }

            foreach (ProjectJianghu.Config.ArenaMapDef row in tables.TbArenaMap.DataList)
            {
                _arenaMaps.Add(new ArenaMapConfig
                {
                    Id = row.Id,
                    Width = row.Width,
                    Height = row.Height,
                    Mask = Copy(row.Mask),
                    NameKey = row.NameKey,
                });
            }

            foreach (ProjectJianghu.Config.TournamentDef row in tables.TbTournament.DataList)
            {
                _tournaments.Add(new TournamentConfig
                {
                    Id = row.Id,
                    Seats = row.Seats,
                    RewardChoices = row.RewardChoices,
                    AttributePoints = row.AttributePoints,
                    NameKey = row.NameKey,
                });
            }

            foreach (ProjectJianghu.Config.RewardPoolDef row in tables.TbRewardPool.DataList)
            {
                _rewardPools.Add(new RewardPoolConfig
                {
                    Id = row.Id,
                    RoundMin = row.RoundMin,
                    RoundMax = row.RoundMax,
                    Weight = row.Weight,
                    GearId = row.GearId,
                    SkillId = row.SkillId,
                    NoteKey = row.NoteKey,
                });
            }

            foreach (ProjectJianghu.Config.BackgroundDef row in tables.TbBackground.DataList)
            {
                _backgrounds.Add(new BackgroundConfig
                {
                    Id = row.Id,
                    AttributePoints = row.AttributePoints,
                    Attributes = MapAttrMods(row.Attributes),
                    StartSkillIds = Copy(row.StartSkillIds),
                    StartGearIds = Copy(row.StartGearIds),
                    NameKey = row.NameKey,
                    DescKey = row.DescKey,
                });
            }
        }

        /// <inheritdoc />
        public string ConfigHash { get; }

        /// <inheritdoc />
        public ConstantConfig Constant { get; }

        /// <inheritdoc />
        public IReadOnlyList<LocalizationEntry> Localization => _localizationList;

        /// <inheritdoc />
        public IReadOnlyList<AttributeConfig> Attributes => _attributes;

        /// <inheritdoc />
        public IReadOnlyList<StatFormulaConfig> StatFormulas => _statFormulas;

        /// <inheritdoc />
        public IReadOnlyList<SkillConfig> Skills => _skills;

        /// <inheritdoc />
        public IReadOnlyList<SkillLevelConfig> SkillLevels => _skillLevels;

        /// <inheritdoc />
        public IReadOnlyList<GearConfig> Gear => _gear;

        /// <inheritdoc />
        public IReadOnlyList<StatusConfig> Statuses => _statuses;

        /// <inheritdoc />
        public IReadOnlyList<FighterConfig> Fighters => _fighters;

        /// <inheritdoc />
        public IReadOnlyList<AiProfileConfig> AiProfiles => _aiProfiles;

        /// <inheritdoc />
        public IReadOnlyList<ArenaMapConfig> ArenaMaps => _arenaMaps;

        /// <inheritdoc />
        public IReadOnlyList<TournamentConfig> Tournaments => _tournaments;

        /// <inheritdoc />
        public IReadOnlyList<RewardPoolConfig> RewardPools => _rewardPools;

        /// <inheritdoc />
        public IReadOnlyList<BackgroundConfig> Backgrounds => _backgrounds;

        /// <inheritdoc />
        public bool TryGetLocalization(string key, out LocalizationEntry value)
        {
            if (string.IsNullOrEmpty(key))
            {
                value = null!;
                return false;
            }

            return _localization.TryGetValue(key, out value!);
        }

        /// <inheritdoc />
        /// <exception cref="KeyNotFoundException">The key is not in the table.</exception>
        public string GetText(string key, Language language)
        {
            if (!_localization.TryGetValue(key, out LocalizationEntry? entry))
            {
                throw new KeyNotFoundException("no localization row for key '" + key + "'");
            }

            string text = entry.For(language);

            // An empty cell falls back to the other language rather than to the key: a build
            // missing one translation is still readable, while a build printing "ui.title" is not.
            return string.IsNullOrEmpty(text) ? entry.For(language == Language.En ? Language.Th : Language.En) : text;
        }

        /// <summary>
        /// Builds the config from raw table bytes.
        /// </summary>
        /// <param name="readTable">
        /// Maps a Luban table name (see <see cref="TableNames"/>) to its raw bytes.
        /// Must return non-null for every requested table.
        /// </param>
        /// <param name="configHash">SHA-256 over exactly those bytes; see tools/gen.ps1.</param>
        /// <exception cref="ConfigLoadException">
        /// A table is missing, the loader threw, or a mapped value is malformed.
        /// </exception>
        public static LubanGameConfig Load(Func<string, byte[]> readTable, string configHash)
        {
            if (readTable is null)
            {
                throw new ArgumentNullException(nameof(readTable));
            }

            if (string.IsNullOrEmpty(configHash))
            {
                throw new ArgumentException("configHash must be set", nameof(configHash));
            }

            GeneratedTables tables;
            try
            {
                tables = new GeneratedTables(name =>
                {
                    byte[]? bytes = readTable(name);
                    if (bytes is null)
                    {
                        throw new ConfigLoadException(ConfigError.TableMissing, name,
                            "table '" + name + "' was not provided");
                    }

                    return new ByteBuf(bytes);
                });
            }
            catch (ConfigLoadException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new ConfigLoadException(ConfigError.TableUnreadable, "tables",
                    "Luban failed to read the tables: " + exception.Message, exception);
            }

            return new LubanGameConfig(configHash, tables);
        }

        private static List<AttrMod> MapAttrMods(List<ProjectJianghu.Config.AttrMod> rows)
        {
            var mapped = new List<AttrMod>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                mapped.Add(new AttrMod { Attribute = (EAttribute)rows[i].Attribute, Value = rows[i].Value });
            }

            return mapped;
        }

        private static List<StatTerm> MapStatTerms(List<ProjectJianghu.Config.StatTerm> rows)
        {
            var mapped = new List<StatTerm>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                mapped.Add(new StatTerm
                {
                    Attribute = (EAttribute)rows[i].Attribute,
                    Coefficient = rows[i].Coefficient,
                    Min = rows[i].Min,
                    Max = rows[i].Max,
                });
            }

            return mapped;
        }

        private static List<SkillLevelBonus> MapLevelBonuses(List<ProjectJianghu.Config.SkillLevelBonus> rows)
        {
            var mapped = new List<SkillLevelBonus>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                mapped.Add(new SkillLevelBonus
                {
                    Level = rows[i].Level,
                    Attribute = (EAttribute)rows[i].Attribute,
                    Value = rows[i].Value,
                });
            }

            return mapped;
        }

        private static List<string> Copy(List<string> rows) => new List<string>(rows);
    }

    /// <summary>Machine-readable config failure codes.</summary>
    public static class ConfigError
    {
        /// <summary>A table the adapter asked for was not supplied.</summary>
        public const string TableMissing = "config.table_missing";

        /// <summary>A table was supplied but could not be parsed.</summary>
        public const string TableUnreadable = "config.table_unreadable";

        /// <summary>Two localization rows share a key.</summary>
        public const string DuplicateLocalizationKey = "localization.duplicate_key";
    }

    /// <summary>
    /// Raised when the generated config cannot be turned into a usable snapshot.
    /// <see cref="Code"/> is a stable identifier (never player text) so the data
    /// validator can report it without parsing a message.
    /// </summary>
    public sealed class ConfigLoadException : Exception
    {
        /// <summary>Creates a failure without an inner exception.</summary>
        public ConfigLoadException(string code, string target, string message)
            : base(message)
        {
            Code = code;
            Target = target;
        }

        /// <summary>Creates a failure that wraps the loader's own exception.</summary>
        public ConfigLoadException(string code, string target, string message, Exception innerException)
            : base(message, innerException)
        {
            Code = code;
            Target = target;
        }

        /// <summary>Stable failure identifier, e.g. <c>config.table_missing</c>.</summary>
        public string Code { get; }

        /// <summary>Where the failure is, e.g. <c>tblocalization</c>.</summary>
        public string Target { get; }
    }
}
