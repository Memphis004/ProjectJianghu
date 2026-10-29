using System.Linq;
using ProjectJianghu.Core.Application.Arena.Fake;
using ProjectJianghu.Core.Domain.Arena;
using Xunit;

namespace ProjectJianghu.Core.Tests.Arena
{
    /// <summary>
    /// The fake's geometry is the Stage 06 domain's, not its own: the walk query, the jump
    /// query, the skill preview, the hit area, the command validation and the facing all go
    /// through <see cref="ArenaPathfinder"/>, <see cref="ArenaArea"/> and
    /// <see cref="ArenaFacing"/> over one <see cref="ArenaGrid"/>. These facts pin that
    /// agreement, which is what lets the presentation trust a highlight.
    /// </summary>
    public sealed class FakeArenaGeometryTests
    {
        private static readonly GridCoord PlayerStart = new GridCoord(1, 1);

        [Fact]
        public void A_fighter_in_the_way_shortens_the_walk_and_the_command_says_blocked()
        {
            using IArenaSession session = new FakeArenaSession(Match(
                Fighter(0, 0, true, PlayerStart),
                Fighter(1, 1, false, new GridCoord(2, 1))));

            byte player = Start(session);
            var tiles = new GridCoord[ArenaConstants.MaxGridTiles];
            int count = session.Query.GetReachableTiles(player, tiles);

            // (3,1) is two tiles away by Manhattan distance and the move range is three, but
            // every four-directional path is four steps: the fighter at (2,1) is a wall.
            Assert.True(count > 0);
            Assert.DoesNotContain(new GridCoord(3, 1), tiles.Take(count));

            Assert.Equal(CommandResult.RejectedBlocked, session.Submit(ArenaCommand.Move(player, new GridCoord(3, 1))));
            Assert.Equal(CommandResult.RejectedBlocked, session.Submit(ArenaCommand.Move(player, new GridCoord(2, 1))));
        }

