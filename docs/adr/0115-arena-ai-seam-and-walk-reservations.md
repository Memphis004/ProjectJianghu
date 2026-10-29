# ADR 0115 — The AI seam, and what a walker owns

- **Status:** Accepted
- **Date:** 2026-10-30
- **Stage:** 07
- **Supersedes:** nothing
- **Related:** [ADR 0114](0114-arena-simulation-tick-resolve-and-damage.md) (the tick),
  [ADR 0113](0113-grid-geometry-anchor-direction-and-size.md) (occupancy is a mirror),
  GDD §1.6 (four AI archetypes), `llmwiki/systems/arena-simulation.md`

## Context

Stage 07 has to run a match with no human at the controls — the tournament resolves other
people's matches headless (GDD §1.7) — while the real utility AI is Stage 08's work. That needs a
seam: one interface the placeholder and the real AI both implement, and a rule for how a decision
made mid-tick sees the state that other fighters have already changed.

The first attempt produced two defects that only showed up when 31 authored fighters were actually
run against each other, which is why the decisions below are recorded rather than left in the
code:

- **Two fights that never started.** With `stat.move_range` at its old value (12 tiles), two
  fighters of equal reach walked to the tile nearest the other's *start of turn* position, crossed
  mid-path, and repeated that until the 120-second limit. 13 of 200 matches ended with **zero**
  attacks.
- **Two bodies, one tile.** Nothing reserved a walker's destination, so two fighters could be
  routed into the same tile in one tick and end the tick standing on the same square — measured as
  2,822 shared-tile ticks across 20 matches.

## Decision

1. **`IArenaAiHook` is the seam.** The simulation asks it once per ready, non-player-controlled
   fighter per tick, hands it the tick's live frame and an `IArenaQuery` that answers the same
   questions the player's UI asks, and expects one `ArenaCommand` in return. The hook never calls
   back into the session: the simulation validates what comes back through the same
   `ValidateCommand` path a player's command takes, then applies it itself.
2. **The hook is asked with the player-control check off.** Every fighter it speaks for is AI by
   definition, so `RejectedNotControlled` would make the seam unusable. The public
   `IArenaSession.Query`/`Submit` still enforce it; the contract suite checks that they do.
3. **A hook that cannot think does not stall the match.** A `false` return, or a command that fails
   validation, becomes `Meditate` — always legal for a ready fighter, and it gives the turn back
   honestly instead of freezing the match.
4. **Fighters are asked in setup order, and the live frame is refreshed after each of them acts.**
   The frame a later fighter sees carries the earlier fighter's new state. Without this, decisions
   in one tick were made against a snapshot the earlier commands had already invalidated.
   `refresh` is an in-place update of the live frame (`UpdateCurrent`), not a second publish: a
   second publish would push the real previous tick out of the history the view interpolates from
   (ADR 0012 provides the buffer, `ArenaFrameBuffer` keeps the rule).
5. **A walker owns its whole remaining route.** While a fighter is `Moving`, the occupant map holds
   its current tile **and every tile left on its path**; a jumper holds its landing tile. Any walk,
   jump, knockback or reachability query in the same tick therefore sees those tiles as taken and
   routes around them. This is what makes "one fighter per tile" an invariant of the simulation
   rather than a hope about the AI: measured 0 shared-tile ticks after the change.
6. **The placeholder is greedy and honest about it.** `GreedyArenaAiHook` casts the first skill
   that validates against any enemy, then a self-targeted skill, then steps to the reachable tile
   closest to the nearest enemy, then meditates. It scores nothing, ignores `ai_profile`'s
   `engage_range`/`retreat_hp_x100`/`aggression_x100`, and allocates nothing: its tile buffer is
   built once in the constructor, because the hook runs inside the tick.
7. **The archetype row is already wired, if not used.** `AiProfileId` and `AiReactionTicks` reach
   the setup from `fighter.ai_profile_id` and `ai_profile.decision_delay_ticks`, so Stage 08
   replaces the hook without touching the contract, the session or the setup block.
8. **A `Move` is an action that takes ticks, and the contract suite was amended to say so.**
   `Submit` answers `Ok` when the order is accepted; the fighter arrives after `PathCount` ticks,
   one tile per tick. The Stage-02 contract never stated whether a move lands inside the
   submitting tick, and the fake session resolved it instantly, so
   `ArenaSessionContractTests.Every_tile_the_query_reports_is_accepted_and_tiles_outside_are_not`
   asserted the arrival position instead of stepping. It now steps until arrival (and passes on
   both sessions, because the fake arrives in the submitting tick and the loop runs zero times).
   The fake was **not** given a walk: arriving instantly is still legal — the contract is "a walk
   is a legal order and the fighter ends on the tile", not "in one tick" — and the rule that a
   real session genuinely needs is in §5. A view must therefore interpolate from the frame
   (`PrevX`/`PrevY`), never assume the tile moved because a command was accepted.

## Alternatives considered

- **Let the AI call the session directly (`session.Submit`).** Rejected: it makes the tick
  re-entrant, and a hook could end the match from inside its own decision loop.
- **Give the hook a copy of the frame.** Rejected: it is an allocation per fighter per tick in the
  hot path, and the live frame already exists for exactly this purpose.
- **Decide for all fighters, then apply all commands.** Rejected: two fighters would be told to
  walk into the same tile with no chance to react, which is the defect this ADR records. Sequential
  decision + apply, with the frame refreshed, is the cheap correct order.
- **Reserve only the destination tile.** Rejected: two walkers can still cross through a shared
  intermediate tile and swap places, which reads as teleporting and breaks the tile invariant on
  the way through.
- **Stop a walk that finds its destination taken.** Rejected for now: with reservations the case
  cannot arise; a re-path here would be a rule change with no test to justify it.
- **Make the AI hook a delegate or a `Func<>`.** Rejected: an interface can be registered in
  VContainer and faked in a test without a closure.

## Consequences

- **Positive:** the tournament can resolve any pairing headless with the real session, no
  Unity, no presentation and no per-match allocation; the seam is one interface Stage 08 fills.
- **Positive:** the tile invariant now holds for players, AI and knockback alike, because it lives
  in the occupant map the queries already read (ADR 0113 §6).
- **Negative:** the placeholder AI does not retreat, does not use reach, does not value its skills,
  and will happily trade blows with anyone; its output is a *legal* match, not a *good* one. Win
  rate by power rank still separates (43 % → 63 %) because the underlying numbers do.
- **Negative:** reserving the whole remaining route makes a long walk hold most of a small board
  for its duration. On the 12×12 prototype map with 4-tile walks that is 4 tiles; a future skill
  that lets someone walk 12 tiles would hold twelve.
- **Negative:** the decisions are sequential, so the fighter with the lower setup index acts first
  every tick. That is a real, if small, fairness asymmetry — and it is deterministic, which is what
  a replay needs.
- **Negative:** the contract suite now carries a "step until arrived" helper, so a session that
  arrives in one tick and a session that walks are both legal and only presentation notices the
  difference. A future rule that depends on arrival timing must go through the frame, not the
  return value of `Submit`.
- **Verification:** the shared 11-fact contract suite runs against the real session with real
  generated tables (`ArenaSimulationContractTests`); the 40-match sweep in `tools/test.ps1` fails if
  a batch of authored matches stops resolving; the 500-match `artifacts/balance.csv` run is the
  evidence for the two defects above being fixed.
