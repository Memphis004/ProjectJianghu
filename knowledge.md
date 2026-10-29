# ProjectJianghu — template knowledge base

A **genre-free game skeleton**: Unity 6.3 + URP shell, an engine-free rule library
(`lib/`, a local UPM package), a data pipeline whose numbers live in `data/`, and a
tooling set that builds, tests and ships a **Web (WebGL2, single-thread)** player.

The loop this fork ships (Stage 03) is the game's, with two placeholder steps:

```
Boot → Title → CharacterCreate → Bracket → Rest ⇄ Arena → Result → Title
```

Three lifetimes (`App`, `Run`, `Scene`) are wired, a run is started by the title
(ADR 0022), and every scene change goes through one loader. `CharacterCreate` and
`Bracket` are placeholders until Stage 11 and Stage 10; the Rest and Arena screens are
where the tournament loop is played.

This file is the fast on-ramp. Read it first, then follow the links.

---

## This repository is Wuxia: Jianghu Rising

The tree is `template-v1` copied, renamed and booted ([`docs/stages/j-boot-report.md`](docs/stages/j-boot-report.md)).
From Stage 00 on, **the game's documents own the shared files**. The template line is
kept on the `upstream/template` branch with the fork point at tag **`tpl-v1`**, and
upgrades arrive as `generic:` commits only — see
[ADR 0100](docs/adr/0100-forked-template-and-upstream-sync.md).

| Where the game lives | Path |
| --- | --- |
| The game's rules | [`docs/gdd/GDD.md`](docs/gdd/GDD.md) — GDD v0.1 + the ArenaBattle contract v1 |
| The work plan | [`docs/stages/plan.md`](docs/stages/plan.md) — 18 stages, v0.2 |
| Stage reports | [`docs/stages/`](docs/stages/) — `NN-report.md`, from [`_TEMPLATE.md`](docs/stages/_TEMPLATE.md) |
| The game's ADRs | [`docs/adr/`](docs/adr/) — **0100 and up**; 0001–0099 are the template's |
| The game's vocabulary | [`llmwiki/glossary.md`](llmwiki/glossary.md) |
| The game's systems | [`llmwiki/systems/`](llmwiki/systems/) |

### Jianghu stages

Eighteen stages. A row moves to **done** only when its report exists and its gate passed;
`dotnet`-only stages (02, 05, 06) still owe the Unity build at the next stage that opens
the Editor.

| Stage | Deliverable | State | Evidence |
| --- | --- | --- | --- |
| **00** | Fork + docs baseline: GDD, plan, glossary, ADR 0100–0102, scene mapping | **done** | [`docs/stages/00-report.md`](docs/stages/00-report.md) |
| 01 | Luban/protoc rebuilt for the game: 10 enums, 3 beans, 13 tables + localization, `SaveData`/`CharacterState`/`TournamentState`/`ArenaReplay` | **done** | [`docs/stages/01-report.md`](docs/stages/01-report.md) |
| 02 | Arena contract in `Domain.Arena` + shared `ArenaFrameBuffer` + `FakeArenaSession`/factory/auto-resolver + `ArenaReplay` + contract suite | **done** | [`docs/stages/02-report.md`](docs/stages/02-report.md) |
| 03 | Unity shell → Jianghu screens (`CharacterCreate`, `Bracket`, `Rest`, `Arena`); HD-2D iso spike; 3 URP tiers; WebGL canvas | **done** | [`docs/stages/03-report.md`](docs/stages/03-report.md) |
| 04 | Prototype content: attributes, `stat_formula`, 13 skills, gear, 4 statuses, 4 backgrounds, 31 fighters, map, reward pool | **done** | [`docs/stages/04-report.md`](docs/stages/04-report.md) |
| 05 | Character / stats / loadout domain: `StatResolver`, `ModifierStack`, `PowerScore`, `ArenaFighterBuilder`, `CharacterCreation` | **done** | [`docs/stages/05-report.md`](docs/stages/05-report.md) |
| 06 | Grid and targeting domain: reachability, jump, area shapes, facing (pure, zero-alloc) | planned | — |
| 07 | `ArenaSimulation`: struct-array fighters ([ADR 0101](docs/adr/0101-no-ecs-struct-array-fighters.md)), ATB, resolve, stats | planned | — |
| 08 | Arena AI: utility scoring, 4 archetypes, telegraph evasion | planned | — |
| 09 | Arena presentation: HD-2D scene, `ArenaRunner`, sprite pool, tile highlights, HUD | planned | — |
| 10 | Tournament + run flow: bracket, background matches, rewards, `Rest` | planned | — |
| 11 | Character sheet, loadout and character-create UI | planned | — |
| 12 | Vertical slice: the whole loop, 20 `Arena` ⇄ `Rest` crossings | planned | — |
| 13 | Save, resume, settings (protobuf, IndexedDB/browser storage) | planned | — |
| 14 | Content, juice and UX: balance, hit-stop, tutorial, sprite pipeline | planned | — |
| 15 | Test harness + CI: bridge, Playwright arena specs, `tools/ci.ps1` | planned | — |
| 16 | Web release candidate: profiling, stripping, link.xml, clean build | planned | — |
| 17+ | Feature work from the baseline (repeatable prompt) | planned | — |

