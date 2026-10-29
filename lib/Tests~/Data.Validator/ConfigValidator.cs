using System;
using System.Collections.Generic;
using ProjectJianghu.Core.Domain.Config;

namespace ProjectJianghu.Data.Validator
{
    /// <summary>One validation finding: a stable code, the row it is about, and a readable message.</summary>
    /// <remarks>
    /// The code is machine-readable and stable (<c>reference.missing</c>), never a
    /// sentence meant for a player (AGENTS rule 11). The message is for the developer running the
    /// data gate.
    /// </remarks>
    public sealed class Finding
    {
        /// <summary>Creates a finding.</summary>
        public Finding(string code, string target, string message)
        {
            Code = code;
            Target = target;
            Message = message;
        }

        /// <summary>Stable identifier, e.g. <c>arena_map.mask_size</c>.</summary>
        public string Code { get; }

        /// <summary>Where the problem is, e.g. <c>tbfighter['fighter.rival_01'].weapon_id</c>.</summary>
        public string Target { get; }

        /// <summary>Developer-facing description.</summary>
        public string Message { get; }

        /// <inheritdoc />
        public override string ToString() => Code + "  " + Target + "  " + Message;
    }

    /// <summary>
    /// The rules Luban cannot express over the game's 14 tables.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Luban owns the schema: column names, types, unique keys, required <c>#ref</c> targets and
    /// <c>range</c> bounds. Everything here is the other half, and every rule is here for one of
    /// two reasons: the value is legitimately empty in a CSV cell (so a <c>#ref</c> cannot police
    /// it), or the rule spans two table rows (an <c>EAttribute</c> term that has no
    /// <c>attribute</c> row, a mask that does not match its declared size).
    /// </para>
    /// <para>
    /// A game adds its own rules here, next to these, and adds a fixture under
    /// <c>lib/Tests~/fixtures/</c> for each one: the fixture proves the rule fires end-to-end
    /// (data, Luban, <c>.bytes</c>, findings), while <c>ConfigRulesTests</c> proves it on the
    /// loaded snapshot.
    /// </para>
    /// <para>
    /// These rules are deliberately about <i>shape</i>, not balance: a number being small is a
    /// design decision; a pointer at a row that does not exist is a bug.
    /// </para>
    /// </remarks>
    public static class ConfigValidator
    {
        /// <summary>Applies every rule to a loaded snapshot.</summary>
        public static IReadOnlyList<Finding> Validate(IGameConfig config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var findings = new List<Finding>();

            Dictionary<string, HashSet<string>> ids = BuildIdSets(config);

            ValidateConstant(config.Constant, findings);
            ValidateAttributeRules(config.Attributes, findings);
            ValidateProgressionRules(config.SkillLevels, findings);
            ValidateLocalization(config, findings);
            ValidateIds(config, findings);
            ValidateLocalizationKeys(config, findings);
            ValidateReferences(config, ids, findings);
            ValidateStatFormulas(config, findings);
            ValidateSkills(config, findings);
            ValidateGearAndLoadouts(config, findings);
            ValidateArenaMaps(config, findings);
            ValidateRewardPools(config, findings);

            findings.Sort((left, right) =>
                string.CompareOrdinal(left.Code + left.Target, right.Code + right.Target));

            return findings;
        }

        // -- constant ----------------------------------------------------------

