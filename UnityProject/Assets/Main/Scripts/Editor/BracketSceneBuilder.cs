using ProjectJianghu.Core.Application.Flow;
using UnityEditor;

namespace ProjectJianghu.Game.Editor
{
    /// <summary>
    /// Builds the Bracket scene: the placeholder where the 32-entrant tournament tree will be shown.
    /// </summary>
    /// <remarks>
    /// The bracket's data (<c>TournamentState</c>, seeded pairing, background matches) is Stage 10's;
    /// Stage 03 only needs the screen to exist between character creation and the rest house, so the
    /// loop's shape is real before the rules are.
    /// </remarks>
    public static class BracketSceneBuilder
    {
        /// <summary>The page global this screen's report is published on.</summary>
        public const string ReportGlobal = "__projectJianghuBracket";

        [MenuItem("ProjectJianghu/Scenes/6. Create Bracket scene", priority = 106)]
        public static void CreateFromMenu()
        {
            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);

            Create();
            ProjectSetup.ConfigureBuildSettings();
            ProjectSetup.RefreshUrlSceneAllowList();

            ProjectSetup.SaveAndRefresh(
                "ProjectJianghu: Bracket scene created and added to build settings.");
        }

        public static void Create()
        {
            StepSceneTemplate.Build(
                "Bracket",
                titleKey: "bracket.title",
                lineKey: "bracket.line",
                target: SceneId.Rest,
                scene: SceneId.Bracket,
                reportGlobal: ReportGlobal,
                scenePath: ProjectSetup.BracketScenePath);
        }
    }
}
