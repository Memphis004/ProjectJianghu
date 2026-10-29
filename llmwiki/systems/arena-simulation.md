# Arena simulation — the real match loop

`lib/Runtime/Simulation/Arena/` implements the Stage-02 contract for real: no Unity, no VContainer,
no UniTask, only `Shared` and `Domain` (AGENTS rule 2). This note is the implementation-facing
summary; the reasoning is in [ADR 0114](../../docs/adr/0114-arena-simulation-tick-resolve-and-damage.md)
and [ADR 0115](../../docs/adr/0115-arena-ai-seam-and-walk-reservations.md), the contract itself in
[`arena-contract.md`](arena-contract.md), the geometry in
[`grid-and-targeting.md`](grid-and-targeting.md).

## The pieces

| Type | Role |
| --- | --- |
| `ArenaSimulation` | `IArenaSession`: one struct array of fighters, all buffers in the constructor |
| `ArenaSimulationFactory` | `IArenaSessionFactory`; reads `constant.match_ticks` when the setup says 0 |
| `ArenaAutoResolver` | `IArenaAutoResolver`: create both sides as AI, step until done or out of budget |
| `ArenaSimulationQuery` | `IArenaQuery` over the live state (the AI gets one with the player-control check off) |
| `IArenaAiHook` / `GreedyArenaAiHook` | profile-driven utility scorer; uses only the live public frame, shared arena query and encounter RNG |

## Stage 08 AI

`GreedyArenaAiHook` scores legal skill targets (including area coverage and positional advantage),
walks toward the profile's engage range or toward a better flank, retreats/kites when the profile
and HP threshold call for it, jumps only to a landing tile outside incoming telegraphs, and
meditates when that wins on utility. Ties consume the encounter random stream; no hidden session
state is exposed to the hook. `ai_profile.csv` owns the utility weights, four archetypes, and the
base reaction delay. Decisiveness and power rank reduce that configured delay by per-profile
scales. `ArenaFighterBuilder` carries rank and decisiveness into the setup; roster and fake/auto
resolver conversions preserve them. The hook's tile/area buffers are preallocated.

## A tick, in order

1. intro (`IntroTicks` = 30) — no ATB, every command rejected
2. capture the previous positions (interpolation)
3. **ATB fill** — `carry += atbFull`, `delta = carry / AtbFillTicks`, status multiplier applied to
   `delta`; a 0 multiplier (acupoint seal) skips the fighter
4. in `AtbMode.Wait`, a ready player stops the tick (`AwaitingPlayer`)
5. **action progress** — a walk moves one tile and faces it, a jump arcs, a wind-up resolves at 0, a
   recovery returns to `Charging`
6. **statuses** — duration, damage over time, expiry
7. **neili regeneration**
8. **end check** — a side wiped out, or the time limit (HP percentage, integer cross-multiplication)
9. **AI decisions** — ready, non-player fighters in setup order; the live frame is refreshed after
   each one acts
10. the time warning, then the frame is published

## Numbers, and where they come from

| What | Source |
| --- | --- |
| Tick rate (30), intro (30), ring capacity, max fighters (8), skill slots (4) | `ArenaConstants` (contract shape) |
| ATB full (10,000), move/jump/meditate ATB cost, mitigation K, back/flank bonus, jump cooldown, meditate neili | `constant.csv` |
| `AtbFillTicks`, `ActionSpeedX100` (hundredths of a percent, 10,000 = 1.0), move/jump range, accuracy, evasion, crit, counter, resistance | `stat_formula.csv` via `StatResolver` |
| A skill's base damage, ratio, range, shape **and area size**, blow count, wind-up, recovery, status, knockback | `skill.csv` |
| A status's duration, tick interval, damage per tick, ATB and neili multipliers | `status.csv` |
| Match length (3,600 ticks = 120 s) | `constant.match_ticks`, filled by the factory |

A change to any of these is `data/` + `tools/gen.ps1`, never a code edit (AGENTS rule 6).

## Resolve, in one paragraph

Damage is integer and strictly ordered: `base + power×ratio/10000` → mastery → back/flank bonus →
crit → mitigation `K/(defense+K)` (ADR 0114 §4). Hit and crit are two rolls on the match's single
stream; hit chance is `accuracy − evasion`. `skill.hits` repeats the whole hit/crit/mitigation
sequence per blow inside one cast (`falling_rain_slash` cuts twice), while the cast counts once as
a use. A counter uses the defender's first in-range skill and
rolls `CounterChanceX100` after a landed hit. Knockback walks the victim away one tile at a time
and stops at the edge, a wall or a fighter. A status rolls its chance, then the victim's resist, and
keeps the longest duration. A jumping fighter is not hit at all, and a fighter that left the aimed
area during the wind-up is reported as `Evaded` — the same event a miss raises.

## What a walker owns

While a fighter is `Moving`, the occupant map holds its current tile **and every tile left on its
path**; a jumping fighter holds its landing tile. So reachability, jump landings, walk validation
and knockback all route around a walk in progress, and two fighters can never end a tick on the
same tile (ADR 0115 §5, measured 0 shared-tile ticks).

## Reading the state without changing it

- `Frame` is the published buffer; copy what a view needs before the next `Step` (ADR 0012).
- `Query` answers reachability, jump landings, area previews and validation — all into caller spans.
- `Events.Read(ref cursor, span, out dropped)` is the only way to see what happened; the ring drops
  the oldest events under pressure and `dropped` says how many.
- `ComputeStateHash` covers clock, phase, fighters, cooldowns, uses/hits and the occupant map.

## Measuring it

```powershell
pwsh -NoProfile -File tools/sim.ps1 --matches 500 --trace 6   # artifacts/balance.csv + timings
dotnet test lib/Tests~/Core.Tests/ProjectJianghu.Core.Tests.csproj --filter "FullyQualifiedName~ArenaSimulation"
```

`--matches N` implies `--mode balance`. The CSV has one row per power rank (matches, wins, draws,
knockouts, timeouts, win rate, average duration) and one per skill used (uses, hits), plus a `total` row.
A rank row counts **appearances** (a match contributes to both fighters' ranks, as `matches`
always has); the `total` row counts matches.
`avgMsPerMatch` with `verdict=PASS` is the per-match budget, `knockouts` / `timeouts` say how the
matches ended, and the per-skill `uses` / `hits` columns are how a two-blow skill shows up (more
hits than uses). The reference numbers are in
[`../../docs/stages/08-report.md`](../../docs/stages/08-report.md) (current AI and balance results); Stage 07's simulation baseline remains in [`../../docs/stages/07-report.md`](../../docs/stages/07-report.md).

## Known gaps

- This is a first-pass heuristic, not tactical search: it sees current public telegraphs but does not
  predict future occupancy or opponent intent. The current 500-match rank-gap sample has 36.53%
  higher-rank wins (118/323, draws counted as non-wins), below the Stage-08 75% target; 343 matches
  timed out. Pairing and archetype are confounded in the current schedule, so use a stratified matrix
  before drawing causal balance conclusions. Aggregate behavior differences across all four profiles
  have not yet been measured.
- A `Line`/`Cone` is anchored on the aim tile and grows away from the caster, so it can reach one tile
  past `range_max` — geometry, not a rules rule (ADR 0113).
