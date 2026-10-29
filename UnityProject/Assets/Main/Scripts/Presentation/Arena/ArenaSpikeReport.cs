using ProjectJianghu.Game.Presentation.Isometric;
using UnityEngine.Rendering.Universal;
using ProjectJianghu.Game.Presentation.Quality;
using ProjectJianghu.Game.Presentation.Web;
using UnityEngine;

namespace ProjectJianghu.Game.Presentation.Arena
{
    /// <summary>
    /// Publishes what the HD-2D spike is doing: the quality tier it is rendering at, the frame rate it
    /// measured, and the sorting probe's answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two measurements in the Stage 03 report that cannot be taken any other way come from here:
    /// FPS per quality tier (only a real player, in a real browser, at the real tier, produces it) and
    /// the sprite-vs-prop sorting answer (only a rendered frame has it). Both are published as a page
    /// global so a browser script can read the player's own numbers instead of timing the page around
    /// it.
    /// </para>
    /// <para>
    /// A <c>sample</c> object is replaced every window; the <c>sorting</c> object is written once, when
    /// the probe has finished. A reader that needs a settled number waits for
    /// <c>sample.frames &gt;= 60</c> instead of assuming a fixed delay.
    /// </para>
    /// </remarks>
    public static class ArenaSpikeReport
    {
        /// <summary>Page global carrying the spike's measurements.</summary>
        public const string Global = "__projectJianghuArenaSpike";

        /// <summary>The most recent document, in-process. Tests read this.</summary>
        public static string LastJson { get; private set; } = string.Empty;

        /// <summary>How many documents have been published.</summary>
        public static int PublishCount { get; private set; }

        /// <summary>Clears the mirror, so a test starts from a known empty state.</summary>
        public static void Reset()
        {
            LastJson = string.Empty;
            PublishCount = 0;
        }

        /// <summary>Writes and publishes the document. Returns the JSON it published.</summary>
        public static string Publish(
            string sceneName,
            QualityTier tier,
            string tierLabel,
            FrameRateSample sample,
            SortingProbeReport sorting,
            float snapMaxOffsetPixels,
            float zoom,
            float orthographicSize,
            int spriteCount,
            int propCount,
            ArenaSpikeDirector director)
        {
            var report = new JsonReportWriter();
            report.BeginObject();

            report.Write("scene", sceneName ?? string.Empty);

            report.BeginObjectProperty("quality");
            report.Write("tier", tierLabel ?? QualityTierService.Names[(int)tier]);
            report.Write("level", QualitySettings.GetQualityLevel());
            report.Write("pipelineAssigned", QualitySettings.renderPipeline is not null);
            UniversalRenderPipelineAsset pipeline =
                QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            report.Write("renderScale", pipeline is null ? 0f : pipeline.renderScale);
            report.Write("msaa", QualitySettings.antiAliasing);
            report.BeginArrayProperty("tiers");
            for (int i = 0; i < QualityTierService.Names.Length; i++)
            {
                report.WriteArrayString(QualityTierService.Names[i]);
            }

            report.EndArray();
            report.EndObject();

            report.BeginObjectProperty("sample");
            report.Write("frames", sample.Frames);
            report.Write("seconds", sample.Seconds);
            report.Write("fps", sample.AverageFps);
            report.Write("onePercentLowFps", sample.OnePercentLowFps);
            report.Write("worstMillis", sample.WorstMillis);
            report.Write("canvasWidth", Screen.width);
            report.Write("canvasHeight", Screen.height);
            report.EndObject();

            report.BeginObjectProperty("sorting");
            report.Write("measured", sorting is not null);
            if (sorting is not null)
            {
                report.Write("spriteInFrontDrawsOverProp", sorting.SpriteInFrontDrawsOverProp);
                report.Write("propOccludesSpriteBehind", sorting.PropOccludesSpriteBehind);
                report.Write("nearerSpriteDrawsFirst", sorting.NearerSpriteDrawsFirst);
                report.Write("authoredOrderDecides", sorting.AuthoredOrderDecides);
                report.Write("pixelsPerUnit", sorting.MeasuredPixelsPerUnit);
                report.Write("projectedPixelsPerUnit", sorting.ProjectedPixelsPerUnit);
                report.Write("nearSortingOrder", sorting.NearSortingOrder);
                report.Write("farSortingOrder", sorting.FarSortingOrder);
                report.Write("passed", sorting.Passed);
            }

            report.EndObject();

            report.BeginObjectProperty("camera");
            report.Write("orthographic", true);
            report.Write("yaw", IsometricLayout.YawDegrees);
            report.Write("pitch", IsometricLayout.PitchDegrees);
            report.Write("pixelsPerUnit", IsometricLayout.PixelsPerUnit);
            report.Write("zoom", zoom);
            report.Write("orthographicSize", orthographicSize);
            report.EndObject();

            report.BeginObjectProperty("pixelSnap");
            report.Write("pixelsPerUnit", IsometricLayout.PixelsPerUnit);
            report.Write("maxOffsetPixels", snapMaxOffsetPixels);
            report.EndObject();

            report.Write("spriteCount", spriteCount);
            report.Write("propCount", propCount);

            // The three tier buttons, so a browser probe presses the shipped selector instead of
            // calling into the game: the point is the pointer path, and a measurement taken by
            // invoking the game's own method would not say the buttons work.
            report.BeginArrayProperty("buttons");
            for (int i = 0; i < QualityTierService.TierCount; i++)
            {
                if (director is null || !director.TryGetTierScreenPoint(i, out Vector2 screen))
                {
                    continue;
                }

                report.BeginObject();
                report.Write("name", QualityTierService.Names[i].ToLowerInvariant());
                report.Write("x", screen.x);
                report.Write("y", screen.y);
                report.EndObject();
            }

            report.EndArray();

            report.EndObject();

            string json = report.ToString();
            LastJson = json;
            PublishCount++;
            WebGlBridge.Publish(Global, json);
            return json;
        }
    }
}
