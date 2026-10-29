using System;
using System.Collections.Generic;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Core.Shared;

namespace ProjectJianghu.Core.Domain.Character
{
    public sealed class ArenaFighterBuilder
    {
        private readonly IGameConfig _config;
        private readonly StatResolver _stats;
        private readonly LoadoutRules _rules;
        public ArenaFighterBuilder(IGameConfig config, StatResolver stats)
        { _config = config ?? throw new ArgumentNullException(nameof(config)); _stats = stats ?? throw new ArgumentNullException(nameof(stats)); _rules = new LoadoutRules(config); }

        public Result<ArenaFighterSetup> Build(CharacterState character, byte id, byte team, bool playerControlled,
            int aiProfileId, int aiReactionTicks, GridCoord startTile, IsoDir facing, int defId = 0)
        {
            Result validation = _rules.Validate(character);
            if (validation.IsFailure) return Result.Fail<ArenaFighterSetup>(validation.Error);
            var skills = new List<ArenaSkillLoadout>(4);
            for (int slot = 0; slot < 4; slot++)
            {
                LoadoutSkillResult skillState = _rules.GetExternalSkillState(character, slot);
                if (skillState.IsFailure) return Result.Fail<ArenaFighterSetup>(skillState.Error);
                if (!skillState.Enabled) continue;
                string skillId = character.Loadout.ExternalSkillId(slot);
                if (string.IsNullOrEmpty(skillId)) continue;
                SkillConfig skill = FindSkill(skillId);
                LearnedSkill learned = character.FindSkill(skillId);
                int skillIdNumber = StatGrowth.StableId(skillId);
                int statusId = string.IsNullOrEmpty(skill.StatusId) ? 0 : StatGrowth.StableId(skill.StatusId);
                int mastery = MasteryMultiplier(learned.Level);
                skills.Add(new ArenaSkillLoadout
                {
                    SkillId = skillIdNumber, Slot = (byte)slot, Level = (byte)learned.Level,
                    WeaponType = (byte)skill.WeaponType, Targeting = (SkillTargeting)(int)skill.Target - 1,
                    RangeMin = (byte)skill.RangeMin, RangeMax = (byte)skill.RangeMax,
                    Area = (AreaShape)(int)skill.AreaShape - 1, AreaSize = 0,
                    Kind = DamageKind.External, BaseDamage = skill.Power, RatioX100 = skill.RatioX100,
                    MasteryX100 = mastery, Hits = 1, WindUpTicks = skill.WindUpTicks,
                    RecoveryTicks = skill.RecoveryTicks, CooldownTicks = 0, NeiliCost = 0,
                    AtbRefundPermille = 0, StatusId = statusId, StatusChanceX100 = skill.StatusChanceX100,
                    StatusDurationTicks = 0, KnockbackTiles = (byte)skill.KnockbackTiles, Flags = SkillFlags.None,
                });
            }
            skills.Sort((left, right) => left.Slot.CompareTo(right.Slot));
            var passives = new List<int>(4);
            AddPassive(passives, character.Loadout.InnerSkillId);
            AddPassive(passives, character.Loadout.MovementSkillId);
            for (int i = 0; i < 2; i++)
            {
                GearInventoryEntry entry = character.FindGear(character.Loadout.AccessoryInstanceId(i));
                if (entry is not null) passives.Add(StatGrowth.StableId(entry.GearId));
            }
            return Result<ArenaFighterSetup>.Ok(new ArenaFighterSetup
            {
                Id = id, Team = team, DefId = defId, PlayerControlled = playerControlled,
                AiProfileId = aiProfileId, AiReactionTicks = aiReactionTicks, StartTile = startTile,
                StartFacing = facing, StartHp = -1, Stats = _stats.Resolve(character).Stats,
                Skills = skills, PassiveIds = passives,
            });
        }

        private int MasteryMultiplier(int level)
        {
            int value = 10000;
            for (int i = 0; i < _config.SkillLevels.Count; i++)
                if (_config.SkillLevels[i].Level <= level && _config.SkillLevels[i].Level >= 1
                    && _config.SkillLevels[i].CombatMasteryX100 > value)
                    value = _config.SkillLevels[i].CombatMasteryX100;
            return value;
        }
        private void AddPassive(List<int> destination, string id)
        { if (!string.IsNullOrEmpty(id)) destination.Add(StatGrowth.StableId(id)); }
        private SkillConfig FindSkill(string id)
        {
            for (int i = 0; i < _config.Skills.Count; i++) if (string.Equals(_config.Skills[i].Id, id, StringComparison.Ordinal)) return _config.Skills[i];
            return null!;
        }
    }
}
