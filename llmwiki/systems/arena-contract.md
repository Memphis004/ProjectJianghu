# Arena contract — ArenaBattle v1

The rules/presentation seam of the game. Everything under
`lib/Runtime/Domain/Arena/` (namespace `ProjectJianghu.Core.Domain.Arena`) is the
contract: the fake session, the Stage 07 simulation, the Stage 09 runner and the
Stage 10 tournament all speak it. It may not name UnityEngine, R3, VContainer or
UniTask; `ArchitectureTests.Arena_contract_public_types_are_engine_free` enforces
that over the public types *and* their sources.

- Source of truth: [`docs/gdd/GDD.md`](../../docs/gdd/GDD.md) part 2.
- Layer rules: [`docs/architecture/overview.md`](../../docs/architecture/overview.md).
- Fake used by this stage: `lib/Runtime/Application/Arena/Fake/`.
- Shared frame buffer: `lib/Runtime/Domain/Arena/ArenaFrameBuffer.cs`.
- Shared event ring: `Shared.EventRing<T>` ([ADR 0024](../../docs/adr/0024-shared-generic-buffers.md)).

---

## 1. Shape in one picture

```text
              Submit(ArenaCommand)                Step() 30 Hz
Controller  ────────────────────────►  Session  ─────────────────►  Frame (Prev/Cur)
   │                                     │  │                         │
   │ Query.Validate / tile highlights ◄──┘  └──► EventRing<ArenaEvent> │
   │                                          (VFX, audio, UI only)    ▼
   └──────────────────────────────────────────────────────────  Presentation
```

- `Step()` never takes input; all input is `Submit`.
- `Query.Validate` must return exactly what `Submit` would return at the same
  state, and neither changes state while answering.
- `Submit` is atomic: `Ok` means the whole command applied; any other value
  means no state changed.
- `ComputeStateHash` is the determinism check. Replay a command stream through
  any factory and the hash must match.

## 2. Ticks, phases and clocks

| Concept | Rule |
| --- | --- |
| Tick rate | 30 Hz (`ArenaConstants.TickRate`) |
| `Tick` | counts every `Step`, intro included |
| `ElapsedTicks` | counts only after the intro; the time limit uses it |
| Intro | `ArenaConstants.IntroTicks` (30). ATB does **not** fill and every command is `RejectedWrongPhase` |
| `Running` | ATB fills, AI acts, commands are accepted from ready fighters |
| `AwaitingPlayer` | `Wait` mode only: the simulation stops until the ready player's command is accepted; `Step` does not change `Tick` |
| `Finished` | terminal; `Step` is a no-op and `Submit` returns `RejectedWrongPhase` |
| Time limit | `ArenaMatchSetup.TimeLimitTicks`; 0 or less = no limit |

`FakeArenaOptions` adds the fake-only staging knobs `WinAtTick`, `LoseAtTick`
and `TimeoutAtTick` (elapsed ticks). They are not part of the contract; the
real simulation gets its endings from play.

The fake is a stand-in for the **match**, not for the **geometry**: since Stage 06
its walk query, jump query, skill preview, hit area, command validation and facing all
come from `Domain.Arena`'s `ArenaPathfinder`/`ArenaArea`/`ArenaFacing` over one
`ArenaGrid`, so a highlight and the command that later validates cannot disagree.
What is still fake is combat — no crit, evade, counter or knockback — and Move/Jump
arrive in a single tick. See [`grid-and-targeting.md`](grid-and-targeting.md).

## 3. Fighter ids

`ArenaCommand.Actor`, `ArenaEvent.Source/Target`, `IArenaFrame.PlayerFighter`
and `IArenaQuery`'s fighter parameter all carry a **fighter id**
(`ArenaFighterSetup.Id`), never an array index. Ids must be unique and below
`ArenaConstants.MaxFighters` (8) because the frame's cooldown table is indexed
by id. `ArenaConstants.NoFighter` (255) means "none".

## 4. Events

### 4.1 Sequence and delivery

- `ArenaEvent.Seq` is assigned by `Shared.EventRing<ArenaEvent>`: strictly
  increasing, never reused, starting at 0. The ring capacity is
  `ArenaConstants.EventRingCapacity` (2048).
- Each reader owns its cursor. `Read(ref cursor, span, out dropped)` reports how
  many events were lost to overwrite instead of hiding the loss. A reader may
  fall behind and lose events; the match state is unaffected.
- `IArenaEventSource.LatestSeq` is the sequence number of the newest event, not
  an emptiness test. A fresh reader starts at cursor 0.
- `dropped` is diagnostic: a dropped event is a missing sparkle, never a
  missing rule.

### 4.2 Field meanings

`Source`/`Target` are fighter ids or `NoFighter`; `Tile` is `GridCoord.Invalid`
when the event has no place. The fake emits the rows marked **yes**; the rest
are the contract Stage 07 fills in.

