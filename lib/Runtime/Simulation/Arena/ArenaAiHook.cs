using System;
using ProjectJianghu.Core.Domain.Arena;

namespace ProjectJianghu.Core.Simulation.Arena
{
    /// <summary>
    /// The decision point for an AI-controlled fighter — the seam Stage 08 fills with the
    /// real utility AI.
    /// </summary>
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
        /// <param name="fighterId">The AI fighter that is ready.</param>
        /// <param name="command">The command to try.</param>
        bool TryChoose(IArenaFrame frame, IArenaQuery query, byte fighterId, out ArenaCommand command);
    }

    /// <summary>
    /// Stage 07's placeholder AI: cast the first skill that is legal at an enemy, else close
    /// the distance, else meditate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately dumb — it uses the same <see cref="IArenaQuery.Validate"/> a player's
    /// command goes through, so every command it produces is legal by construction, and it
    /// never scores anything. Stage 08 replaces it with utility scoring per
    /// <c>ai_profile</c>; the interface stays.
    /// </para>
    /// <para>
    /// Its tile buffer is allocated once: the hook runs inside the tick, where the
    /// simulation must not allocate.
    /// </para>
    /// </remarks>
    public sealed class GreedyArenaAiHook : IArenaAiHook
    {
        private const int MaxSkillSlots = ArenaConstants.MaxSkillSlots;

        private readonly GridCoord[] _tiles = new GridCoord[ArenaConstants.MaxGridTiles];

        /// <inheritdoc />
        public bool TryChoose(IArenaFrame frame, IArenaQuery query, byte fighterId, out ArenaCommand command)
        {
            command = ArenaCommand.Meditate(fighterId);

            if (frame is null || query is null)
            {
                return false;
            }

            ReadOnlySpan<FighterView> fighters = frame.Fighters;
            int selfIndex = -1;
            for (int i = 0; i < fighters.Length; i++)
            {
                if (fighters[i].Id == fighterId)
                {
                    selfIndex = i;
                    break;
                }
            }

            if (selfIndex < 0)
            {
                return false;
            }

            FighterView self = fighters[selfIndex];
            if (self.Hp <= 0)
            {
                return false;
            }

            // Every legal way to attack something, in a fixed order, is just "ask the query".
            for (int slot = 0; slot < MaxSkillSlots; slot++)
            {
                for (int i = 0; i < fighters.Length; i++)
                {
                    FighterView other = fighters[i];
                    if (other.Team == self.Team || other.Hp <= 0)
                    {
                        continue;
                    }

                    ArenaCommand skill = ArenaCommand.Skill(fighterId, (byte)slot, other.Tile);
                    if (query.Validate(in skill) == CommandResult.Ok)
                    {
                        command = skill;
                        return true;
                    }
                }

                // Self- and ground-targeted skills (inner manuals, movement arts).
                ArenaCommand selfSkill = ArenaCommand.Skill(fighterId, (byte)slot, self.Tile);
                if (query.Validate(in selfSkill) == CommandResult.Ok)
                {
                    command = selfSkill;
                    return true;
                }
            }

            int nearest = -1;
            int nearestDistance = int.MaxValue;
            for (int i = 0; i < fighters.Length; i++)
            {
                FighterView other = fighters[i];
                if (other.Team == self.Team || other.Hp <= 0)
                {
                    continue;
                }

                int distance = self.Tile.Manhattan(other.Tile);
                if (distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = distance;
                }
            }

            if (nearest >= 0 && nearestDistance > 1)
            {
                int count = query.GetReachableTiles(fighterId, _tiles);
                int best = -1;
                int bestDistance = nearestDistance;
                for (int i = 0; i < count; i++)
                {
                    int distance = _tiles[i].Manhattan(fighters[nearest].Tile);
                    if (distance < bestDistance)
                    {
                        best = i;
                        bestDistance = distance;
                    }
                }

                if (best >= 0)
                {
                    command = ArenaCommand.Move(fighterId, _tiles[best]);
                    return true;
                }
            }

            return false;
        }
    }
}
