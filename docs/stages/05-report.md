# Stage 05 — Character, Stats and Loadout Domain

## 1. Summary

Added the pure C# character domain under `lib/Runtime/Domain/Character/`: mutable character and loadout state, source-preserving modifiers, config-backed stat/radar/power resolution, loadout validation, fighter setup construction, background-based creation and mastery progression. Extended Luban attribute, mastery and constant data to author creation bounds, PowerScore/radar weights, mastery multipliers, and power-rank thresholds. No Unity APIs or new ADRs were needed.

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Character state stores attributes, learned skills, inventory and loadout | **PASS** | `CharacterDomainTests` exercises base attributes, skill/gear inventory, slot updates and dirty state. |
| 2 | Modifier contributions retain their source for tooltips | **PASS** | `Modifier_tooltip_keeps_source_kind_and_source_id`, duplicate source, source removal and gear source tests. |
| 3 | Stat resolution uses authored formulas and recomputes only when dirty | **PASS** | Formula/clamp and cache tests; `StatResolver` consumes `stat_formula` and caches `CombatStatBlock`. |
| 4 | PowerScore/rank and five-axis radar are data-driven | **PASS** | Weighted PowerScore/rank and radar tests; weights and 2–9 rank thresholds are in Luban tables. |
| 5 | Loadout enforces slot/type rules; mismatched external skills are retained but disabled | **PASS** | Equip rejection, invalid slots, weapon swap, validation and builder omission tests. |
| 6 | Builder maps a character to the Stage 02 arena contract deterministically | **PASS** | Stable-order, slot-order, and disabled-skill builder tests. |
| 7 | Mastery grants XP, applies Intellect rate and advances multiple levels | **PASS** | Mastery, Intellect rate, invalid input, stable skill id and atomic unknown-id tests. |
| 8 | Character creation applies backgrounds and respects authored allocation bounds | **PASS** | Background setup, allocation/refund, unknown attribute/background and validation tests. |
| 9 | Tests and coverage meet Stage 05 DoD (≥35 cases; Character coverage ≥85%) | **PASS** | 42 Stage-05 domain facts; Character aggregate 512/560 lines = **91.43%**. |
| 10 | Domain remains UnityEngine-free | **PASS** | Pure C# sources in `Domain`; Fast architecture/build gates passed. |

## 3. Commands actually run

```powershell
pwsh -NoProfile -File tools/gen.ps1
# PASS — generated schema/readers and config data from data/.

pwsh -NoProfile -File tools/check-fixtures.ps1
# PASS 12 | FAIL 0 — stat-term-attribute fixture rejected for the expected semantic finding.

dotnet test lib/Tests~/Core.Tests/ProjectJianghu.Core.Tests.csproj --no-restore --verbosity minimal
# PASS — 208 passed, 0 failed.

dotnet test lib/Tests~/Core.Tests/ProjectJianghu.Core.Tests.csproj --no-build --no-restore --settings lib/Tests~/coverage-domain.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/dotnet/coverage --verbosity minimal
# PASS — 208 passed; wrote Cobertura report under artifacts/dotnet/coverage/.

node tools/coverage-report.mjs artifacts/dotnet/coverage/8dd7992d-64d1-49b1-b7ae-07bd732546f3/coverage.cobertura.xml Character/
# PASS — aggregate 512/560 lines (91.43%).

pwsh -NoProfile -File tools/test.ps1 -Suite Fast
# PASS 9 | FAIL 0 | NOT RUN 3; xUnit 208, Unity EditMode 42/42.
```

## 4. Not tested, and risks

- **NOT RUN:** Unity PlayMode, WebGL build and Playwright; the Fast suite was run, not Full.
- **NOT RUN:** independent Unity Editor compile for this stage; the repository's Unity EditMode gate passed as part of Fast.
- `StatGrowth` maps the stable integer `SkillUseStat.SkillId` via the same deterministic hash exposed by `StatGrowth.StableId`; saved/replayed identifiers must keep that mapping stable.
- Content values are structurally validated, not balance-tested; combat balance is deferred to simulation/content stages.

## 5. Files changed

- `lib/Runtime/Domain/Character/*.cs` — eight pure domain types/implementations.
- Config schema/data, generated Luban output, config adapters and validator/tests — authored stat weights, allocation bounds, mastery progression, PowerScore thresholds and cross-row rules.
- `docs/stages/05-report.md`, `knowledge.md`, `llmwiki/index.md`, `llmwiki/stage-log.md`, `llmwiki/systems/character-domain.md`, `llmwiki/systems/data-schema.md` — stage and system documentation.
