using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace ProjectJianghu.Game.Presentation.Steps
{
    /// <summary>
    /// The placeholder step's view: it mirrors three strings into three labels and forwards one press.
    /// </summary>
    /// <remarks>
    /// AGENTS rule 10: no logic, no numbers computed here. Every string was decided by
    /// <see cref="StepPresenter"/>; the press only records an intent that the scene's director turns
    /// into a scene change. The page report is published from <c>LateUpdate</c> for the same reason
    /// the Rest screen does it one frame late: the layout has to be final before a click point means
    /// anything (<c>llmwiki/gotchas/layout-report-one-frame-early.md</c>).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class StepView : MonoBehaviour
    {
        [SerializeField] private RootLifetimeScope _scope;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _lineLabel;
        [SerializeField] private Button _continueButton;
        [SerializeField] private TMP_Text _continueLabel;

        [Tooltip("Page global this screen's report is published on, e.g. __projectJianghuBracket.")]
        [SerializeField] private string _reportGlobal = string.Empty;

        private StepUiState _state;
        private StepPresenter _presenter;
        private bool _reportPending;
        private IDisposable _title;
        private IDisposable _line;
        private IDisposable _continueCaption;

        /// <summary>True once the view has a state to read.</summary>
        public bool Bound => _state is not null;

        /// <summary>The logic this view drives; null until <see cref="Bind"/>.</summary>
        public StepPresenter Presenter => _presenter;

        private void Start() => Bind();

        private void OnDestroy()
        {
            _title?.Dispose();
            _line?.Dispose();
            _continueCaption?.Dispose();
            _title = null;
            _line = null;
            _continueCaption = null;

            if (_continueButton is not null)
            {
                _continueButton.onClick.RemoveListener(PressContinue);
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
                Debug.LogError("StepView needs a built RootLifetimeScope.", this);
                return;
            }

            _state = _scope.Container.Resolve<StepUiState>();
            _presenter = _scope.Container.Resolve<StepPresenter>();

            _title = _state.Title.Subscribe(value => Assign(_titleLabel, value));
            _line = _state.Line.Subscribe(value => Assign(_lineLabel, value));
            _continueCaption = _state.ContinueLabel.Subscribe(value => Assign(_continueLabel, value));

            if (_continueButton is not null)
            {
                _continueButton.onClick.AddListener(PressContinue);
            }

            _reportPending = true;
        }

        private void LateUpdate()
        {
            if (!_reportPending || _state is null)
            {
                return;
            }

            _reportPending = false;
            StepReport.Publish(_state, this, gameObject.scene.name, _reportGlobal);
        }

        /// <summary>
        /// Where the continue control is on screen, in screen pixels with the origin at the
        /// bottom-left. False when the scene has not wired it.
        /// </summary>
        public bool TryGetContinueScreenPoint(out Vector2 screen) =>
            UiScreenRects.TryCentre(_continueButton is null ? null : _continueButton.transform as RectTransform, out screen);

        /// <summary>What the control does. Public so a test can press it without a pointer.</summary>
        public void PressContinue()
        {
            if (_presenter is not null)
            {
                _presenter.RequestContinue();
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
