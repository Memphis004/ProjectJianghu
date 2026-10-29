# Stage 02 — ArenaBattle Contract v1

## 1. Summary

The arena seam is in place: `lib/Runtime/Domain/Arena/` now holds the whole contract from
the GDD (constants, phases, `GridCoord`, commands/results, event ring source, frame/query,
setup blocks, result types, session/factory/auto-resolver), and it names no engine, R3,
VContainer or UniTask — a guard test inspects the public types and the sources. The shared
`ArenaFrameBuffer` publishes Prev/Cur ticks and rewrites the current slot after a mid-tick
command, so the fake and the Stage 07 simulation can use one frame implementation.
`Application/Arena/Fake` contains a fully deterministic fake (constant ATB, instant
move/jump, flat-damage skills, scripted endings), `FakeAutoResolver`, and `ArenaReplay`
which reproduces a match's `ComputeStateHash` through any factory. The contract suite is
generic over `IArenaSessionFactory`: **11 contract facts**, plus 4 fake-specific and 2
frame-buffer cases (**17 arena tests**), including 5,000 random commands where `Validate`
must equal `Submit`, a 600-tick zero-allocation measurement, a 2,048-event ring overflow,
and replay hash equality. `EventRing<T>` already existed as the
generic Shared ring (ADR 0024), so item 2 needed no refactor and no new ADR.

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Create the contract files in `lib/Runtime/Domain/Arena/` (namespace `ProjectJianghu.Core.Domain.Arena`) | **PASS** | 10 files: `ArenaConstants`, `ArenaEnums`, `GridCoord`, `ArenaCommand`, `ArenaEvent`, `ArenaFrame`, `ArenaFrameBuffer`, `ArenaSetup`, `ArenaResult`, `IArenaSession`. Public shapes match the GDD; `IntroTicks = 30` is the one addition, documented in the constant and in `arena-contract.md` §2. |
| 2 | `EventRing<T>` in Shared; refactor a template `BattleEventRing` to generic + ADR if present | **PASS** | `lib/Runtime/Shared/EventRing.cs` is already the generic ring (ADR 0024, `EventRingTests` 9 cases). There is no `BattleEventRing` in the tree, so nothing was refactored and no ADR was written. The arena adapts it in `FakeArenaSession.Read`. |
| 3 | `ArenaFrameBuffer`: concrete `IArenaFrame`, double-buffered Prev/Cur `FighterView`, cooldown array | **PASS** | `Domain/Arena/ArenaFrameBuffer.cs`: three `FighterView` slots (write/current/previous) swapped on `Publish`, two cooldown tables, `UpdateCurrent`/`UpdateCurrentCooldowns` for a mid-tick `Submit`. `ArenaFrameBufferTests` (2 cases) proves the swap keeps Prev intact and the in-place update does not touch it. |
| 4 | `FakeArenaSession`/factory in `Application/Arena/Fake`: constant ATB, warped Move/Jump, flat-damage skill, `WinAtTick`/`LoseAtTick`/`TimeoutAtTick`, empty-grid Manhattan query | **PASS** | `FakeArenaSession`, `FakeArenaSessionFactory`, `FakeArenaQuery`, `FakeArenaOptions`; `FakeArenaSessionTests` covers all three scripted endings. The query is an empty 12×12 grid with Manhattan reachability/jump/range; a fake skill resolves on one target tile. |
| 5 | `FakeAutoResolver`: result depends on seed | **PASS** | `FakeAutoResolver` forces every fighter onto AI, steps to the end or the tick budget, and falls back to the HP-ratio timeout. `AutoResolver_is_reproducible_for_one_seed_and_varies_by_seed`: two runs of seed 7 match on outcome/reason/duration/per-fighter HP/damage; 20 seeds produce more than one digest. |
| 6 | `ArenaReplay`: replay through any factory and compare `ComputeStateHash` | **PASS** | `Application/Arena/ArenaReplay.cs` (`ArenaReplayCommand` = tick + command + recorded result). `Replay_reproduces_the_state_hash_and_event_sequence_is_continuous` records 120 ticks of accepted commands and asserts the replayed hash equals the original. |
| 7 | `ArenaSessionContractTests<TFactory>`: intro ATB/WrongPhase, NotReady/NotControlled, Wait/Active, 5,000-command Validate==Submit, reachable tiles, timeout HP%, surrender/dev, replay+Seq+dropped, zero-alloc | **PASS** | `lib/Tests~/Core.Tests/Arena/ArenaSessionContractTests.cs` (11 facts) + `FakeArenaSessionContractTests` (the concrete binding) + `FakeArenaSessionTests` (4 fake-specific) + `ArenaFrameBufferTests` (2) = **17 passed, 0 failed**. The random test runs 5,000 commands (including AI actors, invalid ids/slots/tiles, Surrender and dev commands) and asserts `Validate` twice, then `Submit`, on every one. `Every_tile_the_query_reports…` creates a fresh session per returned tile and submits the Move. The zero-alloc fact passes over 600 measured iterations after 600 warm-up. |
| 8 | Architecture test: public `Domain.Arena` types do not reference UnityEngine/R3/VContainer | **PASS** | `ArchitectureTests.Arena_contract_public_types_are_engine_free`: reflects every public type in `ProjectJianghu.Core.Domain.Arena`, walks base types, interfaces, fields, properties, method/constructor parameters and generic arguments for `UnityEngine*`, `UnityEditor*`, `R3*`, `VContainer*`, `Cysharp*`, and scans `Runtime/Domain/Arena/**` sources after comment stripping. `dotnet test` 156/156. |
| 9 | `llmwiki/systems/arena-contract.md`: DefId/Value/Value2 per event, span lifetime, "events are VFX/audio/UI only" | **PASS** | New system note: 20-event table with Source/Target/DefId/Value/Value2/Tile/Flags, which rows the fake emits, the Seq/ring/dropped rules, a span-lifetime table, and §4.3's three reasons a rule may never read the ring. Linked from `llmwiki/index.md`. |
| **DoD-1** | Contract suite passes against the fake, every fact | **PASS** | `dotnet test --filter "FullyQualifiedName~ProjectJianghu.Core.Tests.Arena"` → **17 passed, 0 failed**; whole project **156 passed, 0 failed** (138 before). |
| **DoD-2** | Unity compiles with no new warning | **PASS** | Editor `6000.3.9f1` open: the new files imported and generated their `.meta` (ignored for `lib/`, per `.gitignore`), `console_status` → `compilationFailed: false`. After clearing the buffer, `recompile` answered `up_to_date` and the console holds 0 errors, 0 warnings. `tools/test.ps1` Fast ran Unity EditMode **32/32**. |

