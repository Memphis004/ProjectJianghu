using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectJianghu.Game.Editor
{
    /// <summary>
    /// The project's own WebGL template: where it lives, what it is called, and whether it is intact.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity's default desktop template pins the player canvas at 960×600, so a window resize never
    /// reaches the game (see <c>llmwiki/gotchas/webgl-template-canvas-960x600.md</c>). The project's
    /// template fills the viewport instead; ADR 0111 records the decision and the measurements.
    /// </para>
    /// <para>
    /// The folder name is the template name: <c>Assets/WebGLTemplates/Jianghu</c> is selected as
    /// <c>PROJECT:Jianghu</c>. This type exists so the Player Settings write, the EditMode test and
    /// the build check all read the same three strings instead of three copies of them.
    /// </para>
    /// </remarks>
    public static class WebGlTemplate
    {
        /// <summary>Folder every project template lives in.</summary>
        public const string Folder = "Assets/WebGLTemplates";

        /// <summary>This project's template name; also its folder name.</summary>
        public const string ProjectTemplateName = "Jianghu";

        /// <summary>Absolute project-relative path of the template's folder.</summary>
        public const string TemplateFolder = Folder + "/" + ProjectTemplateName;

        /// <summary>The page the template is.</summary>
        public const string IndexPath = TemplateFolder + "/index.html";

        /// <summary>The value <c>PlayerSettings.WebGL.template</c> must carry.</summary>
        public const string PlayerSettingValue = "PROJECT:" + ProjectTemplateName;

        /// <summary>True when the template's page is on disk.</summary>
        public static bool Exists => File.Exists(IndexPath);

        /// <summary>
        /// True when the Player Settings select this template. False is a real failure: the build would
        /// ship Unity's pinned canvas and the browser would be told 960×600 whatever the window is.
        /// </summary>
        public static bool IsSelected =>
            PlayerSettings.WebGL.template == PlayerSettingValue;

        /// <summary>Checks the template is present and selected, and logs what is wrong.</summary>
        public static bool Verify()
        {
            if (!Exists)
            {
                Debug.LogError("WebGlTemplate: " + IndexPath + " is missing.");
                return false;
            }

            if (!IsSelected)
            {
                Debug.LogError(
                    "WebGlTemplate: PlayerSettings.WebGL.template is '" +
                    PlayerSettings.WebGL.template + "', expected '" + PlayerSettingValue + "'.");
                return false;
            }

            return true;
        }
    }
}
