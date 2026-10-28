# J-Boot — the template copy boots (no game content yet)

## 1. Summary

ProjectJianghu is `template-v1` renamed by `tools/rename.ps1`; this stage proves the copy imports,
builds and plays before any game rule exists. The player settings now carry the game's own identity
— company `Wuxia`, product `Wuxia: Jianghu Rising`, bundle `com.wuxia.jianghurising` — and all six
scenes were rebuilt through the Editor builders, so `Assets/Main/Scenes/*.unity` is machine output
again rather than a file carried over from the source repository. One new EditMode test
(`ShellAssetIntegrityTests`) closes the gap the prompt assumed was covered: missing scripts and
unassigned references are now checked over every scene in the build list and every prefab, not only
over the scenes a PlayMode run happens to load. Everything the stage asks for passes: EditMode 32/32,
PlayMode 6/6, the data gates, a Release WebGL build of 60.50 MiB raw / 18.04 MiB gzip / 12.45 MiB
Brotli, and all four Playwright specs against that Release player.

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | PlayerSettings `productName` / `companyName` / `applicationIdentifier` set through the Editor API, then every scene rebuilt | **PASS** | `ProjectSettings.asset` — `companyName: Wuxia`, `productName: 'Wuxia: Jianghu Rising'`, `applicationIdentifier: {Standalone: com.wuxia.jianghurising, WebGL: com.wuxia.jianghurising}`. The values are authored in one place, `ProjectSetup.ProductName/CompanyName/ApplicationIdentifier`, so re-running `ProjectJianghu/Stage 03/1. Configure project` keeps them. All six builders ran (`boot=OK; title=OK; thai=OK; hub=OK; encounter=OK; result=OK`), build list = 6 scenes, and Boot → Title renders (`artifacts/ui/j-boot-title-after-scene-rebuild.png`, 1280×720, Editor Play Mode). |
| 2 | Missing script **and** missing reference over every scene and prefab; the template's EditMode test run and reported; each file the rename reported as carrying the old name in text form confirmed one by one | **PASS** | Template's own check is `ShellPlayModeTests.Every_scene_in_build_settings_loads_without_missing_scripts` (PlayMode, not EditMode) — **6/6 PASS**. It does not cover missing references or prefabs, so `Assets/Main/Tests/EditMode/ShellAssetIntegrityTests.cs` was added: **32/32 EditMode PASS**, both new cases included (`artifacts/j-boot/unity-editmode-status.txt`, `artifacts/j-boot/unity-playmode-status.txt`). Per-file rename confirmation: §2.1. |
| 3 | Unlink Unity Services/Cloud left over from the source project, and record it | **PASS** (nothing was linked) | `ProjectSettings.asset`: `cloudProjectId:` and `organizationId:` are empty; `UnityConnectSettings.asset`: `m_Enabled: 0`, `InsightsSettings.m_Enabled: 0`, `CrashReportingSettings.m_EnableCloudDiagnosticsReporting: 0`, `UnityAnalyticsSettings.m_Enabled: 0`; no `com.unity.services.*` / `com.unity.analytics` / cloud package in `manifest.json` or `packages-lock.json`; no `ProjectSettings/Services*.asset`. There was nothing to unlink, so nothing was changed. |
| 4 | Editor closed, then `doctor` → `gen` → `verify-generated` → `validate-data -DataDir "lib/Tests~/data"` → `test -Suite Full` → `build-web` (Release, CLI) → Playwright (all specs) | **PASS** | `doctor` PASS 13 / FAIL 0 / WARN 1 (Firefox) / SKIP 2; `gen` OK configHash `DDFDF338…`; `verify-generated` PASS 9 / FAIL 0; `validate-data` PASS 5 / FAIL 0 (no findings); `test -Suite Full` PASS 10 / FAIL 0 / NOT RUN 2 — the two NOT RUN are the Unity steps, which need the Editor open (621.7 s); `build-web -Configuration Release -Force` OK, 0 `error CS`, 63,452,981 B; Playwright **4 passed (20.2 s)** in Chrome against the Release player. Logs: `artifacts/j-boot/{doctor,gen,verify-generated,validate-data,test-full-editor-closed,build-release,playwright-release}.log`. |
| 5 | Playwright reads the scene through the new `__projectJianghu*` globals | **PASS** | `tests/web/globals.mjs` exports `SCENE = "__projectJianghuScene"` and the specs import only those constants; the Release run published `__projectJianghuSmoke` (7 probes, `failedCount 0`, hash `DDFDF338`), `__projectJianghuTitle` → `Hub` → `Encounter` → `Hub` → `Result` → `Title`, and `__projectJianghuThaiSpike` (`totalMissingGlyphs 0`). A player that still published an old name would fail `smoke.spec.ts`: the probe list is asserted by name. `artifacts/j-boot/playwright-release.log`. |
| 6 | `AGENTS.md` gains a "Reference repo" section: read-only, no whole-file copies without naming them in the report | **PASS** | `AGENTS.md` permanent rule **15**: the reference repo (path and tag `survive-stage-10`) is read-only, and copying a whole file is allowed only when the stage report names it; rule 14 above it keeps `docs/template/examples/` as examples rather than rules. The file was rewritten to this game's rules while the stage ran and that rewrite already carried the requirement, so this stage verified and committed it instead of adding a second, duplicated section. |
| 7 | `docs/stages/j-boot-report.md` in the shape of `_TEMPLATE.md` | **PASS** | this file. |
| **DoD** | Every item passes; a case-insensitive `git grep` for the reference name finds it only in `docs/template/ORIGIN.md` and `docs/template/examples/`; commit `bootstrap: unity import + verification` | **PASS with one documented exception** | 11 files: `docs/template/ORIGIN.md` + 9 under `docs/template/examples/` + **`AGENTS.md`** (rule 15, which item 6 requires; this report deliberately does not spell the name again). The two requirements contradict each other and item 6 is the explicit instruction, so the grep exception is recorded here rather than absorbed silently. No hit in `lib/`, `data/`, `proto/`, `tools/`, `tests/`, `UnityProject/`, `knowledge.md`, `docs/adr/`, `llmwiki/`. |

