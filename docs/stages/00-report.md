# Stage 00 — fork the template and lay down the documents

## 1. Summary

The game's baseline documents now exist on top of `template-v1`: the GDD and the v0.2 stage
plan moved out of the repository root into `docs/gdd/GDD.md` and `docs/stages/plan.md`, the
glossary became a Thai↔English table for the **whole** stat vocabulary (34 attributes in five
categories, 21 derived combat values, skills/gear, ATB, statuses, grid and the tournament),
and three game ADRs (0100–0102) record the fork's sync rule, the no-ECS decision and the
two-drawn-facings art rule. `knowledge.md` gained the 18-stage status table,
`llmwiki/systems/loop-and-scopes.md` gained the placeholder → real scene mapping, and two
stale references in `AGENTS.md` were corrected. No code changed; the suite still reports
124 xUnit, 32/32 EditMode and PASS 9 / FAIL 0 / NOT RUN 3 on the Fast run.

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | `docs/gdd/GDD.md`: GDD v0.1 from the attached file | **PASS** | `docs/gdd/GDD.md` — the attached document, moved with its CRLF endings intact and unchanged except the opening block (+2 lines): title (`GDD v0.1 และ ArenaBattle Contract v1`), a provenance line pointing at the plan and the glossary, and links from the two locked decisions to ADR 0101 / ADR 0102. Sections 1–2 (GDD + `ArenaBattle` contract v1) are byte-identical to the attachment. |
| 2 | `docs/stages/plan.md`: the Stage Plan revised per the v0.2 table | **PASS** | `docs/stages/plan.md` — the attached v0.2 plan (Stage 00 "ฉบับ v0.2", Stage 03 v0.2 items) with a header giving its provenance (`template-v1`), the report shape (`_TEMPLATE.md`) and the ADR start (0100). |
| 3 | `llmwiki/glossary.md`: Thai↔English for every stat, ATB, external/inner/movement skills, neili, tournament, rest | **PASS** | `llmwiki/glossary.md`: §1 the five `EAttrCategory` values and **34** attribute rows (Martial 7, Neili 4, Body 4, Mind 5, Meridian 2, LifeSkill 12); §2 **21** derived `CombatStatBlock` values with the `X100`/`Permille` convention; §3 skills (`ExternalSkill`/`InnerSkill`/`MovementSkill`), mastery, loadout, weapon/gear slots; §4 ATB (`Atb`, `AtbFull`, `AtbMode`, `ArenaPhase`, `FighterState`, all six commands, wind-up/resolve/recovery, knockback/counter/flank/backstab, `CommandResult`); §5 the four statuses; §6 grid and `IsoDir`; §7 tournament, `Rest`, reward, renown; §8 all `SceneId`s; §9 the four RNG streams. The template's layer/determinism/data rows are kept as §10–13 because they are live type names. |
| 4 | ADR 0100 (from the template + the `upstream/template` sync), 0101 (no ECS), 0102 (4 facings, 2 drawn) | **PASS** | [`../adr/0100-forked-template-and-upstream-sync.md`](../adr/0100-forked-template-and-upstream-sync.md): the refs as they exist (`main` = game, `upstream/template` = `6e64295`, `tpl-v1` = `6e64295`), take `generic:` commits by `cherry-pick -x`, never merge, resolve docs conflicts by rule, gates after each sync. [`../adr/0101-no-ecs-struct-array-fighters.md`](../adr/0101-no-ecs-struct-array-fighters.md): `ArenaFighter[8]`, occupancy `byte[1024]`, no ECS package (confirmed absent from `manifest.json` and `versions.lock.json`). [`../adr/0102-four-facing-sprites-two-drawn-mirrored.md`](../adr/0102-four-facing-sprites-two-drawn-mirrored.md): `SE`/`NE` drawn, `SW`/`NW` mirrored, facing math stays in the enum, 60 frames/character instead of 120. |
| 5 | `knowledge.md`: the Jianghu Stage 00–17 table and the `template-v1` reference | **PASS** | `knowledge.md` → new section *This repository is Wuxia: Jianghu Rising* with the fork refs, a path table, and an 18-row stage table (00 done, 01–17 planned) linking `docs/stages/00-report.md`; the template's T0–T5 table is kept below it. |
| 6 | `llmwiki/systems/loop-and-scopes.md`: placeholder → real scene mapping (Hub → Rest + Bracket, Encounter → Arena, Result → Result, + CharacterCreate) | **PASS** | `llmwiki/systems/loop-and-scopes.md` §6: the target loop `Boot → Title → CharacterCreate → Bracket → Arena → Rest → Bracket → … → Result → Title`, a seven-row table with the landing stage per screen, why `Hub` splits (a waiting screen vs a non-waiting one), and the two shell constraints that survive. |
| **DoD-a** | `check-links` passes | **PASS** | `node tools/check-links.mjs` → `264 link(s) in 64 file(s): all resolve, and every target is in the repository` (exit 0). |
| **DoD-b** | `test.ps1 -Suite Fast` passes | **PASS** | `pwsh -NoProfile -File tools/test.ps1 -Suite Fast` → `PASS 9 \| FAIL 0 \| NOT RUN 3 \| WARN 0`, `RESULT: OK`; xUnit 124 passed, Unity EditMode 32/32. The three NOT RUN rows are Full-only steps. Run twice — 174.8 s cold, 36.7 s warm — with the same verdict, the second one on the exact tree committed here. |
| **DoD-c** | commit `stage-00: jianghu docs baseline` | **PASS** | `git log -1 --format=%s` → `stage-00: jianghu docs baseline`. |

