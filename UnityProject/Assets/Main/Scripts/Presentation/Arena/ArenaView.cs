using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace ProjectJianghu.Game.Presentation.Arena
{
    /// <summary>
    /// The Arena screen's view: four strings mirrored, two presses forwarded.
    /// </summary>
    /// <remarks>
    /// AGENTS rule 10. The two presses are the placeholder's whole interaction; a game's arena replaces
    /// this view together with its presenter, keeping the same boundary.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaView : MonoBehaviour
    {
        [SerializeField] private RootLifetimeScope _scope;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _progressLabel;
        [SerializeField] private Button _winButton;
        [SerializeField] private Button _loseButton;
        [SerializeField] private TMP_Text _winLabel;
        [SerializeField] private TMP_Text _loseLabel;

        private ArenaUiState _state;
        private ArenaPresenter _presenter;
        private bool _reportPending;
        private IDisposable _title;
        private IDisposable _progress;
        private IDisposable _winCaption;
        private IDisposable _loseCaption;

        /// <summary>True once the view has a state to read.</summary>
        public bool Bound => _state is not null;

        /// <summary>The logic this view drives; null until <see cref="Bind"/>.</summary>
        public ArenaPresenter Presenter => _presenter;

        /// <summary>The controls the page's report publishes a click point for.</summary>
        public enum ArenaButton
        {
            /// <summary>Clears the encounter.</summary>
            Win,

            /// <summary>Loses it.</summary>
            Lose,
        }

        private void Start() => Bind();

        private void OnDestroy()
        {
            _title?.Dispose();
            _progress?.Dispose();
            _winCaption?.Dispose();
            _loseCaption?.Dispose();
            _title = null;
            _progress = null;
            _winCaption = null;
            _loseCaption = null;

            if (_winButton is not null)
            {
                _winButton.onClick.RemoveListener(PressWin);
            }

            if (_loseButton is not null)
            {
                _loseButton.onClick.RemoveListener(PressLose);
            }

            _state = null;
            _presenter = null;
        }

        /// <summary>Connects to the scene scope and pushes the first values. Idempotent.</summary>
        public void Bind()
        {
            if (_state is not null)
            {
                return;
            }

            if (_scope is null || _scope.Container is null)
            {
                Debug.LogError("ArenaView needs a built RootLifetimeScope.", this);
                return;
            }

            _state = _scope.Container.Resolve<ArenaUiState>();
            _presenter = _scope.Container.Resolve<ArenaPresenter>();

            _title = _state.Title.Subscribe(value => Assign(_titleLabel, value));
            _progress = _state.ProgressLine.Subscribe(value => Assign(_progressLabel, value));
            _winCaption = _state.WinLabel.Subscribe(value => Assign(_winLabel, value));
            _loseCaption = _state.LoseLabel.Subscribe(value => Assign(_loseLabel, value));

            if (_winButton is not null)
            {
                _winButton.onClick.AddListener(PressWin);
            }

            if (_loseButton is not null)
            {
                _loseButton.onClick.AddListener(PressLose);
            }

            // Published a frame later, for the same reason as the Hub: the presenter fills the state in
            // its own `Start`, the order of the two is undefined, and a click point wants the final layout.
            _reportPending = true;
        }

        private void LateUpdate()
        {
            if (!_reportPending || _state is null)
            {
                return;
            }

            _reportPending = false;
            ArenaReport.Publish(_state, this, gameObject.scene.name);
        }

        /// <summary>
        /// Where one of the screen's controls is on screen, in screen pixels with the origin at the
        /// bottom-left. False when the scene has not wired that control.
        /// </summary>
        public bool TryGetButtonScreenPoint(ArenaButton button, out Vector2 screen)
        {
            Button target = button == ArenaButton.Win ? _winButton : _loseButton;

            return UiScreenRects.TryCentre(target is null ? null : target.transform as RectTransform, out screen);
        }

        /// <summary>What the win control does. Public so a test can press it without a pointer.</summary>
        public void PressWin()
        {
            if (_presenter is not null)
            {
                _presenter.RequestWin();
            }
        }

        /// <summary>What the lose control does. Public so a test can press it without a pointer.</summary>
        public void PressLose()
        {
            if (_presenter is not null)
            {
                _presenter.RequestLose();
            }
        }

        private static void Assign(TMP_Text label, string value)
        {
            if (label is not null)
            {
                label.text = value;
            }
        }
    }
}