### 2.1 The files the rename reported as carrying the name in text form

The rename tool reports the assets whose **name** (not an identifier the compiler checks) contained
the old name, plus every file whose content it rewrote. A renamed identifier fails loudly at compile
time; a renamed `m_Name`, menu path, page global or package key fails silently. Each one, and how it
was confirmed working here:

| File | What it carries | How it was confirmed | Verdict |
| --- | --- | --- | --- |
| `Assets/Main/Settings/ProjectJianghuURP.asset` (+ `.meta`) | `m_Name: ProjectJianghuURP`, GUID `3a80b40d…` | `GraphicsSettings.m_CustomRenderPipeline` and all six quality levels point at that GUID **by reference** (GUID, not name); the rebuilt scenes render | PASS |
| `Assets/Main/Settings/ProjectJianghuURP_Renderer.asset` (+ `.meta`) | `m_Name: ProjectJianghuURP_Renderer`, GUID `cf44cd81…` | the URP asset's renderer list holds `{fileID: 11400000, guid: cf44cd811a59cd44c90864ffb4cc3796}` | PASS |
| `Assets/Main/Plugins/WebGL/ProjectJianghuWeb.jslib` | `ProjectJianghu_Report/_Scene/_Publish/_Log` | the four `[DllImport("__Internal")]` declarations in `WebGlBridge.cs` match the four exports one for one; EditMode `The_webgl_plugin_hard_codes_no_page_global`; the Release player publishes through them | PASS |
| `Assets/Main/Scripts/Presentation/Web/WebGlBridge.cs` | `__projectJianghuSmoke/Scene/ThaiSpike`, events `projectjianghu:smoke` / `:scene` | EditMode `The_three_page_globals_keep_their_published_names`, `The_browser_suite_exports_the_same_three_names`; Playwright reads them all | PASS |
| `tests/web/globals.mjs` (+ `report-types.ts`, `smoke/title/loop` specs) | the same seven globals | Playwright 4/4 against the Release build | PASS |
| `UnityProject/Packages/manifest.json` | `"com.projectjianghu.core": "file:../../lib"` | the package resolves; `ProjectJianghu.Game.*` / `ProjectJianghu.Core.*` assemblies appear in the Editor's component menu; 32 EditMode + 6 PlayMode tests compile and pass against them | PASS |
| `lib/package.json` | `name`, `displayName`, `keywords` | reached through the manifest entry above (same resolve) | PASS |
| `data/luban.conf` | `"topModule": "ProjectJianghu.Config"` | `gen` reproduces hash `DDFDF338…`; `verify-generated` PASS 9; EditMode `Config_tables_load_from_resources_and_match_the_generated_hash`; the player reports the same hash | PASS |
| `proto/app.proto` | `package projectjianghu.api.v1`, `csharp_namespace` | protoc output byte-identical in `verify-generated`; protobuf round-trip in the Editor and in the player (`CheckpointProbe 81 bytes`) | PASS |
| `ProjectSettings/ProjectSettings.asset` | `companyName`, `productName` | set to `Wuxia` / `Wuxia: Jianghu Rising`; the built page's own title reads `Unity Web Player \| Wuxia Jianghu Rising` (Unity drops the colon in the HTML title) | PASS |
| `Assets/Main/Scripts/Editor/*.cs` | menu paths `ProjectJianghu/Stage 03/…`, `ProjectJianghu/Scenes/…` | the Editor lists the six builder items under that prefix, and each ran to completion in item 1 | PASS |

## 3. Not tested, and risks

- **Firefox is not installed**, so cross-browser behaviour is NOT RUN. Every browser number above is
  Chrome 156.0.8078.48; `tools/versions.lock.json` still records 154.0.8037.98 (the lock is a record,
  not a pin). Edge was not exercised either.
