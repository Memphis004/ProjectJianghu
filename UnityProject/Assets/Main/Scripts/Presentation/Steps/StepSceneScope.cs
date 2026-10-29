using ProjectJianghu.Core.Application.Flow;
using ProjectJianghu.Core.Application.Text;
using ProjectJianghu.Game.Presentation.Flow;
using UnityEngine;
using VContainer;

namespace ProjectJianghu.Game.Presentation.Steps
{
    /// <summary>
    /// The composition root of a placeholder step scene: its state, its content and its presenter.
    /// </summary>
    /// <remarks>
    /// One scope serves both placeholder screens; the three keys and the target scene are authored
    /// per scene, so the same code is a different screen in each. A screen that becomes real
    /// (Stage 10's Bracket, Stage 11's CharacterCreate) replaces this class in its own scene rather
    /// than editing the other screen's wiring.
    /// </remarks>
    public sealed class StepSceneScope : RootLifetimeScope
    {
        [Tooltip("Localization key of the heading.")]
        [SerializeField] private string _titleKey = string.Empty;

        [Tooltip("Localization key of the one line the screen shows.")]
        [SerializeField] private string _lineKey = string.Empty;

        [Tooltip("Localization key of the control caption.")]
        [SerializeField] private string _continueKey = string.Empty;

        [Tooltip("The scene the control asks the loop for.")]
        [SerializeField] private SceneId _target = SceneId.Bracket;

        /// <summary>The content this scene registered; a test reads it rather than a copy.</summary>
        public StepContent Content => new StepContent(_titleKey, _lineKey, _continueKey, _target);

        /// <inheritdoc />
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            StepContent content = Content;

            builder.Register(_ => new StepUiState(), Lifetime.Singleton);

            builder.Register(
                resolver =>
                {
                    RunScopeOwner owner = resolver.Resolve<RunScopeOwner>();

                    // The scope resolves the flow from the live run: a step is always played inside
                    // one (the title creates it), and asking the owner keeps a direct scene load
                    // working the same way the loop does.
                    owner.RequireRun();
                    return new StepPresenter(
                        owner.Flow,
                        resolver.Resolve<LocalizationService>(),
                        resolver.Resolve<StepUiState>(),
                        content);
                },
                Lifetime.Singleton);
        }
    }
}
