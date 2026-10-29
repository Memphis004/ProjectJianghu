# Static data schema (`data/`)

Everything the prototype is balanced with lives here and nowhere else (AGENTS rule 6). Code
holds keys, never numbers or sentences; player-facing text comes only from
`localization.csv` (AGENTS rule 11).

> Pipeline shape: [ADR 0005](../../docs/adr/0005-data-pipeline-shape-and-format.md).
> How the runtime reads it: [ADR 0006](../../docs/adr/0006-config-port-and-generated-code-isolation.md).
> This schema's own decisions (bean-list encoding, stable enum numbers, `#ref` vs the
> validator): [ADR 0103](../../docs/adr/0103-data-schema-encoding-and-stable-identifiers.md).
> Vocabulary: [glossary](../glossary.md).

## Layout

```
data/
  luban.conf                     Luban 5.1.0 config (paths are relative to this file)
  Defines/schema.xml             the 10 enums, the 3 beans, and one bean per table
  Defines/__tables__.csv         the table registry (one row per table)
  tables/*.csv                   one CSV per table (13 of them)
  localization/localization.csv  every player-facing string, th + en
```

`SchemaVersion` (currently **4**) and the `ConfigHash` in
`lib/Runtime/Generated/Luban/ConfigBuildMetadata.g.cs` are written by `tools/gen.ps1`. The
hash covers the generated `.bytes`, so it changes when data changes and not before.

## Commands

```powershell
# data/ + proto/ -> lib/Runtime/Generated/** + both .bytes trees + the config hash
pwsh -NoProfile -File tools/gen.ps1

# the cross-row rules over the generated .bytes (fast; no Luban needed)
pwsh -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"

# prove a regeneration reproduces what is committed (idempotency gate)
pwsh -NoProfile -File tools/verify-generated.ps1

# prove broken data is rejected, and for the right reason
pwsh -NoProfile -File tools/check-fixtures.ps1
```

All four also run inside `tools/test.ps1`. Exit codes: `0` ok, `1` a required check failed,
`2` the tool could not run.

## Tables

`constant` is `mode=one` (exactly one row); every other table is keyed by its first column.

| Table | Key | What it is | Stage that fills it |
| --- | --- | --- | --- |
| `localization` | `key` | every player-facing string, `th` + `en` | ongoing |
| `constant` | one row | ATB bar, match length, command costs, mitigation K, mastery cap | 04 |
| `attribute` | `id` | display metadata per `EAttribute`: category, sort order, display-only, name key | 04 |
| `stat_formula` | `id` | one derived stat: `clamp(base + Σ terms, min, max)` | 04 |
| `skill` | `id` | external / inner / movement skills, their cost, area, telegraph and level bonuses | 04 |
| `skill_level` | `id` | the global mastery ladder (XP per level) | 04 |
| `gear` | `id` | weapons, armour, accessories and their attribute mods | 04 |
| `status` | `id` | the four abnormal states and what they do per tick | 04 |
| `fighter` | `id` | the 31 tournament opponents: rank, AI, loadout, attribute values | 04 |
| `ai_profile` | `id` | the 4 archetypes and their decision parameters | 04 |
| `arena_map` | `id` | the grid size and its walkable mask | 04 |
| `tournament` | `id` | bracket seats, reward choices, attribute points | 04 |
| `reward_pool` | `id` | weighted reward offers per round band | 04 |
| `background` | `id` | the 4 character-creation backgrounds | 04 |

Stage 01 ships **1–2 rows per table** so the pipeline, the port and the rules are exercised;
Stage 04 replaces the sample rows with the prototype's content.

## Encoding: a bean list is a flat token stream

`schema.xml` declares `list#sep=|,StatTerm`. The CSV cell holds the elements back to back,
and the list separator also separates the bean's own fields:

```
stat.max_hp   base 30000   terms: Toughness|600|0|99999999|Sinew|200|0|99999999
```

That is two `StatTerm`s — `(Toughness, 600, 0, 99999999)` and `(Sinew, 200, 0, 99999999)`.

Rules that follow from it:

- **A field inside a bean list is always required.** An empty token is skipped, so
  `A|5||B|7` shifts every following value (and the failure usually shows up as a type error
  on the *next* field). Optional text therefore lives on a top-level column.
- **Changing a bean changes every cell that carries it.** Keep bean lists for shapes whose
  term count really varies per row: `StatTerm`, `AttrMod`, `SkillLevelBonus`.
- **A list of plain strings** (`fighter.skill_ids`, `arena_map.mask`,
  `background.start_gear_ids`) is a normal `|`-separated cell and may be empty.
- A **`#ref` inside a list is checked** by the strict flag exactly like a top-level one
  (measured; see ADR 0103 §1).

## References: two mechanisms, one owner each

