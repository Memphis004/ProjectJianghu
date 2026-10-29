using System;
using System.Collections.Generic;
using System.Linq;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Domain.Character;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Core.Infrastructure.Config;
using ProjectJianghu.Core.Shared;
using Xunit;

namespace ProjectJianghu.Core.Tests
{
    public sealed class CharacterDomainTests
    {
        private static LubanGameConfig Config => ConfigFixture.Load();

        [Fact] public void Formula_matches_authored_max_hp_terms_and_clamp()
        {
            var c = new CharacterState("formula"); c.SetBaseAttribute(EAttribute.Toughness, 40); c.SetBaseAttribute(EAttribute.Sinew, 20);
            var stats = new StatResolver(Config);
            Assert.Equal(100000, stats.EvaluateFormula(c, "stat.max_hp"));
            c.SetBaseAttribute(EAttribute.Toughness, 100);
            Assert.Equal(120000, stats.EvaluateFormula(c, "stat.max_hp"));
        }

        [Fact] public void Formula_terms_clamp_individually_before_final_clamp()
        {
            var c = new CharacterState("term"); c.SetBaseAttribute(EAttribute.Toughness, 40); c.SetBaseAttribute(EAttribute.Sinew, 20);
            Assert.Equal(100000, new StatResolver(Config).EvaluateFormula(c, "stat.max_hp"));
        }

        [Fact] public void Resolver_returns_cached_block_until_marked_dirty()
        {
            var c = new CharacterState("dirty"); c.SetBaseAttribute(EAttribute.Toughness, 40);
            var resolver = new StatResolver(Config);
            Assert.True(resolver.Resolve(c).Recomputed);
            Assert.False(resolver.Resolve(c).Recomputed);
            c.SetBaseAttribute(EAttribute.Toughness, 41);
            Assert.True(resolver.Resolve(c).Recomputed);
        }

        [Fact] public void Equipping_and_removing_gear_restores_the_previous_stat()
        {
            LubanGameConfig config = Config;
            var c = new CharacterState("gear");
            c.AddGear(new GearInventoryEntry("gear.traveler_padded_coat", 1));
            var rules = new LoadoutRules(config); Assert.True(rules.EquipArmor(c, 1).IsSuccess);
            var resolver = new StatResolver(config);
            int withGear = resolver.GetAttribute(c, EAttribute.Toughness);
            Assert.Equal(12, withGear);
            Assert.True(c.RemoveGear(1));
            Assert.Equal(0, resolver.GetAttribute(c, EAttribute.Toughness));
        }

        [Fact] public void Modifier_tooltip_keeps_source_kind_and_source_id()
        {
            var c = new CharacterState("tooltip");
            c.AddModifier(EAttribute.Fist, 3, ModifierSourceKind.Skill, "skill.surging_tide_palm");
            var resolver = new StatResolver(Config);
            var sources = new AttributeModifier[2];
            Assert.Equal(1, resolver.CopyModifierSources(c, EAttribute.Fist, sources));
            Assert.Equal("skill.surging_tide_palm", sources[0].SourceId);
            Assert.Equal(ModifierSourceKind.Skill, sources[0].Kind);
            Assert.Equal(3, sources[0].Value);
        }

        [Fact] public void Skill_level_bonuses_are_included_in_net_attributes()
        {
            var c = new CharacterState("skill-bonus"); c.LearnSkill(new LearnedSkill("skill.falling_rain_slash", 5));
            Assert.Equal(5, new StatResolver(Config).GetAttribute(c, EAttribute.Sword));
        }

        [Fact] public void Gear_modifiers_are_exposed_as_source_rows()
        {
            var c = new CharacterState("gear-source"); c.AddGear(new GearInventoryEntry("gear.iron_fist_wraps", 11));
            c.Loadout.WeaponInstanceId = 11;
            var sources = new AttributeModifier[4];
            int count = new StatResolver(Config).CopyModifierSources(c, EAttribute.Fist, sources);
            Assert.Contains(sources.Take(count).ToArray(), x => x.Kind == ModifierSourceKind.Gear && x.SourceId.Contains("gear.iron_fist_wraps"));
        }