        [Fact]
        public void A_jump_lands_behind_a_fighter_the_walk_cannot_pass()
        {
            using IArenaSession session = new FakeArenaSession(Match(
                Fighter(0, 0, true, PlayerStart),
                Fighter(1, 1, false, new GridCoord(2, 1))));

            byte player = Start(session);
            var tiles = new GridCoord[ArenaConstants.MaxGridTiles];
            int count = session.Query.GetJumpTiles(player, tiles);

            Assert.Contains(new GridCoord(3, 1), tiles.Take(count));
            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Jump(player, new GridCoord(3, 1))));
            Assert.Equal(new GridCoord(3, 1), FindView(session.Frame, player).Tile);
        }

        [Fact]
        public void The_preview_is_exactly_the_area_the_resolve_hits()
        {
            using IArenaSession session = new FakeArenaSession(Match(
                Fighter(0, 0, true, PlayerStart, area: AreaShape.Cross, areaSize: 1, targeting: SkillTargeting.Ground),
                Fighter(1, 1, false, new GridCoord(3, 1)),
                Fighter(2, 1, false, new GridCoord(3, 2))));

            byte player = Start(session);
            var target = new GridCoord(3, 1);
            var preview = new GridCoord[ArenaConstants.MaxGridTiles];
            int count = session.Query.PreviewSkillArea(player, 0, target, preview);

            var expected = new GridCoord[ArenaConstants.MaxGridTiles];
            var grid = new ArenaGrid(12, 12);
            int expectedCount = ArenaArea.Resolve(
                AreaShape.Cross, target, ArenaFacing.Toward(PlayerStart, target), 1, grid, expected);

            Assert.Equal(5, count);
            Assert.Equal(expectedCount, count);
            Assert.Equal(expected.Take(expectedCount), preview.Take(count));

            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Skill(player, 0, target)));

            // The enemy on the aim tile and the one that is only inside the cross.
            Assert.Equal(990, FindView(session.Frame, 1).Hp);
            Assert.Equal(990, FindView(session.Frame, 2).Hp);
        }

        [Theory]
        [InlineData(4, 1, IsoDir.SE)]
        [InlineData(1, 4, IsoDir.NE)]
        [InlineData(0, 1, IsoDir.NW)]
        [InlineData(1, 0, IsoDir.SW)]
        public void Facing_after_a_move_is_the_stage06_direction(int x, int y, IsoDir expected)
        {
            // A second fighter keeps the match running: with only one team left the fake
            // resolves the victory and every command becomes WrongPhase.
            using IArenaSession session = new FakeArenaSession(Match(
                Fighter(0, 0, true, PlayerStart, moveRange: 4),
                Fighter(1, 1, false, new GridCoord(10, 10))));

            byte player = Start(session);
            var target = new GridCoord(x, y);

            Assert.Equal(CommandResult.Ok, session.Submit(ArenaCommand.Move(player, target)));
            Assert.Equal(expected, ArenaFacing.Toward(PlayerStart, target));
            Assert.Equal(expected, FindView(session.Frame, player).Facing);
        }

        // ------------------------------------------------------------- helpers

        /// <summary>Runs the intro and forces the player's ATB so a command is legal.</summary>
        private static byte Start(IArenaSession session)
        {
            for (int i = 0; i < ArenaConstants.IntroTicks + 4 && session.Phase == ArenaPhase.Intro; i++)
            {
                session.Step();
            }

            Assert.Equal(ArenaPhase.Running, session.Phase);

            byte player = session.Frame.PlayerFighter;
            Assert.Equal(
                CommandResult.Ok,
                session.Submit(ArenaCommand.Dev(ArenaCommandKind.DevFillAtb, player)));
            return player;
        }

        private static FighterView FindView(IArenaFrame frame, byte id)
        {
            foreach (FighterView view in frame.Fighters)
            {
                if (view.Id == id)
                {
                    return view;
                }
            }

            Assert.Fail("fighter " + id + " is not in the frame");
            return default;
        }

        private static ArenaMatchSetup Match(params ArenaFighterSetup[] fighters) => new ArenaMatchSetup
        {
            Seed = 7,
            ArenaMapId = 3,
            Mode = AtbMode.Active,
            TimeLimitTicks = 0,
            DevCheatsAllowed = true,
            Fighters = fighters,
        };

        private static ArenaFighterSetup Fighter(
            byte id,
            byte team,
            bool playerControlled,
            GridCoord tile,
            int moveRange = 3,
            int jumpRange = 2,
            int skillRange = 3,
            AreaShape area = AreaShape.Single,
            int areaSize = 0,
            SkillTargeting targeting = SkillTargeting.Enemy,
            int damage = 10)
        {
            return new ArenaFighterSetup
            {
                Id = id,
                Team = team,
                DefId = id + 1,
                PlayerControlled = playerControlled,
                AiProfileId = playerControlled ? 0 : 1,
                AiReactionTicks = 0,
                StartTile = tile,
                StartFacing = IsoDir.SE,
                StartHp = -1,
                Stats = new CombatStatBlock
                {
                    MaxHp = 1000,
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
                    // The player is ready after a tick or two; the enemy never is, so it
                    // stands still and the geometry under test cannot move.
                    AtbFillTicks = playerControlled ? 1 : 500,
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
                        Targeting = targeting,
                        RangeMin = 1,
                        RangeMax = (byte)skillRange,
                        Area = area,
                        AreaSize = (byte)areaSize,
                        Kind = DamageKind.External,
                        BaseDamage = damage,
                        Hits = 1,
                        WindUpTicks = 0,
                        RecoveryTicks = 0,
                        CooldownTicks = 0,
                        NeiliCost = 0,
                        StatusId = 0,
                        StatusChanceX100 = 0,
                        StatusDurationTicks = 0,
                        KnockbackTiles = 0,
                        Flags = SkillFlags.None,
                    },
                },
            };
        }
    }
}
