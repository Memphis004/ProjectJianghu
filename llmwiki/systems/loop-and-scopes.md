# The loop: three scopes, seven screens, one run

The game plays end to end (Stage 03):

**Boot → Title → CharacterCreate → Bracket → Rest ⇄ Arena → Result → Title**

Nothing is simulated yet: `CharacterCreate` and `Bracket` are placeholder steps, and the
Arena's "progress" is the run's encounter counter. What the loop does provide is the thing
a game would otherwise get wrong first — **where a run lives**, and what happens when a
scene changes or a run ends. The template's own five-screen shape (Hub ⇄ Encounter) is
what it was built from; [`../../docs/stages/03-report.md`](../../docs/stages/03-report.md)
records the rename and the additions.

| Piece | File | What it owns |
| --- | --- | --- |
| App scope | `Assets/Main/Scripts/Presentation/RootLifetimeScope.cs` | the shell: config, localization, clock, `SceneLoader`, `RunScopeOwner`. Persists across scenes (`SceneBoot.PersistAcrossScenes`) |
| Run scope | `Assets/Main/Scripts/Presentation/Flow/RunLifetimeScope.cs` (+ `RunScopeOwner`) | exactly one `RunSession` — seed, streams, encounter counter, command log |
| Scene scope | `RestSceneScope` / `ArenaSceneScope` / `StepSceneScope` / `ResultSceneScope` | that scene's presenter, UI state and subscriptions |

The rule that makes the loop work is **a child scope never re-registers the shell,
and a scene never creates a run**. `RootLifetimeScope.Configure` returns early when
it has a parent, so a scene scope's `Resolve<RunSession>()` comes from the App scope's
run scope — the same object across every crossing.

## 1. Leaving a scene is one place

`Presentation.Flow.SceneLoader` is the only way a scene changes: one transition at a
time, behind a fade, with a typed refusal (`transition.in_progress`,
`transition.scene_missing`, `transition.load_failed`, `transition.disposed`) instead
of a half-applied move. Two behaviours are load-bearing:

- **A scene that is not in the build cannot be loaded** — checked *before* the fade,
  so a refusal leaves the player on the screen they were on.
- **Arriving at `SceneId.Title` disposes the Run scope.** "Returning to the title
  disposes the run" is a property of the transition, not something each screen
  remembers — which is what makes "the title has no stale run" a one-line assertion.

A director asks for a departure **after** it has committed the domain change, never
before: the Arena records the round's outcome and only then asks to leave; the Result
screen records the player's intent and leaves once the loader accepted it.

`SceneFlow` (Application) is the *policy* half — which edges exist and whether the run
is in a state that allows one. `SceneLoader` is the *mechanism* half. Keeping them
apart is what lets `SceneFlow` be tested with `dotnet test` and `SceneLoader` be the
only thing that touches `SceneManager`.

## 2. The run loop

1. the Title starts a run (`RunSession.Start`) and asks for `CharacterCreate`;
2. `CharacterCreate` and `Bracket` are placeholder steps: each shows its three localized
   strings and asks for the next scene (`Bracket`, then `Rest`);
3. the Rest house opens a round (`BeginEncounter`) and departs for the Arena;
4. the Arena ends the round (`EndEncounter(outcome)`): a cleared round raises the
   counter, a lost one finishes the run;
5. the director chooses the destination from the outcome — `Result` when the run is
   over, `Rest` otherwise;
6. `Result` either starts a new run (`CharacterCreate`) or returns to the Title, which
   disposes the run scope.

**The departure is asked for repeatedly, and that is deliberate.** A screen can end
while a transition is still in flight, and nothing else will ask again, so the
director retries every frame and logs the first refusal only. A single attempt would
strand the player on a finished screen.

## 3. The three published documents

A WebGL player cannot run Unity's tests, so each screen publishes what it drew and
where its controls are:

| Global | Screen | Contents |
| --- | --- | --- |
| `__projectJianghuTitle` | Title | the two localized strings, build line, canvas size, one `start` control with its centre in canvas pixels |
| `__projectJianghuCharacterCreate` | CharacterCreate | the heading, the one line, the `continue` control |
| `__projectJianghuBracket` | Bracket | the heading, the one line, the `continue` control |
| `__projectJianghuRest` | Rest | cleared-round count, the two ways out (`depart`, `finish`), their centres |
| `__projectJianghuArena` | Arena | the round's progress line, `win` and `lose`, their centres |
| `__projectJianghuArenaSpike` | Arena (spike) | quality tier, measured FPS, the sorting probe, the camera, the three tier buttons |
| `__projectJianghuResult` | Result | whether the run finished, rounds cleared, the two controls |

`__projectJianghuScene` is published separately by every scene's director. The names
have one home per side (`WebGlBridge` constants and `tests/web/globals.mjs`), and
`WebGlBridgeTests` fails when they drift.

