using System;
using R3;

namespace ProjectJianghu.Game.Presentation.Steps
{
    /// <summary>
    /// The UI-level state a placeholder step screen renders: a heading, one line and one caption.
    /// </summary>
    /// <remarks>
    /// R3 carries only UI-level state (AGENTS rule 10). Every value is written by
    /// <see cref="StepPresenter"/>; the view mirrors it and nothing else.
    /// </remarks>
    public sealed class StepUiState : IDisposable
    {
        /// <summary>The screen's heading.</summary>
        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>One line describing what the screen will do once it is built.</summary>
        public ReactiveProperty<string> Line { get; } = new ReactiveProperty<string>(string.Empty);

        /// <summary>Caption of the control that leaves the screen.</summary>
        public ReactiveProperty<string> ContinueLabel { get; } = new ReactiveProperty<string>(string.Empty);

        /// <inheritdoc />
        public void Dispose()
        {
            Title.Dispose();
            Line.Dispose();
            ContinueLabel.Dispose();
        }
    }
}
