using System;

namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>
    /// The tile set an area skill covers, from its shape, its aim tile and the direction the
    /// caster faces — pure geometry, written into a caller span.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The anchor is the aimed tile (<see cref="ArenaCommand.Target"/>), never the caster.
    /// The direction comes from the caster — <see cref="ArenaFacing.Toward(GridCoord, GridCoord)"/>
    /// — and rotates the shapes that have a front: <see cref="AreaShape.Line"/> and
    /// <see cref="AreaShape.Cone"/> grow <b>away</b> from the caster, through the anchor.
    /// <see cref="AreaShape.Cross"/>, <see cref="AreaShape.Diamond"/> and
    /// <see cref="AreaShape.Square"/> are symmetric, so the direction does not change them;
    /// they still take it so every shape resolves through one signature.
    /// </para>
    /// <para>What <see cref="ArenaSkillLoadout.AreaSize"/> means per shape:</para>
    /// <list type="table">
    /// <item><term>Single</term><description>ignored; one tile.</description></item>
    /// <item><term>Line</term><description>total tiles, anchor included; 0 counts as 1.</description></item>
    /// <item><term>Cross</term><description>arm length; each arm is that many tiles from the anchor.</description></item>
    /// <item><term>Diamond</term><description>Manhattan radius; 1 is the anchor plus its four neighbours.</description></item>
    /// <item><term>Square</term><description>half extent; 1 is a 3×3 block.</description></item>
    /// <item><term>Cone</term><description>length in rows; row <c>i</c> is <c>2i+1</c> tiles wide.</description></item>
    /// </list>
    /// <para>
    /// Tiles outside the grid are dropped — the highlight a player sees is clipped exactly
    /// like the damage will be. Tiles inside the grid but blocked by the mask are
    /// <b>kept</b>: the shape is geometry, and whether a wall stops a skill is a Stage 07
    /// rule that reads the same set. Output order is the order the shape generates, which is
    /// stable for a given shape, direction and size; a truncated result (the span was too
    /// small) keeps the first entries.
    /// </para>
    /// </remarks>
    public static class ArenaArea
    {
        /// <summary>
        /// Writes the covered tiles of <paramref name="shape"/> and returns how many were
        /// written.
        /// </summary>
        /// <param name="shape">The skill's shape.</param>
        /// <param name="anchor">The aimed tile.</param>
        /// <param name="direction">The caster's facing, which orients line and cone.</param>
        /// <param name="size">The size, interpreted per shape as above.</param>
        /// <param name="grid">The board, used for its bounds.</param>
        /// <param name="destination">Caller buffer for the result.</param>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
        public static int Resolve(
            AreaShape shape,
            GridCoord anchor,
            IsoDir direction,
            int size,
            ArenaGrid grid,
            Span<GridCoord> destination)
        {
            if (grid is null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (destination.Length == 0)
            {
                return 0;
            }

            int extent = Math.Max(0, size);
            int count = 0;

            switch (shape)
            {
                case AreaShape.Line:
                    GridCoord forward = ArenaFacing.Delta(direction);
                    int length = Math.Max(1, extent);
                    for (int i = 0; i < length; i++)
                    {
                        count = Emit(grid, destination, count, anchor.X + (forward.X * i), anchor.Y + (forward.Y * i));
                    }

                    return count;

                case AreaShape.Cross:
                    count = Emit(grid, destination, count, anchor.X, anchor.Y);
                    for (int step = 1; step <= extent; step++)
                    {
                        count = Emit(grid, destination, count, anchor.X + step, anchor.Y);
                        count = Emit(grid, destination, count, anchor.X - step, anchor.Y);
                        count = Emit(grid, destination, count, anchor.X, anchor.Y + step);
                        count = Emit(grid, destination, count, anchor.X, anchor.Y - step);
                    }

                    return count;

                case AreaShape.Diamond:
                    for (int dy = -extent; dy <= extent; dy++)
                    {
                        int reach = extent - Math.Abs(dy);
                        for (int dx = -reach; dx <= reach; dx++)
                        {
                            count = Emit(grid, destination, count, anchor.X + dx, anchor.Y + dy);
                        }
                    }

                    return count;

                case AreaShape.Square:
                    for (int dy = -extent; dy <= extent; dy++)
                    {
                        for (int dx = -extent; dx <= extent; dx++)
                        {
                            count = Emit(grid, destination, count, anchor.X + dx, anchor.Y + dy);
                        }
                    }

                    return count;

                case AreaShape.Cone:
                    GridCoord along = ArenaFacing.Delta(direction);
                    // Perpendicular in grid space: a horizontal facing widens vertically.
                    int acrossX = along.Y;
                    int acrossY = along.X;
                    for (int row = 0; row < Math.Max(1, extent); row++)
                    {
                        int centreX = anchor.X + (along.X * row);
                        int centreY = anchor.Y + (along.Y * row);
                        for (int offset = -row; offset <= row; offset++)
                        {
                            count = Emit(grid, destination, count, centreX + (acrossX * offset), centreY + (acrossY * offset));
                        }
                    }

                    return count;

                default:
                    return Emit(grid, destination, 0, anchor.X, anchor.Y) > 0 ? 1 : 0;
            }
        }

        /// <summary>Appends one tile when it is inside the grid and the result still has room.</summary>
        private static int Emit(ArenaGrid grid, Span<GridCoord> destination, int count, int x, int y)
        {
            if (count == destination.Length)
            {
                return count;
            }

            var tile = new GridCoord(x, y);
            if (!grid.InBounds(tile))
            {
                return count;
            }

            destination[count] = tile;
            return count + 1;
        }
    }
}