        private static void ValidateConstant(ConstantConfig constant, List<Finding> findings)
        {
            RequirePositive(constant.AtbFull, "atb_full", findings);
            RequirePositive(constant.MatchTicks, "match_ticks", findings);
            RequirePositive(constant.DamageMitigationK, "damage_mitigation_k", findings);
            RequirePositive(constant.MaxMasteryLevel, "max_mastery_level", findings);
            RequireNonNegative(constant.BackDamageBonusX100, "back_damage_bonus_x100", findings);
            RequireNonNegative(constant.FlankDamageBonusX100, "flank_damage_bonus_x100", findings);
            RequireNonNegative(constant.MasteryXpPerUse, "mastery_xp_per_use", findings);

            RequireNonNegative(constant.MoveAtbCost, "move_atb_cost", findings);
            RequireNonNegative(constant.JumpAtbCost, "jump_atb_cost", findings);
            RequireNonNegative(constant.MeditateAtbCost, "meditate_atb_cost", findings);
            RequireNonNegative(constant.JumpCooldownTicks, "jump_cooldown_ticks", findings);
            RequireNonNegative(constant.MeditateNeiliRestore, "meditate_neili_restore", findings);
            RequireNonNegative(constant.StartingAttributePoints, "starting_attribute_points", findings);

            // A command cannot cost more than the bar it spends: the fighter would be stuck
            // holding a full bar it can never afford to use.
            if (constant.AtbFull > 0)
            {
                RequireAtMostBar(constant.MoveAtbCost, constant.AtbFull, "move_atb_cost", findings);
                RequireAtMostBar(constant.JumpAtbCost, constant.AtbFull, "jump_atb_cost", findings);
                RequireAtMostBar(constant.MeditateAtbCost, constant.AtbFull, "meditate_atb_cost", findings);
            }

            int[] rankThresholds =
            {
                constant.PowerRank2Min, constant.PowerRank3Min, constant.PowerRank4Min,
                constant.PowerRank5Min, constant.PowerRank6Min, constant.PowerRank7Min,
                constant.PowerRank8Min, constant.PowerRank9Min,
            };
            bool hasRankThresholds = false;
            for (int i = 0; i < rankThresholds.Length; i++) hasRankThresholds |= rankThresholds[i] != 0;
            if (hasRankThresholds)
            {
                for (int i = 1; i < rankThresholds.Length; i++)
                {
                    if (rankThresholds[i] <= rankThresholds[i - 1])
                    {
                        findings.Add(new Finding("constant.power_rank_order", "tbconstant.power_rank_" + (i + 2) + "_min",
                            "power rank thresholds must be strictly increasing"));
                        break;
                    }
                }
            }
        }

        private static void ValidateAttributeRules(IReadOnlyList<AttributeConfig> attributes, List<Finding> findings)
        {
            foreach (AttributeConfig row in attributes)
            {
                if (row.CreationMax < row.CreationMin)
                {
                    findings.Add(new Finding("attribute.creation_bounds", "tbattribute['" + row.Id + "']",
                        "creation_max is below creation_min"));
                }
                if (row.PowerWeight < 0 || row.RadarWeight < 0)
                {
                    findings.Add(new Finding("attribute.negative_weight", "tbattribute['" + row.Id + "']",
                        "power and radar weights must be non-negative"));
                }
                if (row.DisplayOnly && row.Category != EAttrCategory.LifeSkill && row.PowerWeight != 0)
                {
                    findings.Add(new Finding("attribute.display_only_power", "tbattribute['" + row.Id + "']",
                        "display-only attributes cannot contribute to combat PowerScore"));
                }
            }
        }

        private static void ValidateProgressionRules(IReadOnlyList<SkillLevelConfig> levels, List<Finding> findings)
        {
            int previousXp = -1;
            for (int i = 0; i < levels.Count; i++)
            {
                SkillLevelConfig level = levels[i];
                if (level.MasteryXp < previousXp || level.CombatMasteryX100 < 0)
                {
                    findings.Add(new Finding("mastery.progression_order", "tbskilllevel['" + level.Id + "']",
                        "mastery XP thresholds and combat mastery multipliers must be non-decreasing and non-negative"));
                }
                previousXp = level.MasteryXp;
            }
        }

        // -- localization ------------------------------------------------------