The browser suite clicks **the point a screen published**, never a hard-coded
coordinate, and asserts the next screen's own document. That is why `loop.spec.ts`
can walk the whole loop without knowing the layout.

## 4. Facts that cost time to learn

- **Frame boundary between `Screen` and `CanvasScaler`.** A report written in the
  frame a canvas resizes carries the new size with the previous geometry. See
  [`../gotchas/layout-report-one-frame-early.md`](../gotchas/layout-report-one-frame-early.md).
- **The scaler's virtual width follows the aspect.** At the default 960×600 canvas
  the reference rect is 1821.6 × 1138.5, so 1920-wide arithmetic is off-centre. See
  [`../gotchas/canvas-scaler-follows-aspect.md`](../gotchas/canvas-scaler-follows-aspect.md).
- **A scene must publish its name.** A missing marker looks like a boot failure. See
  [`../gotchas/scene-must-publish-name.md`](../gotchas/scene-must-publish-name.md).
- **The fade overlay needs an EventSystem, and it must die with the transition.** The
  game era's overlay fallback was created on the `DontDestroyOnLoad` canvas and never
  taken down, so every later scene carried two EventSystems and Unity logged a
  warning every frame — a linear 314 KB per scene change, ~5.4 MiB over 18 crossings.
  It is created and destroyed inside the transition now.

## 5. What a game replaces

| Placeholder | A game's version |
| --- | --- |
| Template `Hub` | a rest area, a shop, a loadout screen |
| Template `Encounter` | its real play loop (a battle, a puzzle, a stage) behind `FixedTicker` |
| `Result` | its own summary and rewards |
| `RunSession`'s four commands | its own command set, same atomic/typed shape (AGENTS rule 7) |
| `EncounterOutcome` | its own outcomes |

The shape to keep is: **one run, one loader, one place per scope that creates and
disposes it.**

## 6. The Jianghu mapping (Stage 00)

The placeholder loop becomes the tournament loop. Five placeholder slots turn into six
screens: `Hub` splits in two, because "between two matches" and "between two rounds"
are different waits with different data.

**Boot → Title → CharacterCreate → Bracket → Rest ⇄ Arena → Result → Title**

(Stage 03 implements exactly these edges. Stage 10 adds the bracket's real draw and the
round loop; the placeholder `Bracket` is a step, not yet a screen the Arena returns to.)

| Template placeholder | Jianghu screen | `SceneId` | Lands in | What it inherits |
| --- | --- | --- | --- | --- |
| `Boot` | unchanged | `Boot` | already | persistent `SceneBoot` |
| `Title` | unchanged: start / continue | `Title` | already | starts the run (ADR 0022) and asks the loader to leave |
| — (new) | **CharacterCreate** — pick one of four `Background`s, spend `AttributePoints`, pick a starting skill | `CharacterCreate` | Stage 03 (placeholder), Stage 11 (UI) | the run already exists; this screen writes the run's `CharacterState` |
| `Hub` (half) | **Bracket** — the 32-seat draw, who plays whom, and the background matches resolving | `Bracket` | Stage 03 (placeholder), Stage 10 (real) | a between-round screen: it waits on `IArenaAutoResolver` progress, so it can stay on screen across frames |
| `Hub` (half) | **Rest** — HP and neili restored, mastery gained, pick 1 of 3 rewards, edit the loadout, read the character sheet | `Rest` | Stage 03 (placeholder), Stage 10 (rules), Stage 11 (UI) | a between-match screen: no waiting, one departure |
| `Encounter` | **Arena** — the ATB match itself, 1v1 on the iso grid | `Arena` | Stage 03 (fake session), Stage 09 (real sim) | the only screen that steps a session on a fixed ticker; its scope owns `IArenaSession` and must dispose it |
| `Result` | **Result** — round reached, run stats, skills that levelled | `Result` | already; content in Stage 12 | reset → new run, or return to Title (which disposes the run) |

The Arena placeholder's "progress is a number of rounds" becomes a real match; nothing
else about the shell changes. Two constraints carry over unchanged:

- **One run, created by the Title, disposed by arriving at it.** `CharacterCreate` and
  `Bracket` do not create a run; `Arena` does not create a session that outlives its
  scope.
- **One departure per screen.** `Bracket` and `Rest` each ask once; `Arena` asks after it
  has committed `ArenaMatchResult` — win → `Rest`, loss or timeout draw → `Result`
  (GDD §1.3: a draw in the tournament counts as the player's loss).

Each new screen publishes its own `__projectJianghu*` document in the same shape as the
five above (`WebGlBridge` constant + `tests/web/globals.mjs` row + a bridge test), because
the browser suite is the only check that sees a real player.

See also: [`../glossary.md`](../glossary.md#8-scenes-sceneid) for the names, and
[ADR 0100](../../docs/adr/0100-forked-template-and-upstream-sync.md) for why the game's
documents, not the template's, decide this.
