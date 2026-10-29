using System;
using ProjectJianghu.Core.Application.Flow;
using ProjectJianghu.Core.Application.Run;
using ProjectJianghu.Core.Application.Text;
using ProjectJianghu.Core.Shared;
using UnityEngine;

namespace ProjectJianghu.Game.Presentation.Steps
{
    /// <summary>
    /// What one placeholder step shows and where it leads: three localization keys and the
    /// <see cref="SceneId"/> its control asks for.
    /// </summary>
    /// <remarks>
    /// Keys are identifiers, not player-visible text (AGENTS rule 5), so they may live in code; the
    /// strings they resolve to come from the shipped localization table. A screen that becomes real
    /// (CharacterCreate in Stage 11, Bracket in Stage 10) replaces its scope's registration with its
    /// own presenter and keeps this type for the screens that are still placeholders.
    /// </remarks>
    public readonly struct StepContent
    {
        /// <summary>Creates the description of one step.</summary>
        public StepContent(string titleKey, string lineKey, string continueKey, SceneId target)
        {
            TitleKey = titleKey;
            LineKey = lineKey;
            ContinueKey = continueKey;
            Target = target;
        }

        /// <summary>Localization key of the heading.</summary>
        public string TitleKey { get; }

        /// <summary>Localization key of the one line the screen shows.</summary>
        public string LineKey { get; }

        /// <summary>Localization key of the control caption.</summary>
        public string ContinueKey { get; }

        /// <summary>The scene the control asks the loop for.</summary>
        public SceneId Target { get; }
    }

    /// <summary>
    /// The logic of a placeholder step: write its three strings, and record one way out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both <c>CharacterCreate</c> and <c>Bracket</c> are placeholders in Stage 03 — the character
    /// rules land in Stage 05/11 and the bracket in Stage 10 — so they share one presenter rather
    /// than carrying two copies of "show a heading and leave through the loop". What is not shared
    /// is the scene: each scene still has its own scope, its own view and its own page report.
    /// </para>
    /// <para>
    /// The move is asked of the <see cref="SceneFlow"/> first (AGENTS rule 7): a scene the loop does
    /// not allow from here is refused with a typed code and the screen does not change.
    /// </para>
    /// </remarks>
    public sealed class StepPresenter
    {
        private readonly SceneFlow _flow;
        private readonly LocalizationService _text;
        private readonly StepUiState _state;
        private readonly StepContent _content;

        /// <summary>Creates the presenter for a step.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public StepPresenter(
            SceneFlow flow, LocalizationService text, StepUiState state, StepContent content)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _content = content;
        }

        /// <summary>Where the player asked to go, or null while no request is outstanding.</summary>
        public SceneId? RequestedScene { get; private set; }

        /// <summary>Why the last command refused, or an empty string.</summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>The scene this step leads to.</summary>
        public SceneId Target => _content.Target;

        /// <summary>Writes every string the screen shows. Idempotent.</summary>
        public void Refresh()
        {
            _state.Title.Value = _text.Get(_content.TitleKey);
            _state.Line.Value = _text.Get(_content.LineKey);
            _state.ContinueLabel.Value = _text.Get(_content.ContinueKey);
        }

        /// <summary>Asks the loop for the next scene.</summary>
        /// <returns>False when a request is outstanding or the loop refused; nothing changes.</returns>
        public bool RequestContinue()
        {
            if (RequestedScene is not null)
            {
                return false;
            }

            Result allowed = _flow.TryGo(_content.Target);
            if (allowed.IsFailure)
            {
                LastError = "the step refused to leave: " + allowed.Error.Code;
                // A refusal is the loop answering, not a bug: warn, so a test run and a player log
                // are not filled with errors for a press the rules were designed to refuse.
                Debug.LogWarning(LastError);
                return false;
            }

            LastError = string.Empty;
            RequestedScene = _content.Target;
            return true;
        }
    }
}