| Event | Fake | Source | Target | `DefId` | `Value` | `Value2` | `Tile` | Flags |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `MatchIntro` | yes | — | — | 0 | intro ticks | 0 | Invalid | — |
| `MatchStarted` | yes | — | — | 0 | 0 | 0 | Invalid | — |
| `AtbReady` | yes | fighter | fighter | 0 | ATB (10000) | 0 | fighter tile | — |
| `CommandAccepted` | yes | actor | — | 0 | `(int)ArenaCommandKind` | slot | target tile | — |
| `MoveStarted` | yes | actor | — | 0 | Manhattan distance | 0 | destination | — |
| `MoveEnded` | yes | actor | — | 0 | Manhattan distance | 0 | destination | — |
| `JumpStarted` | yes | actor | — | 0 | Manhattan distance | 0 | landing tile | — |
| `Landed` | yes | actor | — | 0 | Manhattan distance | 0 | landing tile | — |
| `SkillWindUp` | yes | caster | target fighter / — | skill id | slot | wind-up ticks | area centre | — |
| `SkillCast` | yes | caster | target fighter / — | skill id | flat damage | 0 | area centre | `Inner`, `Ultimate` |
| `Hit` | yes | attacker | victim | skill id | damage dealt | target HP after | victim tile | `Critical`, `Backstab`, `Flank`, `Inner`, `Ultimate` |
| `Evaded` | no | attacker | target | skill id | 0 | target HP | target tile | — |
| `Countered` | no | counter-attacker | original attacker | counter skill id | damage dealt | target HP after | target tile | — |
| `KnockedBack` | no | pusher | pushed | 0 | tiles pushed | 0 | landing tile | — |
| `StatusApplied` | no | applier / — | recipient | status id | duration ticks | 0 | recipient tile | `Inner` when internal |
| `StatusExpired` | no | — | recipient | status id | 0 | 0 | recipient tile | — |
| `NeiliRecovered` | yes | fighter | fighter | 0 | amount gained | new total | fighter tile | — |
| `FighterDefeated` | yes | — | victim | 0 | 0 | 0 | victim tile | — |
| `TimeWarning` | yes | — | — | 0 | ticks remaining | 0 | Invalid | — |
| `MatchEnded` | yes | — | — | 0 | `(int)ArenaEndReason` | winner team / `NoFighter` | Invalid | — |

Notes that the table cannot carry:

- `SkillWindUp` is the telegraph. The full area is
  `Query.PreviewSkillArea(caster, slot, Tile)` — never reconstruct it from the
  event's tile alone.
- `SkillCast` is the cut-in/impact moment, not the damage decision; `Hit`
  carries the numbers. The fake resolves both in the same tick (it has no
  wind-up), the simulation separates them by time.
- `TimeWarning` is emitted once, when 30 s remain.
- `MatchEnded` is the last event of the match. The presentation must also call
  `TryGetResult`, which is the authoritative outcome.
- Flags are hints for the reaction (crit shake, cut-in); a missing flag never
  changes the damage already reported in `Value`.

### 4.3 The rule: events are for VFX, sound and UI only

**No rule may read the event ring.** Damage, death, turn order, the result, the
save hash and every AI decision are computed from the session's own state and
the contract's getters. The event ring is a one-way presentation feed.

Reasons this is not negotiable:

1. Events are **droppable** by design; a reader that falls behind loses the
   oldest ones. A rule that read them would change with the frame rate.
2. A cursor belongs to a reader. Two readers (HUD and VFX) must not affect each
   other, and neither may affect the simulation.
3. The save/replay hash must not depend on how many events someone consumed.

So a subscriber may animate, play a sound or show a damage number; it may not
apply damage, spend inner power or decide that a fighter died.

## 5. Span lifetimes

| Span | Lifetime | What to do |
| --- | --- | --- |
| `IArenaFrame.Fighters` | until the next `Step`/`Submit` | copy before stepping (`llmwiki/gotchas/copy-frame-before-step.md`) |
| `IArenaFrame.SkillCooldowns(id)` | same as the frame | copy if kept past the tick |
| `IArenaQuery` output spans | caller-owned | valid until the caller writes them again |
| `IArenaEventSource.Read` destination | caller-owned | the copied `ArenaEvent`s are stable; a ring slot is overwritten after 2048 newer events |
| `ArenaCommand` | value struct | safe to store in a replay entry |

`ArenaFrameBuffer` publishes two ticks (`Current` and `Previous`) and swaps
slots instead of copying. An accepted `Submit` rewrites the current slot in
place, deliberately without publishing a second time: a second publish in one
tick would push the real previous tick out of the history.

## 6. What the contract suite guarantees

`lib/Tests~/Core.Tests/Arena/ArenaSessionContractTests<TFactory>` runs the same
facts against any factory:

- intro does not fill ATB and rejects commands;
- not-ready and AI-controlled commands are refused;
- Wait mode stops the clock; Active mode does not, and the AI keeps acting;
- `Query.Validate` equals `Submit` over 5,000 random commands;
- every tile a query reports is accepted, tiles outside are not;
- timeout compares HP percentage and ties draw;
- surrender aborts, dev commands are refused without cheats;
- replay reproduces the hash, event `Seq` is continuous, dropped events do not
  change state;
- `Step`, `Submit` and query paths allocate nothing after warm-up.

Stage 07 adds `ArenaSessionContractTests<ArenaSessionFactory>` and must not
weaken a fact to make the simulation pass. If the contract itself is wrong, write
an ADR and change the contract *and* the fake together.