## 3. Files changed

| Area | Files |
| --- | --- |
| Domain | `lib/Runtime/Domain/Arena/` (new): `ArenaConstants.cs`, `ArenaEnums.cs`, `GridCoord.cs`, `ArenaCommand.cs`, `ArenaEvent.cs`, `ArenaFrame.cs`, `ArenaFrameBuffer.cs`, `ArenaSetup.cs`, `ArenaResult.cs`, `IArenaSession.cs` (+ Unity `.meta` files) |
| Application | `lib/Runtime/Application/Arena/ArenaReplay.cs` (new); `Application/Arena/Fake/`: `FakeArenaOptions.cs`, `FakeArenaSessionFactory.cs`, `FakeArenaSession.cs`, `FakeArenaQuery.cs`, `FakeAutoResolver.cs` (new) |
| Tests | `lib/Tests~/Core.Tests/Arena/ArenaTestSetup.cs`, `ArenaSessionContractTests.cs`, `FakeArenaSessionContractTests.cs`, `FakeArenaSessionTests.cs`, `ArenaFrameBufferTests.cs` (new); `ArchitectureTests.cs` (+ the arena guard) |
| Docs | `llmwiki/systems/arena-contract.md` (new), `llmwiki/index.md`, `llmwiki/stage-log.md`, `knowledge.md`, this report |

