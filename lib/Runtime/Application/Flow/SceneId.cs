namespace ProjectJianghu.Core.Application.Flow
{
    /// <summary>
    /// The scenes the shell can show, in the order a session visits them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A scene name is an Application concept, not a rule: Domain never needs to know that a
    /// "Hub" or an "Encounter" exists, while Presentation and the scene loader do. That is why
    /// this enum lives here rather than in <c>Domain</c> — see
    /// <c>docs/template/inventory.md</c> (row <c>SceneId</c>).
    /// </para>
    /// <para>
    /// The game's loop is
    /// <c>Boot → Title → CharacterCreate → Bracket → Rest ⇄ Arena → Result → Title</c>. The shape
    /// is the template's: one place where a run is born, one where it is prepared, one where it
    /// is played, one where it is summarised.
    /// </para>
    /// </remarks>
    public enum SceneId
    {
        /// <summary>Composition root only; never shown to the player.</summary>
        Boot = 0,

        /// <summary>Where a run is born (ADR 0022) and where arriving ends one.</summary>
        Title = 1,

        /// <summary>Where a run's character is made: background, attributes, starting skill.</summary>
        CharacterCreate = 2,

        /// <summary>The tournament bracket: who fights whom in this round.</summary>
        Bracket = 3,

        /// <summary>The rest house between rounds: recover, learn, keep the loadout.</summary>
        Rest = 4,

        /// <summary>One unit of play, stepped by <c>FixedTicker</c>.</summary>
        Arena = 5,

        /// <summary>The run's summary, reachable from Rest and Arena.</summary>
        Result = 6,
    }
}
