using System;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Core.Shared;

namespace ProjectJianghu.Core.Domain.Character
{
    public sealed class CharacterCreation
    {
        private readonly IGameConfig _config;
        private readonly LoadoutRules _loadoutRules;
        public CharacterCreation(IGameConfig config)
        { _config = config ?? throw new ArgumentNullException(nameof(config)); _loadoutRules = new LoadoutRules(config); }

        public Result<CharacterState> Create(string characterId, string backgroundId)
        {
            if (string.IsNullOrEmpty(characterId)) return Result.Fail<CharacterState>("creation.character_id_required");
            BackgroundConfig background = FindBackground(backgroundId);
            if (background is null) return Result.Fail<CharacterState>("creation.background_not_found", ErrorKind.NotFound);
            var character = new CharacterState(characterId) { BackgroundId = backgroundId };
            for (int i = 0; i < background.Attributes.Count; i++) character.SetBaseAttribute(background.Attributes[i].Attribute, background.Attributes[i].Value);
            for (int i = 0; i < background.StartSkillIds.Count; i++)
                character.LearnSkill(new LearnedSkill(background.StartSkillIds[i], 1, 0));
            for (int i = 0; i < background.StartGearIds.Count; i++) character.AddGear(new GearInventoryEntry(background.StartGearIds[i], i + 1));

            EquipBackgroundLoadout(character, background);
            character.UnspentAttributePoints = _config.Constant.StartingAttributePoints + background.AttributePoints;
            return Result<CharacterState>.Ok(character);
        }

        public Result Allocate(CharacterState character, EAttribute attribute, int delta)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            AttributeConfig definition = FindAttribute(attribute);
            if (definition is null) return Result.Fail("creation.attribute_not_found", ErrorKind.NotFound);
            if (delta == 0) return Result.Ok();
            if (delta > 0 && delta > character.UnspentAttributePoints) return Result.Fail("creation.insufficient_points", ErrorKind.Insufficient);
            int current = character.GetBaseAttribute(attribute);
            int target;
            try { target = checked(current + delta); }
            catch (OverflowException) { return Result.Fail("creation.attribute_out_of_bounds"); }
            if (target < definition.CreationMin || target > definition.CreationMax) return Result.Fail("creation.attribute_out_of_bounds");
            character.SetBaseAttribute(attribute, target);
            character.UnspentAttributePoints -= delta;
            return Result.Ok();
        }

        public Result Validate(CharacterState character)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            BackgroundConfig background = FindBackground(character.BackgroundId);
            if (background is null) return Result.Fail("creation.background_not_found", ErrorKind.NotFound);
            for (int i = 0; i < _config.Attributes.Count; i++)
            {
                AttributeConfig attribute = _config.Attributes[i];
                int value = character.GetBaseAttribute(attribute.Attribute);
                if (value < attribute.CreationMin || value > attribute.CreationMax)
                    return Result.Fail("creation.attribute_out_of_bounds");
            }
            return _loadoutRules.Validate(character);
        }

        private void EquipBackgroundLoadout(CharacterState character, BackgroundConfig background)
        {
            for (int i = 0; i < background.StartGearIds.Count; i++)
            {
                GearConfig gear = FindGear(background.StartGearIds[i]);
                if (gear is null) continue;
                switch (gear.Slot)
                {
                    case EGearSlot.Weapon: character.Loadout.WeaponInstanceId = i + 1; break;
                    case EGearSlot.Armor: character.Loadout.ArmorInstanceId = i + 1; break;
                    case EGearSlot.Accessory:
                        if (character.Loadout.AccessoryInstanceId(0) == 0) character.Loadout.SetAccessory(0, i + 1);
                        else if (character.Loadout.AccessoryInstanceId(1) == 0) character.Loadout.SetAccessory(1, i + 1);
                        break;
                }
            }
            int external = 0;
            for (int i = 0; i < background.StartSkillIds.Count; i++)
            {
                SkillConfig skill = FindSkill(background.StartSkillIds[i]);
                if (skill is null) continue;
                if (skill.Kind == ESkillKind.External && external < 4) character.Loadout.SetExternalSkill(external++, skill.Id);
                else if (skill.Kind == ESkillKind.Inner) character.Loadout.InnerSkillId = skill.Id;
                else if (skill.Kind == ESkillKind.Movement) character.Loadout.MovementSkillId = skill.Id;
            }
        }

        private BackgroundConfig FindBackground(string id)
        { for (int i = 0; i < _config.Backgrounds.Count; i++) if (string.Equals(_config.Backgrounds[i].Id, id, StringComparison.Ordinal)) return _config.Backgrounds[i]; return null!; }
        private AttributeConfig FindAttribute(EAttribute attribute)
        { for (int i = 0; i < _config.Attributes.Count; i++) if (_config.Attributes[i].Attribute == attribute) return _config.Attributes[i]; return null!; }
        private GearConfig FindGear(string id)
        { for (int i = 0; i < _config.Gear.Count; i++) if (string.Equals(_config.Gear[i].Id, id, StringComparison.Ordinal)) return _config.Gear[i]; return null!; }
        private SkillConfig FindSkill(string id)
        { for (int i = 0; i < _config.Skills.Count; i++) if (string.Equals(_config.Skills[i].Id, id, StringComparison.Ordinal)) return _config.Skills[i]; return null!; }
    }
}