```xml
<var name="ai_profile_id" type="string#ref=TbAiProfile"/>   <!-- required: fails gen.ps1 -->
<var name="weapon_id"     type="string"/>                   <!-- optional: validator rule    -->
<var name="terms"         type="list#sep=|,StatTerm"/>      <!-- cross-row: validator rule   -->
```

- **`#ref=TbX`** points at another table's key column and is enforced with the strict flag,
  so a dangling cell stops the build before any `.bytes` are written.
- **An optional reference stays `string`**, because an empty cell is a legal ref and Luban
  cannot express "if set, then it must exist". `Data.Validator` reports `reference.missing`.
- **`#(range=[a,b])`** is Luban's own value validator; the error names the table and column.

## Cross-table rules (`lib/Tests~/Data.Validator`)

| Code | What it means |
| --- | --- |
| `localization.empty` | the localization table has no rows |
| `localization.blank_key` | a row has no key |
| `localization.duplicate_key` | two rows share a key (the index collapses them, so only a snapshot sees it) |
| `localization.blank_text` | both languages are empty, so the row can never be shown |
| `localization.missing_language` | one language is filled and the other is not |
| `localization.missing_key` | an optional text column (`desc_key`, `note_key`) names a key that does not exist |
| `data.empty` | one of the 12 keyed data tables has no rows |
| `data.blank_id` / `data.duplicate_id` | a key column is blank, or repeats |
| `reference.missing` | an optional or list reference names a row that does not exist |
| `constant.non_positive` / `constant.negative` / `constant.out_of_range` | a tuned number a division or a comparison would break on (a cost above a full ATB bar is `out_of_range`) |
| `stat_formula.clamp_order` | `clamp_max` is below `clamp_min` |
| `stat_formula.term_attribute_unknown` | a `StatTerm` names an `EAttribute` with no `attribute` row |
| `skill.range_order` | `range_max` is below `range_min` |
| `arena_map.mask_size` | the mask has a row count ≠ `height`, or a row length ≠ `width` |
| `arena_map.mask_char` | a mask cell is neither `.` (walkable) nor `#` (blocked) |
| `reward_pool.source` | an offer names both a gear row and a skill, or neither |
| `reward_pool.round_order` | `round_max` is below `round_min` |

Rules are about **shape, never balance**: a number being small is a design decision; a
pointer at a row that does not exist is a bug.

Add a rule by editing `ConfigValidator.cs`, adding a case to `ConfigRulesTests.cs`, and —
when data can express it — a fixture under `lib/Tests~/fixtures/` whose `fixture.json` names
the expected code and the stage that must catch it (`luban` or `semantic`).

## Adding or changing a table

1. Edit `Defines/schema.xml` (bean/enum) and `Defines/__tables__.csv` (registry row), then the CSV.
2. `pwsh tools/gen.ps1` — rewrites the generated C#, both `.bytes` trees and the config hash.
3. Add the domain type (`Domain/Config/ConfigTables.cs`), the list on `IGameConfig`, the
   mapping in `Infrastructure/Config/LubanGameConfig.cs`, and the table name in
   `LubanGameConfig.TableNames` — the validator, the Sim and the Unity loaders iterate that
   list, so a table missing from it is a table nothing checks.
4. Bump `SchemaVersion` in `tools/gen.ps1` when a field is added or renamed.
5. `tools/test.ps1 -Suite Fast` and commit `data/` **together with** the regenerated
   `lib/Runtime/Generated/**` and `UnityProject/Assets/Main/Resources/Config/**`.

## Values that are easy to get wrong

- **An XML comment in `schema.xml` may not contain a double dash.** Luban reports a line and
  column *inside* the comment, not the flag being described. (`--strict` in prose has cost
  this project a build.)
- **`base` is a preserved keyword** in Luban's code generator: the stat formula's constant is
  `base_value`.
- **A duplicate key is a `luban`-stage fixture, not a validator finding.** The table index
  collapses duplicates during generation; `data.duplicate_id` exists for snapshots.
- **Ticks are 30 Hz.** `constant.match_ticks = 3600` is 120 seconds; a "seconds" column
  would be a float in a hashed state, which AGENTS rule 5 forbids.
- **`x100` suffix means times 100** (`ratio_x100`, `retreat_hp_x100`, `atb_multiplier_x100`
  — 100 is "unchanged"). Integers everywhere, so two replays can be compared byte for byte.
- **IDs are stable strings, not row numbers** (`skill.falling_rain_slash`). They appear in
  saves and replays, so renaming one is a data migration.
- **An enum value is stable too.** Numbers are written explicitly and grouped in blocks; a
  new member takes the next free number in its block. `ConfigEnumParityTests` compares the
  Domain mirror by name *and* value.
- **`data/` is not live until `gen.ps1` runs.** The player and the tests read generated
  bytes; `verify-generated` is what proves they match.