The template's own T0–T5 steps are separate and already closed; their table stays below.

---

## Status

The template is extracted from a finished prototype (the game repo, tag
`survive-stage-10`). Its own steps are T0–T5; **reports live in `docs/template/`**.

| Step | Title | State | Evidence |
| --- | --- | --- | --- |
| **T0** | Inventory and plan | done | [`docs/template/inventory.md`](docs/template/inventory.md) |
| **T1** | Cut `lib/` + `data/`, rebuild the Unity shell, re-point the tools | done, green | [`docs/template/t1-report.md`](docs/template/t1-report.md) |
| **T2** | Data pipeline, proto, validator and fixtures | done, green | [`docs/template/t2-report.md`](docs/template/t2-report.md) |
| **T3** | Docs and knowledge: layers, ADR split, gotchas, links | done, green | [`docs/template/t3-report.md`](docs/template/t3-report.md) |
| **T4** | Rename tool + a rehearsal on a throwaway copy | done, green | [`docs/template/t4-report.md`](docs/template/t4-report.md) · [`rename-rehearsal.md`](docs/template/rename-rehearsal.md) |
| **T5** | Baseline commit and `template-v1` tag | done, green | [`docs/template/t5-report.md`](docs/template/t5-report.md) · [`docs/template/baseline.md`](docs/template/baseline.md) · [`docs/template/CHANGELOG.md`](docs/template/CHANGELOG.md) |

A game built from this template starts its own stage numbering at 01 and writes
its reports under `docs/stages/` (skeleton: [`docs/stages/_TEMPLATE.md`](docs/stages/_TEMPLATE.md)).

**Current state:** Stage 05 adds the pure C# `Domain.Character` rules (character state, source-preserving modifiers, config-driven stat resolution, loadout validation, character creation, mastery growth and arena-fighter setup building). The package has the game's fourteen data tables (`localization`,
`constant`, `attribute`, `stat_formula`, `skill`, `skill_level`, `gear`, `status`,
`fighter`, `ai_profile`, `arena_map`, `tournament`, `reward_pool`, `background`), the
`IGameConfig` port with its Infrastructure adapter, the shared
deterministic primitives (`IRandomSource`/`Xoshiro128`/`RandomStreams`,
`IClock`/`SystemClock`, `FixedTicker`, `EventRing<T>`, `PrevCurBuffer<T>`,
`Result`/`Error`), the run skeleton (`RunSession`, `SceneFlow`, `ReplayLog`), a
serialization probe, and the ArenaBattle v1 contract (`Domain.Arena`: commands, frames,
queries, events, results) with the shared `ArenaFrameBuffer`, a deterministic
`FakeArenaSession`/factory/auto-resolver and `ArenaReplay` in Application. Unity ships eight
scenes (Boot, Title, ThaiTextSpike, CharacterCreate, Bracket, Rest, Arena, Result), the HD-2D
isometric spike with its sorting probe, three URP quality tiers, a project WebGL template, a
7-probe smoke suite, the page bridge and the Web build pipeline. `dotnet` carries **208** xUnit cases;
Unity carries **42** EditMode and **7** PlayMode cases; Playwright has **4** tests in
three specs (`smoke`, `title`, `loop`).