- **One Full run cannot judge everything.** With the Editor closed the harness reports the two Unity
  steps NOT RUN (as designed); EditMode 32/32 and PlayMode 6/6 were taken in a separate run against
  the open Editor before it was closed. Both halves are in evidence; neither substitutes for the other.
- **The rebuilt scenes renumber YAML `fileID`s.** GUIDs (and therefore every cross-reference and the
  `.meta` files) are unchanged, but `git diff` on `Assets/Main/Scenes/*.unity` is large and must not
  be read as scene content changing.
- **The template had no EditMode test for missing scripts or references** — the prompt assumed one.
  Its missing-script check is `ShellPlayModeTests` (PlayMode, scenes in the build list only). The new
  `ShellAssetIntegrityTests` covers all build-list scenes and prefabs under `Assets/Main`; it does
  not walk the imported TextMesh Pro examples, and "reference" means object references on this
  project's own `MonoBehaviour`s, since a null reference on a package component is a Unity default.
- **The working tree had an uncommitted `com.unity.pipeline` bump to `0.9.1-exp.1`** (from the MCP
  plugin, not from this stage). That version renames the pipeline commands (`test run`, `test status`),
  which breaks `tools/test.ps1` — it calls `run_tests` / `test_status`. The manifest was restored to
  the committed `0.8.0-exp.1` and the suite went green; the bump will recur whenever the plugin
  updates itself, and fixing the harness for it is a separate task.
- **`npm ci --prefix tests/web` was run** to satisfy the Playwright step; `tests/web/node_modules` is
  not tracked.
- **Item 6 and the DoD grep contradict each other** (see the DoD row). If the intent was that the name
  appears only in provenance, the `AGENTS.md` mention has to be phrased without it — that is the
  user's call, not a silent rewrite here.
- **`AGENTS.md` was replaced with the game's own rules while this stage ran** (01:03): rule 15 there
  already states item 6's requirement. That rewrite is what the commit carries; the separate
  "Reference repo" block written earlier in the stage was overwritten by it before the commit, so
  there is one section, not two. The rest of that rewrite is the user's, not this stage's.
- **Untracked `GDD.md` and `Stage Plan (ProjectJianghu).md`** appeared in the tree while this stage ran
  (01:03–01:04). They are not this stage's work and are not in this commit. The new `AGENTS.md`
  reading list points at `docs/GDD.md`, which does not exist yet — the draft is the untracked
  `GDD.md` at the root, so that path is a decision for the user.
- **Commit prefix.** The new `AGENTS.md` rule 13 asks for `stage-NN: <summary>`; this stage's DoD
  named `bootstrap: unity import + verification`, which is what the commit uses. Later stages follow
  rule 13.
- **No performance claim is made.** The 60.50 MiB payload equals the template baseline because the
  content is still the template's; it is not a measurement of the game.

## 4. Commands actually run

```powershell
# identity + scenes, through the Editor API (Editor open)
ProjectSetup.ConfigureProject()                       # product=Wuxia: Jianghu Rising, id=com.wuxia.jianghurising
BootSceneBuilder/TitleSceneBuilder/ThaiTextSpikeSceneBuilder/HubSceneBuilder/EncounterSceneBuilder/ResultSceneBuilder.CreateFromMenu()
                                                      # boot=OK; title=OK; thai=OK; hub=OK; encounter=OK; result=OK; build=6

# tests, Editor open
unity command run_tests --mode editor   --async_tests  # 32/32 passed
unity command run_tests --mode playmode --async_tests  # 6/6 passed

# gates, Editor closed, in this order
pwsh -NoProfile -File tools/doctor.ps1                                  # PASS 13 | FAIL 0 | WARN 1 | SKIP 2
pwsh -NoProfile -File tools/gen.ps1                                     # OK, DDFDF338…
pwsh -NoProfile -File tools/verify-generated.ps1                        # PASS 9 | FAIL 0
pwsh -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data" # PASS 5 | FAIL 0
npm ci --prefix tests/web                                               # 3 packages
pwsh -NoProfile -File tools/test.ps1 -Suite Full                        # PASS 10 | FAIL 0 | NOT RUN 2, 621.7 s
pwsh -NoProfile -File tools/build-web.ps1 -Configuration Release -Force # OK, 63,452,981 B, 0 error CS
node tools/serve-web.mjs --port 8080
BASE_URL=http://127.0.0.1:8080 npx playwright test                      # 4 passed (20.2 s)
node tools/measure-web-size.mjs --dir UnityProject/Builds/Web --label "J-Boot Release" --json artifacts/j-boot/size-release.json --markdown artifacts/j-boot/size-release.md
                                                                        # 60.50 MiB raw / 18.04 gzip / 12.45 Brotli
git grep -il -e <reference name>                                       # 11 files: 10 under docs/template/, plus AGENTS.md
```

**Screenshot:** `artifacts/ui/j-boot-title-after-scene-rebuild.png` — the Title screen after the
rebuild, captured from Editor Play Mode at 1280×720 (not a Release player).
