# Stage 07 — Arena simulation core

## 1. Summary

The arena contract from Stage 02 is now implemented for real in `lib/Runtime/Simulation/Arena/`:
`ArenaSimulation` (one struct array of fighters, every buffer sized in the constructor),
`ArenaSimulationFactory`, `ArenaSimulationQuery`, `ArenaAutoResolver` and the `IArenaAiHook` seam
that Stage 08 fills. `tools/sim.ps1 --matches 500` resolves 500 authored matches headless in
about 230 ms total (**0.46–0.47 ms per 3,600-tick match**, budget 20 ms) and writes
`artifacts/balance.csv`. Running 31 authored fighters against each other found three things the
tests could not: two authored stat formulas read in the wrong unit, two fighters that could stand
on one tile, and a balance scale where a hit is ~1.5 % of HP — fixed / recorded in ADR 0114 and
ADR 0115.

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Struct-array fighters (no ECS, ADR 0101), reusing `ArenaFrameBuffer` and `EventRing` | **PASS** | `ArenaSimulation` holds `ArenaFighter[]` + parallel arrays (`_skills`, `_cooldowns`, `_paths`, `_statusLeft/_statusTimer`), allocated once; `_frame`, `_liveFrame` are `ArenaFrameBuffer`, `_ring` is `EventRing<ArenaEvent>(2048)`. `ArchitectureTests` keeps `Simulation` on Shared+Domain only; `noEngineReferences` asmdef. |
| 2 | Tick order: Intro → ATB fill → Ready/AwaitingPlayer → AI decide → action progress → status → neili → defeat/timeout | **PASS** (*decide runs last, see ADR 0114 §2*) | `Step`, `FillAtb`, `AdvanceActions`, `TickStatuses`, `RegenerateNeili`, `CheckEnd`, `RunAi`. Progress-before-decision is a deliberate ordering, argued in ADR 0114 §2. Exercised by `The_acupoint_seal_stops_the_victims_atb_bar` (fill ×0), `Wait_mode_stops_at_the_player_and_step_does_not_advance_the_tick` (real session). |
| 3 | Resolve: hit/evade, crit, backstab/flank, mitigation, counter, knockback (stops at edge/body), status | **PASS** | `Damage_pipeline_is_power_mastery_position_crit_mitigation` (3 hand-derived goldens: 1,292 / 1,486 / 1,939), `A_critical_hit_multiplies_before_mitigation` (1,939), `A_counter_hits_back_with_the_defenders_first_skill`, `Knockback_stops_at_a_blocked_tile`; 500-match sweep records hits *and* evades per skill (`skill.falling_rain_slash` 756 casts / 466 hits). |
| 4 | Jump during a wind-up; landing outside the area is a dodge, decided at resolve | **PASS** | `A_jumping_fighter_is_invulnerable_while_airborne`, `Landing_outside_the_telegraph_is_a_dodge` (both go through `ResolveSkill`'s area test + the `Jumping` i-frame). |
| 5 | `FighterMatchStat` + `SkillUseStat` recorded | **PASS** | `ArenaMatchResult.Fighters` filled in `BuildResult`; uses counted at `BeginSkill`, hits at the connecting blow. The sweep's CSV rows (`uses`/`hits` per skill) come only from those structs. |
| 6 | `ArenaAutoResolver`: both sides AI, step until finished or `maxTicks` | **PASS** | `The_auto_resolver_finishes_inside_its_budget` (`maxTicks` 600, winner team set); 500 authored matches in `artifacts/balance.csv`, 500/500 resolved. |
| 7 | `tools/sim.ps1 --matches 500` → `artifacts/balance.csv` (win rate by rank, match time, skills used) | **PASS** | `balance: matches=500 wins=500 draws=0 avgTicks=3600 avgSeconds=120`; CSV rows `rank_1 … rank_7` with matches/wins/win_rate/avg duration and one row per skill with uses/hits, plus `total`. Also a `sim: balance sweep` step in `tools/test.ps1` (40 matches, 0.5 ms each). |
| 8 | The Stage-02 contract suite still passes; new cases for the damage golden, counter, knockback, timeout, i-frame | **PASS** | `ArenaSimulationContractTests` runs all 11 shared facts against the real session with real generated tables; `ArenaSimulationTests` adds 12 (damage theory ×3, crit, counter, knockback, i-frame, telegraph dodge, preview == resolve, acupoint seal, timeout, resolver budget) → xUnit **302 passed, 0 failed**. |
| 9 | *(prompt)* contract tests must not be weakened — amend the contract + fake only with an ADR | **PASS** | One amendment: `Every_tile_the_query_reports…` now steps until arrival instead of asserting an in-tick teleport; reasoned in **ADR 0115 §8**. The fake was left arriving in one tick, which stays legal. No assertion deleted, no fact disabled. |
| 10 | **DoD:** auto-resolve one match under 20 ms on .NET | **PASS** | `avgMsPerMatch=0.46` / `0.47` on two identical runs (500 matches, 230 / 235 ms total, Debug, this machine — every match ran its full 3,600 ticks, the worst case), verdict=PASS against the 20 ms budget. |
| 11 | **DoD:** zero GC allocation per tick after warm-up | **PASS** | Shared contract fact `Step_submit_and_query_do_not_allocate_after_warm_up` now covers `ArenaSimulation` (it asserted 0 bytes before this stage against the fake and still does against the real session). |

Balance artifact (`artifacts/balance.csv`, 500 matches, seed 20261013):

| Rank | Matches | Win rate | Avg duration |
| --- | --- | --- | --- |
| 1 | 165 | 43.0 % | 120 s |
| 2 | 128 | 44.5 % | 120 s |
| 3 | 161 | 46.6 % | 120 s |
| 4 | 129 | 46.5 % | 120 s |
| 5 | 160 | 52.5 % | 120 s |
| 6 | 129 | 56.6 % | 120 s |
| 7 | 128 | 62.5 % | 120 s |

## 3. Not tested, and risks

- **NOT RUN:** Unity PlayMode, WebGL build and Playwright (no `-Suite Full`); Unity **EditMode 42/42
  passed** and the Editor compiled the new `Simulation` assembly, so the code is Unity-clean, but no
  scene uses the session yet — Stage 09 owns that.
- **Balance is untuned** (the finding, not a bug): every one of the 500 matches was decided by the
  120 s HP-percentage rule. A hit removes ~1.4 k of 89–102 k HP (~1.5 %), so nobody can be defeated
  in 20 turns. The rules are right; `skill.power` / `ratio_x100` or the 80 k HP floor need a pass.
  `artifacts/balance.csv` is the measurement to start from.
- **`skill.csv` has no `area_size` or `hits`**, so `ArenaFighterBuilder` hands every area skill
  `AreaSize = 0, Hits = 1`: Line/Cone/Cross/Diamond/Square resolve as a single tile today and
  `falling_rain_slash`'s two cuts are not modelled. Geometry and pipeline are tested; the column is
  missing.
- **`stat.move_range`/`stat.jump_range` are an approximation** of the GDD's `3 + floor(L/30)`: the
  formula engine has integer coefficients only, so the Lightness contribution is capped at +1
  (4 walk / 3 jump). A divisor in the formula engine would fix it properly.
- **The placeholder AI is temporary**: it never retreats, ignores `engage_range`,
  `retreat_hp_x100` and `aggression_x100`, and always casts the first legal skill. It produces legal
  matches, not good ones — Stage 08 replaces the hook (the seam is one interface).
- **Walk reservations hold a whole remaining route**: correct for the prototype, but a longer walk
  would hold more of a small board. Only one test (`Knockback_stops_at_a_blocked_tile`) exercises
  knockback against an occupied tile.
- **Decision order is sequential and index-ordered**, so the lower setup index acts first every tick
  — deterministic, but a small fairness asymmetry that Stage 08 should keep in mind.
- **The ~0.47 ms figure is Debug on this machine**, not a WebGL number; Stage 16 owns the browser.

## 4. Commands actually run

```powershell
pwsh -NoProfile -File tools/gen.ps1                                     # PASS 42 .cs / 14 .bytes, configHash BDFE1BE2…
dotnet build lib/Tests~/Layers/ProjectJianghu.Core.Simulation.csproj    # PASS 0 warnings, 0 errors
dotnet test lib/Tests~/Core.Tests/ProjectJianghu.Core.Tests.csproj      # PASS 302 passed, 0 failed
pwsh -NoProfile -File tools/sim.ps1 --matches 500 --trace 6             # PASS avgMsPerMatch=0.47 verdict=PASS (repeat: 0.46)
pwsh -NoProfile -File tools/test.ps1 -Suite Fast                        # PASS 10 | FAIL 0 | NOT RUN 3
node tools/check-links.mjs                                              # PASS 344 links, 0 broken
```

The three data corrections in ADR 0114 §14 were each measured before and after with
`tools/sim.ps1 --matches 200` (draws 13 → 7 → 0; shared-tile ticks 2,822 → 0 across 20 matches).

---

- **Decisions:** [ADR 0114](../adr/0114-arena-simulation-tick-resolve-and-damage.md) (the 30 Hz tick
  order, ATB, the resolve pipeline, endings, determinism, and the three authored numbers corrected)
  and [ADR 0115](../adr/0115-arena-ai-seam-and-walk-reservations.md) (the AI seam, decision order,
  what a walker owns, and the `Move`-takes-ticks contract amendment). Implementation summary:
  [`llmwiki/systems/arena-simulation.md`](../../llmwiki/systems/arena-simulation.md).