No generated file, data table, proto, scene, prefab or Unity asset was touched.

## 4. Commands actually run

```powershell
dotnet build "lib/Tests~/ProjectJianghu.Core.sln" -c Debug        # 0 warnings, 0 errors
dotnet test "lib/Tests~/Core.Tests" --filter "…Core.Tests.Arena" # 17 passed, 0 failed
dotnet test "lib/Tests~/Core.Tests"                              # 156 passed, 0 failed
pwsh -NoProfile -File tools/test.ps1                             # PASS 9 | FAIL 0 | NOT RUN 3 | 126 s
# Unity 6000.3.9f1 (Editor open)
#   clear_console -> recompile: "up_to_date"; compilationFailed = false; 0 errors, 0 warnings
#   unity: EditMode (inside the Fast run): 32/32 passed
node tools/check-links.mjs                                       # 282 links in 68 files: all resolve
```

`Full` was **NOT RUN**: it needs the Editor closed (WebGL build + Playwright) and this
stage changed no screen or player. The three NOT RUN rows are Full-only steps by design.

## 5. Not tested, and risks

- **The fake is a fake.** It has no crit/evade/counter/knockback/statuses and always hits.
  `Evaded`, `Countered`, `KnockedBack`, `StatusApplied` and `StatusExpired` are contract
  rows the fake never emits; `arena-contract.md` marks which rows the fake emits so Stage
  07 knows what to fill in, but the contract suite never sees those paths exercised.
- **`IntroTicks` is an addition.** The attached contract had an `Intro` phase but no
  length. It is a shared constant (30) and the suite exposes `protected virtual
  IntroTicks`; Stage 07 must either honour it or move it into the `constant` table and
  override the value in its test subclass. Moving it to data would change the contract
  test base, not the session API.
- **Fighter ids are capped at 8.** The frame's cooldown table is indexed by id, so
  `FakeArenaSession` rejects an id at or above `MaxFighters`. The contract says "fighter
  id" but does not say the cap; the real factory must enforce the same rule or change the
  frame buffer before teams grow.
- **The seed only shifts the first turn.** The fake derives each fighter's initial ATB
  from the seed (0–12.5%). With `AtbFillTicks` of 8 or less that is at most one tick of
  difference, so seed variation is weak in a very fast match (the resolver test uses 20
  ticks). The real simulation's AI/random stream is where seed variety will properly live.
- **The AI bypasses the player-control gate.** `FakeArenaSession` validates the player's
  commands with `enforcePlayerControl: true` and its own AI's with `false`. A simulation
  that reuses `Validate` verbatim for AI commands must make the same split, or its AI will
  reject every action as `RejectedNotControlled`.
- **Replay is not persisted yet.** `ArenaReplay` takes an in-memory command list; the
  protobuf `ArenaReplay` message (Stage 01) has no reader/writer until Stage 10/13. The
  replay test proves the contract, not the wire format.
- **`ComputeStateHash` is per implementation.** The contract only requires two runs of
  the same factory + setup + commands to match. Cross-factory hashes (fake vs simulation)
  are intentionally not compared and are not promised.
- **Full/PlayMode and the browser were not run.** Nothing in this stage touches a scene,
  so the risk is limited to a new public API being unused by the shell; the Editor compile
  and EditMode suite are the Unity evidence for now.

## 6. Decisions

No new ADR: the two decisions that could have needed one were already covered.
`EventRing<T>` was already generic (ADR 0024), and the frame double-buffer follows ADR
0012. The `IntroTicks` addition and the field meanings are recorded in
[`llmwiki/systems/arena-contract.md`](../../llmwiki/systems/arena-contract.md).
