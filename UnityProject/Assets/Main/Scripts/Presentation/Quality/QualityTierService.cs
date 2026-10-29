using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectJianghu.Game.Presentation.Quality
{
    /// <summary>The three quality levels the game ships, low to high.</summary>
    public enum QualityTier
    {
        /// <summary>Weakest GPUs and the smallest WebGL player: one URP asset with a reduced render scale.</summary>
        Low = 0,

        /// <summary>The default: full resolution, 2× MSAA.</summary>
        Medium = 1,

        /// <summary>Desktop-grade: 4× MSAA, the longest shadow distance.</summary>
        High = 2,
    }

    /// <summary>
    /// The one place the game changes quality: three named levels, each with its own URP asset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Three levels, not six.</b> Unity creates six quality levels; the game names the first three
    /// and leaves the rest out of the build (the level count is authored by
    /// <c>ProjectSetup.ConfigureQualityTiers</c>). A tier is a <i>render pipeline asset</i> — render
    /// scale, MSAA, shadow distance — so switching a tier changes what the GPU is asked to do, which
    /// is what makes an FPS measurement per tier meaningful.
    /// </para>
    /// <para>
    /// The tier is not persisted in Stage 03: saving settings is Stage 13's job (a
    /// <c>SettingsData</c> field), and a selector that invented its own storage would have to be
    /// migrated into that format later. The selection lives for the session.
    /// </para>
    /// </remarks>
    public sealed class QualityTierService
    {
        /// <summary>How many tiers the game ships.</summary>
        public const int TierCount = 3;

        /// <summary>The name of each tier, in index order — the same names the quality levels carry.</summary>
        public static readonly string[] Names = { "Low", "Medium", "High" };

        /// <summary>The tier the player is on.</summary>
        public QualityTier Current
        {
            get
            {
                int level = QualitySettings.GetQualityLevel();
                return (QualityTier)Mathf.Clamp(level, 0, TierCount - 1);
            }
        }

        /// <summary>The tier's own name, as the quality level spells it.</summary>
        public string CurrentName => Names[(int)Current];

        /// <summary>True when the current level resolves to a render pipeline asset (a tier without one renders with the built-in pipeline).</summary>
        public bool CurrentHasPipeline => QualitySettings.renderPipeline is not null;

        /// <summary>Applies a tier for the rest of the session.</summary>
        /// <returns>False when the level does not exist in the build.</returns>
        public bool Select(QualityTier tier)
        {
            int level = (int)tier;
            if (level < 0 || level >= QualitySettings.names.Length)
            {
                Debug.LogError(
                    "QualityTierService: quality level " + level.ToString(CultureInfo.InvariantCulture) +
                    " does not exist in this build (" + QualitySettings.names.Length + " levels).");
                return false;
            }

            QualitySettings.SetQualityLevel(level, applyExpensiveChanges: true);
            return true;
        }

        /// <summary>Selects by index, for a button that was authored as one of three.</summary>
        public bool Select(int tierIndex) => Select((QualityTier)Mathf.Clamp(tierIndex, 0, TierCount - 1));

        /// <summary>
        /// The pipeline asset a tier renders with, or null. Public so a test can prove each tier has
        /// one without switching the level the editor is on.
        /// </summary>
        public static RenderPipelineAsset PipelineFor(QualityTier tier)
        {
            int previous = QualitySettings.GetQualityLevel();
            try
            {
                int level = Mathf.Clamp((int)tier, 0, QualitySettings.names.Length - 1);
                QualitySettings.SetQualityLevel(level, false);
                return QualitySettings.renderPipeline;
            }
            finally
            {
                QualitySettings.SetQualityLevel(previous, false);
            }
        }
    }
}