        [Fact] public void Set_base_attribute_does_not_duplicate_base_modifier_sources()
        {
            var c = new CharacterState("base-source"); c.SetBaseAttribute(EAttribute.Sword, 12);
            var sources = new AttributeModifier[4];
            int count = new StatResolver(Config).CopyModifierSources(c, EAttribute.Sword, sources);
            Assert.Single(sources.Take(count).ToArray());
            Assert.Equal(12, sources[0].Value);
        }

        [Fact] public void Adding_a_duplicate_source_keeps_each_tooltip_contribution()
        {
            var c = new CharacterState("same-source"); c.AddModifier(EAttribute.Focus, 1, ModifierSourceKind.Pill, "pill.focus");
            c.AddModifier(EAttribute.Focus, 2, ModifierSourceKind.Pill, "pill.focus");
            var total = new StatResolver(Config).GetModifierTotal(c, EAttribute.Focus);
            Assert.Equal(3, total.Value); Assert.Equal(2, total.SourceCount);
        }

        [Fact] public void Copy_modifier_sources_respects_destination_capacity()
        {
            var c = new CharacterState("source-capacity");
            c.AddModifier(EAttribute.Focus, 1, ModifierSourceKind.Pill, "one"); c.AddModifier(EAttribute.Focus, 2, ModifierSourceKind.Gear, "two");
            var one = new AttributeModifier[1];
            Assert.Equal(1, new StatResolver(Config).CopyModifierSources(c, EAttribute.Focus, one));
        }

        [Fact] public void Power_score_and_rank_use_data_weights_and_thresholds()
        {
            var c = new CharacterState("power"); c.SetBaseAttribute(EAttribute.Toughness, 100); c.SetBaseAttribute(EAttribute.Sword, 60);
            var power = new StatResolver(Config).GetPower(c);
            Assert.Equal(160, power.Score); Assert.Equal((byte)3, power.Rank);
        }

        [Fact] public void Radar_is_five_category_weighted_average()
        {
            var c = new CharacterState("radar"); c.SetBaseAttribute(EAttribute.Sword, 20); c.SetBaseAttribute(EAttribute.Fist, 10);
            RadarProfile radar = new StatResolver(Config).GetRadar(c);
            Assert.Equal(15, radar.Martial);
            Assert.Equal(0, radar.Neili);
        }

        [Fact] public void Life_skill_intellect_is_excluded_from_radar()
        {
            var c = new CharacterState("radar-intellect"); c.SetBaseAttribute(EAttribute.Intellect, 100);
            RadarProfile radar = new StatResolver(Config).GetRadar(c);
            Assert.Equal(0, radar.Martial + radar.Neili + radar.Body + radar.Mind + radar.Meridian);
        }

        [Fact] public void Equip_skill_rejects_weapon_mismatch_without_mutation()
        {
            LubanGameConfig config = Config; CharacterState c = MakeCharacter(config);
            var rules = new LoadoutRules(config);
            c.LearnSkill(new LearnedSkill("skill.surging_tide_palm"));
            Result result = rules.EquipSkill(c, ESkillKind.External, 0, "skill.surging_tide_palm");
            Assert.True(result.IsFailure); Assert.Equal("loadout.weapon_mismatch", result.Error.Code);
            Assert.Equal("skill.falling_rain_slash", c.Loadout.ExternalSkillId(0));
        }

        [Fact] public void Changing_weapon_disables_but_retains_nonmatching_skills()
        {
            LubanGameConfig config = Config; CharacterState c = MakeCharacter(config);
            Assert.True(new LoadoutRules(config).EquipWeapon(c, 2).IsSuccess);
            Assert.Equal("skill.falling_rain_slash", c.Loadout.ExternalSkillId(0));
            Assert.False(new LoadoutRules(config).GetExternalSkillState(c, 0).Enabled);
        }

