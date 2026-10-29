using System;
using System.Collections.Generic;

namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>
    /// The arena's tile map: which tiles can be stood on, and who stands on them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built from the <c>arena_map</c> table's <c>mask</c> (one string per row, <c>.</c>
    /// walkable and <c>#</c> blocked). The grid owns only geometry and occupancy; the
    /// rules that consume it — reachability, jump, area shapes — are stateless functions
    /// in <see cref="ArenaPathfinder"/> and <see cref="ArenaArea"/> so a query never
    /// mutates the grid.
    /// </para>
    /// <para>
    /// <see cref="GridCoord"/> and an index cross at <see cref="ToIndex"/> /
    /// <see cref="FromIndex"/>, row-major (<c>Y * Width + X</c>). The index is what the
    /// search algorithms use internally; it is not part of the save format, so changing
    /// the packing breaks nothing a player can see.
    /// </para>
    /// <para>
    /// Occupancy is a mirror of the fighter positions, not the truth: Stage 07 keeps the
    /// fighters' tiles and calls <see cref="TryOccupy"/> / <see cref="TryVacate"/> so
    /// every query sees one consistent board. <see cref="ArenaConstants.NoFighter"/>
    /// means "empty", and writing it through <see cref="TryOccupy"/> is rejected.
    /// </para>
    /// <para>
    /// Construction allocates two byte arrays; every query after that allocates nothing.
    /// </para>
    /// </remarks>
    public sealed class ArenaGrid
    {
        /// <summary>The mask character of a walkable tile.</summary>
        public const char WalkableChar = '.';

        /// <summary>The mask character of a blocked tile.</summary>
        public const char BlockedChar = '#';

        private const byte BlockedFlag = 1;

        private readonly byte[] _blocked;
        private readonly byte[] _occupant;

        /// <summary>Creates an empty, fully walkable grid.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// An edge is below 1 or above <see cref="ArenaConstants.MaxGridSize"/>.
        /// </exception>
        public ArenaGrid(int width, int height)
        {
            if (width < 1 || width > ArenaConstants.MaxGridSize)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "A grid edge must be 1..ArenaConstants.MaxGridSize tiles.");
            }

            if (height < 1 || height > ArenaConstants.MaxGridSize)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "A grid edge must be 1..ArenaConstants.MaxGridSize tiles.");
            }

            Width = width;
            Height = height;
            TileCount = width * height;
            _blocked = new byte[TileCount];
            _occupant = new byte[TileCount];
            ClearOccupancy();
        }

        /// <summary>Number of tiles per row.</summary>
        public int Width { get; }

        /// <summary>Number of rows.</summary>
        public int Height { get; }

        /// <summary><see cref="Width"/> × <see cref="Height"/>; at most <see cref="ArenaConstants.MaxGridTiles"/>.</summary>
        public int TileCount { get; }

        /// <summary>
        /// Builds a grid from an <c>arena_map</c> mask: <paramref name="rows"/> has
        /// <paramref name="height"/> rows of exactly <paramref name="width"/> characters,
        /// each <see cref="WalkableChar"/> or <see cref="BlockedChar"/>.
        /// </summary>
        /// <remarks>
        /// The shape is validated here as well as in <c>Data.Validator</c> on purpose: the
        /// data rules protect the authored tables, this one protects a caller that built a
        /// mask in code or loaded a row from a save.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
        /// <exception cref="ArgumentException">The mask's shape or characters are wrong.</exception>
        public static ArenaGrid FromMask(int width, int height, IReadOnlyList<string> rows)
        {
            if (rows is null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            var grid = new ArenaGrid(width, height);

            if (rows.Count != height)
            {
                throw new ArgumentException("An arena mask needs exactly height rows; got " + rows.Count + " for height " + height + ".", nameof(rows));
            }

            for (int y = 0; y < height; y++)
            {
                string row = rows[y];
                if (row is null)
                {
                    throw new ArgumentException("Arena mask row " + y + " is missing.", nameof(rows));
                }

                if (row.Length != width)
                {
                    throw new ArgumentException("Arena mask row " + y + " is " + row.Length + " characters; width is " + width + ".", nameof(rows));
                }

                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                {
                    char tile = row[x];
                    if (tile == BlockedChar)
                    {
                        grid._blocked[rowStart + x] = BlockedFlag;
                    }
                    else if (tile != WalkableChar)
                    {
                        throw new ArgumentException("Arena mask row " + y + " column " + x + " has the unknown tile character '" + tile + "'.", nameof(rows));
                    }
                }
            }

            return grid;
        }

        /// <summary>True when <paramref name="tile"/> is inside the grid.</summary>
        public bool InBounds(GridCoord tile) =>
            tile.X >= 0 && tile.Y >= 0 && tile.X < Width && tile.Y < Height;

        /// <summary>True when the tile is inside the grid and its mask is walkable.</summary>
        public bool IsWalkable(GridCoord tile) =>
            InBounds(tile) && _blocked[(tile.Y * Width) + tile.X] == 0;

        /// <summary>True when a fighter stands on the tile. Out of bounds is never occupied.</summary>
        public bool IsOccupied(GridCoord tile) =>
            InBounds(tile) && _occupant[(tile.Y * Width) + tile.X] != ArenaConstants.NoFighter;

        /// <summary>
        /// True when a fighter could stand on the tile: in bounds, walkable and empty.
        /// This is the "landing tile" test of a walk and of a jump.
        /// </summary>
        public bool IsFree(GridCoord tile) =>
            IsWalkable(tile) && _occupant[(tile.Y * Width) + tile.X] == ArenaConstants.NoFighter;

        /// <summary>The id standing on the tile, or <see cref="ArenaConstants.NoFighter"/>.</summary>
        public byte OccupantAt(GridCoord tile) =>
            InBounds(tile) ? _occupant[(tile.Y * Width) + tile.X] : ArenaConstants.NoFighter;

        /// <summary>Row-major index of the tile, or -1 when it is outside the grid.</summary>
        public int ToIndex(GridCoord tile) => InBounds(tile) ? (tile.Y * Width) + tile.X : -1;

        /// <summary>The tile at a row-major index, or <see cref="GridCoord.Invalid"/> when the index is outside the grid.</summary>
        public GridCoord FromIndex(int index)
        {
            if (index < 0 || index >= TileCount)
            {
                return GridCoord.Invalid;
            }

            return new GridCoord(index % Width, index / Width);
        }

        /// <summary>
        /// Marks the tile as held by <paramref name="fighterId"/>. Fails for an id of
        /// <see cref="ArenaConstants.NoFighter"/>, a tile that is not walkable, and a tile
        /// that already holds someone (including the same id).
        /// </summary>
        public bool TryOccupy(GridCoord tile, byte fighterId)
        {
            if (fighterId == ArenaConstants.NoFighter || !IsWalkable(tile))
            {
                return false;
            }

            int index = (tile.Y * Width) + tile.X;
            if (_occupant[index] != ArenaConstants.NoFighter)
            {
                return false;
            }

            _occupant[index] = fighterId;
            return true;
        }

        /// <summary>Frees the tile. False when nobody stood there.</summary>
        public bool TryVacate(GridCoord tile)
        {
            if (!IsOccupied(tile))
            {
                return false;
            }

            _occupant[(tile.Y * Width) + tile.X] = ArenaConstants.NoFighter;
            return true;
        }

        /// <summary>Clears every occupant, keeping the mask.</summary>
        public void ClearOccupancy()
        {
            for (int i = 0; i < _occupant.Length; i++)
            {
                _occupant[i] = ArenaConstants.NoFighter;
            }
        }
    }
}
