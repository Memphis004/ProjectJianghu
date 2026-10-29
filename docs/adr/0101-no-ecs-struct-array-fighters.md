# ADR 0101 — No ECS: the arena is a struct array of eight fighters

- **Status:** Accepted
- **Date:** 2026-10-29
- **Stage:** 00 (binding from Stage 02, where the contract declares the constants, and
  Stage 07, where the simulation is written)
- **Supersedes:** nothing
- **Related:** [GDD §2](../gdd/GDD.md) (`ArenaConstants`, `ArenaFighterSetup`)

## Context

The template shipped without a simulation engine: the EcsLite-based battle rules left
with the game the template was cut from
([`examples/gotchas/ecslite-pool-refs.md`](../template/examples/gotchas/ecslite-pool-refs.md)),
`Simulation` is an empty guarded layer, and no ECS package is in
`UnityProject/Packages/manifest.json` or `tools/versions.lock.json` today.

This game does need a simulation — ATB is a time-dependent system, and the tournament
auto-resolves background matches headless — so the question is open again. The GDD's
contract answers the size question before the first line exists:

| Constant (GDD §2) | Value | What it bounds |
| --- | --- | --- |
| `MaxFighters` | 8 | everything the simulation iterates |
| `MaxSkillSlots` | 4 | per-fighter cooldown arrays |
| `MaxGridSize` | 32 → `MaxGridTiles` 1,024 | occupancy and path buffers |
| `EventRingCapacity` | 2,048 | events per match |
| Prototype field | 2 fighters, 12×12 grid (144 tiles) | the actual match |

The workload per tick is therefore bounded and small: at most 8 fighters, at most 28
unordered pairs, at most 1,024 tiles scanned by a range query, 30 ticks a second. The
whole simulation must also run in a plain .NET process (`tools/test.ps1`'s xUnit and the
`Sim` harness) with zero allocations after warm-up, and be deterministic inside one
runtime (AGENTS #5).

## Decision

1. **No ECS package, no custom ECS.** The arena simulation stores its state in
   **fixed-size struct arrays** owned by the session:

   | State | Shape |
   | --- | --- |
   | fighters | `ArenaFighter[MaxFighters]` (AoS: HP, ATB, tile, facing, state, timers) |
   | occupancy | `byte[MaxGridTiles]` — `0` = empty, else `fighterId + 1` |
   | cooldowns | `ushort[MaxFighters * MaxSkillSlots]`, exposed as a `Span` slice per fighter |
   | walkable mask | `bool[MaxGridTiles]`, loaded once from `arena_map` |

   Iteration is an index loop over `0..FighterCount`, never `foreach`, LINQ or a
   delegate (AGENTS #7). A match's fighters are created once when the setup is applied
   and never added or removed during it.
2. **The buffers the presentation sees are not the simulation's arrays.** `ArenaFrame`
   is a `PrevCurBuffer<FighterView>` copy (ADR 0012), so a view can hold one frame while
   the simulation steps into the next.
3. **This is a rule about the arena, not about the whole game.** A future open-world
   layer with crowds may need a different structure; it does not get to change
   `IArenaSession`.
4. **The contract is the test.** `ArenaSessionContractTests<TFactory>` must pass for the
   fake and the real simulation with the same suite (GDD, Stage 02), so the storage
   choice is invisible above `IArenaSession`.

## Alternatives considered

- **EcsLite** (the engine the source game used). Rejected: 8 entities do not need
  archetypes, chunks or filtered queries; the engine adds a package, a pool-ref hazard
  and an IL2CPP stripping surface for a loop that fits in two nested `for`s. Its gotcha
  note is kept as an example, not as a starting point.
- **Unity DOTS / Entities.** Rejected outright: it pulls a package family, a separate
  world and Burst/Jobs assumptions that a single-threaded WebGL2 build cannot lean on.
- **Plain `List<Fighter>` of classes.** Rejected: heap objects per fighter, reference
  identity in the state hash, and a per-tick allocation risk in a hot path.
- **`struct` array behind an object pool that grows on demand.** Rejected: the
  contract already caps the field at `MaxFighters = 8`; a growable pool would add code
  to solve a problem the design has ruled out.

## Consequences

- **Positive:** no new dependency; the simulation builds and tests in plain .NET; state
  hashing is a loop over a fixed array (deterministic by construction, no dictionary
  order); zero-alloc is achievable with spans alone.
- **Negative:** iteration, spatial queries and "which fighters are in this area" are
  hand-written against the occupancy array — a correctness cost paid once per query
  kind in Stage 06.
- **Negative:** a fighter cannot gain or lose a capability mid-match (no structural
  change). Acceptable: a loadout is fixed for the match; temporary effects are fields
  on the struct plus `StatusMask`.
- **Risk:** `MaxFighters = 8` is a contract constant, so raising it later is a contract
  change with golden-data churn, even though the array would happily hold 16.
- **Revisit if** the arena needs more than ~16 simultaneous entities per side
  (projectiles, summons, an open-world crowd sharing the simulation), or if profiling
  shows a tile scan dominating a tick. Then the ECS would sit *behind* `IArenaSession`,
  and this ADR is superseded rather than edited.
