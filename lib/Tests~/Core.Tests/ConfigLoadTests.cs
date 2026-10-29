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

            // One authored row per table is enough for the skeleton; Stage 04 fills the content.
            Assert.NotEmpty(config.Attributes);
            Assert.NotEmpty(config.StatFormulas);
            Assert.NotEmpty(config.Skills);
            Assert.NotEmpty(config.SkillLevels);
            Assert.NotEmpty(config.Gear);
            Assert.NotEmpty(config.Statuses);
            Assert.NotEmpty(config.Fighters);
            Assert.NotEmpty(config.AiProfiles);
            Assert.NotEmpty(config.ArenaMaps);
            Assert.NotEmpty(config.Tournaments);
            Assert.NotEmpty(config.RewardPools);
            Assert.NotEmpty(config.Backgrounds);
        }

        [Fact]
        public void The_constant_row_carries_the_prototype_numbers()
        {
            // Values read from data/tables/constant.csv; a stub could not produce them.
            ConstantConfig constant = ConfigFixture.Load().Constant;

            Assert.Equal(10000, constant.AtbFull);
            Assert.Equal(3600, constant.MatchTicks);
            Assert.Equal(5000, constant.JumpAtbCost);
            Assert.Equal(1000, constant.DamageMitigationK);
            Assert.Equal(10, constant.MaxMasteryLevel);
        }

        [Fact]
        public void A_stat_formula_keeps_its_terms_in_data_order()
        {
            StatFormulaConfig formula = ConfigFixture.Load().StatFormulas.Single(row => row.Id == "stat.max_hp");

            Assert.Equal(30000, formula.BaseValue);
            Assert.Equal(2, formula.Terms.Count);
            Assert.Equal(EAttribute.Toughness, formula.Terms[0].Attribute);
            Assert.Equal(600, formula.Terms[0].Coefficient);
            Assert.Equal(EAttribute.Sinew, formula.Terms[1].Attribute);
            Assert.Equal(200, formula.Terms[1].Coefficient);
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

            // An empty optional cell reads back as an empty string, never as an invented default:
            // "this skill applies no status" and "this gear piece has no blurb" are values the data
            // can carry.
            Assert.Equal(string.Empty, external.StatusId);
            Assert.Equal(string.Empty, config.Gear.Single(row => row.Id == "gear.iron_sword").DescKey);
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

        /// <summary>A hand-built config, for the paths a real load cannot reach.</summary>
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

            // Empty rather than rows: this fake exists for the localization paths, and an empty
            // table is already the shape Data.Validator has a rule for.
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

            public bool TryGetLocalization(string key, out LocalizationEntry value) =>
                _byKey.TryGetValue(key, out value!);

            public string GetText(string key, Language language)
            {
                if (!_byKey.TryGetValue(key, out LocalizationEntry? entry))
                {
                    throw new KeyNotFoundException(key);
                }

                string text = entry.For(language);
                return string.IsNullOrEmpty(text) ? entry.For(language == Language.En ? Language.Th : Language.En) : text;
            }
        }
    }
}
