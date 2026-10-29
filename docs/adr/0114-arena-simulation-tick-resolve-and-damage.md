# ADR 0114 — The arena tick: order, timing and the resolve pipeline

- **Status:** Accepted
- **Date:** 2026-10-30
- **Stage:** 07
- **Supersedes:** nothing
- **Related:** [ADR 0101](0101-no-ecs-struct-array-fighters.md) (struct-array fighters),
  [ADR 0113](0113-grid-geometry-anchor-direction-and-size.md) (geometry, anchor, occupancy),
  [ADR 0115](0115-arena-ai-seam-and-walk-reservations.md) (the AI seam and walk reservations),
  GDD §1.6, `llmwiki/systems/arena-simulation.md`

## Context

The contract (Stage 02) fixed *what* a session answers — phases, commands, a frame, events, a
result — and Stage 06 fixed the geometry. Neither said in what order a tick does its work, how
long an action lasts, what a hit costs, or which of the many reachable orderings is the one a
replay depends on. Those are the questions that decide whether two runs of the same seed are the
same match, so they are written down once here instead of being re-derived in every later stage.

The stage prompt listed the loop as *Intro → ATB fill → Ready/AwaitingPlayer → AI decide → action
progress → status tick → neili regen → defeat/timeout check*. The implementation keeps every step
and runs **action progress before the decisions** (§2), because the other order would take one
tick off every wind-up: a fighter that becomes ready on tick *T* would start and already advance
its action in the same tick.

## Decision

1. **The tick is fixed at 30 Hz** (`ArenaConstants.TickRate`); every duration in the contract is
   in ticks and a match's length comes from `constant.match_ticks` (3,600 = 120 s). The factory
   fills a `TimeLimitTicks` of 0 from that row; inside the session, `0` still means "no limit"
   exactly as the contract promises. The intro is `ArenaConstants.IntroTicks` (30); ATB does not
   fill and commands are rejected during it.
2. **The order inside `Step`, once the intro is over:**
   1. `_elapsedTicks++`, and the previous positions are captured for interpolation.
   2. **ATB fill** for every charging fighter: `carry += atbFull`, `delta = carry / AtbFillTicks`,
      `carry -= delta * AtbFillTicks`, then `delta` is scaled by the status multiplier. Exact
      integer arithmetic with a carried remainder, so no drift and no float. A multiplier of 0
      (acupoint seal) skips the fighter entirely. Full → `Ready`, and the AI reaction delay is
      armed.
   3. In `AtbMode.Wait`, a ready player stops the tick here (`AwaitingPlayer`, no further steps).
   4. **Action progress**, at most one step per fighter: a walk takes one tile per tick and turns
      to face it, a jump arcs over the tiles in between, a wind-up counts down to `ResolveSkill`,
      a recovery counts down to `Charging`.
   5. **Statuses**: duration, damage-over-time on its interval, expiry.
   6. **Neili regeneration**, scaled by the status multiplier.
   7. **The end check** (all of a side defeated, or the time limit).
   8. **AI decisions** for every ready, non-player fighter — see ADR 0115.
   9. The time warning, then the frame is published.
3. **A command costs ATB from `constant`**: Move 10,000 (the whole bar), Jump 5,000, Meditate
   10,000. A walk is `steps` ticks long, a jump `Manhattan distance` ticks (and it holds the
   landing tile while airborne), and a skill is `wind_up_ticks` and `recovery_ticks` scaled by
   the caster's `ActionSpeedX100`.
4. **The resolve pipeline is integer, in this order** (`ComputeDamage`, all from `data/`):
   `raw = base + power × ratio/10000` → `× mastery/10000` → `× (1 + back|flank bonus)` → `× crit
   damage` → `× mitigation K/(defense + K)`. `base`, `ratio`, `mastery`, `power` and the two
   positional bonuses are the skill row, the mastery level and the `constant` row; nothing is
   hardcoded (AGENTS rule 6).
5. **Hit and crit are two rolls on the match's single stream**: hit chance is
   `accuracy − evasion` clamped to 0..10,000 and a roll at or above it misses; the position is
   read from the *victim's* facing relative to the attacker (back = ¼ turn around the front
   cone), and a crit multiplies before mitigation, so the two cannot be reordered.
6. **A counter is the defender's first skill whose range covers the distance**, rolled against
   `CounterChanceX100` after a landed hit, and only for skills without `Uncounterable`.
7. **Knockback is one tile at a time** away from the attacker along the dominant axis, stopping
   at the grid edge, a wall or another fighter, shortened by `KnockbackResist`.
8. **A status lands on a chance roll, then a resist roll**, keeps the longest remaining duration
   on re-application, and is read from `status.csv` by kind: the ATB multiplier (acupoint seal
   stops the bar), the neili multiplier and the damage per tick.
9. **The jump is an i-frame, and leaving the telegraph is a dodge.** A fighter in `Jumping` is
   not hit at all; a fighter that was aimed at but is outside the resolved area when the wind-up
   ends is reported as `Evaded` — the same event an accuracy miss raises.
10. **Endings, in one place:** every opposing fighter defeated → `AllEnemiesDefeated` /
    `AllAlliesDefeated`; the time limit → HP percentage compared as `hp_a × max_b` vs
    `hp_b × max_a` (integer, no division), a tie is `TimeoutDraw`; surrender and the dev commands
    are the contract's own reasons.
