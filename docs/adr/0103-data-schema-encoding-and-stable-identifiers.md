# ADR 0103 — Data schema: encoding, stable identifiers, and what the validator owns

- **Status:** Accepted
- **Date:** 2026-10-29
- **Stage:** 01 (binding from Stage 04, which fills the content, and Stage 02, which reads the rows)
- **Supersedes:** nothing
- **Related:** [ADR 0005](0005-data-pipeline-shape-and-format.md) (pipeline shape),
  [ADR 0006](0006-config-port-and-generated-code-isolation.md) (the port),
  [`llmwiki/systems/data-schema.md`](../../llmwiki/systems/data-schema.md),
  [glossary §1–§5](../../llmwiki/glossary.md)

## Context

Stage 01 replaces the template's three Survive-era tables (`localization`, `constant`,
`sample`) with the game's fourteen and declares the enums and beans the GDD talks about.
Four questions had no answer in the template, and every one of them had to be settled
before a single row could be written:

1. **How does a bean inside a list appear in a CSV cell?** The GDD wants
   `stat_formula = base + Σ(coefficient × attribute)`, i.e. a list of `StatTerm`.
2. **Which references does the schema enforce, and which does `Data.Validator` own?**
   Luban's `#ref` rejects a dangling cell during `gen.ps1`; an *empty* cell is a legal
   ref, so "if set, it must exist" cannot be a schema rule.
3. **How do enums behave as identifiers?** They reach replays, saves and the wire
   (`ArenaReplay`, `CharacterState.attributes`), so a renumbering is a data migration.
4. **Where does "this attribute is display-only" live** — the enum or a table?

The three answers below were measured against the installed Luban 5.1.0 CLI, not taken
from documentation; the probes are recorded in §Decision 1 and in the system note.

## Decision

### 1. A bean list is a flat token stream, and an empty token is forbidden

`schema.xml` declares `list#sep=|,StatTerm`; the CSV cell is

```
Toughness|600|0|99999999|Sinew|200|0|99999999
```

Two `StatTerm`s, because **the list separator also separates the bean's own fields**.
Rejected alternatives, all measured:

| Cell form | Result |
| --- | --- |
| `A,5\|B,7` (quoted, comma inside) | `parse_failed`: the whole element is read as one enum name |
| `A;5\|B;7` | the same: a bean field is not separated by `;` |
| `{A,5}\|{B,7}` | the same: no brace syntax in a CSV cell |
| `A\|5\|B\|7` | **accepted**: positional, in field order |

A consequence that is a rule, not a footnote: an **empty token shifts every following
value** (`A|5||B|7` parses `7` as the second element's first field). So a field inside a
bean list is always required, and optional text lives on a top-level column, where an
empty CSV cell is legal. That is why `AttrMod` is `{ Attribute, Value }` (the owning row
is the source) and why a skill's "special at level 5/10" is a `skill_level` row's key
rather than a field of `SkillLevelBonus`.

The encoding is documented next to the types (`schema.xml` header, the system note) and a
bean whose fields change rewrites every cell that carries it — so a bean list is worth it
only when the term count varies per row, which is true for `StatTerm`, `AttrMod` and
`SkillLevelBonus` and false for everything else.

### 2. `#ref` for a required pointer, the validator for an optional one

| Column kind | Enforced by | Example |
| --- | --- | --- |
| required reference, one value | Luban `#ref` + the strict flag | `fighter.ai_profile_id`, `skill.name_key` |
| required reference, inside a list | Luban (verified: a ref inside a list is checked too) | — (none needed yet) |
| optional reference, one value | `ConfigValidator` → `reference.missing` | `fighter.weapon_id`, `skill.status_id`, `reward_pool.gear_id` |
| optional reference, inside a list | `ConfigValidator` → `reference.missing` | `fighter.skill_ids`, `background.start_gear_ids` |
| cross-row rule no schema can state | `ConfigValidator` | `stat_formula.term_attribute_unknown`, `arena_map.mask_size` |

The optional columns are plain `string` on purpose. Turning them into `#ref` would make
"this fighter carries no armour" impossible to write, which is a worse trade than one
validator rule.

### 3. Enum numbers are stable and blocked; the table is the display metadata

Every enum value is written explicitly (Luban otherwise takes declaration order), and
`EAttribute`'s values sit in per-category blocks — Martial 1–9, Neili 10–19, Body 20–29,
Mind 30–39, Meridian 40–49, LifeSkill 50–69 — so an attribute added for the open-world
layer takes a free number in its block instead of renumbering saves. `ConfigEnumParityTests`
proves the Domain mirror (which Domain needs so it never references generated code,
ADR 0006) still agrees *by value*, not merely by name.

The enum says what code may switch on; the `attribute` table says how the value is shown,
whether it is `display_only`, and what order it appears in. Two tables of truth is
deliberate: the enum is a compile-time vocabulary, the table is content a designer edits
without a rebuild of the rules. The validator closes the gap in the one direction that
matters — a `StatTerm` may not name an attribute no `attribute` row describes — but does
not require all 34 rows yet (Stage 04 authors them).

### 4. Additions beyond the stage prompt, and why each exists

| Addition | Why |
| --- | --- |
| `EWeaponType.None = 0` | an inner/movement skill and a non-weapon gear row carry no weapon; an empty enum cell reads back as 0, so 0 must be a legal member |
| `ESkillTarget { Enemy, Ground, Self }` | `ArenaCommand` always carries a target tile (GDD §2), so a skill row must say whether that tile must hold an enemy, may be empty, or is the caster's own — without it Stage 02 cannot validate a `UseSkill` |
| `tournament` keyed by `id`, not `mode=one` | the table is a tournament *definition*; a second tournament type is a row, not a schema change, and it keeps the validator's one id rule |
| `SkillLevelBonus` on `skill`, `skill_level` as the ladder | the prompt asks for both tables; a bonus belongs to the skill that grants it, while the XP ladder is global |
| `arena_map.mask` as a list of strings | one string per row makes the validator's size rule a row-count and a line-length check, and keeps a 12×12 map readable in the CSV |

## Consequences

- **`data/` and `lib/Runtime/Generated/**` change together.** `SchemaVersion` in
  `ConfigBuildMetadata.g.cs` is hand-bumped to 4 for this re-baseline.
- **A bean list is a schema-wide edit.** Adding a field to `StatTerm` means editing every
  `stat_formula` cell; that is acceptable at four tables and would not be at forty.
- **Eight validator codes are new** (`data.empty`, `data.blank_id`, `data.duplicate_id`,
  `reference.missing`, `localization.missing_key`, `stat_formula.clamp_order`,
  `stat_formula.term_attribute_unknown`, `skill.range_order`, `arena_map.mask_size`,
  `arena_map.mask_char`, `reward_pool.source`, `reward_pool.round_order`) and each has
  either a fixture under `lib/Tests~/fixtures/` or a case in `ConfigRulesTests`.
- **The id namespace is not global.** `gear`, `skill`, `fighter` and the rest each own
  their ids; only `background` and `fighter` share the `background.*` prefix by
  convention. A future feature that needs cross-kind addressing should introduce one
  table, not widen every id.
- **`sample` is gone**, with its bean, its CSV, its fixtures and its tests. The two
  reference mechanisms it demonstrated are now carried by real tables:
  `fighter.ai_profile_id` (required, `#ref`) and `fighter.weapon_id` (optional, validator).
