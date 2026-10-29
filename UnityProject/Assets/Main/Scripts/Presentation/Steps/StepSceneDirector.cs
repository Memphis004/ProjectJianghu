using System;
using ProjectJianghu.Core.Application.Flow;
using ProjectJianghu.Core.Application.Run;
using ProjectJianghu.Core.Shared;
using ProjectJianghu.Game.Presentation.Flow;
using ProjectJianghu.Game.Presentation.Web;
using UnityEngine;
using VContainer;

namespace ProjectJianghu.Game.Presentation.Steps
{
    /// <summary>
    /// Brings a placeholder step up, tells the loop which scene is on screen, and commits its one way
    /// out.
    /// </summary>
    /// <remarks>
    /// Deliberately thin, like the other scene directors: the strings come from the presenter, the
    /// scene change from the <see cref="SceneLoader"/>. A refusal that will not clear on its own is
    /// logged once; <c>transition.in_progress</c> is retried on the next frame because the fade that
    /// brought this scene up can still be finishing.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class StepSceneDirector : MonoBehaviour
    {
        [SerializeField] private StepSceneScope _scope;

        [Tooltip("The scene this director is in; it is what the loop is told is on screen.")]
        [SerializeField] private SceneId _scene = SceneId.CharacterCreate;

        private SceneLoader _loader;
        private StepPresenter _presenter;
        private bool _started;
        private bool _departed;

        /// <summary>The screen's logic (null until the scene has started).</summary>
        public StepPresenter Presenter => _presenter;

        /// <summary>Why the last start or move was refused, or an empty string.</summary>
        public string StartError { get; private set; } = string.Empty;

        /// <summary>True once the screen is up.</summary>
        public bool Started => _started;

        /// <summary>True once a way out has been committed.</summary>
        public bool Departed => _departed;

        private void Start() => TryStart();

        /// <summary>Brings the screen up. Idempotent, so a test may drive it without <c>Start</c>.</summary>
        public bool TryStart()
        {
            if (_started)
            {
                return true;
            }

            if (_scope is null || _scope.Container is null)
            {
                StartError = "StepSceneDirector has no built StepSceneScope.";
                Debug.LogError(StartError, this);
                return false;
            }

            _presenter = _scope.Container.Resolve<StepPresenter>();
            _loader = _scope.Container.Resolve<SceneLoader>();

            RunScopeOwner owner = _scope.Container.Resolve<RunScopeOwner>();
            RunSession run = owner.RequireRun();
            owner.Flow.SyncTo(_scene);

            // A step opened directly (the Editor's scene list, a ?scene= handover, a restart that
            // dropped the previous run) has a run nobody started. Starting it here is the same call the
            // title makes, so a direct load reaches the same state the loop does instead of failing on
            // a rule one scene later.
            if (!run.IsStarted)
            {
                Result started = run.Start();
                if (started.IsFailure)
                {
                    StartError = "the run could not start: " + started.Error.Code;
                    Debug.LogError(StartError, this);
                    return false;
                }
            }

            _presenter.Refresh();
            WebGlBridge.PublishScene(gameObject.scene.name);

            _started = true;
            return true;
        }

        private void Update()
        {
            if (!_started || _departed || _presenter is null)
            {
                return;
            }

            SceneId? requested = _presenter.RequestedScene;
            if (requested is not SceneId target)
            {
                return;
            }

            if (_loader is null || !_loader.TryGo(target))
            {
                string detail = _loader is null ? "no loader" : _loader.LastError;
                StartError = "the move to " + target + " was refused: " + detail;

                if (!string.Equals(detail, TransitionError.InProgress, StringComparison.Ordinal))
                {
                    Debug.LogError(StartError, this);
                }

                return;
            }

            _departed = true;
            StartError = string.Empty;
        }
    }
}
