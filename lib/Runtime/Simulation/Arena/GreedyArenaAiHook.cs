using System;
using ProjectJianghu.Core.Domain.Arena;
using ProjectJianghu.Core.Domain.Character;
using ProjectJianghu.Core.Domain.Config;
using ProjectJianghu.Core.Shared;

namespace ProjectJianghu.Core.Simulation.Arena
{
    /// <summary>Chooses the highest-utility legal action using the fighter's public frame and arena queries.</summary>
    public sealed class GreedyArenaAiHook : IArenaAiHook
    {
        private readonly IGameConfig _config;
        private readonly GridCoord[] _tiles = new GridCoord[ArenaConstants.MaxGridTiles];
        private readonly GridCoord[] _area = new GridCoord[ArenaConstants.MaxGridTiles];

        public GreedyArenaAiHook(IGameConfig config) =>
            _config = config ?? throw new ArgumentNullException(nameof(config));

        public bool TryChoose(IArenaFrame frame, IArenaQuery query, IRandomSource random, byte fighterId, out ArenaCommand command)
        {
            command = ArenaCommand.Meditate(fighterId);
            if (frame is null || query is null || random is null)
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
            if (self.Hp <= 0 || self.MaxHp <= 0)
            {
                return false;
            }

            AiProfileConfig profile = FindProfile(fighters[selfIndex].AiProfileId);
            int nearestIndex = FindNearestEnemy(fighters, self, out int nearestDistance);
            int bestScore = int.MinValue;
            ArenaCommand best = ArenaCommand.Meditate(fighterId);
            int bestTieCount = 0;
            ReadOnlySpan<ushort> cooldowns = frame.SkillCooldowns(fighterId);

            // Telegraphs are frame state, not hidden simulation data. Jump only away if the
            // public preview confirms the landing tile escapes the currently aimed area.
            bool telegraphed = HasIncomingTelegraph(fighters, self, query);
            if (telegraphed && profile.JumpWeight > 0)
            {
                int count = query.GetJumpTiles(fighterId, _tiles);
                for (int i = 0; i < count; i++)
                {
                    GridCoord tile = _tiles[i];
                    if (IsInAnyTelegraph(fighters, self.Team, query, tile))
                    {
                        continue;
                    }

                    ArenaCommand candidate = ArenaCommand.Jump(fighterId, tile);
                    Consider(query, random, in candidate, profile.JumpWeight, ref bestScore, ref bestTieCount, ref best);
                }
            }

            int hurtRatio = (int)((long)self.Hp * 10000 / self.MaxHp);
            bool retreat = profile.RetreatHpX100 > 0 && hurtRatio <= profile.RetreatHpX100;

            int skillCount = cooldowns.Length;
            Span<GridCoord> rangeTiles = _tiles;
            for (int slot = 0; slot < skillCount; slot++)
            {
                if (cooldowns[slot] != 0)
                {
                    continue;
                }

                int rangeCount = query.GetSkillRangeTiles(fighterId, (byte)slot, rangeTiles);
                for (int tileIndex = 0; tileIndex < rangeCount; tileIndex++)
                {
                    GridCoord target = rangeTiles[tileIndex];
                    ArenaCommand candidate = ArenaCommand.Skill(fighterId, (byte)slot, target);
                    if (query.Validate(in candidate) != CommandResult.Ok)
                    {
                        continue;
                    }

                    int covered = query.PreviewSkillArea(fighterId, (byte)slot, target, _area);
                    int hits = 0;
                    int positionalBonus = 0;
                    for (int areaIndex = 0; areaIndex < covered; areaIndex++)
                    {
                        GridCoord hitTile = _area[areaIndex];
                        for (int enemyIndex = 0; enemyIndex < fighters.Length; enemyIndex++)
                        {
                            FighterView enemy = fighters[enemyIndex];
                            if (enemy.Team == self.Team || enemy.Hp <= 0 || enemy.Tile != hitTile)
                            {
                                continue;
                            }

                            hits++;
                            RelativeAngle angle = ArenaFacing.Relative(enemy.Tile, enemy.Facing, self.Tile);
                            positionalBonus += angle == RelativeAngle.Back ? profile.FlankWeight * 2
                                : angle == RelativeAngle.Flank ? profile.FlankWeight : 0;
                        }
                    }

                    int score = profile.SkillWeight + positionalBonus;
                    if (profile.Archetype == EAiArchetype.InnerTank && self.Neili < self.MaxNeili / 2)
                    {
                        score -= profile.MeditateWeight;
                    }

                    if (retreat)
                    {
                        score = score * profile.AggressionX100 / 10000;
                    }

                    score = score * Math.Max(1, hits);
                    Consider(query, random, in candidate, score, ref bestScore, ref bestTieCount, ref best);
                }
            }

            if (nearestIndex >= 0)
            {
                int moveCount = query.GetReachableTiles(fighterId, _tiles);
                int bestDistance = int.MaxValue;
                GridCoord bestTile = GridCoord.Invalid;
                for (int i = 0; i < moveCount; i++)
                {
                    int distance = _tiles[i].Manhattan(fighters[nearestIndex].Tile);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestTile = _tiles[i];
                    }

                    // If kiting or hurt, retreat to the furthest reachable tile.
                    bool prefersRange = profile.Archetype == EAiArchetype.Kiter || profile.Archetype == EAiArchetype.Evader;
                    bool kites = profile.Archetype == EAiArchetype.Kiter;
                    bool wantsRange = kites || retreat;
                    bool needsRetreat = (retreat || kites) && profile.RetreatWeight > 0;
                    int desiredDistance = profile.EngageRange;
                    int currentRangeError = Math.Abs(nearestDistance - desiredDistance);
                    int rangeError = Math.Abs(distance - desiredDistance);
                    bool betterRange = wantsRange ? rangeError < currentRangeError
                        : prefersRange || retreat ? distance > nearestDistance : distance < nearestDistance;
                    if (needsRetreat && betterRange)
                    {
                        ArenaCommand candidate = ArenaCommand.Move(fighterId, _tiles[i]);
                        int improvement = wantsRange ? currentRangeError - rangeError : distance - nearestDistance;
                        int score = profile.MoveWeight + profile.RetreatWeight * improvement;
                        Consider(query, random, in candidate, score, ref bestScore, ref bestTieCount, ref best);
                    }
                }

                if (bestTile != GridCoord.Invalid && bestDistance < nearestDistance
                    && bestDistance > profile.EngageRange)
                {
                    ArenaCommand candidate = ArenaCommand.Move(fighterId, bestTile);
                    int improvement = nearestDistance - bestDistance;
                    int score = profile.MoveWeight + profile.AggressionX100 * improvement;

                    Consider(query, random, in candidate, score, ref bestScore, ref bestTieCount, ref best);
                }

                // Seek a flank/back tile when straight-line positioning has no better move.
                int flankCount = query.GetReachableTiles(fighterId, _tiles);
                for (int i = 0; i < flankCount; i++)
                {
                    GridCoord tile = _tiles[i];
                    int distance = tile.Manhattan(fighters[nearestIndex].Tile);
                    if (distance > profile.EngageRange || distance > nearestDistance
                        || FlankValue(tile, fighters[nearestIndex]) <= FlankValue(self.Tile, fighters[nearestIndex]))
                    {
                        continue;
                    }

                    ArenaCommand candidate = ArenaCommand.Move(fighterId, tile);
                    int score = profile.MoveWeight + profile.FlankWeight;
                    Consider(query, random, in candidate, score, ref bestScore, ref bestTieCount, ref best);
                }

                // Ground targeting is legal on empty tiles as well; include it in decisions.
                for (int slot = 0; slot < skillCount; slot++)
                {
                    if (cooldowns[slot] != 0)
                    {
                        continue;
                    }

                    int rangeCount = query.GetSkillRangeTiles(fighterId, (byte)slot, rangeTiles);
                    for (int i = 0; i < rangeCount; i++)
                    {
                        GridCoord target = rangeTiles[i];
                        if (HasEnemyAt(fighters, self.Team, target))
                        {
                            continue;
                        }

                        ArenaCommand candidate = ArenaCommand.Skill(fighterId, (byte)slot, target);
                        if (query.Validate(in candidate) == CommandResult.Ok)
                        {
                            Consider(query, random, in candidate, profile.SkillWeight / 4, ref bestScore, ref bestTieCount, ref best);
                        }
                    }
                }
            }

