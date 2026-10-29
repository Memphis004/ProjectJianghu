using System;
using System.Collections.Generic;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Core.Domain.Arena;

namespace ProjectJianghu.Core.Domain.Character
{
    public sealed class LearnedSkill
    {
        public LearnedSkill(string skillId, int level = 1, int masteryXp = 0)
        { SkillId = skillId ?? throw new ArgumentNullException(nameof(skillId)); Level = level; MasteryXp = masteryXp; }
        public string SkillId { get; }
        public int Level { get; private set; }
        public int MasteryXp { get; private set; }
        internal void SetProgress(int level, int masteryXp) { Level = level; MasteryXp = masteryXp; }
    }

    public sealed class GearInventoryEntry
    {
        public GearInventoryEntry(string gearId, int instanceId)
        { GearId = gearId ?? throw new ArgumentNullException(nameof(gearId)); InstanceId = instanceId; }
        public string GearId { get; }
        public int InstanceId { get; }
    }

    /// <summary>Mutable character snapshot whose mutations invalidate derived stats.</summary>
    public sealed class CharacterState
    {
        private readonly Dictionary<EAttribute, int> _baseAttributes = new Dictionary<EAttribute, int>();
        private readonly List<LearnedSkill> _skills = new List<LearnedSkill>();
        private readonly List<GearInventoryEntry> _inventory = new List<GearInventoryEntry>();
        private readonly ModifierStack _modifiers = new ModifierStack();
        private readonly CharacterLoadout _loadout;
        private bool _dirty = true;
        private CombatStatBlock _combatStats;
        private string _backgroundId = string.Empty;

        public CharacterState(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Character id is required.", nameof(id));
            Id = id;
            _loadout = new CharacterLoadout(this);
        }

        public string Id { get; }
        public string BackgroundId { get => _backgroundId; set { _backgroundId = value ?? string.Empty; MarkDirty(); } }
        public CharacterLoadout Loadout => _loadout;
        public IReadOnlyDictionary<EAttribute, int> BaseAttributes => _baseAttributes;
        public IReadOnlyList<LearnedSkill> Skills => _skills;
        public IReadOnlyList<GearInventoryEntry> Inventory => _inventory;
        internal ModifierStack Modifiers => _modifiers;
        public int UnspentAttributePoints { get; internal set; }
        public bool IsDirty => _dirty;
        public int GetBaseAttribute(EAttribute attribute) => _baseAttributes.TryGetValue(attribute, out int value) ? value : 0;

        public void SetBaseAttribute(EAttribute attribute, int value)
        {
            _baseAttributes[attribute] = value;
            MarkDirty();
        }

        public void AddModifier(EAttribute attribute, int value, ModifierSourceKind kind, string sourceId)
        { _modifiers.Add(attribute, value, kind, sourceId); MarkDirty(); }
        public void RemoveModifiers(ModifierSourceKind kind, string sourceId) { _modifiers.RemoveSource(kind, sourceId); MarkDirty(); }
        public void ClearModifiers() { _modifiers.Clear(); MarkDirty(); }

        public LearnedSkill FindSkill(string skillId)
        {
            for (int i = 0; i < _skills.Count; i++)
                if (string.Equals(_skills[i].SkillId, skillId, StringComparison.Ordinal)) return _skills[i];
            return null!;
        }
        public void LearnSkill(LearnedSkill skill)
        {
            if (skill is null) throw new ArgumentNullException(nameof(skill));
            if (FindSkill(skill.SkillId) is not null) throw new ArgumentException("Skill already learned.", nameof(skill));
            _skills.Add(skill);
            MarkDirty();
        }
        public void SetMastery(LearnedSkill skill, int level, int xp)
        { skill.SetProgress(level, xp); MarkDirty(); }

        public void AddGear(GearInventoryEntry gear)
        {
            if (gear is null) throw new ArgumentNullException(nameof(gear));
            if (gear.InstanceId <= 0) throw new ArgumentOutOfRangeException(nameof(gear));
            if (FindGear(gear.InstanceId) is not null) throw new ArgumentException("Duplicate gear instance id.", nameof(gear));
            _inventory.Add(gear);
            MarkDirty();
        }
        public GearInventoryEntry FindGear(int instanceId)
        {
            for (int i = 0; i < _inventory.Count; i++) if (_inventory[i].InstanceId == instanceId) return _inventory[i];
            return null!;
        }
        public bool RemoveGear(int instanceId)
        {
            for (int i = 0; i < _inventory.Count; i++)
            {
                if (_inventory[i].InstanceId != instanceId) continue;
                _inventory.RemoveAt(i);
                if (_loadout.WeaponInstanceId == instanceId) _loadout.WeaponInstanceId = 0;
                if (_loadout.ArmorInstanceId == instanceId) _loadout.ArmorInstanceId = 0;
                for (int slot = 0; slot < 2; slot++) if (_loadout.AccessoryInstanceId(slot) == instanceId) _loadout.SetAccessory(slot, 0);
                MarkDirty();
                return true;
            }
            return false;
        }

        internal void MarkDirty() => _dirty = true;
        internal bool StoreCombatStats(in CombatStatBlock stats)
        { bool changed = _dirty; _combatStats = stats; _dirty = false; return changed; }
        internal CombatStatBlock CachedCombatStats => _combatStats;
    }

    public sealed class CharacterLoadout
    {
        private readonly CharacterState _owner;
        private readonly string[] _externalSkillIds = new string[4];
        private readonly int[] _accessoryInstanceIds = new int[2];
        private string _innerSkillId = string.Empty, _movementSkillId = string.Empty;
        private int _weaponInstanceId, _armorInstanceId;
        internal CharacterLoadout(CharacterState owner) => _owner = owner;
        public IReadOnlyList<string> ExternalSkillIds => _externalSkillIds;
        public string ExternalSkillId(int slot) => _externalSkillIds[slot];
        public string InnerSkillId { get => _innerSkillId; set { _innerSkillId = value ?? string.Empty; _owner.MarkDirty(); } }
        public string MovementSkillId { get => _movementSkillId; set { _movementSkillId = value ?? string.Empty; _owner.MarkDirty(); } }
        public int WeaponInstanceId { get => _weaponInstanceId; set { _weaponInstanceId = value; _owner.MarkDirty(); } }
        public int ArmorInstanceId { get => _armorInstanceId; set { _armorInstanceId = value; _owner.MarkDirty(); } }
        public int AccessoryInstanceId(int slot) => _accessoryInstanceIds[slot];
        public void SetExternalSkill(int slot, string id) { _externalSkillIds[slot] = id ?? string.Empty; _owner.MarkDirty(); }
        public void SetAccessory(int slot, int id) { _accessoryInstanceIds[slot] = id; _owner.MarkDirty(); }
    }
}
