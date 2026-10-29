using ProjectJianghu.Core.Application.Flow;
using ProjectJianghu.Game.Presentation.Steps;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace ProjectJianghu.Game.Editor
{
    /// <summary>
    /// The one scene shape both placeholder screens share: a heading, one line, a continue control and
    /// the page report, wired to <see cref="StepSceneScope"/>.
    /// </summary>
    /// <remarks>
    /// Two scenes (CharacterCreate, Bracket) are the same placeholder in Stage 03, so they are built
    /// from one template rather than two copies of the same 90 lines. Each still has its own builder
    /// file with its own menu item, its own scene file and its own page global, so replacing one with
    /// the real screen (Stage 11, Stage 10) leaves the other alone.
    /// </remarks>
    public static class StepSceneTemplate
    {
        /// <summary>Builds and saves one step scene.</summary>
        public static void Build(
            string sceneName,
            string titleKey,
            string lineKey,
            SceneId target,
            SceneId scene,
            string reportGlobal,
            string scenePath)
        {
            Scene unityScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (TMP_FontAsset font, _) = ProjectSetup.EnsureFontAssets();

            var scopeObject = new GameObject(sceneName);
            StepSceneScope scope = scopeObject.AddComponent<StepSceneScope>();
            StepSceneDirector director = scopeObject.AddComponent<StepSceneDirector>();

            ProjectSetup.SetString(scope, "_titleKey", titleKey);
            ProjectSetup.SetString(scope, "_lineKey", lineKey);
            ProjectSetup.SetString(scope, "_continueKey", ProjectSetup.GenericContinueKey);
            ProjectSetup.SetEnum(scope, "_target", (int)target);
            ProjectSetup.SetEnum(director, "_scene", (int)scene);

            ProjectSetup.CreateCamera();
            ProjectSetup.CreateEventSystem();

            Canvas canvas = ProjectSetup.CreateCanvas(out GameObject canvasObject);
            var canvasRect = (RectTransform)canvas.transform;

            RectTransform column = ProjectSetup.CreateUiRect(
                canvasRect, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1200f, 700f));

            var background = column.gameObject.AddComponent<Image>();
            background.color = new Color(0.07f, 0.09f, 0.12f, 0.92f);
            background.raycastTarget = false;

            TMP_Text title = ProjectSetup.CreateUiText(
                column, "Title", new Vector2(0f, -60f), new Vector2(1200f, 140f), 88f, font,
                TextAlignmentOptions.Center);

            TMP_Text line = ProjectSetup.CreateUiText(
                column, "Line", new Vector2(0f, -240f), new Vector2(1120f, 120f), 30f, font,
                TextAlignmentOptions.Center);

            Button continueButton = ProjectSetup.CreateUiButton(
                column, "ContinueButton", new Vector2(400f, -380f), new Vector2(400f, 76f), font,
                out TMP_Text continueLabel);

            StepView view = canvasObject.AddComponent<StepView>();
            ProjectSetup.SetObjectReference(view, "_scope", scope);
            ProjectSetup.SetObjectReference(view, "_titleLabel", title);
            ProjectSetup.SetObjectReference(view, "_lineLabel", line);
            ProjectSetup.SetObjectReference(view, "_continueButton", continueButton);
            ProjectSetup.SetObjectReference(view, "_continueLabel", continueLabel);
            ProjectSetup.SetString(view, "_reportGlobal", reportGlobal);

            ProjectSetup.SetObjectReference(director, "_scope", scope);

            EditorSceneManager.SaveScene(unityScene, scenePath);
        }
    }
}
