using System;
using System.Collections.Generic;

namespace ProjectJianghu.Core.Domain.Arena
{
    /// <summary>The result from the player's point of view.</summary>
    public enum ArenaOutcome : byte
    {
        /// <summary>The player's side won.</summary>
        Victory,

        /// <summary>The player's side lost.</summary>
        Defeat,

        /// <summary>Neither side won.</summary>
        Draw,

        /// <summary>The player gave up; the match is not a normal win or loss.</summary>
        Aborted,
    }

    /// <summary>Why the match ended.</summary>
    public enum ArenaEndReason : byte
    {
        /// <summary>The match has not ended.</summary>
        None,

        /// <summary>Every fighter on the opposing side is defeated.</summary>
        AllEnemiesDefeated,

        /// <summary>Every fighter on the player's side is defeated.</summary>
        AllAlliesDefeated,

        /// <summary>The time limit expired and HP percentage decided it.</summary>
        TimeoutHpRatio,

        /// <summary>The time limit expired with equal HP percentages.</summary>
        TimeoutDraw,

        /// <summary>The player surrendered.</summary>
        Surrendered,

        /// <summary>A development command ended the match.</summary>
        DevForced,
    }

    /// <summary>How often one skill was used, and how often it connected.</summary>
    /// <remarks>
    /// Consumed by <c>StatGrowth</c> ("the more you use it, the better you get"), which
    /// is why it is part of the match result and not a presentation statistic.
    /// </remarks>
    public readonly struct SkillUseStat
    {
        /// <summary>The data row's skill id.</summary>
        public readonly int SkillId;

        /// <summary>How many times the skill was cast.</summary>
        public readonly int Uses;

        /// <summary>How many of those casts connected.</summary>
        public readonly int Hits;

        /// <summary>Creates one entry.</summary>
        public SkillUseStat(int skillId, int uses, int hits)
        {
            SkillId = skillId;
            Uses = uses;
            Hits = hits;
        }
    }

    /// <summary>What one fighter did and took in the match.</summary>
    public sealed class FighterMatchStat
    {
        /// <summary>Fighter id.</summary>
        public byte Id;

        /// <summary>HP when the match ended.</summary>
        public int FinalHp;

        /// <summary>Total damage dealt.</summary>
        public int DamageDealt;

        /// <summary>Total damage taken.</summary>
        public int DamageTaken;

        /// <summary>Attacks that missed this fighter.</summary>
        public int Evades;

        /// <summary>Counter-attacks this fighter landed.</summary>
        public int Counters;

        /// <summary>Per-skill use counts, for mastery growth.</summary>
        public IReadOnlyList<SkillUseStat> SkillUses = Array.Empty<SkillUseStat>();
    }

    /// <summary>The finished match, as consumed by the tournament, the result screen and save.</summary>
    public sealed class ArenaMatchResult
    {
        /// <summary>Outcome from the player's side.</summary>
        public ArenaOutcome Outcome;

        /// <summary>Why it ended.</summary>
        public ArenaEndReason Reason;

        /// <summary>The winning team, or <see cref="ArenaConstants.NoFighter"/> for a draw.</summary>
        public byte WinnerTeam;

        /// <summary>Length of the match in ticks (intro excluded).</summary>
        public int DurationTicks;

        /// <summary>One row per fighter, in setup order.</summary>
        public IReadOnlyList<FighterMatchStat> Fighters = Array.Empty<FighterMatchStat>();
    }
}
