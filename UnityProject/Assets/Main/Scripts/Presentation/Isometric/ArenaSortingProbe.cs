using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectJianghu.Game.Presentation.Isometric
{
    /// <summary>
    /// What the sorting probe measured, one field per question the ADR has to answer.
    /// </summary>
    public sealed class SortingProbeReport
    {
        public SortingProbeReport(
            bool spriteInFrontDrawsOverProp,
            bool propOccludesSpriteBehind,
            bool nearerSpriteDrawsFirst,
            bool authoredOrderDecides,
            float measuredPixelsPerUnit,
            int nearSortingOrder,
            int farSortingOrder)
        {
            SpriteInFrontDrawsOverProp = spriteInFrontDrawsOverProp;
            PropOccludesSpriteBehind = propOccludesSpriteBehind;
            NearerSpriteDrawsFirst = nearerSpriteDrawsFirst;
            AuthoredOrderDecides = authoredOrderDecides;
            MeasuredPixelsPerUnit = measuredPixelsPerUnit;
            NearSortingOrder = nearSortingOrder;
            FarSortingOrder = farSortingOrder;
        }

        /// <summary>A sprite held in front of a 3D prop is drawn over it.</summary>
        public bool SpriteInFrontDrawsOverProp { get; }

        /// <summary>A sprite held behind the same prop is hidden by it (the depth test, not luck).</summary>
        public bool PropOccludesSpriteBehind { get; }

        /// <summary>Of two overlapping sprites, the one nearer the camera is the visible one.</summary>
        public bool NearerSpriteDrawsFirst { get; }

        /// <summary>Moving the far sprite's authored order above the near one swaps the pixel.</summary>
        public bool AuthoredOrderDecides { get; }

        /// <summary>Screen pixels per world unit, measured through the real camera.</summary>
        public float MeasuredPixelsPerUnit { get; }

        /// <summary>The order <see cref="BillboardSprite"/> gave the nearer probe sprite.</summary>
        public int NearSortingOrder { get; }

        /// <summary>The order it gave the farther probe sprite.</summary>
        public int FarSortingOrder { get; }

        /// <summary>True when the measured pixels per unit equal what the projection implies.</summary>
        public bool MeasureAgreesWithProjection { get; private set; } = true;

        /// <summary>The projection's own pixels per world unit, for the comparison.</summary>
        public float ProjectedPixelsPerUnit { get; private set; } = IsometricLayout.PixelsPerUnit;

        /// <summary>Records the projection's expectation and settles the agreement flag.</summary>
        internal void ApplyProjection(float projectedPixelsPerUnit)
        {
            ProjectedPixelsPerUnit = projectedPixelsPerUnit;
            MeasureAgreesWithProjection = Mathf.Abs(MeasuredPixelsPerUnit - projectedPixelsPerUnit) <= 0.5f;
        }

        /// <summary>True when every question was answered the way the ADR requires.</summary>
        public bool Passed =>
            SpriteInFrontDrawsOverProp
            && PropOccludesSpriteBehind
            && NearerSpriteDrawsFirst
            && AuthoredOrderDecides
            && MeasureAgreesWithProjection;

        /// <summary>One line for the log, with every measured value in it.</summary>
        public string ToLog()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "iso sorting probe: spriteOverProp={0} propOccludes={1} nearerFirst={2} orderDecides={3} ppu={4:0.##} orders={5}/{6} pass={7}",
                SpriteInFrontDrawsOverProp,
                PropOccludesSpriteBehind,
                NearerSpriteDrawsFirst,
                AuthoredOrderDecides,
                MeasuredPixelsPerUnit,
                NearSortingOrder,
                FarSortingOrder,
                Passed);
        }
    }

    /// <summary>
    /// Measures how the isometric stage resolves what is in front: a sprite against a 3D prop, and a
    /// sprite against another sprite.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The measurement is <b>pixels</b>, not an opinion. The probe builds three throwaway objects in
    /// front of the scene's own camera — an opaque URP/Lit cube and two sprite quads in clearly
    /// different colours — renders through the camera into a small <see cref="RenderTexture"/>, and
    /// samples the centre pixel of each arrangement. It then destroys everything it made, so the
    /// scene is exactly as it was.
    /// </para>
    /// <para>
    /// Two mechanisms are being separated, and the ADR depends on both being true:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Sprite vs opaque prop</b> is decided by the depth buffer: a sprite has its material in the
    /// transparent queue with <c>ZWrite Off</c> but <c>ZTest LEqual</c>, so it draws over a prop in
    /// front of it and is hidden by a prop in front of <i>the camera</i>.
    /// </description></item>
    /// <item><description>
    /// <b>Sprite vs sprite</b> cannot be decided by depth at all (neither writes it), so it is
    /// authored: <see cref="BillboardSprite"/> writes a sorting order from the depth. The probe shows
    /// both halves — the nearer sprite wins, and moving the order swaps the winner.
    /// </description></item>
    /// </list>
    /// <para>
    /// Rendering is submitted through URP's own API (<c>SubmitRenderRequest</c>): under an SRP,
    /// <c>Camera.Render()</c> is not a supported path, and a probe that used it would measure nothing.
    /// </para>
    /// </remarks>
    public sealed class ArenaSortingProbe
    {
        private const string BaseColorProperty = "_BaseColor";

        private static readonly Color PropColour = new Color(0.1f, 0.75f, 0.2f, 1f);
        private static readonly Color NearColour = new Color(0.9f, 0.1f, 0.1f, 1f);
        private static readonly Color FarColour = new Color(0.1f, 0.2f, 0.9f, 1f);

        /// <summary>
        /// Side of the square the probe renders into, in pixels.
        /// </summary>
        /// <remarks>
        /// 256, not 64: the scene's orthographic size is 11.25 world units at the game's zoom, so a
        /// 64-pixel target draws one world unit at 2.8 pixels — less than a probe sprite is wide, and
        /// the centre sample misses it. At 256 the probe prop is about 11 pixels and a probe sprite
        /// about 22, which is what makes a sampled pixel a fact rather than a coin flip.
        /// </remarks>
        private const int ProbeTextureSize = 256;

        /// <summary>How far in front of (and behind) the probe prop the probe sprite is placed, in world units.</summary>
        /// <remarks>
        /// 1.5, not "just past the face": an axis-aligned unit cube seen down a 45°/30° view ray
        /// reaches 0.5 × (|fx| + |fy| + |fz|) ≈ 0.73 units toward the camera from its centre, so an
        /// offset of 0.75 leaves two hundredths of a unit between the two surfaces — inside the depth
        /// buffer's precision at 8 units. The offset has to be unambiguous for the answer to mean
        /// anything.
        /// </remarks>
        private const float SpriteOffset = 1.5f;

        /// <summary>
        /// Runs every arrangement through <paramref name="camera"/> and returns the measurements.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="camera"/> is null.</exception>
        public SortingProbeReport Measure(Camera camera)
        {
            if (camera is null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture target = RenderTexture.GetTemporary(
                ProbeTextureSize, ProbeTextureSize, 24, RenderTextureFormat.ARGB32);
            target.Create();

            var created = new System.Collections.Generic.List<GameObject>(4);
            var temporary = new System.Collections.Generic.List<UnityEngine.Object>(6);
            try
            {
                Vector3 forward = camera.transform.forward;
                Vector3 right = camera.transform.right;
                Vector3 origin = camera.transform.position + (forward * 8f);

                Material propMaterial = CreateProbeMaterial(PropColour);
                temporary.Add(propMaterial);
                GameObject prop = CreateCube(origin, propMaterial);
                created.Add(prop);

                Sprite nearSprite = CreateProbeSprite(NearColour, temporary);
                Sprite farSprite = CreateProbeSprite(FarColour, temporary);

                GameObject near = CreateSprite(origin - (forward * 0.4f) - (right * 0.1f), nearSprite);
                GameObject far = CreateSprite(origin + (forward * 0.4f) + (right * 0.1f), farSprite);
                created.Add(near);
                created.Add(far);

                // Both sprites get their order from the rule the game uses, in real scene depth.
                near.AddComponent<BillboardSprite>().ApplyNow(camera);
                BillboardSprite farBillboard = far.AddComponent<BillboardSprite>();
                farBillboard.ApplyNow(camera);

                int nearOrder = near.GetComponent<BillboardSprite>().AppliedOrder;
                int farOrder = farBillboard.AppliedOrder;

                camera.targetTexture = target;

                // 1. A sprite in front of the prop is drawn over it: the prop is opaque and writes
                //    depth, and the sprite (transparent queue, ZTest LEqual) is nearer the camera.
                //    The offset is 0.75, not 0.4: the probe cube is one unit across, so half of it
                //    (0.5) reaches toward the camera and a sprite inside that box is genuinely
                //    behind the cube's front face — the first version of this probe sat there and
                //    measured the prop while calling it "in front".
                far.SetActive(false);
                near.transform.position = origin - (forward * SpriteOffset);
                Color nearOnly = RenderAndSample(camera, target);
                bool spriteInFrontDrawsOverProp = IsColour(nearOnly, NearColour);

                // 2. The same sprite behind the prop is occluded by it: the pixel is the prop's.
                near.transform.position = origin + (forward * SpriteOffset);
                Color behindProp = RenderAndSample(camera, target);
                bool propOccludesSpriteBehind = IsColour(behindProp, PropColour);

                // 3. Two overlapping sprites, sorted by depth: the nearer one wins. (The prop is off
                //    and the near sprite is back in front of the camera.)
                prop.SetActive(false);
                far.SetActive(true);
                near.transform.position = origin - (forward * 0.3f);
                far.transform.position = origin + (forward * 0.3f);
                near.GetComponent<BillboardSprite>().ApplyNow(camera);
                farBillboard.ApplyNow(camera);
                nearOrder = near.GetComponent<BillboardSprite>().AppliedOrder;
                farOrder = farBillboard.AppliedOrder;
                Color depthSorted = RenderAndSample(camera, target);
                bool nearerSpriteDrawsFirst = IsColour(depthSorted, NearColour);

                // 4. Give the far sprite an order above the near one (a bias): the pixel follows the
                //    order, not the geometry. That is the difference between "sorted by accident" and
                //    "sorted by the scene".
                SpriteRenderer farRenderer = far.GetComponent<SpriteRenderer>();
                farRenderer.sortingOrder = nearOrder + 100;
                Color orderSwapped = RenderAndSample(camera, target);
                bool authoredOrderDecides = IsColour(orderSwapped, FarColour);

                float pixelsPerUnit = MeasurePixelsPerUnit(camera);
                var report = new SortingProbeReport(
                    spriteInFrontDrawsOverProp,
                    propOccludesSpriteBehind,
                    nearerSpriteDrawsFirst,
                    authoredOrderDecides,
                    pixelsPerUnit,
                    nearOrder,
                    farOrder);

                report.ApplyProjection(camera.pixelHeight / (2f * camera.orthographicSize));
                return report;
            }
            finally
            {
                camera.targetTexture = previousTarget;

                for (int i = 0; i < created.Count; i++)
                {
                    if (created[i] is not null)
                    {
                        UnityEngine.Object.Destroy(created[i]);
                    }
                }

                for (int i = 0; i < temporary.Count; i++)
                {
                    if (temporary[i] is not null)
                    {
                        UnityEngine.Object.Destroy(temporary[i]);
                    }
                }

                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>Renders one frame into <paramref name="target"/> and reads its centre pixel.</summary>
        /// <remarks>
        /// No frame is awaited: URP's render request is executed synchronously, and a
        /// <c>ReadPixels</c> from the render target is a GPU sync, so the pixel is the one that was
        /// just drawn.
        /// </remarks>
        private static Color RenderAndSample(Camera camera, RenderTexture target)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
            {
                throw new InvalidOperationException(
                    "the active render pipeline does not support a single-camera render request");
            }

            RenderPipeline.SubmitRenderRequest(camera, request);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(ProbeTextureSize / 2, ProbeTextureSize / 2, 1, 1), 0, 0);
            texture.Apply(false, false);
            Color pixel = texture.GetPixel(0, 0);
            UnityEngine.Object.Destroy(texture);
            RenderTexture.active = previous;
            return pixel;
        }

        private static bool IsColour(Color actual, Color expected) =>
            Mathf.Abs(actual.r - expected.r) <= 0.08f
            && Mathf.Abs(actual.g - expected.g) <= 0.08f
            && Mathf.Abs(actual.b - expected.b) <= 0.08f;

        private static float MeasurePixelsPerUnit(Camera camera)
        {
            // Two points one world unit apart, projected through the camera the game actually renders
            // with. The distance between them in pixels is the PPU, including any resize the camera
            // rig applied.
            Vector3 centre = camera.transform.position + (camera.transform.forward * 8f);
            Vector3 a = camera.WorldToScreenPoint(centre);
            Vector3 b = camera.WorldToScreenPoint(centre + (camera.transform.right * 1f));
            return Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
        }

        /// <summary>
        /// A material that renders the probe prop in exactly <paramref name="colour"/>.
        /// </summary>
        /// <remarks>
        /// Unlit, not Lit: the scene's directional light shades a lit cube to a fraction of its base
        /// colour, and a probe that sampled a shaded face against the authored colour failed on the
        /// lighting rather than on the sorting it was built to answer. The sprite being compared is
        /// unlit too, so both sides of the question are compared in the same units.
        /// </remarks>
        private static Material CreateProbeMaterial(Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader is null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader is null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader);

            // URP/Lit reads _BaseColor; the built-in Standard shader reads _Color (which is what
            // Material.color writes). Setting only the latter leaves a URP cube the shader's default
            // white, and a probe that compares "the prop's colour" then fails for the wrong reason.
            if (material.HasProperty(BaseColorProperty))
            {
                material.SetColor(BaseColorProperty, colour);
            }
            else
            {
                material.color = colour;
            }

            return material;
        }

        private static GameObject CreateCube(Vector3 position, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "SortingProbeProp";
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one;

            Collider collider = cube.GetComponent<Collider>();
            if (collider is not null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static Sprite CreateProbeSprite(
            Color colour, System.Collections.Generic.List<UnityEngine.Object> temporary)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
            };

            var pixels = new Color32[64 * 64];
            Color32 solid = colour;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = solid;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            temporary.Add(texture);

            // Centre pivot, so a sprite placed at the probe origin covers the centre pixel the probe
            // samples. The game's own sprites pivot at the feet; the probe is measuring sorting, not
            // footing, and a feet pivot would put the sample on the sprite's bottom edge.
            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), IsometricLayout.PixelsPerUnit);
            temporary.Add(sprite);
            return sprite;
        }

        private static GameObject CreateSprite(Vector3 position, Sprite sprite)
        {
            var gameObject = new GameObject("SortingProbeSprite");
            gameObject.transform.position = position;

            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            return gameObject;
        }
    }
}
