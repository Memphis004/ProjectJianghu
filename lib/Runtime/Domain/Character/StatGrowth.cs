using System;
using System.Collections.Generic;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Core.Shared;

namespace ProjectJianghu.Core.Domain.Character
{
    public readonly struct MasteryChange
    {
        public readonly string SkillId;
        public readonly int OldLevel, NewLevel, OldXp, NewXp, GainedXp;
        public MasteryChange(string skillId, int oldLevel, int newLevel, int oldXp, int newXp, int gainedXp)
        { SkillId = skillId; OldLevel = oldLevel; NewLevel = newLevel; OldXp = oldXp; NewXp = newXp; GainedXp = gainedXp; }
    }

    public sealed class MasteryApplyResult
    {
        public IReadOnlyList<MasteryChange> Changes { get; }
        public MasteryApplyResult(IReadOnlyList<MasteryChange> changes) => Changes = changes;
    }

    public sealed class StatGrowth
    {
        private readonly IGameConfig _config;
        public StatGrowth(IGameConfig config) => _config = config ?? throw new ArgumentNullException(nameof(config));

        public Result<MasteryApplyResult> ApplyMastery(CharacterState character, IReadOnlyList<SkillUseStat> uses)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if (uses is null) throw new ArgumentNullException(nameof(uses));
            var changes = new List<MasteryChange>();
            int intellect = character.GetBaseAttribute(EAttribute.Intellect);
            long multiplier = 10000L + (long)intellect * _config.Constant.MasteryIntellectBonusX100PerPoint;
            if (multiplier < 0) return Result.Fail<MasteryApplyResult>("mastery.invalid_xp_rate");

            for (int i = 0; i < uses.Count; i++)
            {
                if (uses[i].Uses < 0 || uses[i].Hits < 0 || uses[i].Hits > uses[i].Uses)
                    return Result.Fail<MasteryApplyResult>("mastery.invalid_use_stat");
                if (FindSkill(uses[i].SkillId) is null)
                    return Result.Fail<MasteryApplyResult>("mastery.skill_not_found", ErrorKind.NotFound);
            }

            for (int i = 0; i < uses.Count; i++)
            {
                SkillConfig skill = FindSkill(uses[i].SkillId)!;
                LearnedSkill learned = character.FindSkill(skill.Id);
                if (learned is null) continue;
                int oldXp = learned.MasteryXp;
                int oldLevel = learned.Level;
                if (uses[i].Uses == 0) continue;
                long gainedLong = (long)uses[i].Uses * _config.Constant.MasteryXpPerUse * multiplier / 10000L;
                int gained = gainedLong > int.MaxValue ? int.MaxValue : (int)gainedLong;
                long total = Math.Min(int.MaxValue, (long)oldXp + gained);
                int level = Math.Min(learned.Level, _config.Constant.MaxMasteryLevel);
                for (int j = 0; j < _config.SkillLevels.Count; j++)
                    if (_config.SkillLevels[j].Level > level && _config.SkillLevels[j].MasteryXp <= total)
                        level = _config.SkillLevels[j].Level;
                character.SetMastery(learned, level, (int)total);
                changes.Add(new MasteryChange(skill.Id, oldLevel, level, oldXp, (int)total, gained));
            }
            return Result<MasteryApplyResult>.Ok(new MasteryApplyResult(changes));
        }

        private SkillConfig FindSkill(int id)
        {
            for (int i = 0; i < _config.Skills.Count; i++)
                if (StableId(_config.Skills[i].Id) == id) return _config.Skills[i];
            return null!;
        }

        private SkillConfig FindSkill(string id)
        {
            for (int i = 0; i < _config.Skills.Count; i++)
                if (string.Equals(_config.Skills[i].Id, id, StringComparison.Ordinal)) return _config.Skills[i];
            return null!;
        }

        public static int StableId(string id)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < id.Length; i++) { hash ^= id[i]; hash *= 16777619; }
                return (int)(hash & 0x7fffffff);
            }
        }
    }
}
