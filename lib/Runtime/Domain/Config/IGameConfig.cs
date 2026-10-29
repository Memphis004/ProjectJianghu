using System.Collections.Generic;

namespace ProjectJianghu.Core.Domain.Config
{
    /// <summary>
    /// The read-only configuration the rule engine runs on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This port is the <b>only</b> way Domain / Application / Simulation see
    /// balance data. It is declared in Domain and implemented in Infrastructure
    /// by an adapter over the Luban-generated tables
    /// (<c>ProjectJianghu.Config.Tables</c>), so the generated row types never
    /// leak into the rule engine (AGENTS rule 2, ADR 0006).
    /// </para>
    /// <para>
    /// Every list is exposed in data-file order and every id is the stable string
    /// authored in <c>data/</c>. There is deliberately no "default" fallback that
    /// invents values: a successful load came from real <c>.bytes</c> tables, and
    /// a failed load throws instead of returning a stub.
    /// </para>
    /// </remarks>
    public interface IGameConfig
    {
        /// <summary>
        /// SHA-256 over the generated table bytes, upper-case hex. Recorded in
        /// build metadata so a bug report can be tied to exact balance data.
        /// </summary>
        string ConfigHash { get; }

        /// <summary>The singleton row of tuned numbers.</summary>
        ConstantConfig Constant { get; }

        /// <summary>Every localization row, in data-file order.</summary>
        IReadOnlyList<LocalizationEntry> Localization { get; }

        /// <summary>Display metadata for each authored attribute.</summary>
        IReadOnlyList<AttributeConfig> Attributes { get; }

        /// <summary>Every derived combat stat and its formula.</summary>
        IReadOnlyList<StatFormulaConfig> StatFormulas { get; }

        /// <summary>Every external, inner and movement skill.</summary>
        IReadOnlyList<SkillConfig> Skills { get; }

        /// <summary>The global mastery ladder.</summary>
        IReadOnlyList<SkillLevelConfig> SkillLevels { get; }

        /// <summary>Every weapon, armour piece and accessory.</summary>
        IReadOnlyList<GearConfig> Gear { get; }

        /// <summary>Every abnormal state.</summary>
        IReadOnlyList<StatusConfig> Statuses { get; }

        /// <summary>Every tournament opponent.</summary>
        IReadOnlyList<FighterConfig> Fighters { get; }

        /// <summary>Every AI behaviour profile.</summary>
        IReadOnlyList<AiProfileConfig> AiProfiles { get; }

        /// <summary>Every arena layout.</summary>
        IReadOnlyList<ArenaMapConfig> ArenaMaps { get; }

        /// <summary>Every tournament configuration.</summary>
        IReadOnlyList<TournamentConfig> Tournaments { get; }

        /// <summary>Every weighted reward offer.</summary>
        IReadOnlyList<RewardPoolConfig> RewardPools { get; }

        /// <summary>Every character-creation background.</summary>
        IReadOnlyList<BackgroundConfig> Backgrounds { get; }

        /// <summary>Looks one localization row up by key.</summary>
        bool TryGetLocalization(string key, out LocalizationEntry value);

        /// <summary>
        /// Player-facing text for <paramref name="key"/>. Throws
        /// <see cref="System.Collections.Generic.KeyNotFoundException"/> when the key is missing: a
        /// missing localization key is a data bug, never something to paper over with the key itself
        /// (AGENTS rule 11).
        /// </summary>
        string GetText(string key, Language language);
    }
}
