using System;

namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>
    /// Walk reachability, walk paths and jump landings — pure queries over an
    /// <see cref="ArenaGrid"/> that write into a caller span and allocate nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A walk is a breadth-first search of the four Manhattan neighbours, limited to
    /// <c>moveRange</c> steps. A fighter may not walk through an occupied tile and may not
    /// end on one, but may walk past a blocked <b>corner</b> — there is no diagonal step, so
    /// there is no corner rule to break.
    /// </para>
    /// <para>
    /// A jump is not a search: it is every tile within <c>jumpRange</c> Manhattan distance
    /// whose landing tile is walkable and empty. Tiles in between are irrelevant, which is
    /// exactly what lets a jump clear a fighter standing in the way (GDD §1.6).
    /// </para>
    /// <para>
    /// Every method writes its result into a caller-provided result span and returns the
    /// number of entries written. When the span is too small the result is truncated rather
    /// than throwing — a caller that wants everything passes
    /// <see cref="ArenaConstants.MaxGridTiles"/> and always gets it. The search's own
    /// bookkeeping is stack memory, never a field or a heap array, so two queries cannot
    /// observe each other and the allocation count per query is zero.
    /// </para>
    /// <para>
    /// Output order is row-major tile index, not discovery order: two calls with the same
    /// inputs return the same list in the same order regardless of which neighbour was
    /// examined first, which is what makes the golden tests in Stage 06 meaningful.
    /// </para>
    /// </remarks>
    public static class ArenaPathfinder
    {
        private const int Unseen = -1;

        /// <summary>
        /// Writes every tile the fighter could walk to, excluding its own tile, and returns
        /// how many were written.
        /// </summary>
        /// <param name="grid">The board.</param>
        /// <param name="start">The fighter's tile; the search starts here and never reports it.</param>
        /// <param name="moveRange">Maximum number of steps; 0 or less yields no tiles.</param>
        /// <param name="destination">Caller buffer for the result.</param>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
        public static int GetReachable(ArenaGrid grid, GridCoord start, int moveRange, Span<GridCoord> destination)
        {
            if (grid is null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (moveRange <= 0 || destination.Length == 0)
            {
                return 0;
            }

            // Sized to the board, not to MaxGridTiles: a 12×12 match pays for 144 entries,
            // not for the 32×32 contract maximum. Stack memory, reclaimed when this method
            // returns, so the allocation count of a query is zero.
            Span<int> distance = stackalloc int[grid.TileCount];
            Span<int> queue = stackalloc int[grid.TileCount];
            Span<byte> parent = stackalloc byte[grid.TileCount];

            Search(grid, start, moveRange, distance, queue, parent);

            int count = 0;
            for (int index = 0; index < grid.TileCount; index++)
            {
                int steps = distance[index];
                if (steps <= 0 || steps > moveRange)
                {
                    continue;
                }

                if (count == destination.Length)
                {
                    break;
                }

                destination[count] = grid.FromIndex(index);
                count++;
            }

            return count;
        }

        /// <summary>
        /// Writes the walk from <paramref name="start"/> to <paramref name="goal"/> one
        /// adjacent tile at a time — the start is excluded, the goal is the last entry — and
        /// returns the number of steps, or 0 when the goal cannot be walked to.
        /// </summary>
        /// <remarks>
        /// 0 is the answer for every refusal: an unwalkable or occupied goal, a tile outside
        /// the grid, a start that is not walkable, a walk longer than
        /// <paramref name="moveRange"/>, a walk longer than <paramref name="path"/>, and a
        /// goal equal to the start. A caller that needs the reason asks
        /// <see cref="ArenaGrid.IsFree"/> first; the command layer maps those to
        /// <see cref="CommandResult"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
        public static int FindPath(ArenaGrid grid, GridCoord start, GridCoord goal, int moveRange, Span<GridCoord> path)
        {
            if (grid is null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (moveRange <= 0 || path.Length == 0)
            {
                return 0;
            }

            int startIndex = grid.ToIndex(start);
            int goalIndex = grid.ToIndex(goal);
            if (startIndex < 0 || goalIndex < 0 || startIndex == goalIndex)
            {
                return 0;
            }

            if (!grid.IsWalkable(start) || !grid.IsFree(goal))
            {
                return 0;
            }

            Span<int> distance = stackalloc int[grid.TileCount];
            Span<int> queue = stackalloc int[grid.TileCount];
            Span<byte> parent = stackalloc byte[grid.TileCount];

            Search(grid, start, moveRange, distance, queue, parent);

            int steps = distance[goalIndex];
            if (steps <= 0 || steps > moveRange || steps > path.Length)
            {
                return 0;
            }

            // Walk the parent chain back from the goal; every node's parent is one step
            // closer to the start, so filling the span backwards lands the tiles in order.
            int node = goalIndex;
            for (int i = steps - 1; i >= 0; i--)
            {
                path[i] = grid.FromIndex(node);
                GridCoord step = ArenaFacing.Delta((IsoDir)parent[node]);
                node -= (step.Y * grid.Width) + step.X;
            }

            return steps;
        }

        /// <summary>
        /// Writes every landing tile of a jump from <paramref name="start"/> — Manhattan
        /// distance 1..<paramref name="jumpRange"/>, walkable and empty — and returns how
        /// many were written.
        /// </summary>
        /// <remarks>
        /// Fighters and walls between the start and the landing tile do not matter, but
        /// landing on a blocked tile or on a fighter does. Row-major order, like the walk.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
        public static int GetJumpTargets(ArenaGrid grid, GridCoord start, int jumpRange, Span<GridCoord> destination)
        {
            if (grid is null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (jumpRange <= 0 || destination.Length == 0)
            {
                return 0;
            }

            int minX = Math.Max(0, start.X - jumpRange);
            int maxX = Math.Min(grid.Width - 1, start.X + jumpRange);
            int minY = Math.Max(0, start.Y - jumpRange);
            int maxY = Math.Min(grid.Height - 1, start.Y + jumpRange);

            int count = 0;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var tile = new GridCoord(x, y);
                    int distance = start.Manhattan(tile);
                    if (distance == 0 || distance > jumpRange || !grid.IsFree(tile))
                    {
                        continue;
                    }

                    if (count == destination.Length)
                    {
                        return count;
                    }

                    destination[count] = tile;
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Fills <paramref name="distance"/> with the step count from <paramref name="start"/>
        /// (0 at the start, <see cref="Unseen"/> elsewhere) by breadth-first search.
        /// </summary>
        private static void Search(
            ArenaGrid grid,
            GridCoord start,
            int moveRange,
            Span<int> distance,
            Span<int> queue,
            Span<byte> parent)
        {
            distance.Fill(Unseen);
            parent.Fill(0);

            int startIndex = grid.ToIndex(start);
            if (startIndex < 0 || !grid.IsWalkable(start))
            {
                return;
            }

            distance[startIndex] = 0;
            queue[0] = startIndex;
            int head = 0;
            int tail = 1;

            while (head < tail)
            {
                int current = queue[head];
                head++;

                int steps = distance[current];
                if (steps >= moveRange)
                {
                    continue;
                }

                int currentX = current % grid.Width;
                int currentY = current / grid.Width;

                for (int direction = 0; direction < 4; direction++)
                {
                    GridCoord delta = ArenaFacing.Delta((IsoDir)direction);
                    var neighbour = new GridCoord(currentX + delta.X, currentY + delta.Y);
                    int neighbourIndex = grid.ToIndex(neighbour);
                    if (neighbourIndex < 0 || distance[neighbourIndex] != Unseen)
                    {
                        continue;
                    }

                    // An occupied tile is entered by nobody: the fighter standing there is
                    // the reason the walk stops, and its own tile is where it already is.
                    if (!grid.IsWalkable(neighbour) || grid.IsOccupied(neighbour))
                    {
                        continue;
                    }

                    distance[neighbourIndex] = steps + 1;
                    parent[neighbourIndex] = (byte)direction;
                    queue[tail] = neighbourIndex;
                    tail++;
                }
            }
        }
    }
}
