using System;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Shared;

namespace ProjectJianghu.Core.Simulation.Arena
{
    /// <summary>The decision point for an AI-controlled fighter.</summary>
    /// <remarks>
    /// <para>
    /// A hook is asked once per ready fighter per tick, with the tick's live state (the
    /// <paramref name="frame"/> is written fresh for this decision, not the published
    /// frame of the previous tick) and a query that answers the same questions the player's
    /// UI asks. It must not call back into the session: the simulation applies the command
    /// itself, after validating it exactly as it would validate a player's.
    /// </para>
    /// <para>
    /// Returning false (or a command that fails validation) does not stall the match: the
    /// simulation falls back to <see cref="ArenaCommandKind.Meditate"/>. Implementation
    /// note: an interface call per fighter per tick allocates nothing, so this seam costs
    /// no garbage in the hot path (AGENTS rule 7).
    /// </para>
    /// </remarks>
    public interface IArenaAiHook
    {
        /// <summary>
        /// Chooses one command for <paramref name="fighterId"/>, or returns false when the
        /// hook has nothing to suggest.
        /// </summary>
        /// <param name="frame">This tick's live state, valid for this call only.</param>
        /// <param name="query">Reachability, previews and validation for the live state.</param>
        /// <param name="random">The encounter random stream used to break equal-utility ties.</param>
        /// <param name="fighterId">The AI fighter that is ready.</param>
        /// <param name="command">The command to try.</param>
        bool TryChoose(IArenaFrame frame, IArenaQuery query, IRandomSource random, byte fighterId, out ArenaCommand command);
    }
}
