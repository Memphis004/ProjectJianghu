using System;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Core.Shared;

namespace ProjectJianghu.Core.Domain.Character
{
    public readonly struct AttributeValue
    {
        public readonly EAttribute Attribute;
        public readonly int Value;
        public AttributeValue(EAttribute attribute, int value) { Attribute = attribute; Value = value; }
    }

    public readonly struct StatSourceValue
    {
        public readonly int Value;
        public readonly int SourceCount;
        public StatSourceValue(int value, int sourceCount) { Value = value; SourceCount = sourceCount; }
    }

    public readonly struct StatResolveResult
    {
        public readonly CombatStatBlock Stats;
        public readonly bool Recomputed;
        public StatResolveResult(CombatStatBlock stats, bool recomputed) { Stats = stats; Recomputed = recomputed; }
    }

    public readonly struct RadarProfile
    {
        public readonly int Martial, Neili, Body, Mind, Meridian;
        public RadarProfile(int martial, int neili, int body, int mind, int meridian)
        { Martial = martial; Neili = neili; Body = body; Mind = mind; Meridian = meridian; }

        public int Get(EAttrCategory category)
        {
            switch (category)
            {
                case EAttrCategory.Martial: return Martial;
                case EAttrCategory.Neili: return Neili;
                case EAttrCategory.Body: return Body;
                case EAttrCategory.Mind: return Mind;
                case EAttrCategory.Meridian: return Meridian;
                default: return 0;
            }
        }
    }

    public readonly struct PowerProfile
    {
        public readonly long Score;
        public readonly byte Rank;
        public PowerProfile(long score, byte rank) { Score = score; Rank = rank; }
    }

    public readonly struct LoadoutSkillResult
    {
        public readonly bool IsSuccess;
        public bool IsFailure => !IsSuccess;
        public readonly bool Enabled;
        public readonly Error Error;
        private LoadoutSkillResult(bool success, bool enabled, Error error) { IsSuccess = success; Enabled = enabled; Error = error; }
        public static LoadoutSkillResult Ok(bool enabled) => new LoadoutSkillResult(true, enabled, default);
        public static LoadoutSkillResult Fail(string code) => new LoadoutSkillResult(false, false, new Error(code));
    }
}
