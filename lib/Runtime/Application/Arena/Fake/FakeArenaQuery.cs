using System;
using ProjectJianghu.Core.Domain.Arena;

namespace ProjectJianghu.Core.Application.Arena.Fake
{
    /// <summary>
    /// The fake's <see cref="IArenaQuery"/>: an empty grid, Manhattan movement and one
    /// tile per skill. The geometry rules of Stage 06 replace it in the simulation;
    /// keeping this version deliberately simple is what makes the contract suite a
    /// test of the contract rather than of the map.
    /// </summary>
    public sealed class FakeArenaQuery : IArenaQuery
    {
        private readonly FakeArenaSession _session;

        internal FakeArenaQuery(FakeArenaSession session)
        {
            _session = session;
        }

        /// <inheritdoc />
        public int Width => _session.GridWidth;

        /// <inheritdoc />
        public int Height => _session.GridHeight;

        /// <inheritdoc />
        public int GetReachableTiles(byte fighter, Span<GridCoord> destination) =>
            _session.WriteReachableTiles(fighter, destination);

        /// <inheritdoc />
        public int GetJumpTiles(byte fighter, Span<GridCoord> destination) =>
            _session.WriteJumpTiles(fighter, destination);

        /// <inheritdoc />
        public int GetSkillRangeTiles(byte fighter, byte slot, Span<GridCoord> destination) =>
            _session.WriteSkillRangeTiles(fighter, slot, destination);

        /// <inheritdoc />
        public int PreviewSkillArea(byte fighter, byte slot, GridCoord target, Span<GridCoord> destination) =>
            _session.WriteSkillArea(fighter, slot, target, destination);

        /// <inheritdoc />
        public CommandResult Validate(in ArenaCommand command) => _session.Validate(in command);

        /// <inheritdoc />
        public bool IsWalkable(GridCoord tile) => _session.IsWalkableTile(tile);
    }
}
