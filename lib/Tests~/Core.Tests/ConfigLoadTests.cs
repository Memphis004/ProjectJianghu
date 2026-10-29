using System;
using System.Collections.Generic;
using System.Linq;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Core.Infrastructure.Config;
using ProjectJianghu.Data.Validator;
using Xunit;

namespace ProjectJianghu.Core.Tests
{
    /// <summary>
    /// The generated code compiles and the harness reads the real tables, plus "no stub that
    /// returns a constant instead of real config", which is why every assertion below is on a
    /// value that can only come from <c>lib/Tests~/data</c>.
    /// </summary>
    public sealed class ConfigLoadTests
    {
        [Fact]
        public void Every_table_loads_and_exposes_its_rows()
        {
            LubanGameConfig config = ConfigFixture.Load();

            Assert.NotEmpty(config.Localization);
            Assert.Equal(config.Localization.Count, config.Localization.Select(entry => entry.Key).Distinct().Count());
            Assert.Equal(34, config.Attributes.Count);
            Assert.Equal(19, config.StatFormulas.Count);
            Assert.Equal(13, config.Skills.Count);
            Assert.Equal(10, config.SkillLevels.Count);
            Assert.Equal(12, config.Gear.Count);
            Assert.Equal(4, config.Statuses.Count);
            Assert.Equal(31, config.Fighters.Count);
            Assert.Equal(4, config.AiProfiles.Count);
            Assert.Single(config.ArenaMaps);
            Assert.Single(config.Tournaments);
            Assert.NotEmpty(config.RewardPools);
            Assert.Equal(4, config.Backgrounds.Count);
        }

        [Fact]
        public void All_authored_attribute_rows_cover_combat_groups_and_display_only_life_skills()
        {
            IGameConfig config = ConfigFixture.Load();

            Assert.Equal(5, config.Attributes.Where(row => !row.DisplayOnly && row.Category != EAttrCategory.LifeSkill)
                .Select(row => row.Category).Distinct().Count());
            Assert.Equal(new[] { EAttribute.Fist, EAttribute.Sword, EAttribute.Saber, EAttribute.HiddenWeapon },
                config.Attributes.Where(row => row.Category == EAttrCategory.Martial && !row.DisplayOnly)
                    .Select(row => row.Attribute).ToArray());
            Assert.All(config.Attributes.Where(row => row.Category == EAttrCategory.LifeSkill),
                row => Assert.True(row.DisplayOnly));
            Assert.All(config.Attributes.Where(row => row.Category == EAttrCategory.Martial && row.DisplayOnly),
                row => Assert.Contains(row.Attribute, new[] { EAttribute.Finger, EAttribute.Spear, EAttribute.Staff }));
        }

        [Fact]
        public void Derived_stat_formula_contains_clamped_hp_at_the_midpoint_target()
        {
            StatFormulaConfig formula = ConfigFixture.Load().StatFormulas.Single(row => row.Id == "stat.max_hp");
            var middleAttributes = new Dictionary<EAttribute, int>
            {
                [EAttribute.Toughness] = 40,
                [EAttribute.Sinew] = 20,
            };
            int value = formula.BaseValue;
            foreach (StatTerm term in formula.Terms)
            {
                int attribute = middleAttributes[term.Attribute];
                value += Math.Clamp(term.Coefficient * attribute, term.Min, term.Max);
            }
            value = Math.Clamp(value, formula.ClampMin, formula.ClampMax);

            Assert.Equal(100000, value);
            Assert.Equal(80000, formula.ClampMin);
            Assert.Equal(120000, formula.ClampMax);
        }

        [Fact]
        public void Skill_table_has_required_weapon_distribution_levels_and_special_milestones()
        {
            IGameConfig config = ConfigFixture.Load();

            Assert.Equal(8, config.Skills.Count(row => row.Kind == ESkillKind.External));
            Assert.Equal(3, config.Skills.Count(row => row.Kind == ESkillKind.Inner));
            Assert.Equal(2, config.Skills.Count(row => row.Kind == ESkillKind.Movement));
            foreach (EWeaponType weapon in new[] { EWeaponType.Fist, EWeaponType.Sword, EWeaponType.Saber, EWeaponType.HiddenWeapon })
            {
                Assert.Equal(2, config.Skills.Count(row => row.Kind == ESkillKind.External && row.WeaponType == weapon));
            }

            Assert.Equal(Enumerable.Range(1, 10), config.SkillLevels.Select(row => row.Level).OrderBy(level => level));
            Assert.All(config.Skills, skill =>
            {
                Assert.Contains(skill.LevelBonus, bonus => bonus.Level == 5);
                Assert.Contains(skill.LevelBonus, bonus => bonus.Level == 10);
                Assert.True(config.TryGetLocalization(skill.Level5SpecialKey, out _));
                Assert.True(config.TryGetLocalization(skill.Level10SpecialKey, out _));
            });
        }

