using System;
using ProjectJianghu.Core.Domain.Arena;

namespace ProjectJianghu.Core.Tests.Arena
{
    /// <summary>
    /// Builds the two-fighter setup the arena contract suite uses everywhere.
    /// </summary>
    /// <remarks>
    /// The numbers are deliberately small and round: the suite is checking the contract,
    /// not the balance data. The player stands at (1,1) and the enemy at (4,1), three
    /// tiles apart, so a range-3 skill is usable from the start and a range-2 walk is
    /// not enough to close the gap in one action.
    /// </remarks>
    internal static class ArenaTestSetup
    {
        internal static ArenaMatchSetup Create(
            AtbMode mode = AtbMode.Active,
            int playerFillTicks = 8,
            int enemyFillTicks = 8,
            int timeLimitTicks = 0,
            bool devCheats = false,
            int playerHp = 1000,
            int enemyHp = 1000,
            int playerStartHp = -1,
            int enemyStartHp = -1,
            int damage = 10,
            int skillRange = 3,
            int moveRange = 2,
            int jumpRange = 2,
            int cooldownTicks = 0,
            int neiliCost = 0,
            ulong seed = 7)
        {
            return new ArenaMatchSetup
            {
                Seed = seed,
                ArenaMapId = 1,
                Mode = mode,
                TimeLimitTicks = timeLimitTicks,
                DevCheatsAllowed = devCheats,
                Fighters = new[]
                {
                    Fighter(0, 0, 1, true, new GridCoord(1, 1), playerFillTicks, playerHp, playerStartHp,
                        damage, skillRange, moveRange, jumpRange, cooldownTicks, neiliCost),
                    Fighter(1, 1, 2, false, new GridCoord(4, 1), enemyFillTicks, enemyHp, enemyStartHp,
                        damage, skillRange, moveRange, jumpRange, cooldownTicks, neiliCost),
                },
            };
        }

        internal static ArenaFighterSetup Fighter(
            byte id,
            byte team,
            int defId,
            bool playerControlled,
            GridCoord startTile,
            int atbFillTicks,
            int maxHp,
            int startHp,
            int damage,
            int skillRange,
            int moveRange,
            int jumpRange,
            int cooldownTicks,
            int neiliCost)
        {
            return new ArenaFighterSetup
            {
                Id = id,
                Team = team,
                DefId = defId,
                PlayerControlled = playerControlled,
                AiProfileId = playerControlled ? 0 : 1,
                AiReactionTicks = 0,
                StartTile = startTile,
                StartFacing = IsoDir.SE,
                StartHp = startHp,
                Stats = new CombatStatBlock
                {
                    MaxHp = maxHp,
                    MaxNeili = 100,
                    NeiliRegenPerSecX100 = 10,
                    ExternalPower = 50,
                    InnerPower = 50,
                    Defense = 10,
                    InnerDefense = 10,
                    AccuracyX100 = 9000,
                    EvasionX100 = 500,
                    CritChanceX100 = 500,
                    CritDamageX100 = 15000,
                    CounterChanceX100 = 1000,
                    StatusResistX100 = 1000,
                    KnockbackResist = 0,
                    AtbFillTicks = atbFillTicks,
                    ActionSpeedX100 = 10000,
                    MoveRange = (byte)moveRange,
                    JumpRange = (byte)jumpRange,
                    JumpCooldownTicks = 3,
                },
                Skills = new[]
                {
                    new ArenaSkillLoadout
                    {
                        SkillId = 101,
                        Slot = 0,
                        Level = 1,
                        WeaponType = 1,
                        Targeting = SkillTargeting.Enemy,
                        RangeMin = 1,
                        RangeMax = (byte)skillRange,
                        Area = AreaShape.Single,
                        AreaSize = 0,
                        Kind = DamageKind.External,
                        BaseDamage = damage,
                        Hits = 1,
                        WindUpTicks = 0,
                        RecoveryTicks = 0,
                        CooldownTicks = cooldownTicks,
                        NeiliCost = neiliCost,
                        StatusId = 0,
                        StatusChanceX100 = 0,
                        StatusDurationTicks = 0,
                        KnockbackTiles = 0,
                        Flags = SkillFlags.None,
                    },
                },
                PassiveIds = Array.Empty<int>(),
            };
        }
    }
}
