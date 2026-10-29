using System;
using System.Collections.Generic;
using System.Linq;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Data.Validator;
using Xunit;

namespace ProjectJianghu.Core.Tests
{
    /// <summary>
    /// One test per rule in <see cref="ConfigValidator"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each test starts from a valid snapshot and breaks exactly one thing, then asserts both
    /// halves of the contract: the rule fires, and it names the row it is about. Injecting one
    /// violation into a snapshot that is otherwise valid is what keeps a rule from passing
    /// because some unrelated row was wrong all along.
    /// </para>
    /// <para>
    /// The same <see cref="ConfigValidator"/> runs in the pipeline through
    /// tools/validate-data.ps1, so these tests are the fast, exact half of the evidence.
    /// </para>
    /// </remarks>
    public sealed class ConfigRulesTests
    {
        [Fact]
        public void The_shipped_data_has_no_findings()
        {
            // The baseline every other test breaks on purpose.
            Assert.Empty(ConfigValidator.Validate(ConfigFixture.Load()));
        }

        // -- constant ----------------------------------------------------------

        [Fact]
        public void A_non_positive_atb_bar_is_a_finding()
        {
            var config = new TestConfig();
            config.ConstantValue = new ConstantConfig { AtbFull = 0, MatchTicks = 3600, DamageMitigationK = 1000, MaxMasteryLevel = 10 };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("constant.non_positive", finding.Code);
            Assert.Equal("tbconstant.atb_full", finding.Target);
        }

        [Fact]
        public void A_negative_jump_cost_is_a_finding()
        {
            var config = new TestConfig();
            config.ConstantValue = new ConstantConfig
            {
                AtbFull = 10000,
                MatchTicks = 3600,
                DamageMitigationK = 1000,
                MaxMasteryLevel = 10,
                JumpAtbCost = -1,
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("constant.negative", finding.Code);
            Assert.Equal("tbconstant.jump_atb_cost", finding.Target);
        }

        [Fact]
        public void A_command_that_costs_more_than_a_full_bar_is_a_finding()
        {
            var config = new TestConfig();
            config.ConstantValue = new ConstantConfig
            {
                AtbFull = 10000,
                MatchTicks = 3600,
                DamageMitigationK = 1000,
                MaxMasteryLevel = 10,
                MoveAtbCost = 20000,
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("constant.out_of_range", finding.Code);
            Assert.Equal("tbconstant.move_atb_cost", finding.Target);
        }

        [Fact]
        public void Character_creation_bounds_must_be_ordered()
        {
            var config = new TestConfig();
            config.AttributeRows = new List<AttributeConfig>
            {
                new AttributeConfig { Id = "attribute.bad", Attribute = EAttribute.Toughness, CreationMin = 3, CreationMax = 2 },
            };
            config.StatFormulaRows[0].Terms = new List<StatTerm>
            {
                new StatTerm { Attribute = EAttribute.Toughness, Coefficient = 600, Min = 0, Max = 0 },
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("attribute.creation_bounds", finding.Code);
        }

        [Fact]
        public void Power_rank_thresholds_must_be_increasing()
        {
            var config = new TestConfig();
            config.ConstantValue = new ConstantConfig
            {
                AtbFull = 10000, MatchTicks = 3600, DamageMitigationK = 1000, MaxMasteryLevel = 10,
                MoveAtbCost = 10000, JumpAtbCost = 5000, MeditateAtbCost = 10000,
                PowerRank2Min = 10, PowerRank3Min = 9, PowerRank4Min = 20, PowerRank5Min = 30,
                PowerRank6Min = 40, PowerRank7Min = 50, PowerRank8Min = 60, PowerRank9Min = 70,
            };

            Assert.Contains(ConfigValidator.Validate(config), row => row.Code == "constant.power_rank_order");
        }

        // -- localization ------------------------------------------------------

        [Fact]
        public void A_duplicate_localization_key_is_a_finding()
        {
            var config = new TestConfig();
            config.LocalizationRows = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "ui.title", Th = "ไทย", En = "Title" },
                new LocalizationEntry { Key = "ui.title", Th = "ไทย 2", En = "Title 2" },
            };

            Assert.Equal("localization.duplicate_key", Assert.Single(ConfigValidator.Validate(config)).Code);
        }

        [Fact]
        public void A_blank_key_is_a_finding()
        {
            var config = new TestConfig();
            config.SkillRows = new List<SkillConfig>();
            config.FighterRows = new List<FighterConfig>();
            config.BackgroundRows = new List<BackgroundConfig>();
            config.LocalizationRows = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = string.Empty, Th = "ไทย", En = "English" },
            };

