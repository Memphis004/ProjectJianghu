using System;

namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>
    /// A tile on the arena grid. The grid is a rectangle of <c>short</c> coordinates and
    /// every distance the contract uses is Manhattan distance.
    /// </summary>
    /// <remarks>
    /// A readonly struct with <c>short</c> axes: it fits in four bytes, copies without
    /// allocation, and <see cref="Invalid"/> (<c>-1, -1</c>) is distinguishable from every
    /// real tile. <see cref="GetHashCode"/> packs both axes without <c>System.HashCode</c>
    /// so it stays available on Unity's netstandard2.1 profile.
    /// </remarks>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        /// <summary>Horizontal axis, 0 at the left edge of the grid.</summary>
        public readonly short X;

        /// <summary>Vertical axis, 0 at the top edge of the grid.</summary>
        public readonly short Y;

        /// <summary>Creates a coordinate. The cast is the contract's range check.</summary>
        public GridCoord(int x, int y)
        {
            X = (short)x;
            Y = (short)y;
        }

        /// <summary>The "no tile" sentinel; <see cref="IsValid"/> is false for it.</summary>
        public static readonly GridCoord Invalid = new GridCoord(-1, -1);

        /// <summary>True when both axes are non-negative, i.e. this is not <see cref="Invalid"/>.</summary>
        public bool IsValid => X >= 0 && Y >= 0;

        /// <summary>Manhattan distance to <paramref name="other"/>.</summary>
        public int Manhattan(GridCoord other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        /// <inheritdoc />
        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is GridCoord other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (X << 16) ^ (ushort)Y;

        /// <summary>Tile equality.</summary>
        public static bool operator ==(GridCoord left, GridCoord right) => left.Equals(right);

        /// <summary>Tile inequality.</summary>
        public static bool operator !=(GridCoord left, GridCoord right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString() => "(" + X + "," + Y + ")";
    }
}
