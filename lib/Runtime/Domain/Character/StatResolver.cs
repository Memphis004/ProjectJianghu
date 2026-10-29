using System;
using System.Collections.Generic;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Domain.Config;

namespace ProjectJianghu.Core.Domain.Character
{
    public sealed class StatResolver
    {
        private readonly IGameConfig _config;
        private readonly Dictionary<EAttribute, int> _attributeValues = new Dictionary<EAttribute, int>();
        private readonly Dictionary<string, int> _formulaValues = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<AttributeModifier> _resolvedSources = new List<AttributeModifier>();
        private CharacterState? _cachedCharacter;

        public StatResolver(IGameConfig config) => _config = config ?? throw new ArgumentNullException(nameof(config));

        public int GetAttribute(CharacterState character, EAttribute attribute)
        { EnsureAttributes(character); return _attributeValues.TryGetValue(attribute, out int value) ? value : 0; }
        public ModifierTotal GetModifierTotal(CharacterState character, EAttribute attribute)
        {
            EnsureAttributes(character);
            int total = 0, count = 0;
            for (int i = 0; i < _resolvedSources.Count; i++)
                if (_resolvedSources[i].Attribute == attribute) { total = checked(total + _resolvedSources[i].Value); count++; }
            return new ModifierTotal(total, count);
        }

        public int CopyModifierSources(CharacterState character, EAttribute attribute, Span<AttributeModifier> destination)
        {
            EnsureAttributes(character);
            int written = 0;
            for (int i = 0; i < _resolvedSources.Count && written < destination.Length; i++)
                if (_resolvedSources[i].Attribute == attribute) destination[written++] = _resolvedSources[i];
            return written;
        }

        public int EvaluateFormula(CharacterState character, string formulaId)
        {
            EnsureAttributes(character);
            for (int i = 0; i < _config.StatFormulas.Count; i++)
                if (string.Equals(_config.StatFormulas[i].Id, formulaId, StringComparison.Ordinal)) return Evaluate(_config.StatFormulas[i]);
            throw new KeyNotFoundException("No stat formula '" + formulaId + "'.");
        }

        public StatResolveResult Resolve(CharacterState character)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if (!character.IsDirty) return new StatResolveResult(character.CachedCombatStats, false);
            EnsureAttributes(character);
            var block = new CombatStatBlock
            {
                MaxHp = Formula("stat.max_hp"), MaxNeili = Formula("stat.max_neili"),
                NeiliRegenPerSecX100 = Formula("stat.neili_regen_per_sec_x100"), ExternalPower = Formula("stat.external_power"),
                InnerPower = Formula("stat.inner_power"), Defense = Formula("stat.defense"), InnerDefense = Formula("stat.inner_defense"),
                MoveRange = (byte)Formula("stat.move_range"), JumpRange = (byte)Formula("stat.jump_range"),
                AtbFillTicks = Formula("stat.atb_fill_ticks"), ActionSpeedX100 = Formula("stat.action_speed_x100"),
                AccuracyX100 = Formula("stat.accuracy_x100"), EvasionX100 = Formula("stat.evasion_x100"),
                CritChanceX100 = Formula("stat.crit_chance_x100"), CritDamageX100 = Formula("stat.crit_damage_x100"),
                CounterChanceX100 = Formula("stat.counter_chance_x100"), StatusResistX100 = Formula("stat.status_resist_x100"),
                KnockbackResist = Formula("stat.knockback_resist"), JumpCooldownTicks = Formula("stat.jump_cooldown_ticks"),
            };
            character.StoreCombatStats(block);
            return new StatResolveResult(block, true);
        }

        public RadarProfile GetRadar(CharacterState character)
        {
            EnsureAttributes(character);
            long martial = 0, neili = 0, body = 0, mind = 0, meridian = 0;
            long martialWeight = 0, neiliWeight = 0, bodyWeight = 0, mindWeight = 0, meridianWeight = 0;
            for (int i = 0; i < _config.Attributes.Count; i++)
            {
                AttributeConfig row = _config.Attributes[i];
                if (row.RadarWeight == 0 || !_attributeValues.TryGetValue(row.Attribute, out int value)) continue;
                long weighted = (long)value * row.RadarWeight;
                switch (row.Category)
                {
                    case EAttrCategory.Martial: martial += weighted; martialWeight += row.RadarWeight; break;
                    case EAttrCategory.Neili: neili += weighted; neiliWeight += row.RadarWeight; break;
                    case EAttrCategory.Body: body += weighted; bodyWeight += row.RadarWeight; break;
                    case EAttrCategory.Mind: mind += weighted; mindWeight += row.RadarWeight; break;
                    case EAttrCategory.Meridian: meridian += weighted; meridianWeight += row.RadarWeight; break;
                }
            }
            return new RadarProfile(Average(martial, martialWeight), Average(neili, neiliWeight), Average(body, bodyWeight), Average(mind, mindWeight), Average(meridian, meridianWeight));
        }

