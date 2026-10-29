using UnityEngine;

namespace ProjectJianghu.Game.Presentation.Isometric
{
    /// <summary>
    /// The isometric camera: orthographic, 45° yaw / 30° pitch, a pixel-constant orthographic size,
    /// and a position that is snapped to the pixel grid every time it moves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ADR 0110 settles the projection: orthographic at a fixed yaw and pitch, with the size derived
    /// from the viewport so one world unit is always <see cref="IsometricLayout.PixelsPerUnit"/>
    /// pixels. A perspective camera would make the same silhouette a different size in different
    /// rows, which is the one thing an isometric grid cannot afford.
    /// </para>
    /// <para>
    /// <b>Pixel snap.</b> The rig re-snaps whenever the camera would move — a follow target, a sweep,
    /// a resize — so the rendered image never samples halfway between texels. The snap is applied in
    /// the camera's own basis (<see cref="IsometricLayout.SnapToPixel"/>); the motion hidden by it is
    /// at most half a pixel per axis, which is why the sweep below looks smooth rather than steppy at
    /// this scale.
    /// </para>
    /// <para>
    /// The drift/sweep is <b>presentation only</b>: it moves the camera, never the simulation. AGENTS
    /// rule 5's "no Time in Core" is not at risk — this class is in Presentation and drives nothing.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-100)]
    public sealed class IsoCameraRig : MonoBehaviour
    {
        [Tooltip("What the camera looks at. Null leaves the authored position in place.")]
        [SerializeField] private Transform _target;

        [Tooltip("Offset from the target, in world units, in the rig's own basis (x = right, y = up, z = forward).")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, 20f);

        [Tooltip("Screen pixels per sprite pixel. 1 is pixel-exact; 1.5 gives the chunky HD-2D read.")]
        [SerializeField] private float _zoom = 1.5f;

        [Tooltip("Sweep the target back and forth so the pixel snap has something to snap. Visual only.")]
        [SerializeField] private bool _sweep;

        [Tooltip("Half-length of the sweep, in world units.")]
        [SerializeField] private float _sweepAmplitude = 1.5f;

        [Tooltip("Seconds for one full sweep out and back.")]
        [SerializeField] private float _sweepSeconds = 8f;

        private Camera _camera;
        private Vector3 _basePosition;
        private int _appliedScreenHeight = -1;
        private float _sweepTime;

        /// <summary>The camera this rig drives.</summary>
        public Camera Camera => _camera;

        /// <summary>True when the last applied pose was snapped (a test reads it back).</summary>
        public bool Snapped { get; private set; }

        /// <summary>The target the camera follows, if any.</summary>
        public Transform Target => _target;

        /// <summary>The zoom this rig renders at (screen pixels per sprite pixel).</summary>
        public float Zoom => _zoom <= 0f ? 1f : _zoom;

        /// <summary>Screen pixels one world unit is drawn at — the PPU times the zoom.</summary>
        public float PixelsPerWorldUnit => IsometricLayout.PixelsPerUnit * Zoom;

        /// <summary>The orthographic size the rig wants for the current viewport and zoom.</summary>
        public float DesiredOrthographicSize =>
            Mathf.Max(1, Screen.height) / (2f * PixelsPerWorldUnit);

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            transform.rotation = IsometricLayout.CameraRotation;
            _basePosition = transform.position;
            ApplyOrthographicSize();
            ApplyPose(transform.position);
        }

        /// <summary>Sets the follow target and re-snaps immediately.</summary>
        public void SetTarget(Transform target)
        {
            _target = target;
            LateUpdate();
        }

        /// <summary>
        /// Places the camera and snaps it, in one step. Public so a test (or the sorting probe) can
        /// pose the camera without waiting for a frame.
        /// </summary>
        public void ApplyPose(Vector3 position)
        {
            transform.position = IsometricLayout.SnapToPixel(position, transform.rotation);
            Snapped = true;
        }

        /// <summary>
        /// Re-derives the orthographic size from the viewport. Called every frame when the screen size
        /// changes, because the pixel-constant PPU depends on it.
        /// </summary>
        public void ApplyOrthographicSize()
        {
            if (_camera is null)
            {
                _camera = GetComponent<Camera>();
            }

            _camera.orthographic = true;
            _camera.orthographicSize = DesiredOrthographicSize;
            _appliedScreenHeight = Screen.height;
        }

        private void LateUpdate()
        {
            if (Screen.height != _appliedScreenHeight)
            {
                ApplyOrthographicSize();
            }

            Vector3 anchor = _basePosition;
            if (_target is not null)
            {
                anchor = _target.position;
            }

            if (_sweep)
            {
                _sweepTime += Time.unscaledDeltaTime;
                float phase = _sweepSeconds <= 0f ? 0f : (_sweepTime / _sweepSeconds) * Mathf.PI * 2f;
                anchor += transform.right * (Mathf.Sin(phase) * _sweepAmplitude);
            }

            ApplyPose(anchor + (transform.right * _offset.x) + (transform.up * _offset.y) + (transform.forward * _offset.z));
        }
    }
}
