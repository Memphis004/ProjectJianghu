# Stage 08 — Arena AI

## 1. Summary

Added a profile-driven utility AI to the existing `IArenaAiHook` seam. It scores legal skill
placements, approach/retreat/flank moves, telegraph-evading jumps, and meditation; `ai_profile.csv`
owns the four profiles' utility weights and reaction-delay scales. Tie-breaking uses the encounter
random stream, and fighter rank/decisiveness now reach the simulation setup and public telegraph
state. The implementation is exercised by focused skill, delay, evasion, and event-overflow tests.
The simulation is fast, but the required higher-rank win-rate target was **not met**; Stage 08 is
implemented but its balance gate remains open.

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
| 8 | Higher rank by at least two steps wins at least 75% of 500-match comparisons | **FAIL** | From the 500 traced matches, 323 had rank gaps ≥2; the higher rank won 118 (**36.53%**, counting draws as non-wins), and 114 were draws. This is below 75%. The current schedule also confounds rank with fighter/profile pairing, so this is a failing observed sample, not a clean causal rank estimate. |
| 9 | Headless match runtime stays under 20 ms per match | **PASS** | Release sweep measured **0.90 ms/match** (450 ms total / 500) on this machine, against the 20 ms budget. |

The overall balance summary was 347 wins, 153 draws, 157 knockouts, 343 timeouts and 3,324.44
average ticks (110.815 s) per match. A high timeout count and the rank-gap miss require follow-up;
no balance pass is claimed.

## 3. Not tested, and risks

- Unity PlayMode, WebGL build, and Playwright were **NOT RUN**; the Fast suite was requested, not
  Full. The Editor's EditMode suite passed 42/42.
- Archetype distinction has not been demonstrated with controlled, per-profile behavior metrics.
- The 500-match pairing schedule is not stratified by rank and archetype. The measured 36.53% result
  includes 114 draws and is below target; do not use it to claim a monotonic rank advantage.
- A 1,000-match rejected-command count was not measured. Validation is present in code, but a
  statistical zero-rejection claim would need explicit instrumentation or an equivalent test.
- The utility AI is greedy, not tactical search: it evaluates current public state and telegraphs,
  not future occupancy or opponent intent. The 343 timeouts show that many matches still do not
  close within 120 seconds.

## 4. Commands actually run

```powershell
pwsh -NoProfile -File tools/gen.ps1                         # PASS — Luban output regenerated
pwsh -NoProfile -File tools/verify-generated.ps1            # PASS — generated output reproducible
pwsh -NoProfile -File tools/test.ps1 -Suite Fast            # PASS 10 | FAIL 0 | NOT RUN 3; xUnit 309, EditMode 42/42
pwsh -NoProfile -File tools/sim.ps1 --matches 500 --trace 6  # PASS runtime budget; balance criterion #8 FAIL
node tools/check-links.mjs                                  # PASS — 344 links in 82 files
```
