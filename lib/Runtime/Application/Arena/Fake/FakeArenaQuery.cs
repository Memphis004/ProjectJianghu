using System;
using ProjectJianghu.Core.Domain.Arena;

namespace ProjectJianghu.Core.Application.Arena.Fake
{
    /// <summary>
    /// The fake's <see cref="IArenaQuery"/>: an empty grid, and the Stage 06 geometry
    /// rules answering every geometric question — the same functions the session uses to
    /// validate and resolve, which is what makes a highlight trustworthy.
    /// </summary>
    /// <remarks>
    /// What is still fake is the match, not the geometry: no crit, evade, counter or
    /// knockback, and Move/Jump arrive in one tick. The map is an empty rectangle because
    /// the arena setup does not carry one; Stage 07 loads the row and builds the grid.
    /// </remarks>
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
