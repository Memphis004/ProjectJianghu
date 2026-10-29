# Stage 01 — Data pipeline re-baseline

## 1. Summary

The Survive-era data pipeline is gone. `data/Defines/schema.xml` is now the game's own shape —
**10 enums** (`EAttrCategory`, a 34-value `EAttribute` in stable number blocks, `EWeaponType`,
`ESkillKind`, `ESkillTarget`, `EAreaShape`, `EStatus`, `EGearSlot`, `EAiArchetype`, `Language`),
**3 beans** (`AttrMod`, `StatTerm`, `SkillLevelBonus`) and 14 table beans — and
`data/tables/` holds the 13 tables plus localization, each with 1–2 sample rows. `sample`
is deleted with its bean, CSV, fixtures and tests. `proto/app.proto` gained `SaveData`,
`CharacterState`, `TournamentState` and `ArenaReplay` (setup + `(tick, command)` stream).
`IGameConfig` exposes all fourteen tables, `Data.Validator` enforces missing references, a
`StatTerm` naming an `attribute` row, and the arena-mask size, and eight fixtures prove it.
[ADR 0103](../adr/0103-data-schema-encoding-and-stable-identifiers.md) records the two
decisions that were not obvious: a bean inside a list is a flat `|`-separated token stream
(measured against the installed CLI), and enum numbers are stable per-category blocks.

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Clear the Survive tables; create `__enums__` (`EAttrCategory`, `EAttribute`, `EWeaponType`, `ESkillKind{External,Inner,Movement}`, `EAreaShape`, `EStatus`, `EGearSlot`, `EAiArchetype`) | **PASS** | `data/Defines/schema.xml`: the eight named enums + `Language`, with explicit values. `EAttribute` has all 34 attributes from glossary §1, grouped `Martial 1–9 / Neili 10–19 / Body 20–29 / Mind 30–39 / Meridian 40–49 / LifeSkill 50–69`. Two members were added beyond the prompt and justified in ADR 0103 §4: `EWeaponType.None = 0` (so an inner/movement skill can carry no weapon) and `ESkillTarget { Enemy, Ground, Self }` (Stage 02's `UseSkill` needs to know whether the target tile must hold an enemy, may be empty, or is the caster). `sample` is deleted. |
| 2 | `__beans__` (`AttrMod`, `StatTerm`, `SkillLevelBonus`) | **PASS** | `schema.xml` §Beans. `AttrMod { attribute, value }`, `StatTerm { attribute, coefficient, min, max }`, `SkillLevelBonus { level, attribute, value }`. `AttrMod` dropped the glossary's `SourceId`: the row that carries a mod is its source, because a field inside a bean list cannot be blank (ADR 0103 §1); the glossary row was updated. |
| 3 | Empty tables with 1–2 sample rows: `attribute`, `stat_formula`, `skill`, `skill_level`, `gear`, `status`, `fighter`, `ai_profile`, `arena_map`, `tournament`, `reward_pool`, `background`, `constant`, `localization` th/en | **PASS** | `data/Defines/__tables__.csv` registers 14 tables; `data/tables/*.csv` = 13 files (3/2/2/2/2/2/2/2/1/1/2/2 rows + the one-row `constant`). `localization/localization.csv` keeps the shell's `ui.*`, `title.*`, `hub.*`, `encounter.*`, `result.*` keys (the Unity screens read them today) and drops `ui.sample.*` for the game's `attr.*`, `stat.*`, `skill.*`, `gear.*`, `status.*`, `ai.*`, `map.*`, `tournament.*`, `reward.*`, `background.*`, `fighter.*` keys. |
| 4 | `proto/app.proto`: `SaveData`, `CharacterState`, `TournamentState`, `ArenaReplay` (setup + `(tick, command)[]`) | **PASS** | `proto/app.proto` → `lib/Runtime/Generated/Proto/App.cs` (one copy). `ArenaReplay { schema_version, seed, arena_map_id, repeated ReplayFighter fighters, repeated ReplayEntry entries, final_state_hash }`, `ReplayEntry { tick, ReplayCommand }`. `SaveData` nests the existing `SaveHeader` rather than duplicating its two fields, so `CheckpointProbe`/`SaveHeader` and the shell's protobuf probe are untouched. Round-trips covered by `ProbeSerializationTests` (2 new cases + field-number assertions). |
| 5 | `validate_data.ps1` checks: missing references, a `StatTerm` naming a non-existent attribute, a mask of the wrong size | **PASS** | `lib/Tests~/Data.Validator/ConfigValidator.cs`: `reference.missing` (optional single refs and refs in lists), `stat_formula.term_attribute_unknown`, `arena_map.mask_size` (+ `arena_map.mask_char`), plus `data.empty`, `data.blank_id`, `data.duplicate_id`, `localization.missing_key`, `constant.*`, `stat_formula.clamp_order`, `skill.range_order`, `reward_pool.source`, `reward_pool.round_order`. Each has a case in `ConfigRulesTests` and, where data can express it, a fixture. |
| **DoD-1** | `gen` is idempotent (a second run leaves an empty diff) | **PASS** | `tools/gen.ps1` run twice: `42 .cs, 14 .bytes`, `configHash 27912FAC…`, and `diff -r` over a snapshot of `lib/Runtime/Generated` before/after the second run prints nothing. `tools/verify-generated.ps1` independently regenerates into a temp tree: 8 PASS (43 generated `.cs` identical, 14 + 14 `.bytes` identical, `App.cs` identical and unique, metadata hash matches, freshly generated tables validate). |
| **DoD-2** | `validate` passes | **PASS** | `pwsh tools/validate-data.ps1 -DataDir "lib/Tests~/data"` → `[PASS] data rules RESULT: OK (no findings)`, `PASS 5 | FAIL 0 | WARN 0 | SKIP 0`. `tools/check-fixtures.ps1` → `PASS 11 | FAIL 0`, 8 fixtures rejected for the reason each `fixture.json` names (4 `luban` stage, 4 `semantic`). |
| **DoD-3** | a test loads `Tables` | **PASS** | `dotnet test` → **138 passed** (124 before). `ConfigLoadTests` loads all 14 tables through the real `lib/Tests~/data/*.bytes`, asserts the constant row, a formula's two `StatTerm`s in data order, a skill's `SkillLevelBonus` list and its empty optional cells, and the 12×12 mask; `TableNames` is asserted to be the fourteen loader keys. `ConfigEnumParityTests` now compares all ten domain enums against the generated ones by name and value. Unity's own loader path is covered too: `unity: EditMode` **32/32** (including `Config_tables_load_from_resources_and_match_the_generated_hash`) and `unity: PlayMode` **6/6**. |

## 3. Files changed

| Area | Files |
| --- | --- |
| Data | `data/Defines/schema.xml` (rewritten), `data/Defines/__tables__.csv` (14 tables), `data/tables/*.csv` (13 added, `sample.csv` deleted), `data/localization/localization.csv` |
| Proto | `proto/app.proto`, `lib/Runtime/Generated/Proto/App.cs` (generated) |
| Generated | `lib/Runtime/Generated/Luban/**` (42 `.cs` + metadata; the deleted `Sample`/`TbSample`/`Constant` types and their stale `.meta` removed) |
| Domain | `Domain/Config/ConfigEnums.cs` (+9 enums), `ConfigTables.cs` (13 row types + 3 beans; `SampleEntry` deleted), `IGameConfig.cs` (13 lists) |
| Infrastructure | `Infrastructure/Config/LubanGameConfig.cs` (`TableNames` = 14, all mappings) |
| Validator | `lib/Tests~/Data.Validator/ConfigValidator.cs`, `Program.cs` (row report) |
| Tests | `ConfigLoadTests.cs`, `ConfigRulesTests.cs`, `ConfigEnumParityTests.cs`, `ProbeSerializationTests.cs`, `Sim/Program.cs`; fixtures `+3 / −1` (`dangling-ref`, `stat-term-attribute`, `mask-size` added; `missing-note-key` removed; the five kept ones regenerated against the new tables) |
| Unity | `Assets/Main/Tests/EditMode/ShellSmokeTests.cs` (two constant assertions → `AtbFull`/`MatchTicks`/`Skills`) |
| Docs | `docs/adr/0103-…md` (new), `llmwiki/systems/data-schema.md` (rewritten), `llmwiki/glossary.md` (+`SkillLevelBonus`, `EAiArchetype`, `ESkillTarget`, `EWeaponType.None`, the stable-block note, `AttrMod` shape), `llmwiki/stage-log.md`, `knowledge.md`, `docs/testing/test-suites.md`, this report |

## 4. Commands actually run

```powershell
pwsh -NoProfile -File tools/gen.ps1                  # OK: 42 .cs, 14 .bytes, hash 27912FAC…
pwsh -NoProfile -File tools/gen.ps1                  # OK again; diff -r over Generated: empty
pwsh -NoProfile -File tools/verify-generated.ps1     # PASS 8 | WARN 1 (uncommitted output) | RESULT: OK
pwsh -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"   # PASS 5 | FAIL 0 | no findings
pwsh -NoProfile -File tools/check-fixtures.ps1       # PASS 11 | FAIL 0 — 8 fixtures rejected
dotnet test "lib/Tests~/ProjectJianghu.Core.sln"     # Passed 138, Failed 0
pwsh -NoProfile -File tools/test.ps1                 # Fast: PASS 9 | FAIL 0 | NOT RUN 3 | WARN 0 (96.6 s)
node tools/check-links.mjs                           # 275 links in 66 files: all resolve
# Unity, Editor 6000.3.9f1 open: recompile, then
#   run_tests --mode editor      -> 32/32 passed
#   run_tests --mode playmode    -> 6/6 passed
```

`Full` was **NOT RUN**: it needs the Editor closed (WebGL build + Playwright) and this stage
changed no screen. The Fast run's three NOT RUN rows are Full-only steps, by design.

## 5. Not tested, and risks

- **Bean lists are positional and fragile.** `Toughness|600|0|99999999|…` is two terms because
  the list separator also separates a bean's fields, and an empty token shifts everything after
  it. Luban accepts a malformed cell as long as the *types* still line up, so a missing token
  can silently move a coefficient into `min`. Mitigations: every bean field is required, the
  encoding is documented in `schema.xml` and the system note, and the three beans are small.
  A future field addition must rewrite every cell that carries the bean — accepted in ADR 0103.
- **The 34 attributes are not all authored.** `attribute.csv` has 3 rows (Toughness, Sinew,
  NeiliPower), so `stat_formula.term_attribute_unknown` only constrains the terms in use.
  Stage 04 must add the remaining rows; nothing yet forces an `EAttribute` to have a row.
- **Stage 04 inherits a shape, not a balance.** Every number in `data/` is a placeholder chosen
  to satisfy the rules (`match_ticks 3600`, `atb_full 10000`, `damage_mitigation_k 1000`). The
  GDD's 80k–120k HP target is not reachable from the sample formula and is Stage 04's job.
- **`localization` still carries the shell's placeholder keys** (`hub.*`, `encounter.*`), because
  the Unity screens and their tests read them today. Stage 03 renames the scenes and those keys
  move with it; until then the table describes a loop the game no longer wants.
- **The protobuf contract is a shape, not an implementation.** `SaveData`/`TournamentState`/
  `ArenaReplay` have no consumer yet (Stage 02 builds the session, Stage 10 the bracket, Stage 13
  the save). Their field numbers are now wire format and are asserted by
  `ProbeSerializationTests`, so a later rename costs a migration rather than a refactor.
- **`.bytes` are still git-ignored.** A fresh clone must run `gen.ps1` before `dotnet test`;
  `verify-generated`'s WARN row listed 44 uncommitted generated paths at the time of the run —
  expected before the stage commit, and gone after it.
- **`EWeaponType.None` and `ESkillTarget` are additions.** They are small and justified in the
  ADR, but a reviewer who wants the enum list to match the plan exactly will find two extra
  members and one extra enum group.

## 6. Decisions

- [ADR 0103 — Data schema: encoding, stable identifiers, and what the validator owns](../adr/0103-data-schema-encoding-and-stable-identifiers.md)
- System note rewritten: [`llmwiki/systems/data-schema.md`](../../llmwiki/systems/data-schema.md)