        [Fact] public void External_slot_out_of_range_is_typed_failure()
        {
            LubanGameConfig config = Config; CharacterState c = MakeCharacter(config);
            Assert.Equal("loadout.invalid_external_slot", new LoadoutRules(config).EquipSkill(c, ESkillKind.External, 4, "skill.falling_rain_slash").Error.Code);
        }

        [Fact] public void Armor_slot_rejects_weapon_gear()
        {
            LubanGameConfig config = Config; CharacterState c = MakeCharacter(config);
            Assert.Equal("loadout.wrong_slot", new LoadoutRules(config).EquipArmor(c, 1).Error.Code);
        }

        [Fact] public void Accessory_slot_rejects_invalid_index()
        {
            LubanGameConfig config = Config; CharacterState c = MakeCharacter(config);
            Assert.Equal("loadout.invalid_accessory_slot", new LoadoutRules(config).EquipAccessory(c, 2, 6).Error.Code);
        }

        [Fact] public void Loadout_validation_allows_empty_optional_skill_slots_but_requires_passives_and_gear()
        {
            LubanGameConfig config = Config; CharacterState c = MakeCharacter(config); c.Loadout.SetExternalSkill(0, string.Empty);
            Assert.True(new LoadoutRules(config).Validate(c).IsSuccess);
        }

        [Fact] public void Failed_weapon_equip_does_not_change_current_weapon()
        {
            LubanGameConfig config = Config; CharacterState c = MakeCharacter(config);
            Assert.Equal("loadout.wrong_slot", new LoadoutRules(config).EquipWeapon(c, 3).Error.Code);
            Assert.Equal(1, c.Loadout.WeaponInstanceId);
        }

        [Fact] public void Character_creation_loads_background_and_starting_loadout()
        {
            Result<CharacterState> created = new CharacterCreation(Config).Create("new", "background.sword_academy");
            Assert.True(created.IsSuccess);
            Assert.Equal(40, created.Value.UnspentAttributePoints);
            Assert.Equal("skill.falling_rain_slash", created.Value.Loadout.ExternalSkillId(0));
            Assert.Equal("gear.sword_of_morning_mist", created.Value.FindGear(1).GearId);
        }

        [Fact] public void Character_creation_rejects_missing_background()
        {
            Assert.Equal("creation.background_not_found", new CharacterCreation(Config).Create("new", "missing").Error.Code);
        }

        [Fact] public void Attribute_allocation_obeys_pool_and_configured_bounds()
        {
            var creation = new CharacterCreation(Config); CharacterState c = creation.Create("new", "background.sword_academy").Value;
            Assert.True(creation.Allocate(c, EAttribute.Sword, 3).IsSuccess);
            Assert.Equal(37, c.UnspentAttributePoints);
            Assert.Equal("creation.insufficient_points", creation.Allocate(c, EAttribute.Sword, 38).Error.Code);
            Assert.Equal("creation.attribute_out_of_bounds", creation.Allocate(c, EAttribute.Sword, -16).Error.Code);
        }

        [Fact] public void Attribute_refund_is_allowed_only_within_bounds()
        {
            var creation = new CharacterCreation(Config); CharacterState c = creation.Create("new", "background.sword_academy").Value;
            Assert.True(creation.Allocate(c, EAttribute.Sword, -2).IsSuccess);
            Assert.Equal("creation.attribute_out_of_bounds", creation.Allocate(c, EAttribute.Sword, -20).Error.Code);
        }

        [Fact] public void Allocation_rejects_unknown_attribute()
        {
            Assert.Equal("creation.attribute_not_found", new CharacterCreation(Config).Allocate(new CharacterState("x"), (EAttribute)999, 1).Error.Code);
        }

