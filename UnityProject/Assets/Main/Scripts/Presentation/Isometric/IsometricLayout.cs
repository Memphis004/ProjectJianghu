using UnityEngine;

namespace ProjectJianghu.Game.Presentation.Isometric
{
    /// <summary>
    /// The HD-2D isometric spike's geometry, as pure functions: the camera pose, the pixel snap and
    /// the depth-to-sorting-order rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pure and static so the numbers can be asserted in EditMode without a camera, a scene or a
    /// frame — and so the camera rig and the billboards cannot disagree about them. ADR 0110 records
    /// why these four values were chosen and what was measured; this type is where they are written
    /// down once.
    /// </para>
    /// <para>
    /// <b>Pixels per unit is a project constant, not a per-scene one.</b> Every sprite in the game is
    /// authored at 32 pixels per world unit, so a 32×48 sprite is one world unit wide and 1.5 tall,
    /// and one tile of the 12×12 grid is one world unit. Point filtering plus a fixed PPU is what
    /// keeps a pixel a pixel at every zoom the camera allows.
    /// </para>
    /// </remarks>
    public static class IsometricLayout
    {
        /// <summary>Pixels per world unit every sprite is imported and staged at. Never per-scene.</summary>
        public const int PixelsPerUnit = 32;

        /// <summary>Camera yaw, degrees. A 45° yaw is what makes a square tile read as a diamond.</summary>
        public const float YawDegrees = 45f;

        /// <summary>
        /// Camera pitch, degrees. 30° is the classic isometric ratio (2:1 tiles at 26.565° is the
        /// strict one; 30° is the same read with room for a sprite's height).
        /// </summary>
        public const float PitchDegrees = 30f;

        /// <summary>One world unit expressed in whole pixels.</summary>
        public const float UnitsPerPixel = 1f / PixelsPerUnit;

        /// <summary>The camera's isometric rotation: yaw around Y, then pitch around the local X.</summary>
        public static Quaternion CameraRotation => Quaternion.Euler(PitchDegrees, YawDegrees, 0f);

        /// <summary>
        /// The orthographic size that makes one world unit exactly
        /// <paramref name="pixelsPerWorldUnit"/> pixels tall in a viewport
        /// <paramref name="screenHeight"/> pixels high.
        /// </summary>
        /// <remarks>
        /// An orthographic camera shows <c>2 * orthographicSize</c> world units vertically, so the
        /// size is half the viewport in world units. Deriving it from the viewport (rather than
        /// authoring a size) is what keeps the PPU constant across window sizes — the failure this
        /// replaces is a sprite that is 32 px at 720p and 43 px after a resize.
        /// </remarks>
        public static float OrthographicSizeFor(int screenHeight, float pixelsPerWorldUnit = PixelsPerUnit) =>
            Mathf.Max(1, screenHeight) / (2f * Mathf.Max(0.01f, pixelsPerWorldUnit));

        /// <summary>
        /// Snaps a camera position to the pixel grid of <paramref name="rotation"/>: the position's
        /// components along the camera's own right and up axes are rounded to whole pixels, and the
        /// depth component is left alone (it does not change what is drawn under an orthographic
        /// projection).
        /// </summary>
        /// <remarks>
        /// Snapping in the camera's basis, not in world space, is the point: a world-space snap leaves
        /// a 45°-yawed camera sampling halfway between texels, which is exactly the shimmer this
        /// exists to remove. The maximum motion a snap can hide is half a pixel along each axis.
        /// </remarks>
        public static Vector3 SnapToPixel(Vector3 position, Quaternion rotation, int pixelsPerUnit = PixelsPerUnit)
        {
            float unitsPerPixel = 1f / Mathf.Max(1, pixelsPerUnit);
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;

            float x = Vector3.Dot(position, right);
            float y = Vector3.Dot(position, up);
            float depth = Vector3.Dot(position, forward);

            x = Mathf.Round(x / unitsPerPixel) * unitsPerPixel;
            y = Mathf.Round(y / unitsPerPixel) * unitsPerPixel;

            return (right * x) + (up * y) + (forward * depth);
        }

        /// <summary>
        /// Sorting order for a sprite standing at <paramref name="worldPosition"/>: further from the
        /// camera sorts behind, and <paramref name="bias"/> lets one kind of sprite sit in front of
        /// another at the same depth.
        /// </summary>
        /// <remarks>
        /// Depth is measured along the camera's forward axis in <b>pixel</b> steps
        /// (<see cref="PixelsPerUnit"/> orders per world unit), so two sprites a third of a tile apart
        /// still sort differently — a coarser per-tile order would make them flicker as they cross.
        /// The value is clamped to the renderer's 16-bit range.
        /// </remarks>
        public static int SortingOrderFor(Vector3 worldPosition, Vector3 cameraForward, int bias = 0)
        {
            float depth = Vector3.Dot(worldPosition, cameraForward);
            int order = Mathf.RoundToInt(-depth * PixelsPerUnit) + bias;
            return Mathf.Clamp(order, short.MinValue, short.MaxValue);
        }
    }
}
