using System;
using ProjectJianghu.Game.Presentation;
using ProjectJianghu.Game.Presentation.Arena;
using ProjectJianghu.Game.Presentation.Isometric;
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
    /// Builds the Arena scene: the loop's unit of play, the HD-2D isometric spike inside it, and the
    /// quality selector.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One file per scene.</b> Everything is placed through the Editor API (AGENTS rule 11), so the
    /// scene is reproducible from <c>ProjectJianghu/Scenes/4. Create Arena scene</c> and no YAML is
    /// hand-edited. The strings the screen shows arrive at runtime from the localization table
    /// (AGENTS rule 5).
    /// </para>
    /// <para>
    /// <b>The stage is the spike.</b> A 12×12 wood floor made of one scaled cube, three placeholder
    /// props, three sprite billboards and a crimson banner carrying the decorative 武 <i>as a sprite</i>
    /// (ADR 0112). ADR 0110 records why the camera is orthographic at 45°/30°, why the sprite pivot is
    /// the feet, and what the sorting probe measured.
    /// </para>
    /// <para>
    /// <b>The UI does not cover the stage.</b> The template's Encounter screen centred a 1200×900 panel
    /// over everything; an HD-2D spike has to be looked at, so the arena's UI is a top bar, a bottom row
    /// of two controls and a settings panel in the corner, all translucent and none of them over the
    /// middle of the frame.
    /// </para>
    /// </remarks>
    public static class ArenaSceneBuilder
    {
        private const float LineFontSize = 28f;

        /// <summary>Where the player, the rival and the far fighter stand, in tiles.</summary>
        private static readonly Vector3 PlayerTile = new Vector3(4f, 0f, 4f);

        private static readonly Vector3 RivalTile = new Vector3(6.4f, 0f, 6.4f);

        private static readonly Vector3 FarTile = new Vector3(7.6f, 0f, 7.4f);

        /// <summary>Centre of the 12×12 floor, which is what the camera looks at.</summary>
        private static readonly Vector3 StageCentre = new Vector3(5.5f, 0f, 5.5f);

        [MenuItem("ProjectJianghu/Scenes/4. Create Arena scene", priority = 104)]
        public static void CreateFromMenu()
        {
            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);

            Create();
            ProjectSetup.ConfigureBuildSettings();
            ProjectSetup.RefreshUrlSceneAllowList();

            ProjectSetup.SaveAndRefresh("ProjectJianghu: Arena scene created and added to build settings.");
        }

        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (TMP_FontAsset font, _) = ProjectSetup.EnsureFontAssets();
            SpikeArtSetup.EnsureSpikeArt();

            var scopeObject = new GameObject("Arena");
            ArenaSceneScope scope = scopeObject.AddComponent<ArenaSceneScope>();
            ArenaSceneDirector director = scopeObject.AddComponent<ArenaSceneDirector>();

            Camera camera = ProjectSetup.CreateCamera();
            ProjectSetup.CreateEventSystem();
            CreateLight();

            Transform stageCentre = CreateStage(camera);

            IsoCameraRig rig = camera.gameObject.AddComponent<IsoCameraRig>();
            ProjectSetup.SetObjectReference(rig, "_target", stageCentre);
            // The camera sits 20 units back along its own forward axis, so the stage is in front
            // of it. A positive z would put the camera underground looking up.
            ProjectSetup.SetVector3(rig, "_offset", new Vector3(0f, 0f, -20f));
            ProjectSetup.SetBool(rig, "_sweep", true);

            Canvas canvas = ProjectSetup.CreateCanvas(out GameObject canvasObject);
            var canvasRect = (RectTransform)canvas.transform;

            RectTransform topBar = ProjectSetup.StretchUiRect(
                canvasRect, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -120f), new Vector2(0f, 0f));
            ProjectSetup.AddPanelBackground(topBar, new Color(0.05f, 0.06f, 0.09f, 0.72f));

            TMP_Text title = ProjectSetup.CreateUiText(
                topBar, "Title", new Vector2(32f, -18f), new Vector2(900f, 84f), 56f, font,
                TextAlignmentOptions.MidlineLeft);

            TMP_Text progress = ProjectSetup.StretchUiText(
                topBar, "ProgressLine", new Vector2(0.45f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 18f), new Vector2(-32f, -18f), LineFontSize, font,
                TextAlignmentOptions.MidlineRight);

            Button win = ProjectSetup.CreateUiButton(
                canvasRect, "WinButton", new Vector2(-120f, 40f), new Vector2(220f, 60f), font,
                out TMP_Text winLabel, new Vector2(0.5f, 0f));

            Button lose = ProjectSetup.CreateUiButton(
                canvasRect, "LoseButton", new Vector2(120f, 40f), new Vector2(220f, 60f), font,
                out TMP_Text loseLabel, new Vector2(0.5f, 0f));

            (RectTransform settingsPanel, TMP_Text status, Button low, Button medium, Button high,
                TMP_Text lowLabel, TMP_Text mediumLabel, TMP_Text highLabel) = CreateSettingsPanel(canvasRect, font);

            ArenaView view = canvasObject.AddComponent<ArenaView>();
            ProjectSetup.SetObjectReference(view, "_scope", scope);
            ProjectSetup.SetObjectReference(view, "_titleLabel", title);
            ProjectSetup.SetObjectReference(view, "_progressLabel", progress);
            ProjectSetup.SetObjectReference(view, "_winButton", win);
            ProjectSetup.SetObjectReference(view, "_loseButton", lose);
            ProjectSetup.SetObjectReference(view, "_winLabel", winLabel);
            ProjectSetup.SetObjectReference(view, "_loseLabel", loseLabel);

            var spikeObject = new GameObject("Spike");
            ArenaSpikeDirector spike = spikeObject.AddComponent<ArenaSpikeDirector>();
            ProjectSetup.SetObjectReference(spike, "_scope", scope);
            ProjectSetup.SetObjectReference(spike, "_cameraRig", rig);
            ProjectSetup.SetObjectReference(spike, "_statusLabel", status);
            ProjectSetup.SetObjectReference(spike, "_lowButton", low);
            ProjectSetup.SetObjectReference(spike, "_mediumButton", medium);
            ProjectSetup.SetObjectReference(spike, "_highButton", high);
            ProjectSetup.SetObjectReference(spike, "_lowLabel", lowLabel);
            ProjectSetup.SetObjectReference(spike, "_mediumLabel", mediumLabel);
            ProjectSetup.SetObjectReference(spike, "_highLabel", highLabel);

            ProjectSetup.SetObjectReference(director, "_scope", scope);

            if (settingsPanel is null)
            {
                throw new InvalidOperationException("The arena's settings panel was not created.");
            }

            EditorSceneManager.SaveScene(scene, ProjectSetup.ArenaScenePath);
        }

        /// <summary>
        /// The 3D half of the spike: floor, props, sprite billboards and the banner. Returns the
        /// transform the camera looks at.
        /// </summary>
        private static Transform CreateStage(Camera camera)
        {
            var stage = new GameObject("Stage");

            Material ground = ProjectSetup.EnsureMaterial(
                ProjectSetup.MaterialFolder + "/ArenaGround.mat", new Color(0.31f, 0.23f, 0.17f, 1f));
            Material crate = ProjectSetup.EnsureMaterial(
                ProjectSetup.MaterialFolder + "/ArenaCrate.mat", new Color(0.45f, 0.31f, 0.19f, 1f));
            Material stone = ProjectSetup.EnsureMaterial(
                ProjectSetup.MaterialFolder + "/ArenaStone.mat", new Color(0.34f, 0.35f, 0.39f, 1f));
            Material banner = ProjectSetup.EnsureMaterial(
                ProjectSetup.MaterialFolder + "/ArenaBanner.mat", new Color(0.55f, 0.13f, 0.13f, 1f));

            // The floor: one scaled cube. 12×12 tiles at one unit per tile, which is the arena_map
            // size Stage 04 authors.
            GameObject floor = CreateBox(
                stage.transform, "Floor", new Vector3(StageCentre.x, -0.1f, StageCentre.z),
                new Vector3(12f, 0.2f, 12f), ground);

            GameObject crateProp = CreateBox(
                stage.transform, "Crate", new Vector3(3.2f, 0.5f, 6.1f), Vector3.one, crate);
            CreateBox(stage.transform, "CrateLow", new Vector3(8.4f, 0.35f, 8.6f), new Vector3(1f, 0.7f, 1f), crate);
            CreateBox(stage.transform, "Pillar", new Vector3(1.6f, 1.5f, 3.2f), new Vector3(1f, 3f, 1f), stone);

            // The banner: a pole, a cloth quad that the glyph sprite is pinned in front of.
            CreateCylinder(stage.transform, "BannerPole", new Vector3(9.2f, 1.6f, 1.8f), new Vector3(0.09f, 1.6f, 0.09f), stone);

            GameObject cloth = CreateQuad(
                stage.transform, "BannerCloth", new Vector3(9.2f, 2.4f, 1.8f), new Vector3(1.4f, 1.6f, 1f), banner);
            cloth.transform.rotation = Quaternion.Euler(0f, IsometricLayout.YawDegrees, 0f);

            Sprite bannerGlyph = LoadSprite(SpikeArtSetup.BannerPath);
            Sprite fighter = LoadSprite(SpikeArtSetup.FighterPath);
            Sprite rival = LoadSprite(SpikeArtSetup.RivalPath);
            Sprite shadow = LoadSprite(SpikeArtSetup.ShadowPath);

            // Three fighters at three depths: the sprite-vs-sprite rule needs more than one.
            CreateBillboard(stage.transform, "FighterPlayer", PlayerTile, fighter, bias: 0, camera);
            CreateBillboard(stage.transform, "FighterRival", RivalTile, rival, bias: 0, camera);
            CreateBillboard(stage.transform, "FighterFar", FarTile, fighter, bias: 0, camera);

            // The banner's 武, pinned just in front of the cloth along the camera's own axis (the
            // cloth is an opaque prop, so a glyph behind it would be depth-tested away). A sprite,
            // never a font glyph: ADR 0112.
            Vector3 towardCamera = (IsometricLayout.CameraRotation * Vector3.forward) * -0.06f;
            GameObject glyph = CreateBillboard(
                stage.transform, "BannerGlyph", new Vector3(9.2f, 1.66f, 1.8f) + towardCamera,
                bannerGlyph, bias: 8, camera);
            glyph.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

            // The shadow is a sprite too, sorted under everything so it reads as a decal.
            GameObject shadowSprite = CreateBillboard(
                stage.transform, "PlayerShadow", PlayerTile, shadow, bias: -400, camera);
            shadowSprite.transform.position = new Vector3(PlayerTile.x, 0.02f, PlayerTile.z);

            var centre = new GameObject("StageCentre");
            centre.transform.SetParent(stage.transform, false);
            centre.transform.position = StageCentre;
            return centre.transform;
        }

        private static GameObject CreateBox(
            Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;

            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            return box;
        }

        private static GameObject CreateCylinder(
            Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.position = position;
            cylinder.transform.localScale = scale;

            UnityEngine.Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            cylinder.GetComponent<MeshRenderer>().sharedMaterial = material;
            return cylinder;
        }

        private static GameObject CreateQuad(
            Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.position = position;
            quad.transform.localScale = scale;

            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            return quad;
        }

        /// <summary>
        /// A sprite standing on the floor. The transform is the character's feet — the sprite is
        /// imported with a bottom-centre pivot, so nothing has to offset it by half its height.
        /// </summary>
        private static GameObject CreateBillboard(
            Transform parent, string name, Vector3 position, Sprite sprite, int bias, Camera camera)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;

            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;

            BillboardSprite billboard = gameObject.AddComponent<BillboardSprite>();
            ProjectSetup.SetInt(billboard, "_bias", bias);
            // Authored, not found at runtime: the shell's integrity test treats an unassigned
            // serialized reference as a broken scene, and the scene has exactly one camera.
            ProjectSetup.SetObjectReference(billboard, "_camera", camera);
            return gameObject;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite is null)
            {
                throw new InvalidOperationException(
                    "Sprite not found at " + path + "; run ProjectJianghu/Stage 03/1 to generate the spike art.");
            }

            return sprite;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.86f, 1f);
            light.intensity = 1.4f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
        }

        /// <summary>
        /// The quality selector: a heading with the measured frame rate, and one button per tier.
        /// </summary>
        private static (RectTransform panel, TMP_Text status, Button low, Button medium, Button high,
            TMP_Text lowLabel, TMP_Text mediumLabel, TMP_Text highLabel) CreateSettingsPanel(
            RectTransform canvasRect, TMP_FontAsset font)
        {
            RectTransform panel = ProjectSetup.StretchUiRect(
                canvasRect, "SettingsPanel", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-396f, 24f), new Vector2(-24f, 340f));
            ProjectSetup.AddPanelBackground(panel, new Color(0.05f, 0.06f, 0.09f, 0.78f));

            TMP_Text status = ProjectSetup.CreateUiText(
                panel, "StatusLine", new Vector2(20f, -52f), new Vector2(340f, 60f), 22f, font,
                TextAlignmentOptions.TopLeft);

            Button low = ProjectSetup.CreateUiButton(
                panel, "QualityLow", new Vector2(24f, -132f), new Vector2(320f, 48f), font, out TMP_Text lowLabel);
            Button medium = ProjectSetup.CreateUiButton(
                panel, "QualityMedium", new Vector2(24f, -190f), new Vector2(320f, 48f), font, out TMP_Text mediumLabel);
            Button high = ProjectSetup.CreateUiButton(
                panel, "QualityHigh", new Vector2(24f, -248f), new Vector2(320f, 48f), font, out TMP_Text highLabel);

            return (panel, status, low, medium, high, lowLabel, mediumLabel, highLabel);
        }
    }
}
