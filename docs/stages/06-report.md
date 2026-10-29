# Stage 06 — Grid and targeting domain

## 1. Summary

Added the pure C# board rules under `lib/Runtime/Domain/Arena/`: `ArenaGrid` (the `arena_map`
mask, occupancy and `GridCoord` ↔ index), `ArenaPathfinder` (BFS walk reachability, the tile-by-
tile walk path, and jump landings), `ArenaArea` (the six area shapes) and `ArenaFacing` (grid
direction, facing after a step, facing toward a target, and the front / flank / back rule).
No Unity API, no config row, no allocation. Every query writes into the caller's
`Span<GridCoord>`; the search's own bookkeeping is `stackalloc` sized to the board.

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | `ArenaGrid` from an `arena_map` mask; occupancy; `GridCoord` ↔ index | **PASS** | `GridDomainTests.Grid_reads_its_size_and_blocked_tiles_from_a_mask`, `Authored_arena_map_loads_into_a_grid` (the real `map.open_court` row: 12×12, 8 blocked), `Grid_index_round_trips_…`, `Occupancy_is_exclusive_…`, `Grid_rejects_a_malformed_mask`. |
| 2 | Reachability: BFS over four directions within `moveRange`, no walking through a fighter, empty destination, path returned one tile at a time | **PASS** | `Reachable_on_an_open_grid_is_the_manhattan_disk`, `Reachable_does_not_pass_through_a_fighter` (a wall of one fighter makes two in-range tiles unreachable), `Path_is_one_adjacent_step_per_tile_and_ends_on_the_goal`, `Path_refuses_…`, `Path_truncation_reports_failure_…`. |
| 3 | Jump targets within `jumpRange` Manhattan, landing walkable and empty, fighters jumped over | **PASS** | `Jump_lands_only_on_free_tiles_within_manhattan_range` (start (0,0), fighter at (2,0): (3,0) and (4,0) are legal landings), `Jump_excludes_a_blocked_landing_tile_and_far_landings`. |
| 4 | Six area shapes, anchored on the target tile and oriented by the caster's facing, clipped at the grid edge | **PASS** | `Shape_golden` — 20 golden cases (every shape, all four directions), `Line_and_cone_grow_away_from_the_caster_in_generation_order`, `Shape_is_clipped_to_the_grid_bounds` (corner, edge and a 1-tile square). |
| 5 | Facing after a walk, facing toward a target, relative angle → front / flank / back | **PASS** | `Direction_delta_and_opposite_are_the_four_axes`, `Facing_toward_uses_the_dominant_axis_and_ties_go_to_x`, `Relative_angle_is_front_flank_or_back` (20 cases: four facings × four neighbour and diagonal positions). |
| 6 | All buffers are caller spans; no allocation | **PASS** | `Queries_write_into_caller_spans_without_allocating` — `GC.GetAllocatedBytesForCurrentThread` delta over 500 rounds of all four query kinds = **0 bytes**; truncation instead of growth proven by `Reachable_truncates_to_the_callers_span` and `Shape_zero_size_and_small_buffers_are_defined`. |
| 7 | 10,000-iteration property test: path length ≤ range, no path crosses an obstacle | **PASS** | `Every_reachable_tile_has_a_legal_path_and_nothing_else_does` — 10,000 random boards (1..6 × 1..6, 30% blocked, up to two fighters) compared tile-for-tile with an independent flood fill, plus >10,000 reconstructed paths checked for adjacency, walkability, no repeats and length ≤ `moveRange`. |
| 8 | Benchmark query < 0.05 ms | **PASS** | `Arena_queries_stay_under_fifty_microseconds` — reachability + path + jump + cone, 5,000 rounds on the authored 12×12 map: **4.702 µs per query** (0.0047 ms) on the reference machine, .NET 9 Debug. |

Supporting counts: `dotnet test` **272 passed** (64 of them this stage), Stage-06 class 64/64 in
261 ms.

## 3. Not tested, and risks

- **NOT RUN:** Unity PlayMode, WebGL build and Playwright — the Fast suite was run, not Full.
  Stage 06 changed no scene, so no PlayMode gate closes here; the Unity build is still owed at
  the next stage that opens the Editor for it.
- The fake session (`Application/Arena/Fake`) still answers reachability with a plain Manhattan
  scan and previews every skill on one tile. Stage 07 replaces it with these functions; until
  then the fake and the domain can disagree, and only the domain is authoritative.
- `ArenaFacing`'s grid-axis ↔ `IsoDir` mapping is now the single source of truth, but the fake
  has its own private copy of the same rule. They agree today (checked by reading both); the
  fake's copy should be deleted, not maintained, in Stage 07.
- An area keeps tiles the mask blocks — geometry, not legality. If a wall should stop a skill,
  Stage 07 adds that rule, not this one.
- Occupancy is a mirror the simulation must keep current; nothing in the domain can detect a
  stale board. Stage 07's `ComputeStateHash` should include the occupant map so a desync shows
  up as a hash mismatch rather than a movement bug.
- The 4.7 µs figure is measured on the development machine in Debug. It is far inside the
  budget, but it is not a WebGL number; Stage 16 owns the browser measurement.

## 4. Commands actually run

```powershell
dotnet build lib/Tests~/Layers/ProjectJianghu.Core.Domain.csproj -v q   # PASS, 0 warnings, 0 errors
dotnet test lib/Tests~/Core.Tests/ProjectJianghu.Core.Tests.csproj --filter "FullyQualifiedName~GridDomainTests"   # PASS 64/64, 261 ms
dotnet test lib/Tests~/Core.Tests/ProjectJianghu.Core.Tests.csproj      # PASS 272 passed, 0 failed
pwsh -NoProfile -File tools/test.ps1 -Suite Fast                        # PASS 9 | FAIL 0 | NOT RUN 3; xUnit 272, Unity EditMode 42/42
```

The one-off benchmark reading came from the same test with its budget temporarily forced to
0 µs so the assertion message would print the average; the committed budget is 50 µs.

---

- **Decisions:** [ADR 0113](../adr/0113-grid-geometry-anchor-direction-and-size.md) records the
  four things the contract left open — the anchor (aim tile), the direction (caster's facing,
  applied to line and cone), what `AreaSize` measures per shape, and that an area is clipped to
  the grid but not to the mask — plus the `IsoDir` ↔ grid-axis bijection the fake had kept
  privately. The implementation-facing summary is
  [`../llmwiki/systems/grid-and-targeting.md`](../../llmwiki/systems/grid-and-targeting.md).