        private static void ValidateLocalization(IGameConfig config, List<Finding> findings)
        {
            if (config.Localization.Count == 0)
            {
                findings.Add(new Finding(
                    "localization.empty", "tblocalization", "the localization table has no rows"));
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (LocalizationEntry entry in config.Localization)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    findings.Add(new Finding(
                        "localization.blank_key", "tblocalization", "a row has an empty key"));
                    continue;
                }

                if (!seen.Add(entry.Key))
                {
                    findings.Add(new Finding(
                        "localization.duplicate_key",
                        "tblocalization['" + entry.Key + "']",
                        "the key appears more than once"));
                }

                if (string.IsNullOrEmpty(entry.Th) && string.IsNullOrEmpty(entry.En))
                {
                    findings.Add(new Finding(
                        "localization.blank_text",
                        "tblocalization['" + entry.Key + "']",
                        "both languages are empty, so the row can never be shown"));
                }
                else if (string.IsNullOrEmpty(entry.Th) || string.IsNullOrEmpty(entry.En))
                {
                    // One language filled and the other empty. The runtime falls back to the language
                    // that exists, so this is not a crash, but a player reading the build in the other
                    // language would see the wrong one, which is the data bug this rule is for.
                    findings.Add(new Finding(
                        "localization.missing_language",
                        "tblocalization['" + entry.Key + "']",
                        (string.IsNullOrEmpty(entry.Th) ? "th" : "en")
                        + " is empty while the other language is set"));
                }
            }
        }

        // -- every keyed table: not empty, no blank id, no duplicate id ---------

        private static void ValidateIds(IGameConfig config, List<Finding> findings)
        {
            ValidateIdsOf("tbattribute", Project(config.Attributes, row => row.Id), findings);
            ValidateIdsOf("tbstatformula", Project(config.StatFormulas, row => row.Id), findings);
            ValidateIdsOf("tbskill", Project(config.Skills, row => row.Id), findings);
            ValidateIdsOf("tbskilllevel", Project(config.SkillLevels, row => row.Id), findings);
            ValidateIdsOf("tbgear", Project(config.Gear, row => row.Id), findings);
            ValidateIdsOf("tbstatus", Project(config.Statuses, row => row.Id), findings);
            ValidateIdsOf("tbfighter", Project(config.Fighters, row => row.Id), findings);
            ValidateIdsOf("tbaiprofile", Project(config.AiProfiles, row => row.Id), findings);
            ValidateIdsOf("tbarenamap", Project(config.ArenaMaps, row => row.Id), findings);
            ValidateIdsOf("tbtournament", Project(config.Tournaments, row => row.Id), findings);
            ValidateIdsOf("tbrewardpool", Project(config.RewardPools, row => row.Id), findings);
            ValidateIdsOf("tbbackground", Project(config.Backgrounds, row => row.Id), findings);
        }

