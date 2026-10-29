using ProjectJianghu.Core.Domain.Arena;
using Xunit;

namespace ProjectJianghu.Core.Tests.Arena
{
    /// <summary>
    /// The concrete frame the fake and the simulation share: two published ticks,
    /// double-buffered cooldowns, and a mid-tick update that must not destroy the
    /// previous tick the view interpolates from.
    /// </summary>
    public sealed class ArenaFrameBufferTests
    {
        [Fact]
        public void Publishing_swaps_current_and_previous_without_copying()
        {
            var frame = new ArenaFrameBuffer();

            frame.BeginFrame();
            frame.Add(View(0, 1, 100, 1, 1));
            frame.SetCooldowns(0, new ushort[] { 3, 0, 0, 0 });
            frame.SetClock(10, 5, 3600, 0);
            frame.Publish();

            Assert.Equal(1, frame.CurrentCount);
            Assert.Equal(0, frame.PreviousCount);
            Assert.Equal(100, frame.Current[0].Hp);
            Assert.Equal(10, frame.Tick);
            Assert.Equal(5, frame.ElapsedTicks);
            Assert.Equal(3600, frame.TimeLimitTicks);
            Assert.Equal(0, frame.PlayerFighter);
            Assert.Equal(3, frame.SkillCooldowns(0)[0]);

            frame.BeginFrame();
            frame.Add(View(0, 1, 90, 2, 1));
            frame.SetCooldowns(0, new ushort[] { 2, 0, 0, 0 });
            frame.SetClock(11, 6, 3600, 0);
            frame.Publish();

            Assert.Equal(90, frame.Current[0].Hp);
            Assert.Equal(2, frame.Current[0].Tile.X);
            Assert.Equal(100, frame.Previous[0].Hp);
            Assert.Equal(1, frame.Previous[0].Tile.X);
            Assert.Equal(2, frame.SkillCooldowns(0)[0]);
        }

        [Fact]
        public void Updating_the_current_frame_leaves_the_previous_tick_alone()
        {
            var frame = new ArenaFrameBuffer();

            frame.BeginFrame();
            frame.Add(View(0, 0, 100, 1, 1));
            frame.SetCooldowns(0, new ushort[] { 0, 0, 0, 0 });
            frame.Publish();

            frame.BeginFrame();
            frame.Add(View(0, 0, 100, 1, 1));
            frame.SetCooldowns(0, new ushort[] { 0, 0, 0, 0 });
            frame.Publish();

            frame.UpdateCurrent(0, View(0, 0, 80, 3, 1));
            frame.UpdateCurrentCooldowns(0, new ushort[] { 7, 0, 0, 0 });

            Assert.Equal(80, frame.Current[0].Hp);
            Assert.Equal(3, frame.Current[0].Tile.X);
            Assert.Equal(100, frame.Previous[0].Hp);
            Assert.Equal(1, frame.Previous[0].Tile.X);
            Assert.Equal(7, frame.SkillCooldowns(0)[0]);
        }

        private static FighterView View(byte id, byte team, int hp, short x, short y) =>
            new FighterView
            {
                Id = id,
                Team = team,
                State = FighterState.Charging,
                Facing = IsoDir.SE,
                Tile = new GridCoord(x, y),
                PrevX = x,
                PrevY = y,
                X = x,
                Y = y,
                Hp = hp,
                MaxHp = 100,
                Atb = 0,
            };
    }
}
