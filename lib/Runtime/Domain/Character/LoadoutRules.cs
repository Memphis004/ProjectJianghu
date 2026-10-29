using System;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Core.Shared;

namespace ProjectJianghu.Core.Domain.Character
{
    public sealed class LoadoutRules
    {
        private readonly IGameConfig _config;
        public LoadoutRules(IGameConfig config) => _config = config ?? throw new ArgumentNullException(nameof(config));

        public Result EquipWeapon(CharacterState character, int instanceId)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            GearInventoryEntry item = character.FindGear(instanceId);
            GearConfig gear = item is null ? null! : FindGear(item.GearId);
            if (gear is null) return Result.Fail("loadout.gear_not_found", ErrorKind.NotFound);
            if (gear.Slot != EGearSlot.Weapon) return Result.Fail("loadout.wrong_slot");
            character.Loadout.WeaponInstanceId = instanceId;
            return Result.Ok();
        }

        public Result EquipArmor(CharacterState character, int instanceId)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            return EquipSingle(character, instanceId, EGearSlot.Armor, true);
        }

        public Result EquipAccessory(CharacterState character, int slot, int instanceId)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if ((uint)slot >= 2) return Result.Fail("loadout.invalid_accessory_slot");
            GearInventoryEntry item = character.FindGear(instanceId);
            GearConfig gear = item is null ? null! : FindGear(item.GearId);
            if (gear is null) return Result.Fail("loadout.gear_not_found", ErrorKind.NotFound);
            if (gear.Slot != EGearSlot.Accessory) return Result.Fail("loadout.wrong_slot");
            character.Loadout.SetAccessory(slot, instanceId);
            return Result.Ok();
        }

        public Result EquipSkill(CharacterState character, ESkillKind kind, int slot, string skillId)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            SkillConfig skill = FindSkill(skillId);
            if (skill is null) return Result.Fail("loadout.skill_not_found", ErrorKind.NotFound);
            if (character.FindSkill(skillId) is null) return Result.Fail("loadout.skill_not_learned", ErrorKind.InvalidState);
            if (skill.Kind != kind) return Result.Fail("loadout.wrong_skill_kind");
            switch (kind)
            {
                case ESkillKind.External:
                    if ((uint)slot >= 4) return Result.Fail("loadout.invalid_external_slot");
                    EWeaponType equippedWeapon = EquippedWeaponType(character);
                    if (equippedWeapon != EWeaponType.None && skill.WeaponType != equippedWeapon)
                        return Result.Fail("loadout.weapon_mismatch");
                    character.Loadout.SetExternalSkill(slot, skillId);
                    return Result.Ok();
                case ESkillKind.Inner:
                    character.Loadout.InnerSkillId = skillId;
                    return Result.Ok();
                case ESkillKind.Movement:
                    character.Loadout.MovementSkillId = skillId;
                    return Result.Ok();
                default: return Result.Fail("loadout.wrong_skill_kind");
            }
        }

        public LoadoutSkillResult GetExternalSkillState(CharacterState character, int slot)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if ((uint)slot >= 4) return LoadoutSkillResult.Fail("loadout.invalid_external_slot");
            string id = character.Loadout.ExternalSkillId(slot);
            if (string.IsNullOrEmpty(id)) return LoadoutSkillResult.Ok(false);
            SkillConfig skill = FindSkill(id);
            if (skill is null || character.FindSkill(id) is null) return LoadoutSkillResult.Fail("loadout.skill_not_learned");
            EWeaponType weapon = EquippedWeaponType(character);
            return LoadoutSkillResult.Ok(skill.WeaponType == weapon);
        }

        public Result Validate(CharacterState character)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if (!IsGear(character, character.Loadout.WeaponInstanceId, EGearSlot.Weapon)) return Result.Fail("loadout.weapon_required");
            if (!IsGear(character, character.Loadout.ArmorInstanceId, EGearSlot.Armor)) return Result.Fail("loadout.armor_required");
            for (int i = 0; i < 2; i++)
            {
                int accessoryId = character.Loadout.AccessoryInstanceId(i);
                if (accessoryId != 0 && !IsGear(character, accessoryId, EGearSlot.Accessory))
                    return Result.Fail("loadout.invalid_accessory");
            }
            for (int i = 0; i < 4; i++)
            {
                LoadoutSkillResult state = GetExternalSkillState(character, i);
                if (state.IsFailure) return Result.Fail(state.Error);
            }
            if (!IsSkillKind(character, character.Loadout.InnerSkillId, ESkillKind.Inner)) return Result.Fail("loadout.inner_skill_required");
            if (!IsSkillKind(character, character.Loadout.MovementSkillId, ESkillKind.Movement)) return Result.Fail("loadout.movement_skill_required");
            return Result.Ok();
        }

        public EWeaponType EquippedWeaponType(CharacterState character)
        {
            GearInventoryEntry entry = character.FindGear(character.Loadout.WeaponInstanceId);
            GearConfig gear = entry is null ? null! : FindGear(entry.GearId);
            return gear is null ? EWeaponType.None : gear.WeaponType;
        }

        private Result EquipSingle(CharacterState character, int instanceId, EGearSlot slot, bool armor)
        {
            GearInventoryEntry item = character.FindGear(instanceId);
            GearConfig gear = item is null ? null! : FindGear(item.GearId);
            if (gear is null) return Result.Fail("loadout.gear_not_found", ErrorKind.NotFound);
            if (gear.Slot != slot) return Result.Fail("loadout.wrong_slot");
            if (armor) character.Loadout.ArmorInstanceId = instanceId;
            return Result.Ok();
        }

        private bool IsGear(CharacterState character, int instanceId, EGearSlot slot)
        {
            GearInventoryEntry entry = character.FindGear(instanceId);
            if (entry is null) return false;
            GearConfig gear = FindGear(entry.GearId);
            return gear is not null && gear.Slot == slot;
        }
        private bool IsSkillKind(CharacterState character, string id, ESkillKind kind)
        {
            if (string.IsNullOrEmpty(id) || character.FindSkill(id) is null) return false;
            SkillConfig skill = FindSkill(id);
            return skill is not null && skill.Kind == kind;
        }
        private SkillConfig FindSkill(string id)
        {
            for (int i = 0; i < _config.Skills.Count; i++) if (string.Equals(_config.Skills[i].Id, id, StringComparison.Ordinal)) return _config.Skills[i];
            return null!;
        }
        private GearConfig FindGear(string id)
        {
            for (int i = 0; i < _config.Gear.Count; i++) if (string.Equals(_config.Gear[i].Id, id, StringComparison.Ordinal)) return _config.Gear[i];
            return null!;
        }
    }
}
