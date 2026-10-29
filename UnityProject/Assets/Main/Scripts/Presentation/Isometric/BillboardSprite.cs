using UnityEngine;

namespace ProjectJianghu.Game.Presentation.Isometric
{
    /// <summary>
    /// A sprite that stands in the 3D scene: it faces the camera's yaw (never its pitch), and its
    /// sorting order comes from its depth.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Yaw, not pitch.</b> A true billboard that turns to face the camera would tilt the sprite by
    /// the camera's 30° pitch, so a character drawn upright would lean back into the ground. HD-2D's
    /// look — a flat sprite standing in a 3D room — needs the sprite square to the screen and
    /// vertical in the world, which is a yaw-only rotation.
    /// </para>
    /// <para>
    /// <b>Sorting.</b> The order is derived from the sprite's own world position
    /// (<see cref="IsometricLayout.SortingOrderFor"/>), so walking up-screen sorts a character behind
    /// what it passes and walking down-screen sorts it in front. Two sprites at the same depth differ
    /// by <see cref="Bias"/> if the scene needs an authored tie-break (a weapon over a body).
    /// </para>
    /// <para>
    /// The transform's position is the character's <b>feet</b>: the sprite's pivot is imported at
    /// bottom-centre, so the world position is the tile the character stands on and nothing has to
    /// subtract half a sprite's height to place it.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(100)]
    public sealed class BillboardSprite : MonoBehaviour
    {
        [Tooltip("Authored tie-break, added to the depth-derived order. Higher draws in front.")]
        [SerializeField] private int _bias;

        [Tooltip("Camera whose yaw the sprite follows. Found once when the scene has none.")]
        [SerializeField] private Camera _camera;

        private SpriteRenderer _renderer;
        private int _appliedOrder = int.MinValue;

        /// <summary>The renderer whose order this component writes.</summary>
        public SpriteRenderer Renderer => _renderer;

        /// <summary>The order last written to the renderer.</summary>
        public int AppliedOrder => _appliedOrder;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_camera is null)
            {
                _camera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            if (_renderer is null)
            {
                return;
            }

            if (_camera is null)
            {
                _camera = Camera.main;
            }

            float yaw = _camera is null ? IsometricLayout.YawDegrees : _camera.transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Vector3 forward = _camera is null ? IsometricLayout.CameraRotation * Vector3.forward : _camera.transform.forward;
            int order = IsometricLayout.SortingOrderFor(transform.position, forward, _bias);
            if (order != _appliedOrder)
            {
                _appliedOrder = order;
                _renderer.sortingOrder = order;
            }
        }

        /// <summary>
        /// Applies the authored order and rotation once without waiting for a frame. Public so a test
        /// can assert the rule the component follows.
        /// </summary>
        public void ApplyNow(Camera camera)
        {
            if (_renderer is null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            Camera source = camera is not null ? camera : _camera;
            float yaw = source is null ? IsometricLayout.YawDegrees : source.transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Vector3 forward = source is null ? IsometricLayout.CameraRotation * Vector3.forward : source.transform.forward;
            _appliedOrder = IsometricLayout.SortingOrderFor(transform.position, forward, _bias);
            _renderer.sortingOrder = _appliedOrder;
        }
    }
}
