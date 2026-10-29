using System;
using System.Globalization;
using ProjectJianghu.Core.Application.Text;
using ProjectJianghu.Game.Presentation.Isometric;
using ProjectJianghu.Game.Presentation.Quality;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace ProjectJianghu.Game.Presentation.Arena
{
    /// <summary>
    /// The HD-2D spike's own director: stage inventory, the quality selector, the frame-rate probe and
    /// the sorting probe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is deliberately separate from <see cref="ArenaSceneDirector"/>, which owns the loop: the
    /// spike is a look-and-feel experiment that will be replaced by the arena presentation in Stage
    /// 09, and the loop's scene change must not depend on whether the probe succeeded.
    /// </para>
    /// <para>
    /// The quality selector is the "Settings" selector Stage 03 asks for: three buttons that switch
    /// the <see cref="QualityTier"/>, each backed by its own URP asset
    /// (<c>ProjectSetup.ConfigureQualityTiers</c>). The selection is per session — Stage 13 owns
    /// persisted settings.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaSpikeDirector : MonoBehaviour
    {
        /// <summary>Localization keys the spike's labels and buttons read.</summary>
        public static class Keys
        {
            /// <summary>Caption of the Low tier button.</summary>
            public const string Low = "settings.quality.low";

            /// <summary>Caption of the Medium tier button.</summary>
            public const string Medium = "settings.quality.medium";

            /// <summary>Caption of the High tier button.</summary>
            public const string High = "settings.quality.high";

            /// <summary>The line that shows the tier and the measured frame rate.</summary>
            public const string StatusFormat = "settings.quality.status.format";

            /// <summary>Every key this component reads.</summary>
            public static readonly string[] All = { Low, Medium, High, StatusFormat };
        }

        [SerializeField] private RootLifetimeScope _scope;
        [SerializeField] private IsoCameraRig _cameraRig;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private Button _lowButton;
        [SerializeField] private Button _mediumButton;
        [SerializeField] private Button _highButton;
        [SerializeField] private TMP_Text _lowLabel;
        [SerializeField] private TMP_Text _mediumLabel;
        [SerializeField] private TMP_Text _highLabel;

        private static readonly string[] FormatArguments = new string[3];

        private readonly QualityTierService _tiers = new QualityTierService();
        private readonly FrameRateProbe _frames = new FrameRateProbe(3f);
        private readonly ArenaSortingProbe _sortingProbe = new ArenaSortingProbe();

        private LocalizationService _text;
        private SortingProbeReport _sorting;
        private bool _probeStarted;
        private int _spriteCount;
        private int _propCount;
        private bool _started;

        /// <summary>The tier currently applied.</summary>
        public QualityTier CurrentTier => _tiers.Current;

        /// <summary>The frame-rate probe, for a test that wants to feed it directly.</summary>
        public FrameRateProbe Frames => _frames;

        /// <summary>The sorting probe's answer, or null until it has run.</summary>
        public SortingProbeReport Sorting => _sorting;

        /// <summary>True once the spike is up.</summary>
        public bool Started => _started;

        private void Start() => TryStart();

        /// <summary>Brings the spike up. Idempotent, so a test may drive it without <c>Start</c>.</summary>
        public bool TryStart()
        {
            if (_started)
            {
                return true;
            }

            if (_scope is null || _scope.Container is null)
            {
                Debug.LogError("ArenaSpikeDirector needs a built RootLifetimeScope.", this);
                return false;
            }

            _text = _scope.Container.Resolve<LocalizationService>();

            if (_lowLabel is not null)
            {
                _lowLabel.text = _text.Get(Keys.Low);
            }

            if (_mediumLabel is not null)
            {
                _mediumLabel.text = _text.Get(Keys.Medium);
            }

            if (_highLabel is not null)
            {
                _highLabel.text = _text.Get(Keys.High);
            }

            if (_lowButton is not null)
            {
                _lowButton.onClick.AddListener(() => SelectTier(QualityTier.Low));
            }

            if (_mediumButton is not null)
            {
                _mediumButton.onClick.AddListener(() => SelectTier(QualityTier.Medium));
            }

            if (_highButton is not null)
            {
                _highButton.onClick.AddListener(() => SelectTier(QualityTier.High));
            }

            _spriteCount = FindObjectsByType<BillboardSprite>(FindObjectsSortMode.None).Length;
            _propCount = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length;

            _started = true;
            Publish();
            RunSortingProbe();
            return true;
        }

        /// <summary>Switches the quality tier and starts a fresh FPS window.</summary>
        public bool SelectTier(QualityTier tier)
        {
            if (!_tiers.Select(tier))
            {
                return false;
            }

            _frames.Reset();
            Publish();
            return true;
        }

        /// <summary>Switches by index, for a button authored as one of three.</summary>
        public bool SelectTier(int tierIndex) => SelectTier((QualityTier)Mathf.Clamp(tierIndex, 0, QualityTierService.TierCount - 1));

        /// <summary>Where tier <paramref name="tierIndex"/>'s button is, in canvas pixels.</summary>
        /// <remarks>
        /// The tier buttons are on the canvas rather than on a <c>View</c>, so the report asks this
        /// director rather than a view. A browser probe presses the same three controls a player does,
        /// which is what makes a per-tier measurement a measurement of the shipped selector.
        /// </remarks>
        public bool TryGetTierScreenPoint(int tierIndex, out Vector2 screen)
        {
            Button button = tierIndex switch
            {
                0 => _lowButton,
                1 => _mediumButton,
                2 => _highButton,
                _ => null,
            };

            return UiScreenRects.TryCentre(button is null ? null : button.transform as RectTransform, out screen);
        }

        private void Update()
        {
            if (!_started)
            {
                return;
            }

            if (_frames.Add(Time.unscaledDeltaTime))
            {
                Publish();
            }
        }

        /// <summary>
        /// Runs the pixel-sampling probe once. A failure is reported and logged but never stops the
        /// scene: the probe is evidence, not gameplay.
        /// </summary>
        public bool RunSortingProbe()
        {
            if (_probeStarted)
            {
                return _sorting is not null;
            }

            _probeStarted = true;

            Camera camera = _cameraRig is not null && _cameraRig.Camera is not null
                ? _cameraRig.Camera
                : Camera.main;

            if (camera is null)
            {
                Debug.LogWarning("ArenaSpikeDirector: no camera to probe sorting with.", this);
                return false;
            }

            try
            {
                _sorting = _sortingProbe.Measure(camera);
                Debug.Log(_sorting.ToLog(), this);
                UpdateStatusLabel();
                Publish();
                return _sorting.Passed;
            }
            catch (Exception exception)
            {
                Debug.LogError("ArenaSpikeDirector: the sorting probe failed: " + exception.Message, this);
                return false;
            }
        }

        private void Publish()
        {
            Publish(transform.root.gameObject.scene.name);
        }

        private void Publish(string sceneName)
        {
            FrameRateSample sample = _frames.Last.Frames > 0 ? _frames.Last : _frames.Snapshot();
            ArenaSpikeReport.Publish(
                sceneName,
                _tiers.Current,
                _tiers.CurrentName,
                sample,
                _sorting,
                MeasureSnapOffsetPixels(),
                _cameraRig is null ? 1f : _cameraRig.Zoom,
                _cameraRig is null ? 0f : _cameraRig.Camera.orthographicSize,
                _spriteCount,
                _propCount,
                this);

            UpdateStatusLabel();
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel is null)
            {
                return;
            }

            FrameRateSample sample = _frames.Last;
            FormatArguments[0] = _tiers.CurrentName;
            FormatArguments[1] = sample.AverageFps.ToString("0.0", CultureInfo.InvariantCulture);
            FormatArguments[2] = sample.OnePercentLowFps.ToString("0.0", CultureInfo.InvariantCulture);

            _statusLabel.text = string.Format(
                CultureInfo.InvariantCulture, _text.Get(Keys.StatusFormat), FormatArguments);
        }

        /// <summary>
        /// The largest distance a snapped camera pose moves from its unsnapped pose, in pixels, over a
        /// sweep of positions — the evidence behind "pixel snap" in ADR 0110.
        /// </summary>
        public static float MeasureSnapOffsetPixels()
        {
            Quaternion rotation = IsometricLayout.CameraRotation;
            float worst = 0f;

            for (int i = 0; i < 256; i++)
            {
                float t = i / 256f;
                Vector3 position = new Vector3(
                    Mathf.Sin(t * 12.9898f) * 6f,
                    Mathf.Cos(t * 78.233f) * 3f,
                    Mathf.Sin(t * 37.719f) * 2f);

                Vector3 snapped = IsometricLayout.SnapToPixel(position, rotation);
                Vector3 delta = snapped - position;
                float pixels = new Vector2(
                    Vector3.Dot(delta, rotation * Vector3.right),
                    Vector3.Dot(delta, rotation * Vector3.up)).magnitude * IsometricLayout.PixelsPerUnit;

                worst = Mathf.Max(worst, pixels);
            }

            return worst;
        }
    }
}
