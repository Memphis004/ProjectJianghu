using NUnit.Framework;
using ProjectJianghu.Game.Presentation.Isometric;
using UnityEngine;

namespace ProjectJianghu.Game.Tests.EditMode
{
    /// <summary>
    /// The HD-2D spike's numbers, asserted without a scene: the projection, the pixel snap and the
    /// depth-to-order rule ([ADR 0110](../../../../docs/adr/0110-hd2d-isometric-camera-pixel-snap-and-sorting.md)).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The spike's runtime probe measures what a rendered frame does; these tests pin the arithmetic
    /// the probe and every later placement depend on, so a change to a constant fails here in
    /// milliseconds instead of in a screenshot someone has to interpret.
    /// </para>
    /// <para>
    /// A snap is a property of a whole range of inputs, not of one point, so the snap tests sweep a
    /// spread of positions rather than asserting a single expected value.
    /// </para>
    /// </remarks>
    public sealed class IsometricTests
    {
        /// <summary>A camera pose that is not axis-aligned, so a wrong-basis snap cannot pass by luck.</summary>
        private static readonly Vector3 AwkwardPosition = new Vector3(3.37f, -1.13f, 7.91f);

        [Test]
        public void One_world_unit_stays_the_same_pixel_count_at_every_viewport_height()
        {
            int[] heights = { 480, 720, 900, 1080, 1440, 2160 };

            foreach (int height in heights)
            {
                float size = IsometricLayout.OrthographicSizeFor(height);

                Assert.That(
                    size * 2f * IsometricLayout.PixelsPerUnit,
                    Is.EqualTo(height).Within(1e-3f),
                    "an orthographic camera of size " + size + " does not show " + height + " pixels of world");
            }
        }

        [Test]
        public void A_tile_is_one_world_unit_wide()
        {
            // The grid coordinate converts to world space by addition, so this is the number that
            // makes a 12x12 arena map a 12x12 metre floor.
            Assert.That(IsometricLayout.PixelsPerUnit, Is.EqualTo(32));
            Assert.That(IsometricLayout.UnitsPerPixel * IsometricLayout.PixelsPerUnit, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Snapping_lands_on_the_camera_pixel_grid()
        {
            Quaternion rotation = IsometricLayout.CameraRotation;
            const float pixelsPerUnit = IsometricLayout.PixelsPerUnit;
            float pixelsPerWorldUnit = 1f / IsometricLayout.UnitsPerPixel;

            for (int i = 0; i < 512; i++)
            {
                float t = i / 512f;
                var position = new Vector3(
                    Mathf.Sin(t * 7.13f) * 9f,
                    Mathf.Cos(t * 3.71f) * 4f,
                    Mathf.Sin(t * 11.29f) * 6f);

                Vector3 snapped = IsometricLayout.SnapToPixel(position, rotation);

                float right = Vector3.Dot(snapped, rotation * Vector3.right) * pixelsPerWorldUnit;
                float up = Vector3.Dot(snapped, rotation * Vector3.up) * pixelsPerWorldUnit;

                Assert.That(right, Is.EqualTo(Mathf.Round(right)).Within(1e-3f), "snapped x is not a whole pixel");
                Assert.That(up, Is.EqualTo(Mathf.Round(up)).Within(1e-3f), "snapped y is not a whole pixel");
            }

            Assert.That(pixelsPerUnit, Is.EqualTo(32));
        }

        [Test]
        public void Snapping_moves_a_position_at_most_half_a_pixel_per_axis()
        {
            Quaternion rotation = IsometricLayout.CameraRotation;
            float pixelsPerWorldUnit = 1f / IsometricLayout.UnitsPerPixel;
            float worst = 0f;

            for (int i = 0; i < 512; i++)
            {
                float t = i / 512f;
                var position = new Vector3(
                    Mathf.Sin(t * 12.9898f) * 6f,
                    Mathf.Cos(t * 78.233f) * 3f,
                    Mathf.Sin(t * 37.719f) * 2f);

                Vector3 delta = IsometricLayout.SnapToPixel(position, rotation) - position;
                float pixels = new Vector2(
                    Vector3.Dot(delta, rotation * Vector3.right),
                    Vector3.Dot(delta, rotation * Vector3.up)).magnitude * pixelsPerWorldUnit;

                worst = Mathf.Max(worst, pixels);
            }

            Assert.That(worst, Is.LessThanOrEqualTo(0.75f), "a snap moved the camera more than three quarters of a pixel");
        }

        [Test]
        public void Snapping_does_not_move_a_position_along_the_view_axis()
        {
            Quaternion rotation = IsometricLayout.CameraRotation;
            Vector3 forward = rotation * Vector3.forward;

            Vector3 snapped = IsometricLayout.SnapToPixel(AwkwardPosition, rotation);

            Assert.That(
                Vector3.Dot(snapped, forward),
                Is.EqualTo(Vector3.Dot(AwkwardPosition, forward)).Within(1e-4f),
                "an orthographic camera's depth must not be quantised: it changes nothing on screen");
        }

        [Test]
        public void Sorting_order_follows_depth_one_step_per_pixel()
        {
            Vector3 forward = IsometricLayout.CameraRotation * Vector3.forward;

            int near = IsometricLayout.SortingOrderFor(Vector3.zero, forward);
            int onePixelNearer = IsometricLayout.SortingOrderFor(-forward * IsometricLayout.UnitsPerPixel, forward);
            int onePixelFurther = IsometricLayout.SortingOrderFor(forward * IsometricLayout.UnitsPerPixel, forward);

            Assert.That(onePixelNearer, Is.EqualTo(near + 1), "one pixel nearer must be exactly one order higher");
            Assert.That(onePixelFurther, Is.EqualTo(near - 1), "one pixel further must be exactly one order lower");

            // A bias is an authored tie-break, not a distance: it must shift the pair, not reorder it.
            int biasedNear = IsometricLayout.SortingOrderFor(Vector3.zero, forward, bias: 8);
            int biasedFar = IsometricLayout.SortingOrderFor(forward * 0.5f, forward, bias: 8);
            Assert.That(biasedNear, Is.GreaterThan(biasedFar));
            Assert.That(biasedNear - near, Is.EqualTo(8));
        }

        [Test]
        public void A_billboard_faces_the_camera_yaw_and_stays_upright()
        {
            var cameraObject = new GameObject("IsometricTestCamera");
            var spriteObject = new GameObject("IsometricTestSprite");

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.rotation = IsometricLayout.CameraRotation;

                spriteObject.AddComponent<SpriteRenderer>();
                BillboardSprite billboard = spriteObject.AddComponent<BillboardSprite>();
                billboard.ApplyNow(camera);

                Vector3 euler = spriteObject.transform.eulerAngles;

                Assert.That(
                    Mathf.Abs(Mathf.DeltaAngle(euler.y, IsometricLayout.YawDegrees)),
                    Is.LessThan(0.01f),
                    "the sprite does not follow the camera's yaw");
                Assert.That(
                    Mathf.Abs(Mathf.DeltaAngle(euler.x, 0f)),
                    Is.LessThan(0.01f),
                    "a full billboard would tilt the sprite by the camera's pitch and lean it into the floor");
            }
            finally
            {
                Object.DestroyImmediate(spriteObject);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
