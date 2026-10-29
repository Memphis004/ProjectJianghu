using ProjectJianghu.Game.Presentation.Web;
using UnityEngine;

namespace ProjectJianghu.Game.Presentation.Steps
{
    /// <summary>
    /// Publishes what a placeholder step shows, and where its one control is, so a browser test can
    /// leave the screen the way a player does.
    /// </summary>
    /// <remarks>
    /// Same contract as the other screens' reports: strings from the presenter, the point from the
    /// view, and the game's own answer (the scene marker, the next screen's report) is what a spec
    /// asserts on. One report type serves two scenes because the document differs only in the global
    /// name and the scene name, both of which are passed in — the two scenes have different scopes
    /// and different views.
    /// </remarks>
    public static class StepReport
    {
        /// <summary>The name the report uses for the one control.</summary>
        public const string ContinueControl = "continue";

        /// <summary>The most recent report, in-process. The tests read this.</summary>
        public static string LastJson { get; private set; } = string.Empty;

        /// <summary>How many reports have been published.</summary>
        public static int PublishCount { get; private set; }

        /// <summary>Clears the mirror, so a test starts from a known empty state.</summary>
        public static void Reset()
        {
            LastJson = string.Empty;
            PublishCount = 0;
        }

        /// <summary>Writes the report and hands it to the page. Returns the JSON it published.</summary>
        /// <param name="state">The strings the screen is showing, as the presenter filled them.</param>
        /// <param name="view">The screen, asked where its control is.</param>
        /// <param name="sceneName">The scene on screen, so the report can be checked against the marker.</param>
        /// <param name="globalName">The page global this screen publishes on.</param>
        public static string Publish(
            StepUiState state, StepView view, string sceneName, string globalName)
        {
            var report = new JsonReportWriter();
            report.BeginObject();

            report.Write("scene", sceneName ?? string.Empty);
            report.Write("title", state.Title.Value);
            report.Write("line", state.Line.Value);
            report.Write("continueLabel", state.ContinueLabel.Value);

            report.Write("canvasWidth", Screen.width);
            report.Write("canvasHeight", Screen.height);

            report.BeginArrayProperty("buttons");
            if (view is not null && view.TryGetContinueScreenPoint(out Vector2 screen))
            {
                report.BeginObject();
                report.Write("name", ContinueControl);
                report.Write("x", screen.x);
                report.Write("y", screen.y);
                report.EndObject();
            }

            report.EndArray();
            report.EndObject();

            string json = report.ToString();
            LastJson = json;
            PublishCount++;
            WebGlBridge.Publish(globalName, json);
            return json;
        }
    }
}