            Assert.Contains(ConfigValidator.Validate(config), row => row.Code == "localization.blank_key");
        }

        [Fact]
        public void A_row_with_no_text_in_either_language_is_a_finding()
        {
            var config = new TestConfig();
            config.SkillRows = new List<SkillConfig>();
            config.FighterRows = new List<FighterConfig>();
            config.BackgroundRows = new List<BackgroundConfig>();
            config.LocalizationRows = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "ui.empty", Th = string.Empty, En = string.Empty },
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config), row => row.Code == "localization.blank_text");
            Assert.Equal("localization.blank_text", finding.Code);
            Assert.Equal("tblocalization['ui.empty']", finding.Target);
        }

        [Fact]
        public void A_localization_row_missing_one_language_is_a_finding()
        {
            var config = new TestConfig();
            config.SkillRows = new List<SkillConfig>();
            config.FighterRows = new List<FighterConfig>();
            config.BackgroundRows = new List<BackgroundConfig>();
            config.LocalizationRows = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "ui.half", Th = "ไทย", En = string.Empty },
            };

            Assert.Contains(ConfigValidator.Validate(config), row => row.Code == "localization.missing_language");
        }

        [Fact]
        public void An_empty_localization_table_is_one_finding_not_none()
        {
            var config = new TestConfig();
            config.SkillRows = new List<SkillConfig>();
            config.FighterRows = new List<FighterConfig>();
            config.BackgroundRows = new List<BackgroundConfig>();
            config.LocalizationRows = new List<LocalizationEntry>();

            Assert.Contains(ConfigValidator.Validate(config), row => row.Code == "localization.empty");
        }

        // -- every keyed table -------------------------------------------------

        [Fact]
        public void An_empty_table_is_one_finding_not_none()
        {
            // The mastery ladder is the one table no other row points at, so emptying it cannot
            // make an unrelated row dangle: this test is about the empty table, nothing else.
            var config = new TestConfig();
            config.SkillLevelRows = new List<SkillLevelConfig>();

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("data.empty", finding.Code);
            Assert.Equal("tbskilllevel", finding.Target);
        }

        [Fact]
        public void A_duplicate_id_is_a_finding()
        {
            var config = new TestConfig();
            config.GearRows.Add(new GearConfig
            {
                Id = "gear.one",
                Slot = EGearSlot.Weapon,
                WeaponType = EWeaponType.Sword,
                NameKey = "ui.title",
            });

            Assert.Contains(ConfigValidator.Validate(config), row => row.Code == "data.duplicate_id" && row.Target == "tbgear['gear.one']");
        }

        [Fact]
        public void A_blank_id_is_a_finding()
        {
            // A tournament is referenced by nothing, so the blank id is the only finding.
            var config = new TestConfig();
            config.TournamentRows = new List<TournamentConfig>
            {
                new TournamentConfig { Id = "  ", Seats = 32, RewardChoices = 3, NameKey = "ui.title" },
            };

            Assert.Equal("data.blank_id", Assert.Single(ConfigValidator.Validate(config)).Code);
        }

        // -- text keys ---------------------------------------------------------

        [Fact]
        public void An_optional_text_key_that_does_not_exist_is_a_finding()
        {
            var config = new TestConfig();
            config.SkillRows[0].DescKey = "skill.gone";

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("localization.missing_key", finding.Code);
            Assert.Equal("tbskill['skill.one'].desc_key", finding.Target);
        }

        [Fact]
        public void An_empty_optional_text_key_is_not_a_finding()
        {
            // The baseline rows all leave desc_key empty; only a dangling key is a finding.
            Assert.Empty(ConfigValidator.Validate(new TestConfig()));
        }

        // -- references --------------------------------------------------------

        [Fact]
        public void A_gear_id_a_fighter_carries_that_does_not_exist_is_a_finding()
        {
            var config = new TestConfig();
            config.FighterRows[0].WeaponId = "gear.gone";

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("reference.missing", finding.Code);
            Assert.Equal("tbfighter['fighter.one'].weapon_id", finding.Target);
        }

        [Fact]
        public void A_skill_id_in_a_list_that_does_not_exist_is_a_finding()
        {
            var config = new TestConfig();
            config.BackgroundRows[0].StartSkillIds = new List<string> { "skill.one", "skill.gone" };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("reference.missing", finding.Code);
            Assert.Equal("tbbackground['bg.one'].start_skill_ids", finding.Target);
        }

        // -- stat_formula ------------------------------------------------------

        [Fact]
        public void A_term_on_an_attribute_with_no_attribute_row_is_a_finding()
        {
            var config = new TestConfig();
            config.StatFormulaRows[0].Terms = new List<StatTerm>
            {
                new StatTerm { Attribute = EAttribute.Toughness, Coefficient = 600, Min = 0, Max = 0 },
                new StatTerm { Attribute = EAttribute.Saber, Coefficient = 5, Min = 0, Max = 0 },
            };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("stat_formula.term_attribute_unknown", finding.Code);
            Assert.Equal("tbstatformula['stat.hp'].terms", finding.Target);
        }

        [Fact]
        public void A_clamp_max_below_the_clamp_min_is_a_finding()
        {
            var config = new TestConfig();
            config.StatFormulaRows[0].ClampMin = 100;
            config.StatFormulaRows[0].ClampMax = 10;

            Assert.Equal("stat_formula.clamp_order", Assert.Single(ConfigValidator.Validate(config)).Code);
        }

        // -- skill -------------------------------------------------------------

        [Fact]
        public void A_range_max_below_the_range_min_is_a_finding()
        {
            var config = new TestConfig();
            config.SkillRows[0].RangeMin = 2;
            config.SkillRows[0].RangeMax = 1;

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("skill.range_order", finding.Code);
            Assert.Equal("tbskill['skill.one']", finding.Target);
        }

        // -- gear and fighter loadouts -----------------------------------------

        [Fact]
        public void A_fighter_cannot_equip_a_weapon_for_a_different_external_skill()
        {
            var config = new TestConfig();
            config.SkillRows[0].WeaponType = EWeaponType.Fist;

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("fighter.loadout_weapon_mismatch", finding.Code);
            Assert.Equal("tbfighter['fighter.one'].skill_ids", finding.Target);
        }

        [Fact]
        public void A_fighter_weapon_must_come_from_the_weapon_slot()
        {
            var config = new TestConfig();
            config.GearRows.Add(new GearConfig { Id = "gear.armor.bad", Slot = EGearSlot.Armor, NameKey = "ui.title" });
            config.FighterRows[0].WeaponId = "gear.armor.bad";

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("fighter.loadout_weapon_slot", finding.Code);
        }

        [Fact]
        public void A_fighter_requires_all_four_skill_slots()
        {
            var config = new TestConfig();
            config.FighterRows[0].SkillIds = new List<string>();

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("fighter.loadout_skill_count", finding.Code);
            Assert.Equal("tbfighter['fighter.one'].skill_ids", finding.Target);
        }

        // -- arena_map ---------------------------------------------------------

        [Fact]
        public void A_mask_with_the_wrong_number_of_rows_is_a_finding()
        {
            var config = new TestConfig();
            config.ArenaMapRows[0].Mask = new List<string> { ".." };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("arena_map.mask_size", finding.Code);
            Assert.Equal("tbarenamap['map.one'].mask", finding.Target);
        }

        [Fact]
        public void A_mask_row_of_the_wrong_width_is_a_finding()
        {
            var config = new TestConfig();
            config.ArenaMapRows[0].Mask = new List<string> { "..", "..." };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("arena_map.mask_size", finding.Code);
            Assert.Contains("width is 2", finding.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void A_mask_cell_that_is_neither_walkable_nor_blocked_is_a_finding()
        {
            var config = new TestConfig();
            config.ArenaMapRows[0].Mask = new List<string> { "..", ".x" };

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("arena_map.mask_char", finding.Code);
        }

        // -- reward_pool -------------------------------------------------------

        [Fact]
        public void An_offer_naming_both_a_gear_row_and_a_skill_is_a_finding()
        {
            var config = new TestConfig();
            config.RewardPoolRows[0].SkillId = "skill.one";

            Finding finding = Assert.Single(ConfigValidator.Validate(config));
            Assert.Equal("reward_pool.source", finding.Code);
            Assert.Equal("tbrewardpool['reward.one']", finding.Target);
        }

        [Fact]
        public void An_offer_naming_neither_is_a_finding_too()
        {
            var config = new TestConfig();
            config.RewardPoolRows[0].GearId = string.Empty;

            Assert.Equal("reward_pool.source", Assert.Single(ConfigValidator.Validate(config)).Code);
        }

        [Fact]
        public void A_reward_round_band_that_runs_backwards_is_a_finding()
        {
            var config = new TestConfig();
            config.RewardPoolRows[0].RoundMin = 2;
            config.RewardPoolRows[0].RoundMax = 1;

            Assert.Equal("reward_pool.round_order", Assert.Single(ConfigValidator.Validate(config)).Code);
        }

        [Fact]
        public void Findings_are_ordered_so_two_runs_of_the_same_data_print_identically()
        {
            var config = new TestConfig();
            config.LocalizationRows = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "b", Th = string.Empty, En = string.Empty },
                new LocalizationEntry { Key = "a", Th = string.Empty, En = string.Empty },
            };

            IReadOnlyList<Finding> first = ConfigValidator.Validate(config);
            IReadOnlyList<Finding> second = ConfigValidator.Validate(config);

            Assert.Equal(
                first.Select(finding => finding.Code + finding.Target),
                second.Select(finding => finding.Code + finding.Target));
            Assert.Equal("tblocalization['a']", first[0].Target);
        }

        /// <summary>
        /// A valid snapshot a test mutates one field at a time.
        /// </summary>
        /// <remarks>
        /// Every row points at another row that exists, so a test that replaces one list cannot
        /// make an unrelated row dangle and fail for the wrong reason.
        /// </remarks>
        private sealed class TestConfig : IGameConfig
        {
            public string ConfigHash => ConfigFixture.DummyHash;

            public ConstantConfig ConstantValue { get; set; } = new ConstantConfig
            {
                AtbFull = 10000,
                MatchTicks = 3600,
                MoveAtbCost = 10000,
                JumpAtbCost = 5000,
                MeditateAtbCost = 10000,
                JumpCooldownTicks = 45,
                MeditateNeiliRestore = 300,
                DamageMitigationK = 1000,
                MaxMasteryLevel = 10,
                StartingAttributePoints = 20,
            };

            public List<LocalizationEntry> LocalizationRows { get; set; } = new List<LocalizationEntry>
            {
                new LocalizationEntry { Key = "ui.title", Th = "ไทย", En = "Title" },
            };

            public List<AttributeConfig> AttributeRows { get; set; } = new List<AttributeConfig>
            {
                new AttributeConfig
                {
                    Id = "attribute.toughness",
                    Attribute = EAttribute.Toughness,
                    Category = EAttrCategory.Body,
                    SortOrder = 10,
                    PowerWeight = 1,
                    RadarWeight = 1,
                    CreationMin = 0,
                    CreationMax = 100,
                    NameKey = "ui.title",
                },
            };

            public List<StatFormulaConfig> StatFormulaRows { get; set; } = new List<StatFormulaConfig>
            {
                new StatFormulaConfig
                {
                    Id = "stat.hp",
                    BaseValue = 100,
                    Terms = new List<StatTerm>
                    {
                        new StatTerm { Attribute = EAttribute.Toughness, Coefficient = 600, Min = 0, Max = 0 },
                    },
                    ClampMin = 1,
                    ClampMax = 1000000,
                    NameKey = "ui.title",
                },
            };

            public List<SkillConfig> SkillRows { get; set; } = new List<SkillConfig>
            {
                new SkillConfig
                {
                    Id = "skill.one",
                    Kind = ESkillKind.External,
                    WeaponType = EWeaponType.Sword,
                    Target = ESkillTarget.Enemy,
                    AreaShape = EAreaShape.Single,
                    RangeMin = 1,
                    RangeMax = 1,
                    AtbCost = 10000,
                    Hits = 1,
                    StatusId = "status.one",
                    LevelBonus = new List<SkillLevelBonus>
                    {
                        new SkillLevelBonus { Level = 5, Attribute = EAttribute.Sword, Value = 1 },
                        new SkillLevelBonus { Level = 10, Attribute = EAttribute.Sword, Value = 2 },
                    },
                    Level5SpecialKey = "ui.title",
                    Level10SpecialKey = "ui.title",
                    NameKey = "ui.title",
                },
                new SkillConfig
                {
                    Id = "skill.two",
                    Kind = ESkillKind.External,
                    WeaponType = EWeaponType.Sword,
                    Target = ESkillTarget.Enemy,
                    AreaShape = EAreaShape.Single,
                    RangeMin = 1,
                    RangeMax = 1,
                    AtbCost = 10000,
                    Hits = 1,
                    LevelBonus = new List<SkillLevelBonus>
                    {
                        new SkillLevelBonus { Level = 5, Attribute = EAttribute.Sword, Value = 1 },
                        new SkillLevelBonus { Level = 10, Attribute = EAttribute.Sword, Value = 2 },
                    },
                    Level5SpecialKey = "ui.title",
                    Level10SpecialKey = "ui.title",
                    NameKey = "ui.title",
                },
                new SkillConfig
                {
                    Id = "skill.three",
                    Kind = ESkillKind.External,
                    WeaponType = EWeaponType.Sword,
                    Target = ESkillTarget.Enemy,
                    AreaShape = EAreaShape.Single,
                    RangeMin = 1,
                    RangeMax = 1,
                    AtbCost = 10000,
                    Hits = 1,
                    LevelBonus = new List<SkillLevelBonus>
                    {
                        new SkillLevelBonus { Level = 5, Attribute = EAttribute.Sword, Value = 1 },
                        new SkillLevelBonus { Level = 10, Attribute = EAttribute.Sword, Value = 2 },
                    },
                    Level5SpecialKey = "ui.title",
                    Level10SpecialKey = "ui.title",
                    NameKey = "ui.title",
                },
                new SkillConfig
                {
                    Id = "skill.four",
                    Kind = ESkillKind.External,
                    WeaponType = EWeaponType.Sword,
                    Target = ESkillTarget.Enemy,
                    AreaShape = EAreaShape.Single,
                    RangeMin = 1,
                    RangeMax = 1,
                    AtbCost = 10000,
                    Hits = 1,
                    LevelBonus = new List<SkillLevelBonus>
                    {
                        new SkillLevelBonus { Level = 5, Attribute = EAttribute.Sword, Value = 1 },
                        new SkillLevelBonus { Level = 10, Attribute = EAttribute.Sword, Value = 2 },
                    },
                    Level5SpecialKey = "ui.title",
                    Level10SpecialKey = "ui.title",
                    NameKey = "ui.title",
                },
            };

            public List<SkillLevelConfig> SkillLevelRows { get; set; } = new List<SkillLevelConfig>
            {
                new SkillLevelConfig { Id = "mastery.level_1", Level = 1, MasteryXp = 0 },
            };

            public List<GearConfig> GearRows { get; set; } = new List<GearConfig>
            {
                new GearConfig { Id = "gear.one", Slot = EGearSlot.Weapon, WeaponType = EWeaponType.Sword, NameKey = "ui.title" },
                new GearConfig { Id = "gear.other", Slot = EGearSlot.Weapon, WeaponType = EWeaponType.Fist, NameKey = "ui.title" },
                new GearConfig { Id = "gear.armor", Slot = EGearSlot.Armor, NameKey = "ui.title" },
                new GearConfig { Id = "gear.accessory.one", Slot = EGearSlot.Accessory, NameKey = "ui.title" },
                new GearConfig { Id = "gear.accessory.two", Slot = EGearSlot.Accessory, NameKey = "ui.title" },
            };

            public List<StatusConfig> StatusRows { get; set; } = new List<StatusConfig>
            {
                new StatusConfig { Id = "status.one", Kind = EStatus.Poison, DurationTicks = 90, NameKey = "ui.title" },
            };

            public List<FighterConfig> FighterRows { get; set; } = new List<FighterConfig>
            {
                new FighterConfig
                {
                    Id = "fighter.one",
                    PowerRank = 3,
                    AiProfileId = "ai.one",
                    BackgroundId = "bg.one",
                    WeaponId = "gear.one",
                    ArmorId = "gear.armor",
                    AccessoryIds = new List<string> { "gear.accessory.one", "gear.accessory.two" },
                    SkillIds = new List<string> { "skill.one", "skill.two", "skill.three", "skill.four" },
                    NameKey = "ui.title",
                },
            };

            public List<AiProfileConfig> AiProfileRows { get; set; } = new List<AiProfileConfig>
            {
                new AiProfileConfig { Id = "ai.one", Archetype = EAiArchetype.Melee, NameKey = "ui.title" },
            };

            public List<ArenaMapConfig> ArenaMapRows { get; set; } = new List<ArenaMapConfig>
            {
                new ArenaMapConfig
                {
                    Id = "map.one",
                    Width = 2,
                    Height = 2,
                    Mask = new List<string> { "..", ".#" },
                    NameKey = "ui.title",
                },
            };

            public List<TournamentConfig> TournamentRows { get; set; } = new List<TournamentConfig>
            {
                new TournamentConfig { Id = "tour.one", Seats = 32, RewardChoices = 3, NameKey = "ui.title" },
            };

            public List<RewardPoolConfig> RewardPoolRows { get; set; } = new List<RewardPoolConfig>
            {
                new RewardPoolConfig { Id = "reward.one", RoundMin = 1, RoundMax = 1, Weight = 10, GearId = "gear.one" },
            };

            public List<BackgroundConfig> BackgroundRows { get; set; } = new List<BackgroundConfig>
            {
                new BackgroundConfig
                {
                    Id = "bg.one",
                    AttributePoints = 20,
                    StartSkillIds = new List<string> { "skill.one" },
                    StartGearIds = new List<string> { "gear.one" },
                    NameKey = "ui.title",
                },
            };

            public ConstantConfig Constant => ConstantValue;

            public IReadOnlyList<LocalizationEntry> Localization => LocalizationRows;

            public IReadOnlyList<AttributeConfig> Attributes => AttributeRows;

            public IReadOnlyList<StatFormulaConfig> StatFormulas => StatFormulaRows;

            public IReadOnlyList<SkillConfig> Skills => SkillRows;

            public IReadOnlyList<SkillLevelConfig> SkillLevels => SkillLevelRows;

            public IReadOnlyList<GearConfig> Gear => GearRows;

            public IReadOnlyList<StatusConfig> Statuses => StatusRows;

            public IReadOnlyList<FighterConfig> Fighters => FighterRows;

            public IReadOnlyList<AiProfileConfig> AiProfiles => AiProfileRows;

            public IReadOnlyList<ArenaMapConfig> ArenaMaps => ArenaMapRows;

            public IReadOnlyList<TournamentConfig> Tournaments => TournamentRows;

            public IReadOnlyList<RewardPoolConfig> RewardPools => RewardPoolRows;

            public IReadOnlyList<BackgroundConfig> Backgrounds => BackgroundRows;

            public bool TryGetLocalization(string key, out LocalizationEntry value) =>
                (value = LocalizationRows.FirstOrDefault(row => row.Key == key)!) is not null;

            public string GetText(string key, Language language)
            {
                LocalizationEntry? entry = LocalizationRows.FirstOrDefault(row => row.Key == key);
                if (entry is null)
                {
                    throw new KeyNotFoundException(key);
                }

                return entry.For(language);
            }
        }
    }
}