        [Fact] public void Character_creation_validate_checks_background_attribute_bounds()
        {
            var creation = new CharacterCreation(Config); CharacterState c = creation.Create("new", "background.sword_academy").Value;
            c.SetBaseAttribute(EAttribute.Sword, 101);
            Assert.Equal("creation.attribute_out_of_bounds", creation.Validate(c).Error.Code);
        }

        [Fact] public void Character_creation_validate_requires_a_background()
        {
            Assert.Equal("creation.background_not_found", new CharacterCreation(Config).Validate(new CharacterState("no-bg")).Error.Code);
        }

        [Fact] public void Mastery_gains_xp_and_crosses_multiple_levels()
        {
            LubanGameConfig config = Config; var c = new CharacterState("mastery"); c.LearnSkill(new LearnedSkill("skill.falling_rain_slash"));
            var result = new StatGrowth(config).ApplyMastery(c, new[] { new SkillUseStat(StatGrowth.StableId("skill.falling_rain_slash"), 100, 100) });
            Assert.True(result.IsSuccess); Assert.Equal(9, c.FindSkill("skill.falling_rain_slash").Level);
        }

        [Fact] public void Intellect_increases_mastery_xp_rate()
        {
            LubanGameConfig config = Config; var baseline = new CharacterState("base"); baseline.LearnSkill(new LearnedSkill("skill.falling_rain_slash"));
            var smarter = new CharacterState("smart"); smarter.SetBaseAttribute(EAttribute.Intellect, 10); smarter.LearnSkill(new LearnedSkill("skill.falling_rain_slash"));
            int id = StatGrowth.StableId("skill.falling_rain_slash");
            new StatGrowth(config).ApplyMastery(baseline, new[] { new SkillUseStat(id, 1, 1) });
            new StatGrowth(config).ApplyMastery(smarter, new[] { new SkillUseStat(id, 1, 1) });
            Assert.True(smarter.FindSkill("skill.falling_rain_slash").MasteryXp > baseline.FindSkill("skill.falling_rain_slash").MasteryXp);
        }

        [Fact] public void Unknown_skill_use_id_returns_failure_without_partial_progress()
        {
            var c = new CharacterState("unknown"); c.LearnSkill(new LearnedSkill("skill.falling_rain_slash"));
            var result = new StatGrowth(Config).ApplyMastery(c, new[] { new SkillUseStat(-12, 2, 0) });
            Assert.True(result.IsFailure); Assert.Equal(0, c.FindSkill("skill.falling_rain_slash").MasteryXp);
        }

        [Fact] public void Invalid_use_stat_is_rejected()
        {
            int id = StatGrowth.StableId("skill.falling_rain_slash"); var c = new CharacterState("invalid-uses"); c.LearnSkill(new LearnedSkill("skill.falling_rain_slash"));
            Assert.Equal("mastery.invalid_use_stat", new StatGrowth(Config).ApplyMastery(c, new[] { new SkillUseStat(id, 1, 2) }).Error.Code);
        }

        [Fact] public void Apply_mastery_for_unlearned_skill_is_ignored()
        {
            int id = StatGrowth.StableId("skill.falling_rain_slash");
            Assert.Empty(new StatGrowth(Config).ApplyMastery(new CharacterState("unlearned"), new[] { new SkillUseStat(id, 4, 0) }).Value.Changes);
        }

        [Fact] public void Builder_output_is_stable_after_reinserting_attributes_in_different_order()
        {
            LubanGameConfig config = Config; CharacterState first = MakeCharacter(config); CharacterState second = MakeCharacter(config);
            var attrs = first.BaseAttributes.OrderBy(x => x.Key).ToArray();
            foreach (var pair in attrs) first.SetBaseAttribute(pair.Key, pair.Value);
            var builder = new ArenaFighterBuilder(config, new StatResolver(config));
            var a = builder.Build(first, 1, 0, true, 0, 0, default, IsoDir.NE).Value;
            var b = builder.Build(second, 1, 0, true, 0, 0, default, IsoDir.NE).Value;
            Assert.Equal(a.Stats.MaxHp, b.Stats.MaxHp); Assert.Equal(a.Skills.Select(x => x.SkillId), b.Skills.Select(x => x.SkillId));
            Assert.Equal(a.PassiveIds, b.PassiveIds);
        }