## 3. Not tested, and risks

- **Docs-only stage, so the suite proves nothing about the documents.** The gates that ran are
  regression checks: they say the tree still builds, that the 124 xUnit and 32 EditMode cases
  pass and that the data pipeline is unchanged. `check-links` is the only check whose subject is
  this stage's work.
- **The glossary is a proposal until Stage 01 writes the enums.** Section 1's English names are
  what Stage 01 must put in `__enums__`/`__beans__`; a rename after data exists costs a
  migration. It is not yet guarded by a test — a Stage 01 case that walks `EAttribute` and
  asserts every value has a glossary row would close that gap.
- **`check-links` does not validate anchors.** `knowledge.md#jianghu-stages` and
  `glossary.md#8-scenes-sceneid` are hand-checked, and a future heading edit can break them
  silently.
- **ADR 0100 depends on a branch name, not a remote.** There is no `upstream` git remote: the
  template line lives as `origin/upstream/template`, so the commands in the ADR use
  `origin/upstream/template` and `tpl-v1`. If the template is ever a separate repository, an
  `upstream` remote must be added and the ADR amended (not edited).
- **`Hub` no longer exists as a screen.** Stage 03 replaces the `SceneId`, the `SceneFlow` edges,
  both scene builders and the `__projectJianghuHub` document; `tests/web/loop.spec.ts` will have
  to be re-recorded against the new names, and until it is, the browser suite keeps describing
  the old loop.
- **The GDD is the attached document, including its citations.** It names reference games as
  inspiration, which AGENTS #12 does not forbid for a citation but does forbid for assets, skill
  names, text and UI. Before any of it reaches a public page, the design doc needs the same review
  the assets will get.
- **`AGENTS.md` was corrected, not rewritten.** Rule 1 pointed at `docs/GDD.md` (never existed;
  the GDD was untracked at the root) and the header cited `ADR 0002` for "no ECS" — that number is
  the template's thin-MVP ADR. Both now point at `docs/gdd/GDD.md` and ADR 0101.
- **PlayMode, the WebGL build and Playwright are NOT RUN** by the Fast suite by design (they need
  the Full suite, and the build needs the Editor closed). This stage changed no code and no data,
  so no Full run was spent on it.
- **The GDD's contract is pre-Stage-01.** `ArenaConstants`, `IsoDir` etc. exist only in the GDD;
  Stage 02 creates them, and the values (`MaxGridSize = 32` vs the prototype's 12×12) may shift.

## 4. Commands actually run

```powershell
node tools/check-links.mjs                              # 264 links in 64 files: all resolve (exit 0)
pwsh -NoProfile -File tools/test.ps1 -Suite Fast        # PASS 9 | FAIL 0 | NOT RUN 3, 174.8 s cold / 36.7 s warm
git commit -m "stage-00: jianghu docs baseline"         # this commit
```

No `gen.ps1`, `validate-data` or browser run: `data/`, `proto/` and the Unity project were not
touched.