**Engine lock:** Unity **`6000.3.9f1`** (revision `7a9955a4f2fa`), Unity Personal
license. Locked in [`tools/versions.lock.json`](tools/versions.lock.json).

### What the template deliberately does not have

| Absent | Why | Where the game-era version is kept |
| --- | --- | --- |
| A genre, a theme, a combat loop | the template is the shell, not a game | [`docs/template/examples/`](docs/template/examples/) |
| An ECS engine (EcsLite) | it left with the battle rules; `Simulation` is an empty guarded layer a game fills | [`docs/template/examples/gotchas/ecslite-pool-refs.md`](docs/template/examples/gotchas/ecslite-pool-refs.md) |
| A save format | `Contracts/Save` holds only the serialization probe | ADR 0012 and the probe's own remarks |
| The template's original game screens | Hub/Encounter were placeholders in the template baseline | the game's current loop is documented in Stage 03 |

---

## Commands — every tool in `tools/`

Exit codes are part of the contract: `0` = the required checks passed, `1` = a
required check failed, `2` = the tool itself could not run.

| Tool | What it does | Typical use |
| --- | --- | --- |
| [`doctor.ps1`](tools/doctor.ps1) | environment / dependency health check with a reliable exit code | after cloning, or when a tool cannot be found |
| [`test.ps1`](tools/test.ps1) | the Fast and Full suites over dotnet, Unity and the browser; writes `artifacts/test-summary.md` | the working loop (`-Suite Fast`) and stage gates (`-Suite Full`) |
| [`setup-tools.ps1`](tools/setup-tools.ps1) | downloads Luban 5.1.0 + protoc 33.6 into `tools/vendor/` and checks their SHA-256 | once per clone, before `gen.ps1` |
| [`gen.ps1`](tools/gen.ps1) | runs the data pipeline: `data/` + `proto/` → `lib/Runtime/Generated/**`, both `.bytes` sets, the config hash | after any edit under `data/` or `proto/` |
| [`validate-data.ps1`](tools/validate-data.ps1) | runs `lib/Tests~/Data.Validator` over the generated tables (cross-row rules Luban cannot express) | after `gen.ps1`; needs `-DataDir` (see the gotcha) |
| [`verify-generated.ps1`](tools/verify-generated.ps1) | regenerates into a temp directory and compares byte for byte with what is committed | the reproducibility gate, inside Fast |
| [`check-fixtures.ps1`](tools/check-fixtures.ps1) | proves every deliberately broken fixture under `lib/Tests~/fixtures/` is rejected **for the reason its `fixture.json` names** | after adding a data rule |
| [`sim.ps1`](tools/sim.ps1) | builds and runs the headless determinism harness (`lib/Tests~/Sim`): one fingerprint per random stream + `determinismDigest` | determinism and stream-separation checks |
| [`build-web.ps1`](tools/build-web.ps1) | batch-mode WebGL build through the project's own pipeline; Development or Release, optionally with a size profile | makes the player the browser tests need |
| [`serve-web.mjs`](tools/serve-web.mjs) | static server that sets the content types a WebGL player needs | serving `UnityProject/Builds/Web` to Playwright or a person |
| [`measure-web-size.mjs`](tools/measure-web-size.mjs) | raw / gzip -9 / Brotli q11 size of every file in a build | any payload claim |
| [`measure-il2cpp-size.mjs`](tools/measure-il2cpp-size.mjs) | attributes `.wasm` bytes to the managed assembly they came from | deciding whether a package is worth its bytes |
| [`coverage-report.mjs`](tools/coverage-report.mjs) | per-file line coverage from a Cobertura report | measuring a coverage target |
| [`font-metrics.py`](tools/font-metrics.py) | measures a font file's glyph metrics at a fixed display size | an on-screen text-size claim about the face |
| [`screen-text-metrics.py`](tools/screen-text-metrics.py) | measures the ink height of text inside rectangles a player published, from a screenshot | an on-screen text-size claim about the build |
| [`check-links.mjs`](tools/check-links.mjs) | checks that every relative markdown link under `docs/` and `llmwiki/` resolves to a path git tracks | before committing a docs change |
| [`rename.ps1`](tools/rename.ps1) | renames the project identity (six spellings) in file contents, file names and folder names, or in a patch file | once, on a fresh copy, before the game's first commit |
| [`reference/xoshiro128_reference.py`](tools/reference/xoshiro128_reference.py) | an independent reference implementation of the RNG | cross-checking `Xoshiro128Tests` |