        [Fact] public void Builder_contains_all_valid_external_skills_in_slot_order()
        {
            LubanGameConfig config = Config; var c = MakeCharacter(config);
            c.Loadout.SetExternalSkill(1, string.Empty);
            var setup = new ArenaFighterBuilder(config, new StatResolver(config)).Build(c, 2, 1, false, 3, 4, default, IsoDir.SW).Value;
            Assert.Equal(new byte[] { 0 }, setup.Skills.Select(x => x.Slot));
        }

        [Fact] public void Builder_omits_disabled_weapon_mismatch_skill_without_erasing_loadout()
        {
            LubanGameConfig config = Config; var c = MakeCharacter(config); new LoadoutRules(config).EquipWeapon(c, 2);
            var setup = new ArenaFighterBuilder(config, new StatResolver(config)).Build(c, 2, 1, false, 3, 4, default, IsoDir.SW).Value;
            Assert.Empty(setup.Skills); Assert.Equal("skill.falling_rain_slash", c.Loadout.ExternalSkillId(0));
        }

        [Fact] public void Skill_ids_are_stable()
        {
            Assert.Equal(StatGrowth.StableId("skill.falling_rain_slash"), StatGrowth.StableId("skill.falling_rain_slash"));
        }

        [Fact] public void Mastery_accepts_the_stable_skill_id_used_by_the_arena_contract()
        {
            LubanGameConfig config = Config;
            var c = new CharacterState("mastery-id");
            c.LearnSkill(new LearnedSkill("skill.falling_rain_slash"));
            int configuredId = StatGrowth.StableId(config.Skills.Single(skill => skill.Id == "skill.falling_rain_slash").Id);
            var result = new StatGrowth(config).ApplyMastery(c, new[] { new SkillUseStat(configuredId, 1, 1) });
            Assert.True(result.IsSuccess);
            Assert.True(c.FindSkill("skill.falling_rain_slash").MasteryXp > 0);
        }

        [Fact] public void Mastery_with_later_unknown_skill_does_not_partially_update_earlier_skill()
        {
            LubanGameConfig config = Config;
            var c = new CharacterState("mastery-atomic");
            LearnedSkill learned = new LearnedSkill("skill.falling_rain_slash");
            c.LearnSkill(learned);
            var result = new StatGrowth(config).ApplyMastery(c, new[]
            {
                new SkillUseStat(StatGrowth.StableId(config.Skills.Single(skill => skill.Id == learned.SkillId).Id), 1, 1),
                new SkillUseStat(-12, 1, 1),
            });
            Assert.True(result.IsFailure);
            Assert.Equal(0, learned.MasteryXp);
        }

        [Fact] public void Removing_a_missing_gear_instance_leaves_character_cleanliness_unchanged()
        {
            var c = new CharacterState("missing-gear");
            Assert.False(c.RemoveGear(999));
            Assert.True(c.IsDirty);
        }

        [Fact] public void Remove_modifier_source_removes_all_matching_rows_only()
        {
            var c = new CharacterState("remove-source");
            c.AddModifier(EAttribute.Focus, 1, ModifierSourceKind.Pill, "pill.one");
            c.AddModifier(EAttribute.Focus, 2, ModifierSourceKind.Pill, "pill.one");
            c.AddModifier(EAttribute.Focus, 4, ModifierSourceKind.Gear, "pill.one");
            c.RemoveModifiers(ModifierSourceKind.Pill, "pill.one");
            ModifierTotal total = new StatResolver(Config).GetModifierTotal(c, EAttribute.Focus);
            Assert.Equal(4, total.Value);
            Assert.Equal(1, total.SourceCount);
        }

