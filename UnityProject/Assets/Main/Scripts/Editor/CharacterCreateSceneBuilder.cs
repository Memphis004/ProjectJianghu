using ProjectJianghu.Core.Application.Flow;
using UnityEditor;

namespace ProjectJianghu.Game.Editor
{
    /// <summary>
    /// Builds the CharacterCreate scene: the placeholder where a run's character will be made.
    /// </summary>
    /// <remarks>
    /// Stage 03 only needs the screen to exist and to sit in the loop between the title and the
    /// bracket. The rules — background, attribute points, starting skill — are Stage 05 (Domain) and
    /// Stage 11 (UI); when they land, this builder is replaced by one that wires the real presenter
    /// and this file is deleted with its scene.
    /// </remarks>
    public static class CharacterCreateSceneBuilder
    {
        /// <summary>The page global this screen's report is published on.</summary>
        public const string ReportGlobal = "__projectJianghuCharacterCreate";

        [MenuItem("ProjectJianghu/Scenes/5. Create CharacterCreate scene", priority = 105)]
        public static void CreateFromMenu()
        {
            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);

            Create();
            ProjectSetup.ConfigureBuildSettings();
            ProjectSetup.RefreshUrlSceneAllowList();

            ProjectSetup.SaveAndRefresh(
                "ProjectJianghu: CharacterCreate scene created and added to build settings.");
        }

        public static void Create()
        {
            StepSceneTemplate.Build(
                "CharacterCreate",
                titleKey: "charactercreate.title",
                lineKey: "charactercreate.line",
                target: SceneId.Bracket,
                scene: SceneId.CharacterCreate,
                reportGlobal: ReportGlobal,
                scenePath: ProjectSetup.CharacterCreateScenePath);
        }
    }
}
