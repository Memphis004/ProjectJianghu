using System;
using ProjectJianghu.Core.Domain.Arena;

namespace ProjectJianghu.Core.Simulation.Arena
{
    /// <summary>
    /// The real simulation's <see cref="IArenaQuery"/>: the same Stage 06 geometry the
    /// session validates and resolves with, over the session's own occupants.
    /// </summary>
    /// <remarks>
    /// Thin on purpose — every answer comes from <see cref="ArenaSimulation"/>, so a
    /// highlight, a preview and the command that follows cannot disagree. Nothing here
    /// changes match state and nothing allocates.
    /// </remarks>
    public sealed class ArenaSimulationQuery : IArenaQuery
    {
        private readonly ArenaSimulation _session;
        private readonly bool _enforcePlayerControl;

        /// <param name="session">The match to answer for.</param>
        /// <param name="enforcePlayerControl">
        /// True for the public query, whose answers must match <see cref="IArenaSession.Submit"/>.
        /// False for the AI's own copy: an AI fighter's command would be
        /// <see cref="CommandResult.RejectedNotControlled"/> from the public verb, so a hook
        /// probing its own play needs the same validation with that one check skipped.
        /// </param>
        internal ArenaSimulationQuery(ArenaSimulation session, bool enforcePlayerControl)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _enforcePlayerControl = enforcePlayerControl;
        }

        /// <inheritdoc />
        public int Width => _session.Width;

        /// <inheritdoc />
        public int Height => _session.Height;

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
        public CommandResult Validate(in ArenaCommand command) =>
            _session.ValidateForController(in command, _enforcePlayerControl);

        /// <inheritdoc />
        public bool IsWalkable(GridCoord tile) => _session.IsWalkableTile(tile);

    }
}