            ArenaCommand meditate = ArenaCommand.Meditate(fighterId);
            int meditateScore = profile.MeditateWeight;
            if (self.Neili < self.MaxNeili / 2)
            {
                meditateScore += profile.MeditateWeight;
            }

            Consider(query, random, in meditate, meditateScore, ref bestScore, ref bestTieCount, ref best);
            command = best;
            return query.Validate(in command) == CommandResult.Ok;
        }

        private AiProfileConfig FindProfile(int profileId)
        {
            for (int i = 0; i < _config.AiProfiles.Count; i++)
            {
                AiProfileConfig profile = _config.AiProfiles[i];
                if (StatGrowth.StableId(profile.Id) == profileId)
                {
                    return profile;
                }
            }

            return _config.AiProfiles.Count > 0 ? _config.AiProfiles[0] : new AiProfileConfig();
        }

        private static int FindNearestEnemy(ReadOnlySpan<FighterView> fighters, FighterView self, out int distance)
        {
            int nearest = -1;
            distance = int.MaxValue;
            for (int i = 0; i < fighters.Length; i++)
            {
                FighterView other = fighters[i];
                if (other.Team == self.Team || other.Hp <= 0)
                {
                    continue;
                }

                int current = self.Tile.Manhattan(other.Tile);
                if (current < distance)
                {
                    nearest = i;
                    distance = current;
                }
            }

            return nearest;
        }

