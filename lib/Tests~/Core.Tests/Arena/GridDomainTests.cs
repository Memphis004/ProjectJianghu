using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Domain.Config;
using Xunit;

namespace ProjectJianghu.Core.Tests.Arena
{
    /// <summary>
    /// Stage 06 — the grid and targeting domain: <see cref="ArenaGrid"/>,
    /// <see cref="ArenaPathfinder"/>, <see cref="ArenaArea"/> and <see cref="ArenaFacing"/>.
    /// </summary>
    /// <remarks>
    /// The suite is deliberately split into what each kind of test can prove: golden tile
    /// sets pin the geometry, a 10,000-case property run compares the search with an
    /// independent flood fill, and the allocation and timing facts are measurements rather
    /// than assertions about intent.
    /// </remarks>
    public sealed class GridDomainTests
    {
        private static readonly int[] StepX = { 1, -1, 0, 0 };
        private static readonly int[] StepY = { 0, 0, 1, -1 };

        // ------------------------------------------------------------------ grid

        [Fact]
        public void Grid_reads_its_size_and_blocked_tiles_from_a_mask()
        {
            ArenaGrid grid = Park();

            Assert.Equal(12, grid.Width);
            Assert.Equal(12, grid.Height);
            Assert.Equal(144, grid.TileCount);
            Assert.True(grid.IsWalkable(new GridCoord(0, 0)));
            Assert.False(grid.IsWalkable(new GridCoord(4, 2)));
            Assert.False(grid.IsWalkable(new GridCoord(5, 3)));
            Assert.True(grid.IsWalkable(new GridCoord(5, 8)));
            Assert.False(grid.IsWalkable(new GridCoord(6, 8)));

            int blocked = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (!grid.IsWalkable(new GridCoord(x, y)))
                    {
                        blocked++;
                    }
                }
            }