        [Fact]
        public void The_constant_row_carries_the_prototype_numbers()
        {
            ConstantConfig constant = ConfigFixture.Load().Constant;

            Assert.Equal(10000, constant.AtbFull);
            Assert.Equal(3600, constant.MatchTicks);
            Assert.Equal(5000, constant.JumpAtbCost);
            Assert.Equal(1000, constant.DamageMitigationK);
            Assert.Equal(5000, constant.BackDamageBonusX100);
            Assert.Equal(1500, constant.FlankDamageBonusX100);
            Assert.Equal(25, constant.MasteryXpPerUse);
            Assert.Equal(10, constant.MaxMasteryLevel);
        }

        [Fact]
        public void Every_fighter_loadout_uses_real_gear_and_weapon_compatible_external_skills()
        {
            IGameConfig config = ConfigFixture.Load();
            var gear = config.Gear.ToDictionary(row => row.Id, StringComparer.Ordinal);
            var skills = config.Skills.ToDictionary(row => row.Id, StringComparer.Ordinal);

            foreach (FighterConfig fighter in config.Fighters)
            {
                GearConfig weapon = gear[fighter.WeaponId];
                Assert.Equal(EGearSlot.Weapon, weapon.Slot);
                Assert.Equal(EGearSlot.Armor, gear[fighter.ArmorId].Slot);
                Assert.Equal(2, fighter.AccessoryIds.Count);
                Assert.All(fighter.AccessoryIds, id => Assert.Equal(EGearSlot.Accessory, gear[id].Slot));
                foreach (string skillId in fighter.SkillIds)
                {
                    SkillConfig skill = skills[skillId];
                    if (skill.Kind == ESkillKind.External)
                    {
                        Assert.Equal(weapon.WeaponType, skill.WeaponType);
                    }
                }
            }
        }

        [Fact]
        public void Tournament_content_counts_and_background_starts_match_the_prototype()
        {
            IGameConfig config = ConfigFixture.Load();

            Assert.Equal(Enumerable.Range(1, 7), config.Fighters.Select(row => row.PowerRank).Distinct().OrderBy(rank => rank));
            Assert.Equal(32, config.Tournaments.Single().Seats);
            Assert.Equal(new[] { "background.roaming_boxer", "background.saber_heir", "background.shadow_assassin", "background.sword_academy" },
                config.Backgrounds.Select(row => row.Id).OrderBy(id => id, StringComparer.Ordinal));
            Assert.All(config.Backgrounds, background =>
            {
                Assert.NotEmpty(background.Attributes);
                Assert.NotEmpty(background.StartSkillIds);
                Assert.NotEmpty(background.StartGearIds);
                Assert.Equal(20, background.AttributePoints);
            });
            Assert.Equal(new[] { EStatus.AcupointSeal, EStatus.InternalInjury, EStatus.Poison, EStatus.Stagger },
                config.Statuses.Select(row => row.Kind).OrderBy(kind => kind));
        }

        [Fact]
        public void A_stat_formula_keeps_its_terms_in_data_order()
        {
            StatFormulaConfig formula = ConfigFixture.Load().StatFormulas.Single(row => row.Id == "stat.max_hp");

            Assert.Equal(30000, formula.BaseValue);
            Assert.Equal(2, formula.Terms.Count);
            Assert.Equal(EAttribute.Toughness, formula.Terms[0].Attribute);
            Assert.Equal(1500, formula.Terms[0].Coefficient);
            Assert.Equal(EAttribute.Sinew, formula.Terms[1].Attribute);
            Assert.Equal(500, formula.Terms[1].Coefficient);
            Assert.True(formula.ClampMax > formula.ClampMin);
        }

        [Fact]
        public void A_skill_keeps_its_level_bonuses_and_its_optional_cells_empty()
        {
            IGameConfig config = ConfigFixture.Load();

            SkillConfig external = config.Skills.Single(row => row.Id == "skill.falling_rain_slash");
            Assert.Equal(ESkillKind.External, external.Kind);
            Assert.Equal(EWeaponType.Sword, external.WeaponType);
            Assert.Equal(ESkillTarget.Enemy, external.Target);
            Assert.Equal(2, external.LevelBonus.Count);
            Assert.Equal(10, external.LevelBonus[1].Level);
            Assert.Equal(string.Empty, external.StatusId);
        }

        [Fact]
        public void An_arena_mask_reads_back_one_string_per_row()
        {
            ArenaMapConfig map = ConfigFixture.Load().ArenaMaps.Single(row => row.Id == "map.open_court");

            Assert.Equal(12, map.Width);
            Assert.Equal(12, map.Height);
            Assert.Equal(map.Height, map.Mask.Count);
            Assert.All(map.Mask, line => Assert.Equal(map.Width, line.Length));
            Assert.Contains('#', string.Concat(map.Mask));
        }