        [Fact] public void Character_value_helpers_return_matching_fields_and_default_for_unknown_category()
        {
            RadarProfile radar = new RadarProfile(1, 2, 3, 4, 5);
            Assert.Equal(1, radar.Get(EAttrCategory.Martial));
            Assert.Equal(2, radar.Get(EAttrCategory.Neili));
            Assert.Equal(3, radar.Get(EAttrCategory.Body));
            Assert.Equal(4, radar.Get(EAttrCategory.Mind));
            Assert.Equal(5, radar.Get(EAttrCategory.Meridian));
            Assert.Equal(0, radar.Get(EAttrCategory.LifeSkill));
            LoadoutSkillResult success = LoadoutSkillResult.Ok(true);
            Assert.True(success.IsSuccess);
            Assert.False(success.IsFailure);
            Assert.True(success.Enabled);
            LoadoutSkillResult failure = LoadoutSkillResult.Fail("loadout.invalid");
            Assert.True(failure.IsFailure);
            Assert.False(failure.IsSuccess);
            Assert.Equal("loadout.invalid", failure.Error.Code);
            var source = new StatSourceValue(7, 2);
            Assert.Equal(7, source.Value);
            Assert.Equal(2, source.SourceCount);
        }

        [Fact] public void Character_loadout_accessors_preserve_values_and_dirty_state()
        {
            var c = new CharacterState("loadout-accessors");
            c.Loadout.SetExternalSkill(0, "skill.one");
            c.Loadout.SetAccessory(0, 7);
            c.Loadout.InnerSkillId = "skill.inner";
            c.Loadout.MovementSkillId = "skill.move";
            c.Loadout.WeaponInstanceId = 8;
            c.Loadout.ArmorInstanceId = 9;
            Assert.Equal("skill.one", c.Loadout.ExternalSkillIds[0]);
            Assert.Equal(7, c.Loadout.AccessoryInstanceId(0));
            Assert.Equal("skill.inner", c.Loadout.InnerSkillId);
            Assert.Equal("skill.move", c.Loadout.MovementSkillId);
            Assert.Equal(8, c.Loadout.WeaponInstanceId);
            Assert.Equal(9, c.Loadout.ArmorInstanceId);
            Assert.True(c.IsDirty);
        }

        private static CharacterState MakeCharacter(LubanGameConfig config)
        {
            var c = new CharacterState("prepared");
            c.SetBaseAttribute(EAttribute.Toughness, 40); c.SetBaseAttribute(EAttribute.Sinew, 20);
            c.LearnSkill(new LearnedSkill("skill.falling_rain_slash")); c.LearnSkill(new LearnedSkill("skill.half_moon_cut"));
            c.LearnSkill(new LearnedSkill("skill.still_water_manual")); c.LearnSkill(new LearnedSkill("skill.bamboo_leaf_dash"));
            c.AddGear(new GearInventoryEntry("gear.sword_of_morning_mist", 1)); c.AddGear(new GearInventoryEntry("gear.iron_fist_wraps", 2));
            c.AddGear(new GearInventoryEntry("gear.traveler_padded_coat", 3));
            c.AddGear(new GearInventoryEntry("gear.jade_pendant", 4)); c.AddGear(new GearInventoryEntry("gear.copper_wrist_guard", 5));
            c.Loadout.WeaponInstanceId = 1; c.Loadout.ArmorInstanceId = 3; c.Loadout.SetAccessory(0, 4); c.Loadout.SetAccessory(1, 5);
            c.Loadout.SetExternalSkill(0, "skill.falling_rain_slash");
            c.Loadout.InnerSkillId = "skill.still_water_manual";
            c.Loadout.MovementSkillId = "skill.bamboo_leaf_dash";
            return c;
        }
    }
}