            Assert.Equal(8, blocked);
        }

        [Fact]
        public void Authored_arena_map_loads_into_a_grid()
        {
            ArenaMapConfig map = ConfigFixture.Load().ArenaMaps[0];
            ArenaGrid grid = ArenaGrid.FromMask(map.Width, map.Height, map.Mask);

            Assert.Equal(12, grid.Width);
            Assert.Equal(12, grid.Height);
            Assert.False(grid.IsWalkable(new GridCoord(4, 2)));
            Assert.True(grid.IsFree(new GridCoord(6, 6)));
        }

        [Fact]
        public void Grid_index_round_trips_and_rejects_tiles_outside_the_grid()
        {
            ArenaGrid grid = Park();

            Assert.Equal(51, grid.ToIndex(new GridCoord(3, 4)));
            Assert.Equal(new GridCoord(3, 4), grid.FromIndex(51));
            Assert.Equal(new GridCoord(0, 0), grid.FromIndex(0));
            Assert.Equal(new GridCoord(11, 11), grid.FromIndex(143));

            Assert.Equal(-1, grid.ToIndex(new GridCoord(12, 0)));
            Assert.Equal(-1, grid.ToIndex(GridCoord.Invalid));
            Assert.Equal(GridCoord.Invalid, grid.FromIndex(-1));
            Assert.Equal(GridCoord.Invalid, grid.FromIndex(144));
            Assert.False(grid.InBounds(new GridCoord(0, 12)));
            Assert.False(grid.IsWalkable(GridCoord.Invalid));
            Assert.Equal(ArenaConstants.NoFighter, grid.OccupantAt(GridCoord.Invalid));
        }

        [Fact]
        public void Grid_rejects_a_malformed_mask()
        {
            Assert.Throws<ArgumentNullException>(() => ArenaGrid.FromMask(2, 1, null!));
            Assert.Throws<ArgumentException>(() => ArenaGrid.FromMask(2, 2, new[] { ".." }));
            Assert.Throws<ArgumentException>(() => ArenaGrid.FromMask(3, 1, new[] { ".." }));
            Assert.Throws<ArgumentException>(() => ArenaGrid.FromMask(2, 1, new[] { ".x" }));
            Assert.Throws<ArgumentException>(() => ArenaGrid.FromMask(2, 1, new string[] { null! }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaGrid(0, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaGrid(4, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaGrid(ArenaConstants.MaxGridSize + 1, 1));
        }

        [Fact]
        public void Occupancy_is_exclusive_and_only_on_walkable_tiles()
        {
            ArenaGrid grid = Park();
            var tile = new GridCoord(3, 3);

            Assert.True(grid.IsFree(tile));
            Assert.False(grid.TryOccupy(new GridCoord(4, 2), 1));   // blocked by the mask
            Assert.False(grid.TryOccupy(GridCoord.Invalid, 1));      // outside the grid
            Assert.False(grid.TryOccupy(tile, ArenaConstants.NoFighter));

            Assert.True(grid.TryOccupy(tile, 1));
            Assert.Equal((byte)1, grid.OccupantAt(tile));
            Assert.True(grid.IsOccupied(tile));
            Assert.False(grid.IsFree(tile));
            Assert.False(grid.TryOccupy(tile, 1));                   // same id, still taken
            Assert.False(grid.TryOccupy(tile, 2));                   // someone else's tile

            Assert.True(grid.TryVacate(tile));
            Assert.False(grid.TryVacate(tile));
            Assert.True(grid.IsFree(tile));
        }

        [Fact]
        public void Clearing_occupancy_frees_every_tile_but_keeps_the_mask()
        {
            ArenaGrid grid = Park();
            Assert.True(grid.TryOccupy(new GridCoord(0, 0), 1));
            Assert.True(grid.TryOccupy(new GridCoord(1, 0), 2));

            grid.ClearOccupancy();

            Assert.False(grid.IsOccupied(new GridCoord(0, 0)));
            Assert.False(grid.IsOccupied(new GridCoord(1, 0)));
            Assert.False(grid.IsWalkable(new GridCoord(4, 2)));
        }

        // ------------------------------------------------------------- walking

        [Fact]
        public void Reachable_on_an_open_grid_is_the_manhattan_disk()
        {
            var grid = new ArenaGrid(12, 12);
            var buffer = new GridCoord[ArenaConstants.MaxGridTiles];

            int count = ArenaPathfinder.GetReachable(grid, new GridCoord(1, 1), 2, buffer);

            Assert.Equal(
                "0,0;1,0;2,0;0,1;2,1;3,1;0,2;1,2;2,2;1,3",
                Sorted(count, buffer));
        }

        [Fact]
        public void Reachable_never_includes_the_start_tile()
        {
            var grid = new ArenaGrid(5, 5);
            var buffer = new GridCoord[ArenaConstants.MaxGridTiles];

            int count = ArenaPathfinder.GetReachable(grid, new GridCoord(2, 2), 3, buffer);

            for (int i = 0; i < count; i++)
            {
                Assert.NotEqual(new GridCoord(2, 2), buffer[i]);
            }
        }

        [Fact]
        public void Reachable_does_not_pass_through_a_fighter()
        {
            var grid = new ArenaGrid(5, 1);
            var buffer = new GridCoord[ArenaConstants.MaxGridTiles];
            Assert.True(grid.TryOccupy(new GridCoord(2, 0), 1));

            int count = ArenaPathfinder.GetReachable(grid, new GridCoord(0, 0), 4, buffer);

            // (3,0) and (4,0) are within range in Manhattan distance, but the only corridor
            // runs through (2,0), so a walk cannot reach them.
            Assert.Equal("1,0", Sorted(count, buffer));
            Assert.Equal(0, ArenaPathfinder.FindPath(grid, new GridCoord(0, 0), new GridCoord(3, 0), 4, buffer));
        }

        [Fact]
        public void Reachable_walks_around_a_wall_and_returns_zero_for_an_unwalkable_start()
        {
            ArenaGrid grid = Corridor();
            var buffer = new GridCoord[ArenaConstants.MaxGridTiles];

            int shortRange = ArenaPathfinder.GetReachable(grid, new GridCoord(1, 2), 4, buffer);
            Assert.DoesNotContain(new GridCoord(3, 2), buffer.Take(shortRange));

            int longRange = ArenaPathfinder.GetReachable(grid, new GridCoord(1, 2), 6, buffer);
            Assert.Contains(new GridCoord(3, 2), buffer.Take(longRange));

            Assert.Equal(0, ArenaPathfinder.GetReachable(grid, new GridCoord(2, 2), 4, buffer));
            Assert.Equal(0, ArenaPathfinder.GetReachable(grid, GridCoord.Invalid, 4, buffer));
            Assert.Equal(0, ArenaPathfinder.GetReachable(grid, new GridCoord(1, 2), 0, buffer));
        }

        [Fact]
        public void Reachable_truncates_to_the_callers_span()
        {
            var grid = new ArenaGrid(12, 12);
            var full = new GridCoord[ArenaConstants.MaxGridTiles];
            var small = new GridCoord[3];

            int expected = ArenaPathfinder.GetReachable(grid, new GridCoord(1, 1), 3, full);
            int written = ArenaPathfinder.GetReachable(grid, new GridCoord(1, 1), 3, small);

            Assert.Equal(3, written);
            Assert.True(expected > written);
            Assert.Equal(full.Take(3), small.Take(3));
        }

        [Fact]
        public void Path_is_one_adjacent_step_per_tile_and_ends_on_the_goal()
        {
            ArenaGrid grid = Park();
            var path = new GridCoord[ArenaConstants.MaxGridTiles];
            var start = new GridCoord(1, 1);
            var goal = new GridCoord(6, 5);

            int steps = ArenaPathfinder.FindPath(grid, start, goal, 9, path);

            Assert.True(steps > 0);
            Assert.Equal(goal, path[steps - 1]);
            AssertPathIsLegal(grid, start, path, steps, goal);
            Assert.Equal(9, Manhattan(start, goal)); // the park is open along this heading
        }

        [Fact]
        public void Path_refuses_a_goal_that_is_blocked_occupied_out_of_range_or_outside()
        {
            ArenaGrid grid = Corridor();
            var path = new GridCoord[ArenaConstants.MaxGridTiles];
            var start = new GridCoord(1, 2);

            Assert.Equal(0, ArenaPathfinder.FindPath(grid, start, start, 6, path));
            Assert.Equal(0, ArenaPathfinder.FindPath(grid, start, new GridCoord(2, 2), 6, path));
            Assert.Equal(0, ArenaPathfinder.FindPath(grid, start, new GridCoord(3, 2), 5, path));
            Assert.Equal(0, ArenaPathfinder.FindPath(grid, start, new GridCoord(9, 2), 20, path));
            Assert.Equal(0, ArenaPathfinder.FindPath(grid, new GridCoord(2, 2), new GridCoord(1, 2), 6, path));

            Assert.True(grid.TryOccupy(new GridCoord(1, 4), 1));
            Assert.Equal(0, ArenaPathfinder.FindPath(grid, start, new GridCoord(1, 4), 6, path)); // occupied goal
            Assert.Equal(0, ArenaPathfinder.FindPath(grid, start, new GridCoord(3, 2), 6, path)); // the only corridor is blocked

            Assert.True(grid.TryVacate(new GridCoord(1, 4)));
            Assert.Equal(6, ArenaPathfinder.FindPath(grid, start, new GridCoord(3, 2), 6, path));
        }

        [Fact]
        public void Path_truncation_reports_failure_rather_than_a_partial_walk()
        {
            ArenaGrid grid = Corridor();
            var shortPath = new GridCoord[3];

            Assert.Equal(0, ArenaPathfinder.FindPath(grid, new GridCoord(1, 2), new GridCoord(3, 2), 6, shortPath));
        }

        // ---------------------------------------------------------------- jump

        [Fact]
        public void Jump_lands_only_on_free_tiles_within_manhattan_range()
        {
            var grid = new ArenaGrid(5, 1);
            var buffer = new GridCoord[ArenaConstants.MaxGridTiles];
            Assert.True(grid.TryOccupy(new GridCoord(2, 0), 1));

            int count = ArenaPathfinder.GetJumpTargets(grid, new GridCoord(0, 0), 4, buffer);

            // The fighter at (2,0) is jumped over; it is not a landing tile, and the start
            // tile is never a landing tile either.
            Assert.Equal("1,0;3,0;4,0", Sorted(count, buffer));
        }

        [Fact]
        public void Jump_excludes_a_blocked_landing_tile_and_far_landings()
        {
            ArenaGrid grid = Park();
            var buffer = new GridCoord[ArenaConstants.MaxGridTiles];
            var start = new GridCoord(2, 1);

            int count = ArenaPathfinder.GetJumpTargets(grid, start, 2, buffer);

            Assert.DoesNotContain(new GridCoord(4, 2), buffer.Take(count)); // '#' in the mask
            Assert.DoesNotContain(new GridCoord(2, 1), buffer.Take(count)); // the start
            Assert.DoesNotContain(new GridCoord(5, 1), buffer.Take(count)); // Manhattan 3
            Assert.Contains(new GridCoord(4, 1), buffer.Take(count));
            Assert.Contains(new GridCoord(0, 1), buffer.Take(count));       // Manhattan 2
            Assert.DoesNotContain(new GridCoord(0, 3), buffer.Take(count));  // Manhattan 4
            for (int i = 0; i < count; i++)
            {
                Assert.InRange(Manhattan(start, buffer[i]), 1, 2);
                Assert.True(grid.IsFree(buffer[i]));
            }

            Assert.Equal(0, ArenaPathfinder.GetJumpTargets(grid, new GridCoord(0, 0), 0, buffer));
        }

        // -------------------------------------------------------------- shapes

        [Theory]
        [InlineData(AreaShape.Single, IsoDir.NE, "5,5")]
        [InlineData(AreaShape.Single, IsoDir.SW, "5,5")]
        [InlineData(AreaShape.Line, IsoDir.NE, "5,5;5,6")]
        [InlineData(AreaShape.Line, IsoDir.SE, "5,5;6,5")]
        [InlineData(AreaShape.Line, IsoDir.SW, "5,4;5,5")]
        [InlineData(AreaShape.Line, IsoDir.NW, "4,5;5,5")]
        [InlineData(AreaShape.Cross, IsoDir.NE, "5,3;5,4;3,5;4,5;5,5;6,5;7,5;5,6;5,7")]
        [InlineData(AreaShape.Cross, IsoDir.SE, "5,3;5,4;3,5;4,5;5,5;6,5;7,5;5,6;5,7")]
        [InlineData(AreaShape.Cross, IsoDir.SW, "5,3;5,4;3,5;4,5;5,5;6,5;7,5;5,6;5,7")]
        [InlineData(AreaShape.Cross, IsoDir.NW, "5,3;5,4;3,5;4,5;5,5;6,5;7,5;5,6;5,7")]
        [InlineData(AreaShape.Diamond, IsoDir.NE, "5,3;4,4;5,4;6,4;3,5;4,5;5,5;6,5;7,5;4,6;5,6;6,6;5,7")]
        [InlineData(AreaShape.Diamond, IsoDir.SE, "5,3;4,4;5,4;6,4;3,5;4,5;5,5;6,5;7,5;4,6;5,6;6,6;5,7")]
        [InlineData(AreaShape.Diamond, IsoDir.SW, "5,3;4,4;5,4;6,4;3,5;4,5;5,5;6,5;7,5;4,6;5,6;6,6;5,7")]
        [InlineData(AreaShape.Diamond, IsoDir.NW, "5,3;4,4;5,4;6,4;3,5;4,5;5,5;6,5;7,5;4,6;5,6;6,6;5,7")]
        [InlineData(AreaShape.Square, IsoDir.NE, "3,3;4,3;5,3;6,3;7,3;3,4;4,4;5,4;6,4;7,4;3,5;4,5;5,5;6,5;7,5;3,6;4,6;5,6;6,6;7,6;3,7;4,7;5,7;6,7;7,7")]
        [InlineData(AreaShape.Square, IsoDir.SE, "3,3;4,3;5,3;6,3;7,3;3,4;4,4;5,4;6,4;7,4;3,5;4,5;5,5;6,5;7,5;3,6;4,6;5,6;6,6;7,6;3,7;4,7;5,7;6,7;7,7")]
        [InlineData(AreaShape.Cone, IsoDir.NE, "5,5;4,6;5,6;6,6")]
        [InlineData(AreaShape.Cone, IsoDir.SE, "6,4;5,5;6,5;6,6")]
        [InlineData(AreaShape.Cone, IsoDir.SW, "4,4;5,4;6,4;5,5")]
        [InlineData(AreaShape.Cone, IsoDir.NW, "4,4;4,5;5,5;4,6")]
        public void Shape_golden(AreaShape shape, IsoDir direction, string expected)
        {
            var grid = new ArenaGrid(12, 12);
            var buffer = new GridCoord[ArenaConstants.MaxGridTiles];

            int count = ArenaArea.Resolve(shape, new GridCoord(5, 5), direction, 2, grid, buffer);

            Assert.Equal(expected, Sorted(count, buffer));
        }

        [Fact]
        public void Line_and_cone_grow_away_from_the_caster_in_generation_order()
        {
            var grid = new ArenaGrid(12, 12);
            var buffer = new GridCoord[ArenaConstants.MaxGridTiles];
            var anchor = new GridCoord(5, 5);

            int line = ArenaArea.Resolve(AreaShape.Line, anchor, IsoDir.SE, 3, grid, buffer);
            Assert.Equal(new[] { new GridCoord(5, 5), new GridCoord(6, 5), new GridCoord(7, 5) }, buffer.Take(line));

            int cone = ArenaArea.Resolve(AreaShape.Cone, anchor, IsoDir.SE, 3, grid, buffer);
            Assert.Equal(
                new[]
                {
                    new GridCoord(5, 5),
                    new GridCoord(6, 4), new GridCoord(6, 5), new GridCoord(6, 6),
                    new GridCoord(7, 3), new GridCoord(7, 4), new GridCoord(7, 5), new GridCoord(7, 6), new GridCoord(7, 7),
                },
                buffer.Take(cone));
        }

        [Fact]
        public void Shape_is_clipped_to_the_grid_bounds()
        {
            var grid = new ArenaGrid(12, 12);
            var buffer = new GridCoord[ArenaConstants.MaxGridTiles];

            int corner = ArenaArea.Resolve(AreaShape.Cross, new GridCoord(0, 0), IsoDir.SE, 2, grid, buffer);
            Assert.Equal("0,0;1,0;2,0;0,1;0,2", Sorted(corner, buffer));

            int edge = ArenaArea.Resolve(AreaShape.Line, new GridCoord(11, 11), IsoDir.SE, 3, grid, buffer);
            Assert.Equal("11,11", Sorted(edge, buffer));

            int square = ArenaArea.Resolve(AreaShape.Square, new GridCoord(0, 0), IsoDir.NE, 1, grid, buffer);
            Assert.Equal("0,0;1,0;0,1;1,1", Sorted(square, buffer));

            int cornerCone = ArenaArea.Resolve(AreaShape.Cone, new GridCoord(11, 11), IsoDir.SE, 3, grid, buffer);
            Assert.Equal("11,11", Sorted(cornerCone, buffer));
        }

        [Fact]
        public void Shape_zero_size_and_small_buffers_are_defined()
        {
            var grid = new ArenaGrid(12, 12);
            var buffer = new GridCoord[ArenaConstants.MaxGridTiles];
            var anchor = new GridCoord(5, 5);

            int diamond = ArenaArea.Resolve(AreaShape.Diamond, anchor, IsoDir.NE, 0, grid, buffer);
            Assert.Equal("5,5", Sorted(diamond, buffer));

            int square = ArenaArea.Resolve(AreaShape.Square, anchor, IsoDir.NE, 0, grid, buffer);
            Assert.Equal("5,5", Sorted(square, buffer));

            int line = ArenaArea.Resolve(AreaShape.Line, anchor, IsoDir.NE, 0, grid, buffer);
            Assert.Equal("5,5", Sorted(line, buffer));

            int cone = ArenaArea.Resolve(AreaShape.Cone, anchor, IsoDir.NE, 0, grid, buffer);
            Assert.Equal("5,5", Sorted(cone, buffer));

            var small = new GridCoord[2];
            Assert.Equal(2, ArenaArea.Resolve(AreaShape.Square, new GridCoord(5, 5), IsoDir.NE, 1, grid, small));
            Assert.Equal(0, ArenaArea.Resolve(AreaShape.Single, new GridCoord(5, 5), IsoDir.NE, 1, grid, Span<GridCoord>.Empty));
        }

        // -------------------------------------------------------------- facing

        [Fact]
        public void Direction_delta_and_opposite_are_the_four_axes()
        {
            Assert.Equal(new GridCoord(0, 1), ArenaFacing.Delta(IsoDir.NE));
            Assert.Equal(new GridCoord(1, 0), ArenaFacing.Delta(IsoDir.SE));
            Assert.Equal(new GridCoord(0, -1), ArenaFacing.Delta(IsoDir.SW));
            Assert.Equal(new GridCoord(-1, 0), ArenaFacing.Delta(IsoDir.NW));

            Assert.Equal(IsoDir.SW, ArenaFacing.Opposite(IsoDir.NE));
            Assert.Equal(IsoDir.NW, ArenaFacing.Opposite(IsoDir.SE));
            Assert.Equal(IsoDir.NE, ArenaFacing.Opposite(IsoDir.SW));
            Assert.Equal(IsoDir.SE, ArenaFacing.Opposite(IsoDir.NW));
        }

        [Fact]
        public void Facing_toward_uses_the_dominant_axis_and_ties_go_to_x()
        {
            var origin = new GridCoord(5, 5);

            Assert.Equal(IsoDir.SE, ArenaFacing.Toward(origin, new GridCoord(9, 5)));
            Assert.Equal(IsoDir.NW, ArenaFacing.Toward(origin, new GridCoord(1, 3)));
            Assert.Equal(IsoDir.NE, ArenaFacing.Toward(origin, new GridCoord(6, 9)));
            Assert.Equal(IsoDir.SW, ArenaFacing.Toward(origin, new GridCoord(4, 0)));
            Assert.Equal(IsoDir.SE, ArenaFacing.Toward(origin, new GridCoord(6, 6))); // tie → X axis
            Assert.Equal(IsoDir.NE, ArenaFacing.Toward(origin, new GridCoord(6, 7))); // dy dominates
            Assert.Equal(IsoDir.SE, ArenaFacing.Toward(origin, origin));

            Assert.Equal(IsoDir.SE, ArenaFacing.AfterStep(origin, new GridCoord(6, 5)));
            Assert.Equal(IsoDir.SW, ArenaFacing.AfterStep(origin, new GridCoord(5, 4)));
        }

        [Theory]
        [InlineData(IsoDir.NE, 5, 6, RelativeAngle.Front)]   // in front
        [InlineData(IsoDir.NE, 5, 4, RelativeAngle.Back)]    // behind
        [InlineData(IsoDir.NE, 6, 5, RelativeAngle.Flank)]
        [InlineData(IsoDir.NE, 4, 5, RelativeAngle.Flank)]
        [InlineData(IsoDir.SE, 6, 5, RelativeAngle.Front)]
        [InlineData(IsoDir.SE, 4, 5, RelativeAngle.Back)]
        [InlineData(IsoDir.SE, 5, 6, RelativeAngle.Flank)]
        [InlineData(IsoDir.SE, 5, 4, RelativeAngle.Flank)]
        [InlineData(IsoDir.SW, 5, 4, RelativeAngle.Front)]
        [InlineData(IsoDir.SW, 5, 6, RelativeAngle.Back)]
        [InlineData(IsoDir.SW, 6, 5, RelativeAngle.Flank)]
        [InlineData(IsoDir.SW, 4, 5, RelativeAngle.Flank)]
        [InlineData(IsoDir.NW, 4, 5, RelativeAngle.Front)]
        [InlineData(IsoDir.NW, 6, 5, RelativeAngle.Back)]
        [InlineData(IsoDir.NW, 5, 6, RelativeAngle.Flank)]
        [InlineData(IsoDir.NW, 5, 4, RelativeAngle.Flank)]
        [InlineData(IsoDir.SE, 5, 5, RelativeAngle.Front)]   // same tile: nothing to be behind
        [InlineData(IsoDir.SE, 6, 6, RelativeAngle.Front)]   // diagonal, X axis dominates
        [InlineData(IsoDir.NE, 6, 7, RelativeAngle.Front)]   // diagonal, Y axis dominates
        [InlineData(IsoDir.SW, 6, 8, RelativeAngle.Back)]   // far behind, Y axis dominates
        public void Relative_angle_is_front_flank_or_back(IsoDir facing, int attackerX, int attackerY, RelativeAngle expected)
        {
            Assert.Equal(
                expected,
                ArenaFacing.Relative(new GridCoord(5, 5), facing, new GridCoord(attackerX, attackerY)));
        }

        // ----------------------------------------------------- property & budget

        [Fact]
        public void Every_reachable_tile_has_a_legal_path_and_nothing_else_does()
        {
            var random = new Random(20261030);
            var reachable = new GridCoord[ArenaConstants.MaxGridTiles];
            var path = new GridCoord[ArenaConstants.MaxGridTiles];
            int checkedPaths = 0;

            for (int iteration = 0; iteration < 10_000; iteration++)
            {
                int width = 1 + random.Next(6);
                int height = 1 + random.Next(6);
                var rows = new string[height];
                for (int y = 0; y < height; y++)
                {
                    var row = new char[width];
                    for (int x = 0; x < width; x++)
                    {
                        row[x] = random.Next(100) < 30 ? '#' : '.';
                    }

                    rows[y] = new string(row);
                }

                ArenaGrid grid = ArenaGrid.FromMask(width, height, rows);
                var walkable = new List<GridCoord>(grid.TileCount);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var tile = new GridCoord(x, y);
                        if (grid.IsWalkable(tile))
                        {
                            walkable.Add(tile);
                        }
                    }
                }

                if (walkable.Count == 0)
                {
                    continue;
                }

                GridCoord start = walkable[random.Next(walkable.Count)];
                int occupants = random.Next(3);
                for (int i = 0; i < occupants; i++)
                {
                    GridCoord blocker = walkable[random.Next(walkable.Count)];
                    if (blocker != start)
                    {
                        grid.TryOccupy(blocker, (byte)(i + 1));
                    }
                }

                int moveRange = random.Next(0, 7);
                int count = ArenaPathfinder.GetReachable(grid, start, moveRange, reachable);

                int[] expected = ReferenceDistances(grid, start, moveRange);
                var actualMask = new bool[grid.TileCount];
                for (int i = 0; i < count; i++)
                {
                    int index = grid.ToIndex(reachable[i]);
                    Assert.InRange(index, 0, grid.TileCount - 1);
                    Assert.False(actualMask[index]);
                    actualMask[index] = true;
                    Assert.InRange(Manhattan(start, reachable[i]), 1, Math.Max(1, moveRange));
                }

                for (int index = 0; index < grid.TileCount; index++)
                {
                    bool expectedReachable = expected[index] > 0 && expected[index] <= moveRange;
                    if (expectedReachable != actualMask[index])
                    {
                        Assert.Fail("iteration " + iteration + ": tile " + grid.FromIndex(index) + " expected " + expectedReachable);
                    }
                }

                // A handful of goals per iteration is enough to exercise the path
                // reconstruction without making the run about the test harness.
                int probes = Math.Min(count, 4);
                for (int i = 0; i < probes; i++)
                {
                    GridCoord goal = reachable[((i + 1) * count) / (probes + 1)];
                    int steps = ArenaPathfinder.FindPath(grid, start, goal, moveRange, path);

                    Assert.Equal(expected[grid.ToIndex(goal)], steps);
                    Assert.InRange(steps, 1, Math.Max(1, moveRange));
                    AssertPathIsLegal(grid, start, path, steps, goal);
                    checkedPaths++;
                }
            }

            Assert.True(checkedPaths > 10_000, "only " + checkedPaths + " paths were checked");
        }

        [Fact]
        public void Queries_write_into_caller_spans_without_allocating()
        {
            ArenaMapConfig map = ConfigFixture.Load().ArenaMaps[0];
            ArenaGrid grid = ArenaGrid.FromMask(map.Width, map.Height, map.Mask);
            var buffer = new GridCoord[ArenaConstants.MaxGridTiles];
            var path = new GridCoord[ArenaConstants.MaxGridTiles];
            var start = new GridCoord(1, 1);

            for (int i = 0; i < 100; i++)
            {
                RunEveryQuery(grid, start, buffer, path, out _);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 500; i++)
            {
                RunEveryQuery(grid, start, buffer, path, out _);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0L, after - before);
        }

        [Fact]
        public void Arena_queries_stay_under_fifty_microseconds()
        {
            ArenaMapConfig map = ConfigFixture.Load().ArenaMaps[0];
            ArenaGrid grid = ArenaGrid.FromMask(map.Width, map.Height, map.Mask);
            var buffer = new GridCoord[ArenaConstants.MaxGridTiles];
            var path = new GridCoord[ArenaConstants.MaxGridTiles];
            var start = new GridCoord(1, 1);

            for (int i = 0; i < 500; i++)
            {
                RunEveryQuery(grid, start, buffer, path, out _);
            }

            const int iterations = 5_000;
            int sink = 0;
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                RunEveryQuery(grid, start, buffer, path, out int produced);
                sink += produced;
            }

            watch.Stop();

            Assert.True(sink > 0);
            double microseconds = watch.Elapsed.TotalMilliseconds * 1000.0 / (iterations * 4);
            Assert.True(
                microseconds < 50.0,
                "one query averaged " + microseconds.ToString("F3") + " µs, over the 50 µs (0.05 ms) budget");
        }

        // ------------------------------------------------------------- helpers

        /// <summary>The authored 12×12 map as rows, independent of the data pipeline.</summary>
        private static ArenaGrid Park() => ArenaGrid.FromMask(12, 12, new[]
        {
            "............",
            "............",
            "....##......",
            "....##......",
            "............",
            "............",
            "............",
            "............",
            "......####..",
            "............",
            "............",
            "............",
        });

        /// <summary>
        /// A 5×5 board with a wall down column 2 and one opening at the bottom row, so a
        /// detour is the only way from the left half to the right half.
        /// </summary>
        private static ArenaGrid Corridor() => ArenaGrid.FromMask(5, 5, new[]
        {
            "..#..",
            "..#..",
            "..#..",
            "..#..",
            ".....",
        });

        /// <summary>Runs the four query kinds the arena makes, for the timing and allocation facts.</summary>
        private static void RunEveryQuery(ArenaGrid grid, GridCoord start, GridCoord[] buffer, GridCoord[] path, out int produced)
        {
            int count = ArenaPathfinder.GetReachable(grid, start, 4, buffer);
            count += ArenaPathfinder.FindPath(grid, start, new GridCoord(10, 10), 20, path);
            count += ArenaPathfinder.GetJumpTargets(grid, start, 3, buffer);
            count += ArenaArea.Resolve(AreaShape.Cone, new GridCoord(6, 6), IsoDir.NE, 3, grid, buffer);
            produced = count;
        }

        /// <summary>
        /// An independent flood fill: plain arrays and a queue, sharing no code with
        /// <see cref="ArenaPathfinder"/> so the property test compares two implementations
        /// rather than one with itself.
        /// </summary>
        private static int[] ReferenceDistances(ArenaGrid grid, GridCoord start, int range)
        {
            var distance = new int[grid.TileCount];
            for (int i = 0; i < distance.Length; i++)
            {
                distance[i] = -1;
            }

            int startIndex = grid.ToIndex(start);
            if (startIndex < 0 || !grid.IsWalkable(start) || range <= 0)
            {
                return distance;
            }

            distance[startIndex] = 0;
            var queue = new Queue<int>();
            queue.Enqueue(startIndex);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int steps = distance[current];
                if (steps >= range)
                {
                    continue;
                }

                int cx = current % grid.Width;
                int cy = current / grid.Width;
                for (int d = 0; d < 4; d++)
                {
                    var tile = new GridCoord(cx + StepX[d], cy + StepY[d]);
                    if (!grid.IsWalkable(tile) || grid.IsOccupied(tile))
                    {
                        continue;
                    }

                    int index = grid.ToIndex(tile);
                    if (distance[index] != -1)
                    {
                        continue;
                    }

                    distance[index] = steps + 1;
                    queue.Enqueue(index);
                }
            }

            return distance;
        }

        private static void AssertPathIsLegal(ArenaGrid grid, GridCoord start, GridCoord[] path, int steps, GridCoord goal)
        {
            Assert.Equal(goal, path[steps - 1]);

            GridCoord previous = start;
            for (int i = 0; i < steps; i++)
            {
                GridCoord tile = path[i];
                Assert.Equal(1, Manhattan(previous, tile));
                Assert.True(grid.IsWalkable(tile));
                Assert.False(grid.IsOccupied(tile));
                previous = tile;
            }

            // A path never repeats a tile: repeating one would be a walk that wastes a step.
            for (int i = 0; i < steps; i++)
            {
                for (int j = i + 1; j < steps; j++)
                {
                    Assert.NotEqual(path[i], path[j]);
                }
            }
        }

        private static int Manhattan(GridCoord a, GridCoord b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        /// <summary>Renders a tile list sorted by row then column, so a golden string ignores generation order.</summary>
        private static string Sorted(int count, GridCoord[] tiles)
        {
            var ordered = new List<GridCoord>(count);
            for (int i = 0; i < count; i++)
            {
                ordered.Add(tiles[i]);
            }

            ordered.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            return string.Join(";", ordered.Select(t => t.X + "," + t.Y));
        }
    }
}