Two paths in `tools/` are not tools: `versions.lock.json` (the engine, toolchain and
package lock) and `vendor/` (the downloaded CLIs — never committed).

`test.ps1 -Suite Fast` (the default) runs dotnet build + xUnit + the Sim modes + the
four data gates + Unity EditMode. `-Suite Full` adds PlayMode, a WebGL build and
Playwright. Fast needs the **Editor open**; Full's web steps need it **closed**, so
the complete verdict is two runs. Which test lives in which suite, with timings:
[`docs/testing/test-suites.md`](docs/testing/test-suites.md).

```powershell
pwsh -NoProfile -File tools/doctor.ps1
pwsh -NoProfile -File tools/test.ps1                 # Fast
pwsh -NoProfile -File tools/test.ps1 -Suite Full     # Full (add -Release for a gate)
pwsh -NoProfile -File tools/setup-tools.ps1          # once
pwsh -NoProfile -File tools/gen.ps1
pwsh -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"
pwsh -NoProfile -File tools/verify-generated.ps1
pwsh -NoProfile -File tools/check-fixtures.ps1
pwsh -NoProfile -File tools/sim.ps1 --seed 7 --trace 5
node tools/check-links.mjs
pwsh -NoProfile -File tools/rename.ps1 -From ProjectJianghu -To MyGame -DryRun
```

Starting a game is that rename plus a commit: [`docs/template/README.md`](docs/template/README.md)
walks the whole first hour, and [`docs/template/rename-rehearsal.md`](docs/template/rename-rehearsal.md)
is a real run of the tool on a throwaway copy with the suites that followed it.

---

## Environment (verified 2026-10-13)

| Tool | Version |
| --- | --- |
| OS | Windows 11 `10.0.26300` x64 |
| Git / Git LFS | `2.55.0.windows.3` / `3.7.1` |
| PowerShell | Windows PowerShell `5.1.26100.9444`; PowerShell `7` (preview) |
| .NET SDK | `6.0.428`, `9.0.311` |
| Node.js / npm | `v25.6.1` / `11.9.0` |
| Unity Editor | `6000.3.9f1` + WebGL Build Support |
| Browsers | Chrome `154.0.8037.98`; Edge `145.0.3800.70`; **Firefox not installed** |

Reference machine for all performance work: Intel Core i5-14400F, 32 GB RAM,
NVIDIA RTX 5060 Ti — see [ADR 0003](docs/adr/0003-performance-baseline-machine-and-browser.md).

---

## Repository map

