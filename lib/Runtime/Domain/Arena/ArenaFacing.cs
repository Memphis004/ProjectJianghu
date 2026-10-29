using System;

namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>Where a hit lands relative to the defender's facing.</summary>
    /// <remarks>
    /// The rule lives here, once (ADR 0102 decision 2): gameplay reads this enum, never a
    /// sprite. Stage 07 turns <see cref="Back"/> into <see cref="ArenaEventFlags.Backstab"/>
    /// and <see cref="Flank"/> into <see cref="ArenaEventFlags.Flank"/>; the damage bonuses
    /// themselves are <c>constant</c> rows, not geometry.
    /// </remarks>
    public enum RelativeAngle : byte
    {
        /// <summary>The attacker stands in the direction the defender faces.</summary>
        Front = 0,

        /// <summary>The attacker stands to a side; a small bonus.</summary>
        Flank = 1,

        /// <summary>The attacker stands behind; the backstab bonus and crit.</summary>
        Back = 2,
    }

    /// <summary>
    /// Grid directions, the facing they imply, and the front / flank / back rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Movement is four-directional Manhattan, so a fighter only ever faces one of the four
    /// <see cref="IsoDir"/> diagonals (ADR 0102). The grid-axis mapping is fixed here and is
    /// the one <c>FakeArenaSession</c> already used, so swapping the fake for the real
    /// simulation in Stage 07 cannot change which way anyone turns:
    /// </para>
    /// <code>
    /// NE = ( 0, +1)   SE = (+1,  0)   SW = ( 0, -1)   NW = (-1,  0)
    /// </code>
    /// <para>
    /// An offset that is not axis-aligned (a jump landing diagonally, for instance) is
    /// resolved by its dominant axis, ties going to the X axis — so <c>(1,1)</c> is
    /// <see cref="IsoDir.SE"/>. The rule is total and deterministic: there is no
    /// "no facing" answer a caller has to handle.
    /// </para>
    /// </remarks>
    public static class ArenaFacing
    {
        /// <summary>The unit step of a direction on the grid.</summary>
        public static GridCoord Delta(IsoDir direction)
        {
            switch (direction)
            {
                case IsoDir.NE:
                    return new GridCoord(0, 1);
                case IsoDir.SE:
                    return new GridCoord(1, 0);
                case IsoDir.SW:
                    return new GridCoord(0, -1);
                default:
                    return new GridCoord(-1, 0);
            }
        }

        /// <summary>The direction a grid offset points at, by dominant axis.</summary>
        /// <remarks>
        /// A zero offset has no direction; it answers <see cref="IsoDir.SE"/>, the arena's
        /// default facing, so <see cref="Toward(GridCoord, GridCoord)"/> of a tile onto
        /// itself is still defined.
        /// </remarks>
        public static IsoDir FromDelta(int dx, int dy)
        {
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                return dx < 0 ? IsoDir.NW : IsoDir.SE;
            }

            return dy < 0 ? IsoDir.SW : IsoDir.NE;
        }

        /// <summary>The direction a fighter turns to face <paramref name="target"/> from <paramref name="from"/>.</summary>
        public static IsoDir Toward(GridCoord from, GridCoord target) =>
            FromDelta(target.X - from.X, target.Y - from.Y);

        /// <summary>
        /// The facing after one walk step: the direction of the step, which is what the
        /// presentation animates and the rules use for the next attack.
        /// </summary>
        public static IsoDir AfterStep(GridCoord from, GridCoord to) => Toward(from, to);

        /// <summary>The opposite direction.</summary>
        public static IsoDir Opposite(IsoDir direction)
        {
            switch (direction)
            {
                case IsoDir.NE:
                    return IsoDir.SW;
                case IsoDir.SE:
                    return IsoDir.NW;
                case IsoDir.SW:
                    return IsoDir.NE;
                default:
                    return IsoDir.SE;
            }
        }

        /// <summary>
        /// Where <paramref name="attacker"/>'s hit lands on a defender standing on
        /// <paramref name="defenderTile"/> and facing <paramref name="facing"/>.
        /// </summary>
        /// <remarks>
        /// Compared as quarter turns: the direction from the defender to the attacker is the
        /// defender's facing (front), its opposite (back) or one of the two perpendiculars
        /// (flank). The attacker's own facing is irrelevant to the defender's back.
        /// </remarks>
        public static RelativeAngle Relative(GridCoord defenderTile, IsoDir facing, GridCoord attacker)
        {
            IsoDir approach = Toward(defenderTile, attacker);
            if (approach == facing)
            {
                return RelativeAngle.Front;
            }

            return approach == Opposite(facing) ? RelativeAngle.Back : RelativeAngle.Flank;
        }
    }
}