        private bool HasIncomingTelegraph(ReadOnlySpan<FighterView> fighters, FighterView self, IArenaQuery query)
        {
            for (int i = 0; i < fighters.Length; i++)
            {
                FighterView enemy = fighters[i];
                if (enemy.Team == self.Team || enemy.Hp <= 0 || enemy.State != FighterState.WindUp)
                {
                    continue;
                }

                if (enemy.ActiveSkillId == 0)
                {
                    continue;
                }

                for (int slot = 0; slot < ArenaConstants.MaxSkillSlots; slot++)
                {
                    if (enemy.ActiveSkillSlot != slot)
                    {
                        continue;
                    }

                    int count = query.PreviewSkillArea(enemy.Id, (byte)slot, enemy.ActionTarget, _area);
                    for (int tile = 0; tile < count; tile++)
                    {
                        if (_area[tile] == self.Tile)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool IsInAnyTelegraph(ReadOnlySpan<FighterView> fighters, byte team, IArenaQuery query, GridCoord tile)
        {
            for (int i = 0; i < fighters.Length; i++)
            {
                FighterView enemy = fighters[i];
                if (enemy.Team == team || enemy.Hp <= 0 || enemy.State != FighterState.WindUp || enemy.ActiveSkillId == 0)
                {
                    continue;
                }

                for (int slot = 0; slot < ArenaConstants.MaxSkillSlots; slot++)
                {
                    if (enemy.ActiveSkillSlot != slot)
                    {
                        continue;
                    }

                    int count = query.PreviewSkillArea(enemy.Id, (byte)slot, enemy.ActionTarget, _area);
                    for (int areaTile = 0; areaTile < count; areaTile++)
                    {
                        if (_area[areaTile] == tile)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static int FlankValue(GridCoord attacker, FighterView defender)
        {
            RelativeAngle angle = ArenaFacing.Relative(defender.Tile, defender.Facing, attacker);
            return angle == RelativeAngle.Back ? 2 : angle == RelativeAngle.Flank ? 1 : 0;
        }

        private static bool HasEnemyAt(ReadOnlySpan<FighterView> fighters, byte team, GridCoord tile)
        {
            for (int i = 0; i < fighters.Length; i++)
            {
                if (fighters[i].Team != team && fighters[i].Hp > 0 && fighters[i].Tile == tile)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Consider(IArenaQuery query, IRandomSource random, in ArenaCommand candidate, int score,
            ref int bestScore, ref int tieCount, ref ArenaCommand best)
        {
            if (score < bestScore || query.Validate(in candidate) != CommandResult.Ok)
            {
                return;
            }

            if (score > bestScore)
            {
                bestScore = score;
                tieCount = 1;
                best = candidate;
                return;
            }

            tieCount++;
            if (random.NextInt(0, tieCount) == 0)
            {
                best = candidate;
            }
        }
    }
}
