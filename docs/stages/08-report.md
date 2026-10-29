# Stage 08 — Arena AI

## 1. Summary

Added a profile-driven utility AI to the existing `IArenaAiHook` seam. It scores legal skill
placements, approach/retreat/flank moves, telegraph-evading jumps, and meditation; `ai_profile.csv`
owns the four profiles' utility weights and reaction-delay scales. Tie-breaking uses the encounter
random stream, and fighter rank/decisiveness now reach the simulation setup and public telegraph
state. The implementation is exercised by focused skill, delay, evasion, and event-overflow tests.
The simulation is fast. A roster-wide stratified follow-up now covers every authored fighter pair
at rank gaps ≥2, for every low/high AI-profile combination and both starting sides using paired
seeds. It removes missing pairings and first-side imbalance, but the required higher-rank win-rate
target remains **not met**; Stage 08 is implemented but its balance gate remains open.

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Utility scoring considers skill targets, approach/retreat/flank movement, telegraph jumps, and meditation | **PASS** | `GreedyArenaAiHook` enumerates legal candidates through `IArenaQuery`; `Utility_ai_uses_a_legal_available_skill` and `Evader_jumps_to_a_tile_outside_an_incoming_telegraph` pass. |
| 2 | Four authored archetypes use profile weights and exhibit measurably distinct behavior | **NOT RUN** | Four archetype profiles and per-action weights are authored in `data/tables/ai_profile.csv`; no controlled comparison of each archetype's distance/jump counts was run, so distinct aggregate behavior is not claimed. |
| 3 | AI uses the player's arena query and only publicly visible match state | **PASS** | AI decisions receive the live `IArenaFrame` and `IArenaQuery`; telegraph skill/aim data is exposed through `FighterView`. No session reference is passed to the hook. |
| 4 | Decisions are deterministic and tie-breaking uses the arena RNG stream | **PASS** | `IArenaAiHook.TryChoose` accepts `IRandomSource`; simulation passes its encounter source to the hook, and `Consider` uses it only to resolve equal scores. Fast determinism and stream-separation checks pass. |
| 5 | Evader can jump outside an incoming telegraph when a legal landing exists | **PASS** | `Evader_jumps_to_a_tile_outside_an_incoming_telegraph` observes `JumpStarted`, then the attack resolves as `Evaded` with unchanged HP. |
| 6 | AI submits no command rejected by `Validate` across 1,000 matches | **NOT RUN** | Candidate and returned commands are validated in the hook and simulation, but no 1,000-match rejected-command counter/instrumentation was run; the test suite does not prove this statistical criterion. |
| 7 | 500-match sweep completes without a stuck match | **PASS** | Seed `20261013`: all 500 matches completed within the configured match tick budget. |
| 8 | Higher rank by at least two steps wins at least 75% of 500-match comparisons | **FAIL** | The initial 500-match run had 323 rank-gap ≥2 matches: high rank won 118 (**36.53%**, draws counted as non-wins), 114 drew. The later complete paired matrix covered all 31 authored fighters, all 15 available rank pairs with gap ≥2, four profiles on each side, and both start sides: 295 distinct fighter pairings, each repeated 20 times and played from both starting sides (5,900 paired trials; 11,800 matches). High rank won 4,649 (**39.40%** including draws; **59.81%** among decisive outcomes), lost 3,124, drew 4,027; 7,717 matches timed out. This is still below the 75% target. |
| 9 | Headless match runtime stays under 20 ms per match | **PASS** | Release sweep measured **0.90 ms/match** (450 ms total / 500) on this machine, against the 20 ms budget. |

The initial 500-match summary was 347 wins, 153 draws, 157 knockouts, 343 timeouts and 3,324.44
average ticks (110.815 s) per match. In the complete stratified matrix, equal-side placement and
paired seeds remove those confounds; rank gaps of exactly two produced a 36.20% high-rank
all-match win rate (55.82% among decisive results), while gaps ≥3 produced 41.09% (61.87%
decisive). Rank-pair outcomes remain uneven (e.g. ranks 3 vs 6: 60.12% high-rank wins including
draws; ranks 4 vs 7: 26.41%), and timeout count remains high. No balance pass is claimed.

## 3. Not tested, and risks

- Unity EditMode was **NOT RUN** because no Unity Editor with this project was connected. Unity
  PlayMode, WebGL build, and Playwright were **NOT RUN** because `-Suite Full` was not requested.
- Archetype distinction has not been demonstrated with controlled, per-profile behavior metrics.
- The complete paired matrix removes roster/profile coverage and starting-side imbalance, but it is a
  benchmark, not a proof of monotonic rank power: rank-pair win rates vary materially, and 4,027 of
  11,800 matches drew (7,717 timed out). The 75% acceptance target is still not met.
- A 1,000-match rejected-command count was not measured. Validation is present in code, but a
  statistical zero-rejection claim would need explicit instrumentation or an equivalent test.
- The utility AI is greedy, not tactical search: it evaluates current public state and telegraphs,
  not future occupancy or opponent intent. The 7,717 stratified-matrix timeouts show that many
  matches still do not close within 120 seconds; the earlier 500-match sample had 343 timeouts.

## 4. Commands actually run

```powershell
pwsh -NoProfile -File tools/verify-generated.ps1            # PASS 9 — generated output reproducible
node tools/check-links.mjs                                  # PASS — 348 links in 83 files
pwsh -NoProfile -File tools/test.ps1 -Suite Fast            # PASS 10 | FAIL 0 | NOT RUN 4; xUnit 309; Unity EditMode unavailable
pwsh -NoProfile -File tools/sim.ps1 --matches 500 --trace 6  # PASS runtime budget; criterion #8 FAIL
pwsh -NoProfile -File tools/sim.ps1 -Mode stratified -MatchCount 20 -BalanceOut artifacts/balance-stratified.csv -Output artifacts/sim-stratified.txt
  # PASS — 11,800 matches; criterion #8 FAIL (high-rank 39.40% incl. draws; 0.455 ms/match)
```