```text
AGENTS.md              permanent rules for every stage
knowledge.md           ← you are here
proto/app.proto        external contract (CheckpointProbe, SaveHeader)
lib/                   local UPM package (com.projectjianghu.core)
  Runtime/{Shared,Domain,Application,Simulation,Infrastructure,Contracts,Generated}
                       one .asmdef per layer
  Runtime/Generated/   machine output (Luban readers, protoc App.cs) — never edited
  third_party/luban-runtime/   vendored Luban C# runtime
  Tests~/              dotnet harness: one csproj per layer + xUnit Core.Tests
                       + Sim console + Data.Validator + fixtures/ (broken data)
data/                  the only place a balance number lives (3 tables)
docs/
  adr/                 template decisions, 0001–0099
  architecture/        layers and dependencies
  testing/             which test runs when
  build/               payload size method
  stages/              a game's stage reports (skeleton: _TEMPLATE.md)
  template/            the extraction's own plan and reports
    ORIGIN.md          provenance: the only file (with examples/) the rename leaves alone
    examples/          the game-era ADRs, notes and gotchas — read-only examples
llmwiki/               index.md glossary.md stage-log.md systems/ gotchas/
tools/                 every script in the table above, + vendor/ (not committed)
tests/web/             Playwright specs against a real WebGL build
artifacts/             untracked logs/reports/screenshots
UnityProject/          the Unity shell
```

---

## Architecture in one breath

`Shared ← Domain ← Application ← Presentation`.

- `Simulation` may reference **Shared and Domain only**.
- `Infrastructure` implements **Application** ports (Application never references it).
- `Contracts` holds DTOs and references nothing of ours.
- `Generated` is machine output and is never hand-edited.

**Domain / Application / Simulation never touch `UnityEngine`; Domain and
Simulation never touch R3, VContainer or a serializer.** Enforced by asmdef *and*
by guard tests in `lib/Tests~`.

Time and randomness are explicit: a **fixed 30 Hz tick** (`FixedTicker`), and
randomness only through `IRandomSource` with separate **run / encounter / reward /
visual** streams. Every mutating command is **atomic** and returns a **typed result**.

Full detail: [`docs/architecture/overview.md`](docs/architecture/overview.md).

---

## Document links

| Document | Purpose |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | permanent rules for every stage |
| [`docs/architecture/overview.md`](docs/architecture/overview.md) | layers, lifetimes, command flow, data pipeline |
| [`docs/architecture/dependencies.md`](docs/architecture/dependencies.md) | dependency matrix (Verified / Proposed / Blocked) |
| [`docs/testing/test-suites.md`](docs/testing/test-suites.md) | Fast vs Full, every test's home, timings, NOT RUN rules |
| [`docs/template/README.md`](docs/template/README.md) | what the template is and how to start or upgrade a game |
| [`docs/adr/`](docs/adr/) | template decisions (0001–0099) |
| [`docs/template/examples/`](docs/template/examples/) | the game's ADRs and notes — examples, not rules |
| [`llmwiki/index.md`](llmwiki/index.md) | wiki index (systems + gotchas) |
| [`llmwiki/glossary.md`](llmwiki/glossary.md) | code names ↔ Thai terms |
| [`llmwiki/systems/data-schema.md`](llmwiki/systems/data-schema.md) | the fourteen tables, their rules, the pipeline |
| [`llmwiki/systems/loop-and-scopes.md`](llmwiki/systems/loop-and-scopes.md) | the loop and the three scopes |
| [`llmwiki/systems/unity-shell.md`](llmwiki/systems/unity-shell.md) | scenes, DI, the published globals, the Web build |
| [`tools/versions.lock.json`](tools/versions.lock.json) | engine + tool version lock |

---

## Known gaps carried forward

- `UI.MVP Lite` has **no repository**; the project owns a thin MVP
  ([ADR 0002](docs/adr/0002-thin-mvp-ui-approach.md)).
- `Simulation` is an empty, guarded layer: a game that needs an ECS engine adds one
  (the game-era EcsLite note is kept under `docs/template/examples/`).
- `Contracts/Save` holds only `CheckpointProbe`; the first real save format is the
  game's decision, and it should start at schema version 1 with a migration story.
- `SaveHeader` has no reader yet.
- The template's scene capture tool was removed with the game screens; UI layout is
  checked from Play Mode screenshots until a game adds one back.
- Firefox is not installed, so cross-browser runs are NOT RUN.
- No CI: every gate is a local command. Stage T5 records the baseline the first
  game starts from.