        public PowerProfile GetPower(CharacterState character)
        {
            EnsureAttributes(character);
            long score = 0;
            for (int i = 0; i < _config.Attributes.Count; i++)
            {
                AttributeConfig row = _config.Attributes[i];
                if (row.PowerWeight != 0 && _attributeValues.TryGetValue(row.Attribute, out int value)) score = checked(score + (long)value * row.PowerWeight);
            }
            ConstantConfig c = _config.Constant;
            byte rank = score >= c.PowerRank9Min ? (byte)9 : score >= c.PowerRank8Min ? (byte)8 : score >= c.PowerRank7Min ? (byte)7 :
                score >= c.PowerRank6Min ? (byte)6 : score >= c.PowerRank5Min ? (byte)5 : score >= c.PowerRank4Min ? (byte)4 :
                score >= c.PowerRank3Min ? (byte)3 : score >= c.PowerRank2Min ? (byte)2 : (byte)1;
            return new PowerProfile(score, rank);
        }

        private int Formula(string id)
        {
            if (_formulaValues.TryGetValue(id, out int value)) return value;
            for (int i = 0; i < _config.StatFormulas.Count; i++)
                if (string.Equals(_config.StatFormulas[i].Id, id, StringComparison.Ordinal)) return _formulaValues[id] = Evaluate(_config.StatFormulas[i]);
            throw new KeyNotFoundException("No stat formula '" + id + "'.");
        }
        private int Evaluate(StatFormulaConfig formula)
        {
            long total = formula.BaseValue;
            for (int i = 0; i < formula.Terms.Count; i++)
            {
                StatTerm term = formula.Terms[i];
                long contribution = (long)(_attributeValues.TryGetValue(term.Attribute, out int value) ? value : 0) * term.Coefficient;
                total += Math.Max(term.Min, Math.Min(term.Max, contribution));
            }
            return (int)Math.Max(formula.ClampMin, Math.Min(formula.ClampMax, total));
        }
        private void EnsureAttributes(CharacterState character)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if (ReferenceEquals(_cachedCharacter, character) && !character.IsDirty) return;
            _cachedCharacter = character;
            _attributeValues.Clear();
            _formulaValues.Clear();
            _resolvedSources.Clear();
            for (int i = 0; i < _config.Attributes.Count; i++)
            {
                EAttribute attribute = _config.Attributes[i].Attribute;
                int value = character.GetBaseAttribute(attribute);
                if (value != 0) AddSource(attribute, value, ModifierSourceKind.Base, "attribute." + (int)attribute);
            }
            for (int i = 0; i < character.Modifiers.Entries.Count; i++)
            {
                AttributeModifier modifier = character.Modifiers.Entries[i];
                AddSource(modifier.Attribute, modifier.Value, modifier.Kind, modifier.SourceId);
            }
            for (int i = 0; i < character.Skills.Count; i++)
            {
                LearnedSkill learned = character.Skills[i];
                SkillConfig skill = FindSkill(learned.SkillId);
                if (skill is null) continue;
                for (int j = 0; j < skill.LevelBonus.Count; j++)
                    if (skill.LevelBonus[j].Level <= learned.Level)
                        AddSource(skill.LevelBonus[j].Attribute, skill.LevelBonus[j].Value, ModifierSourceKind.Skill, skill.Id);
            }
            for (int i = 0; i < character.Inventory.Count; i++)
            {
                GearInventoryEntry item = character.Inventory[i];
                GearConfig gear = FindGear(item.GearId);
                if (gear is null || !IsEquipped(character.Loadout, item.InstanceId)) continue;
                for (int j = 0; j < gear.Mods.Count; j++)
                    AddSource(gear.Mods[j].Attribute, gear.Mods[j].Value, ModifierSourceKind.Gear, gear.Id + "#" + item.InstanceId);
            }
        }
        private void AddSource(EAttribute attribute, int amount, ModifierSourceKind kind, string sourceId)
        {
            _resolvedSources.Add(new AttributeModifier(attribute, amount, kind, sourceId));
            _attributeValues[attribute] = checked((_attributeValues.TryGetValue(attribute, out int value) ? value : 0) + amount);
        }
        private SkillConfig FindSkill(string id)
        { for (int i = 0; i < _config.Skills.Count; i++) if (string.Equals(_config.Skills[i].Id, id, StringComparison.Ordinal)) return _config.Skills[i]; return null!; }
        private GearConfig FindGear(string id)
        { for (int i = 0; i < _config.Gear.Count; i++) if (string.Equals(_config.Gear[i].Id, id, StringComparison.Ordinal)) return _config.Gear[i]; return null!; }
        private static bool IsEquipped(CharacterLoadout loadout, int id)
        {
            if (loadout.WeaponInstanceId == id || loadout.ArmorInstanceId == id) return true;
            for (int i = 0; i < 2; i++) if (loadout.AccessoryInstanceId(i) == id) return true;
            return false;
        }
        private static int Average(long sum, long weight) => weight == 0 ? 0 : (int)(sum / weight);
    }
}