11. **The result carries what the next systems consume**: `FighterMatchStat` (final HP, damage
    dealt/taken, evades, counters) and one `SkillUseStat` per *cast* skill (uses counted when the
    wind-up starts, hits when a hit connects), which is what `StatGrowth` turns into mastery.
12. **One stream, one hash.** The match draws from exactly one `IRandomSource` derived from the
    match seed (`RandomStreamKind.Encounter`); `ComputeStateHash` covers the clock, the phase, the
    fighters (tiles, ATB, states, cooldowns, counters, uses/hits) and the occupant map, so a
    desync between two runs of one seed surfaces as a hash mismatch rather than as a rule bug.
13. **Nothing allocates per tick.** Every buffer (fighters, skills, paths, statuses, the ring, the
    two frames) is allocated in the constructor; a query writes into the caller's span. Measured:
    0 bytes over 600 ticks after warm-up, and 0.47 ms for a whole 3,600-tick match.
14. **Three authored numbers were corrected when the simulation first consumed them.** They are
    data, not code, and each was wrong in a way the arena could not work around:
    - `stat.action_speed_x100` was authored as `100 + Lightness×2 + Decisiveness` with a 50..250
      clamp, but the column (and `CombatStatBlock`) means **hundredths of a percent**, where 1.0 is
      10,000. Read as a scale, a 12-tick wind-up became ~1,000 ticks: fighters spent whole seconds
      in recovery and a 120-second match held two attacks. Re-authored to `10000 + Lightness×200 +
      Decisiveness×100`, clamped 5,000..25,000 (~10 ticks for a 12-tick wind-up).
    - `stat.move_range` and `stat.jump_range` were `3 + Lightness` and `2 + Lightness`, which
      clamps to 12 tiles: one Move crossed the whole board, and two fighters of equal reach walked
      through each other's start tile forever. The GDD's own formula is `3 + floor(Lightness/30)`,
      which the formula engine cannot express (integer coefficients only), so the Lightness term's
      contribution is capped at +1: 4 tiles walking, 3 jumping. Recorded as an approximation, not
      as the intended long-term formula.
    - The two are visible in `artifacts/balance.csv`: before, 13 of 200 matches ended with **no
      attack at all**; after, 0 of 500 did, at 13–29 casts a match.

## Alternatives considered

- **Decide before progressing (the prompt's literal order).** Rejected: it shortens every wind-up
  and recovery by one tick and makes "ready this tick" different from "ready next tick" for no
  reason. Both steps happen every tick; only the order inside one tick is chosen here.
- **Float ATB or float damage.** Rejected: WebGL's 32-bit heap makes float determinism a risk, and
  the contract's replay gate compares exact hashes (ADR 0003/0005, rule 5).
- **A separate random stream per purpose inside one match** (hit / crit / status / AI). Rejected
  for now: one stream makes a hash mismatch immediately meaningful. Splitting later is a contract
  change (the save already carries stream seeds), not a rule change.
- **Resolve a jump as "invulnerable, timing dependent".** Rejected: an explicit `Jumping`
  check is readable and testable, and the "aimed at but outside the area" case is a second,
  separate dodge that the telegraph promises.
- **Apply damage as a percentage of max HP.** Rejected: the GDD's formula is flat + power, and a
  percentage would make the authored `power` column meaningless.
- **Fix the balance data (skill power, HP) instead of only the three authoring bugs.** Rejected
  for this stage: it needs its own pass with targets. See the consequences below.

## Consequences

- **Positive:** the whole match is one deterministic integer loop with no allocation; the same
  seed replays to the same hash against the real session and against the fake (the shared
  contract suite passes on both); a batch of matches is cheap enough to run in `test.ps1`.
- **Positive:** the numbers the rules use are all read from `constant`, `skill`, `status` and
  `stat_formula`, so a balance pass is a data edit plus `tools/gen.ps1`.
- **Negative — the balance finding this stage produced:** every match of the 500-match sweep hit
  the 120-second limit (win rate by rank 43 % → 63 %, no draws, decided by HP percentage). A hit
  does ~1.4 k against 89–102 k HP, about 1.5 %: roughly 60 hits to defeat anyone. The rules are
  correct — the authored `skill.power`/`ratio_x100` values and the 80 k HP floor are simply an
  order of magnitude apart. Tuning them is a balance pass, not a simulation change, and
  `artifacts/balance.csv` is the measurement to start from.
- **Negative:** `skill.csv` has no `area_size` (or `hits`) column, so `ArenaFighterBuilder` hands
  every area skill `AreaSize = 0` and `Hits = 1`. Line, Cone, Cross, Diamond and Square therefore
  resolve as a single tile today, and `skill.falling_rain_slash`'s "two cuts" is not modelled. The
  geometry is implemented and tested; the data column is missing.
- **Negative:** AI reaction time (`ai_profile.decision_delay_ticks`) is applied as a delay after
  the bar fills, not as a decision cadence, and the placeholder AI ignores `engage_range`,
  `retreat_hp_x100` and `aggression_x100` entirely — Stage 08 owns those.
- **Verification:** `ArenaSimulationTests` (10 facts: damage golden, crit, counter, knockback,
  i-frame, telegraph dodge, preview == resolve, acupoint seal, timeout, resolver budget) and the
  11 shared contract facts against the real session; `tools/test.ps1 -Suite Fast` also runs the
  40-match sweep as a step, so the loop cannot regress silently.