        [Fact]
        public void The_shipped_hash_matches_the_tables_on_disk()
        {
            Dictionary<string, byte[]> tables = ConfigFixture.ReadTables();
            LubanGameConfig config = ConfigFixture.Load();

            Assert.Equal(ConfigHash.Compute(tables), config.ConfigHash);
            Assert.Equal(ProjectJianghu.Config.ConfigBuildMetadata.ConfigHash, config.ConfigHash);
        }

        [Fact]
        public void Localization_serves_both_languages_and_falls_back_to_the_other()
        {
            IGameConfig config = ConfigFixture.Load();
            LocalizationEntry entry = config.Localization[0];

            Assert.Equal(entry.Th, config.GetText(entry.Key, Language.Th));
            Assert.Equal(entry.En, config.GetText(entry.Key, Language.En));

            var partiallyTranslated = new FakeConfig(
                new[] { new LocalizationEntry { Key = "only.th", Th = "ไทย", En = string.Empty } });
            Assert.Equal("ไทย", partiallyTranslated.GetText("only.th", Language.En));
        }

        [Fact]
        public void A_missing_localization_key_throws_instead_of_returning_the_key()
        {
            IGameConfig config = ConfigFixture.Load();

            Assert.Throws<KeyNotFoundException>(() => config.GetText("ui.does_not_exist", Language.En));
            Assert.False(config.TryGetLocalization("ui.does_not_exist", out _));
            Assert.True(config.TryGetLocalization(config.Localization[0].Key, out _));
        }

        [Fact]
        public void A_missing_table_is_a_typed_failure()
        {
            var thrown = Assert.Throws<ConfigLoadException>(() =>
                ConfigFixture.LoadWith(name =>
                {
                    if (name == "tbconstant")
                    {
                        throw new NullReferenceException("no such table");
                    }

                    return ConfigFixture.ReadTables()[name];
                }));

            Assert.Equal(ConfigError.TableUnreadable, thrown.Code);
        }

        [Fact]
        public void The_adapter_refuses_to_be_built_without_a_hash()
        {
            Assert.Throws<ArgumentException>(() =>
                LubanGameConfig.Load(_ => Array.Empty<byte>(), string.Empty));
            Assert.Throws<ArgumentNullException>(() =>
                LubanGameConfig.Load(null!, ConfigFixture.DummyHash));
        }

        [Fact]
        public void TableNames_are_the_fourteen_the_game_ships()
        {
            Assert.Equal(
                new[]
                {
                    "tblocalization", "tbconstant", "tbattribute", "tbstatformula", "tbskill",
                    "tbskilllevel", "tbgear", "tbstatus", "tbfighter", "tbaiprofile", "tbarenamap",
                    "tbtournament", "tbrewardpool", "tbbackground",
                },
                LubanGameConfig.TableNames);
        }

        private sealed class FakeConfig : IGameConfig
        {
            private readonly Dictionary<string, LocalizationEntry> _byKey;

            public FakeConfig(IReadOnlyList<LocalizationEntry> rows)
            {
                Localization = rows;
                _byKey = rows.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
            }

            public string ConfigHash => ConfigFixture.DummyHash;
            public ConstantConfig Constant { get; } = new ConstantConfig();
            public IReadOnlyList<LocalizationEntry> Localization { get; }
            public IReadOnlyList<AttributeConfig> Attributes { get; } = Array.Empty<AttributeConfig>();
            public IReadOnlyList<StatFormulaConfig> StatFormulas { get; } = Array.Empty<StatFormulaConfig>();
            public IReadOnlyList<SkillConfig> Skills { get; } = Array.Empty<SkillConfig>();
            public IReadOnlyList<SkillLevelConfig> SkillLevels { get; } = Array.Empty<SkillLevelConfig>();
            public IReadOnlyList<GearConfig> Gear { get; } = Array.Empty<GearConfig>();
            public IReadOnlyList<StatusConfig> Statuses { get; } = Array.Empty<StatusConfig>();
            public IReadOnlyList<FighterConfig> Fighters { get; } = Array.Empty<FighterConfig>();
            public IReadOnlyList<AiProfileConfig> AiProfiles { get; } = Array.Empty<AiProfileConfig>();
            public IReadOnlyList<ArenaMapConfig> ArenaMaps { get; } = Array.Empty<ArenaMapConfig>();
            public IReadOnlyList<TournamentConfig> Tournaments { get; } = Array.Empty<TournamentConfig>();
            public IReadOnlyList<RewardPoolConfig> RewardPools { get; } = Array.Empty<RewardPoolConfig>();
            public IReadOnlyList<BackgroundConfig> Backgrounds { get; } = Array.Empty<BackgroundConfig>();
            public bool TryGetLocalization(string key, out LocalizationEntry value) => _byKey.TryGetValue(key, out value!);

            public string GetText(string key, Language language)
            {
                if (!_byKey.TryGetValue(key, out LocalizationEntry? entry)) throw new KeyNotFoundException(key);
                string text = entry.For(language);
                return string.IsNullOrEmpty(text) ? entry.For(language == Language.En ? Language.Th : Language.En) : text;
            }
        }
    }
}
