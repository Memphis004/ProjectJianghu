using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectJianghu.Game.Tests.EditMode
{
    /// <summary>
    /// Every authored scene and prefab is intact: no component is a missing script, and no
    /// serialized reference a shell component declares was left unassigned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shell is assembled by Editor code (<c>ProjectSetup</c> and the six scene builders),
    /// so the failure mode this guards against is not a typo in YAML but a builder that stops
    /// wiring a field after a rename, a script deleted while a scene still names it, or a scene
    /// re-saved by an older checkout. None of those raise a compile error: the scene loads, the
    /// view's <c>_titleLabel</c> is null, and the failure surfaces as a blank screen in a browser.
    /// </para>
    /// <para>
    /// <b>Two different checks.</b> A missing script is a component slot Unity cannot resolve —
    /// <c>GetComponentsInChildren&lt;Component&gt;</c> returns <c>null</c> for it, and that is
    /// checked on every object of every scene and prefab, including the imported TextMesh Pro
    /// examples. A missing <i>reference</i> cannot be detected generically — a null reference is
    /// also how "this field is optional" is written — so it is checked only on this project's own
    /// components (<c>ProjectJianghu.*</c> assemblies), every one of whose serialized object
    /// references is required. Unity and package components (TMP's <c>m_Material</c>, a
    /// <c>CanvasScaler</c>'s default sprite) are deliberately out of scope: a null there is a
    /// Unity default, not a wiring mistake.
    /// </para>
    /// <para>
    /// Scenes are opened <b>additively</b> and closed again, so the test does not save, dirty or
    /// replace whatever the developer has open. The PlayMode suite additionally loads each scene
    /// and runs the player loop; this test is the half that can run without one and names the
    /// offending object instead of a root GameObject.
    /// </para>
    /// </remarks>
    public sealed class ShellAssetIntegrityTests
    {
        private const string ShellAssetFolder = "Assets/Main";

        [Test]
        public void Every_scene_in_build_settings_has_no_missing_scripts_or_references()
        {
            string[] paths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            Assert.That(paths.Length, Is.GreaterThanOrEqualTo(3), "the build list must still hold Boot, Title and ThaiTextSpike");

            var problems = new List<string>();
            foreach (string path in paths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        CollectProblems(root, path, problems);
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }

            Assert.That(problems, Is.Empty, "broken scene objects:\n" + string.Join("\n", problems));
        }

        [Test]
        public void Every_prefab_has_no_missing_scripts_or_references()
        {
            string[] paths = AssetDatabase
                .FindAssets("t:Prefab", new[] { ShellAssetFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path)
                .ToArray();

            // The template ships no prefabs of its own; the test still walks the folder so a game
            // that adds one gets the check without editing this file.
            TestContext.WriteLine("{0} prefab(s) under {1}", paths.Length, ShellAssetFolder);

            var problems = new List<string>();
            foreach (string path in paths)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    CollectProblems(contents, path, problems);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            Assert.That(problems, Is.Empty, "broken prefab objects:\n" + string.Join("\n", problems));
        }

        private static void CollectProblems(GameObject root, string assetPath, List<string> problems)
        {
            Component[] components = root.GetComponentsInChildren<Component>(includeInactive: true);
            foreach (Component component in components)
            {
                if (component is null)
                {
                    problems.Add(assetPath + ": missing script on an object under '" + root.name + "'");
                    continue;
                }

                if (component is MonoBehaviour behaviour && IsShellComponent(behaviour))
                {
                    CollectUnassignedReferences(behaviour, assetPath, problems);
                }
            }
        }

        /// <summary>True for a component this project authored, false for a package or engine one.</summary>
        private static bool IsShellComponent(MonoBehaviour behaviour)
        {
            string assembly = behaviour.GetType().Assembly.GetName().Name ?? string.Empty;
            return assembly.StartsWith("ProjectJianghu.", System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Reports every serialized object-reference field on <paramref name="behaviour"/> whose
        /// value is unassigned, naming the object and the field path.
        /// </summary>
        private static void CollectUnassignedReferences(MonoBehaviour behaviour, string assetPath, List<string> problems)
        {
            var serialized = new SerializedObject(behaviour);
            SerializedProperty property = serialized.GetIterator();

            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = property.propertyType == SerializedPropertyType.Generic;

                if (property.name == "m_Script" || property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                if (property.objectReferenceValue is null)
                {
                    problems.Add(string.Format(
                        "{0}: {1}.{2} ({3}) is not assigned on '{4}'",
                        assetPath,
                        behaviour.GetType().Name,
                        property.propertyPath,
                        property.type,
                        behaviour.gameObject.name));
                }
            }
        }
    }
}