        private static void ValidateIdsOf(string tableName, List<string> ids, List<Finding> findings)
        {
            if (ids.Count == 0)
            {
                findings.Add(new Finding("data.empty", tableName, "the table has no rows"));
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    findings.Add(new Finding("data.blank_id", tableName, "a row has an empty id"));
                    continue;
                }

                if (!seen.Add(id))
                {
                    // Luban's index collapses the pair during generation, so this can only be
                    // reported over a snapshot; the fixture for it is a generate-stage one.
                    findings.Add(new Finding(
                        "data.duplicate_id",
                        tableName + "['" + id + "']",
                        "the id appears more than once"));
                }
            }
        }

        // -- text keys ---------------------------------------------------------

        private static void ValidateLocalizationKeys(IGameConfig config, List<Finding> findings)
        {
            foreach (AttributeConfig row in config.Attributes)
            {
                RequireKey(config, row.DescKey, "tbattribute", row.Id, "desc_key", findings);
            }

            foreach (SkillConfig row in config.Skills)
            {
                RequireKey(config, row.DescKey, "tbskill", row.Id, "desc_key", findings);
                RequireKey(config, row.Level5SpecialKey, "tbskill", row.Id, "level_5_special_key", findings);
                RequireKey(config, row.Level10SpecialKey, "tbskill", row.Id, "level_10_special_key", findings);
            }

            foreach (GearConfig row in config.Gear)
            {
                RequireKey(config, row.DescKey, "tbgear", row.Id, "desc_key", findings);
            }

            foreach (StatusConfig row in config.Statuses)
            {
                RequireKey(config, row.DescKey, "tbstatus", row.Id, "desc_key", findings);
            }

            foreach (FighterConfig row in config.Fighters)
            {
                RequireKey(config, row.DescKey, "tbfighter", row.Id, "desc_key", findings);
            }

            foreach (BackgroundConfig row in config.Backgrounds)
            {
                RequireKey(config, row.DescKey, "tbbackground", row.Id, "desc_key", findings);
            }

            foreach (RewardPoolConfig row in config.RewardPools)
            {
                RequireKey(config, row.NoteKey, "tbrewardpool", row.Id, "note_key", findings);
            }
        }

        /// <summary>
        /// A localization key a data row points at must exist.
        /// </summary>
        /// <remarks>
        /// A required <c>name_key</c> is a schema <c>#ref</c>, so Luban already rejects a dangling
        /// one during generation. This rule exists for the optional columns (an empty cell is
        /// legal, which is exactly what a <c>#ref</c> cannot express) and as a second net for the
        /// required ones once the bytes exist.
        /// </remarks>
        private static void RequireKey(
            IGameConfig config,
            string key,
            string tableName,
            string rowId,
            string field,
            List<Finding> findings)
        {
            if (string.IsNullOrEmpty(key) || config.TryGetLocalization(key, out _))
            {
                return;
            }

            findings.Add(new Finding(
                "localization.missing_key",
                tableName + "['" + rowId + "']." + field,
                "no localization row for key '" + key + "', so the row cannot be named"));
        }

        // -- references between rows -------------------------------------------

        private static void ValidateReferences(
            IGameConfig config,
            Dictionary<string, HashSet<string>> ids,
            List<Finding> findings)
        {
            foreach (SkillConfig row in config.Skills)
            {
                RequireRef(ids["tbstatus"], "tbskill", row.Id, "status_id", row.StatusId, findings);
            }

            foreach (FighterConfig row in config.Fighters)
            {
                RequireRef(ids["tbbackground"], "tbfighter", row.Id, "background_id", row.BackgroundId, findings);
                RequireRef(ids["tbgear"], "tbfighter", row.Id, "weapon_id", row.WeaponId, findings);
                RequireRef(ids["tbgear"], "tbfighter", row.Id, "armor_id", row.ArmorId, findings);

                foreach (string accessory in row.AccessoryIds)
                {
                    RequireRef(ids["tbgear"], "tbfighter", row.Id, "accessory_ids", accessory, findings);
                }

                foreach (string skill in row.SkillIds)
                {
                    RequireRef(ids["tbskill"], "tbfighter", row.Id, "skill_ids", skill, findings);
                }
            }

            foreach (BackgroundConfig row in config.Backgrounds)
            {
                foreach (string skill in row.StartSkillIds)
                {
                    RequireRef(ids["tbskill"], "tbbackground", row.Id, "start_skill_ids", skill, findings);
                }

                foreach (string gear in row.StartGearIds)
                {
                    RequireRef(ids["tbgear"], "tbbackground", row.Id, "start_gear_ids", gear, findings);
                }
            }

            foreach (RewardPoolConfig row in config.RewardPools)
            {
                RequireRef(ids["tbgear"], "tbrewardpool", row.Id, "gear_id", row.GearId, findings);
                RequireRef(ids["tbskill"], "tbrewardpool", row.Id, "skill_id", row.SkillId, findings);
            }
        }

        private static void RequireRef(
            HashSet<string> known,
            string tableName,
            string rowId,
            string field,
            string value,
            List<Finding> findings)
        {
            if (string.IsNullOrEmpty(value) || known.Contains(value))
            {
                return;
            }

            findings.Add(new Finding(
                "reference.missing",
                tableName + "['" + rowId + "']." + field,
                "'" + value + "' is not a row of " + FieldTarget(field)));
        }

        // -- stat_formula ------------------------------------------------------

        private static void ValidateStatFormulas(IGameConfig config, List<Finding> findings)
        {
            var attributes = new HashSet<EAttribute>();
            foreach (AttributeConfig row in config.Attributes)
            {
                attributes.Add(row.Attribute);
            }

            foreach (StatFormulaConfig row in config.StatFormulas)
            {
                if (row.ClampMax < row.ClampMin)
                {
                    findings.Add(new Finding(
                        "stat_formula.clamp_order",
                        "tbstatformula['" + row.Id + "']",
                        "clamp_max (" + row.ClampMax + ") is below clamp_min (" + row.ClampMin + ")"));
                }

                foreach (StatTerm term in row.Terms)
                {
                    // The enum always names a legal attribute; this rule is about the attribute
                    // TABLE, which is what a UI and a tooltip read. A term on an attribute nobody
                    // authored is a formula that can never be explained.
                    if (!attributes.Contains(term.Attribute))
                    {
                        findings.Add(new Finding(
                            "stat_formula.term_attribute_unknown",
                            "tbstatformula['" + row.Id + "'].terms",
                            "term uses " + term.Attribute + ", which has no attribute row"));
                    }
                }
            }
        }

        // -- skill -------------------------------------------------------------

        private static void ValidateSkills(IGameConfig config, List<Finding> findings)
        {
            foreach (SkillConfig row in config.Skills)
            {
                bool hasLevelFive = false;
                bool hasLevelTen = false;
                foreach (SkillLevelBonus bonus in row.LevelBonus)
                {
                    if (bonus.Level < 1 || bonus.Level > 10)
                    {
                        findings.Add(new Finding("skill.level_bonus_range", "tbskill['" + row.Id + "'].level_bonus",
                            "bonus level must be between 1 and 10"));
                    }
                    if (bonus.Level == 5) hasLevelFive = true;
                    if (bonus.Level == 10)                    hasLevelTen = true;
                }

                if (string.IsNullOrEmpty(row.Level5SpecialKey) || string.IsNullOrEmpty(row.Level10SpecialKey))
                {
                    findings.Add(new Finding("skill.milestone_special_missing", "tbskill['" + row.Id + "']",
                        "level 5 and level 10 special localization keys are required"));
                }
                if (!hasLevelFive || !hasLevelTen)
                {
                    findings.Add(new Finding("skill.milestone_bonus_missing", "tbskill['" + row.Id + "'].level_bonus",
                        "attribute bonuses must include level 5 and level 10"));
                }

                if (row.Kind != ESkillKind.External && row.WeaponType != EWeaponType.None)
                {
                    findings.Add(new Finding("skill.passive_weapon_type", "tbskill['" + row.Id + "']",
                        "inner and movement skills must use weapon type None"));
                }

                if (row.RangeMax < row.RangeMin)
                {
                    findings.Add(new Finding(
                        "skill.range_order",
                        "tbskill['" + row.Id + "']",
                        "range_max (" + row.RangeMax + ") is below range_min (" + row.RangeMin + ")"));
                }
            }
        }

        // -- gear and fighter loadouts -----------------------------------------

        private static void ValidateGearAndLoadouts(IGameConfig config, List<Finding> findings)
        {
            var gearById = new Dictionary<string, GearConfig>(StringComparer.Ordinal);
            foreach (GearConfig gear in config.Gear)
            {
                gearById[gear.Id] = gear;
                if (gear.Slot == EGearSlot.Weapon && gear.WeaponType == EWeaponType.None)
                {
                    findings.Add(new Finding("gear.weapon_type", "tbgear['" + gear.Id + "']",
                        "a weapon must declare its weapon type"));
                }
                else if (gear.Slot != EGearSlot.Weapon && gear.WeaponType != EWeaponType.None)
                {
                    findings.Add(new Finding("gear.non_weapon_type", "tbgear['" + gear.Id + "']",
                        "non-weapon gear must use weapon type None"));
                }
            }

            var skillById = new Dictionary<string, SkillConfig>(StringComparer.Ordinal);
            foreach (SkillConfig skill in config.Skills)
            {
                skillById[skill.Id] = skill;
            }

            foreach (FighterConfig fighter in config.Fighters)
            {
                string target = "tbfighter['" + fighter.Id + "']";
                GearConfig? weapon = null;
                if (string.IsNullOrEmpty(fighter.WeaponId))
                {
                    findings.Add(new Finding("fighter.loadout_weapon_required", target + ".weapon_id",
                        "a fighter must equip one weapon"));
                }
                else if (gearById.TryGetValue(fighter.WeaponId, out GearConfig? foundWeapon))
                {
                    weapon = foundWeapon;
                    if (weapon.Slot != EGearSlot.Weapon)
                    {
                        findings.Add(new Finding("fighter.loadout_weapon_slot", target + ".weapon_id",
                            "weapon_id must reference gear in the Weapon slot"));
                    }
                }

                if (string.IsNullOrEmpty(fighter.ArmorId))
                {
                    findings.Add(new Finding("fighter.loadout_armor_required", target + ".armor_id",
                        "a fighter must equip one armour piece"));
                }
                else if (gearById.TryGetValue(fighter.ArmorId, out GearConfig? armor)
                    && armor.Slot != EGearSlot.Armor)
                {
                    findings.Add(new Finding("fighter.loadout_armor_slot", target + ".armor_id",
                        "armor_id must reference gear in the Armor slot"));
                }

                if (fighter.AccessoryIds.Count != 2)
                {
                    findings.Add(new Finding("fighter.loadout_accessory_count", target + ".accessory_ids",
                        "a fighter must equip exactly two accessories"));
                }

                if (fighter.SkillIds.Count != 4)
                {
                    findings.Add(new Finding("fighter.loadout_skill_count", target + ".skill_ids",
                        "a fighter must carry exactly four skills"));
                }

                foreach (string accessoryId in fighter.AccessoryIds)
                {
                    if (gearById.TryGetValue(accessoryId, out GearConfig? accessory)
                        && accessory.Slot != EGearSlot.Accessory)
                    {
                        findings.Add(new Finding("fighter.loadout_accessory_slot", target + ".accessory_ids",
                            "accessory_ids must reference gear in the Accessory slot"));
                    }
                }

                foreach (string skillId in fighter.SkillIds)
                {
                    if (!skillById.TryGetValue(skillId, out SkillConfig? skill))
                    {
                        continue;
                    }

                    if (skill.Kind == ESkillKind.External && weapon is not null
                        && weapon.Slot == EGearSlot.Weapon && skill.WeaponType != weapon.WeaponType)
                    {
                        findings.Add(new Finding("fighter.loadout_weapon_mismatch", target + ".skill_ids",
                            "external skill '" + skill.Id + "' requires " + skill.WeaponType
                            + " but equipped weapon provides " + weapon.WeaponType));
                    }
                    else if (skill.Kind != ESkillKind.External && skill.WeaponType != EWeaponType.None)
                    {
                        findings.Add(new Finding("skill.passive_weapon_type", "tbskill['" + skill.Id + "']",
                            "inner and movement skills must use weapon type None"));
                    }
                }
            }
        }

        // -- arena_map ---------------------------------------------------------

        private static void ValidateArenaMaps(IGameConfig config, List<Finding> findings)
        {
            foreach (ArenaMapConfig row in config.ArenaMaps)
            {
                if (row.Mask.Count != row.Height)
                {
                    findings.Add(new Finding(
                        "arena_map.mask_size",
                        "tbarenamap['" + row.Id + "'].mask",
                        "the mask has " + row.Mask.Count + " row(s), but height is " + row.Height));
                    continue;
                }

                bool widthReported = false;
                for (int y = 0; y < row.Mask.Count; y++)
                {
                    string line = row.Mask[y];
                    if (!widthReported && line.Length != row.Width)
                    {
                        findings.Add(new Finding(
                            "arena_map.mask_size",
                            "tbarenamap['" + row.Id + "'].mask",
                            "row " + y + " is " + line.Length + " character(s) wide, but width is " + row.Width));
                        widthReported = true;
                    }

                    for (int x = 0; x < line.Length; x++)
                    {
                        char cell = line[x];
                        if (cell != '.' && cell != '#')
                        {
                            findings.Add(new Finding(
                                "arena_map.mask_char",
                                "tbarenamap['" + row.Id + "'].mask",
                                "row " + y + " column " + x + " is '" + cell + "'; only '.' and '#' are legal"));
                            break;
                        }
                    }
                }
            }
        }

        // -- reward_pool -------------------------------------------------------

        private static void ValidateRewardPools(IGameConfig config, List<Finding> findings)
        {
            foreach (RewardPoolConfig row in config.RewardPools)
            {
                bool hasGear = !string.IsNullOrEmpty(row.GearId);
                bool hasSkill = !string.IsNullOrEmpty(row.SkillId);
                if (hasGear == hasSkill)
                {
                    findings.Add(new Finding(
                        "reward_pool.source",
                        "tbrewardpool['" + row.Id + "']",
                        "exactly one of gear_id and skill_id must be set"));
                }

                if (row.RoundMax < row.RoundMin)
                {
                    findings.Add(new Finding(
                        "reward_pool.round_order",
                        "tbrewardpool['" + row.Id + "']",
                        "round_max (" + row.RoundMax + ") is below round_min (" + row.RoundMin + ")"));
                }
            }
        }

        // -- helpers -----------------------------------------------------------

        private static Dictionary<string, HashSet<string>> BuildIdSets(IGameConfig config)
        {
            return new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                { "tbattribute", Set(Project(config.Attributes, row => row.Id)) },
                { "tbstatformula", Set(Project(config.StatFormulas, row => row.Id)) },
                { "tbskill", Set(Project(config.Skills, row => row.Id)) },
                { "tbskilllevel", Set(Project(config.SkillLevels, row => row.Id)) },
                { "tbgear", Set(Project(config.Gear, row => row.Id)) },
                { "tbstatus", Set(Project(config.Statuses, row => row.Id)) },
                { "tbfighter", Set(Project(config.Fighters, row => row.Id)) },
                { "tbaiprofile", Set(Project(config.AiProfiles, row => row.Id)) },
                { "tbarenamap", Set(Project(config.ArenaMaps, row => row.Id)) },
                { "tbtournament", Set(Project(config.Tournaments, row => row.Id)) },
                { "tbrewardpool", Set(Project(config.RewardPools, row => row.Id)) },
                { "tbbackground", Set(Project(config.Backgrounds, row => row.Id)) },
            };
        }

        private static List<string> Project<T>(IReadOnlyList<T> rows, Func<T, string> selector)
        {
            var projected = new List<string>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                projected.Add(selector(rows[i]));
            }

            return projected;
        }

        private static HashSet<string> Set(List<string> ids)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    set.Add(id);
                }
            }

            return set;
        }

        /// <summary>The table a reference column points at, for a readable message.</summary>
        private static string FieldTarget(string field)
        {
            switch (field)
            {
                case "status_id":
                    return "tbstatus";
                case "skill_id":
                case "skill_ids":
                case "start_skill_ids":
                    return "tbskill";
                case "background_id":
                    return "tbbackground";
                case "weapon_id":
                case "armor_id":
                case "accessory_ids":
                case "gear_id":
                case "start_gear_ids":
                    return "tbgear";
                default:
                    return "the referenced table";
            }
        }

        private static void RequirePositive(int value, string field, List<Finding> findings)
        {
            if (value <= 0)
            {
                findings.Add(new Finding(
                    "constant.non_positive", "tbconstant." + field, field + " must be greater than zero"));
            }
        }

        private static void RequireNonNegative(int value, string field, List<Finding> findings)
        {
            if (value < 0)
            {
                findings.Add(new Finding(
                    "constant.negative", "tbconstant." + field, field + " must not be negative"));
            }
        }

        private static void RequireAtMostBar(int value, int atbFull, string field, List<Finding> findings)
        {
            if (value > atbFull)
            {
                findings.Add(new Finding(
                    "constant.out_of_range",
                    "tbconstant." + field,
                    field + " (" + value + ") costs more than a full ATB bar (" + atbFull + ")"));
            }
        }
    }
}
