# Stage 03 — Unity Shell Re-baseline (HD-2D spike)

## 1. Summary

The template's screens are the game's: `Hub` became **Rest**, `Encounter` became **Arena**,
and **CharacterCreate** and **Bracket** are new placeholder steps, all built through
`ProjectSetup`/one builder per scene and all reachable through `SceneFlow`
(**Title → CharacterCreate → Bracket → Rest ⇄ Arena → Result → Title**). The Arena is no
longer a panel over nothing: it is the HD-2D spike — a 12×12 wood floor, three props, three
32×48 billboards and a 武 banner, seen through an orthographic 45°/30° camera at a
pixel-constant 32 PPU with the pixel snap and the sprite-vs-prop sorting **measured** rather
than assumed (ADR 0110). Three URP tiers with a Settings selector and an FPS probe replace
one quality level, the project ships its own WebGL template so the canvas is the window
instead of a pinned 960×600 box (ADR 0111), and the decorative Chinese character is a sprite
so the Thai font atlas does not grow (ADR 0112). Tests: **dotnet 156**, **EditMode 42**,
**PlayMode 7** (including 20 Rest ⇄ Arena crossings), **Playwright 4**, Release payload
**60.81 MiB raw / 17.35 MiB gzip / 12.45 MiB Brotli** against the template's 60.50 / 18.04 /
12.45.

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1a | Scenes through `ProjectSetup` only: `Hub` → `Rest`, `Encounter` → `Arena` | **PASS** | `RestSceneBuilder`, `ArenaSceneBuilder` (renamed builders); `Rest.unity`/`Arena.unity` rebuilt from the menu; `EditorBuildSettings` holds all eight scenes; `ShellAssetIntegrityTests` finds no missing script or unassigned reference in any of them. |
| 1b | Add `CharacterCreate` and `Bracket` as placeholders | **PASS** | `StepSceneTemplate` + `CharacterCreateSceneBuilder`/`BracketSceneBuilder`, one `StepPresenter`/`StepView`/`StepReport` pair for both; `LoopScreenTests` reads their keys and their departure. |
| 1c | Update `SceneFlow` | **PASS** | `SceneId` = Boot, Title, CharacterCreate, Bracket, Rest, Arena, Result; `IsEdgeAllowed` carries the seven edges. `RunSkeletonTests` walks every legal edge and the three refusals (Title→Arena, Result→Arena, run scene without a run). |
| 1d | Loop test ×20 (Rest ⇄ Arena) | **PASS** | `LoopPlayModeTests.Twenty_rest_arena_crossings_keep_one_run_and_count_every_round`: 20 departures/wins, one seed, counter 1…20, **0** `ArenaSceneScope` left behind after each crossing, 18.85 s. |
| 1e | Update `loop.spec.ts` | **PASS** | `tests/web/loop.spec.ts` walks Title → CharacterCreate → Bracket → Rest → Arena → Rest → Result → Title by clicking pixel points the screens published; **4/4 Playwright specs pass in Chrome 156.0.8078.48**. |
| 2 | HD-2D iso spike: 3D placeholder + billboards (point filter, fixed PPU, feet pivot), ortho iso camera, pixel snap, sprite-vs-prop sorting test, ADR | **PASS** | `IsometricLayout`, `IsoCameraRig`, `BillboardSprite`, `ArenaSortingProbe`; sprites imported at 32 PPU / point / bottom-centre (`SpikeArtSetup`). Sorting probe answers all four questions **true**, measured pixels-per-world-unit **17.067** matching the camera projection, snap worst offset **0.667 px**. [ADR 0110](../adr/0110-hd2d-isometric-camera-pixel-snap-and-sorting.md). |
| 3 | URP Low/Med/High + Settings selector + FPS of the spike in the browser at every tier | **PASS** | `ProjectSetup.ConfigureQualityTiers` writes 3 levels, each with its own URP asset + renderer (Low 0.7 render scale / no MSAA, Medium 0.85 / 2×, High 1.0 / 4×). The Arena's settings panel switches tiers; `ArenaSpikeReport` publishes tier + FPS + the three tier buttons. Measured per tier below (`artifacts/web-fps.json`). |
| 4 | WebGL template that is not pinned at 960×600, 16:9 and window resize, ADR | **PASS** | `Assets/WebGLTemplates/Jianghu` selected as `PROJECT:Jianghu` (`webGLTemplate: PROJECT:Jianghu` in `ProjectSettings.asset`); canvas fills the viewport. `title.spec.ts`: boot at 1280×720 → `canvasWidth 1280`, start at x=640; resize to 1920×1080 → `canvasWidth 1920`, start at x=960. [ADR 0111](../adr/0111-webgl-canvas-fills-the-window.md). |
| 5a | Long Thai skill name at 1280×720 | **PASS** | `smoke.spec.ts` asserts the canvas is 1280×720 and then reads the spike: **379.6 px in a 420.0 × 28.6 px slot, fits = true**, 0 missing glyphs across 8 rows in both stacks. |
| 5b | Decorative Chinese character as a sprite, not a font | **PASS** | `banner_wu.png` (72×72, **518 B**) placed as a billboard in front of the banner cloth; the font bake stays at **182** glyphs (`table=182 coversAll=True`). [ADR 0112](../adr/0112-decorative-cjk-glyphs-are-sprites.md). |
| **DoD-1** | PlayMode + Playwright pass | **PASS** | `test.ps1 -Suite Full` (Editor open): EditMode **35/35**, PlayMode **7/7** (before the isometric tests landed); after they landed, EditMode **42/42** and PlayMode **7/7** again. `test.ps1 -Suite Full -Release` (Editor closed): WebGL build PASS, Playwright **4 passed**. |
| **DoD-2** | Two new ADRs | **PASS** | 0110, 0111 — plus 0112, because item 5b is a separate asset decision with its own evidence. |
| **DoD-3** | FPS table for all three tiers in the report | **PASS** | §3 below. |
| **DoD-4** | Build size compared with `docs/template/baseline.md` | **PASS** | §4 below. |

## 3. Measurements

**Frame rate per tier — Release player, Chrome 156.0.8078.48, 1280×720, this machine.**
Each tier was selected by **clicking the arena's own settings button** at the point the arena
published, then read after ≥ 90 frames (`tests/web/scripts/probe-fps.mjs`,
`artifacts/web-fps.json`).

| Tier | Render scale | MSAA | Average FPS | 1% low | Worst frame | Sorting probe |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Low | 0.70 | 0 | 144.0 | 125.0 | 15.0 ms | pass |
| Medium | 0.85 | 2 | 143.3 | 125.0 | 23.0 ms | pass |
| High | 1.00 | 4 | 144.0 | 111.1 | 9.0 ms | pass |

**Read this table as a ceiling, not a ranking.** 144 Hz is the display's refresh rate, and a
WebGL player renders on `requestAnimationFrame`, so no configuration can report more. The
spike is a floor, four camera-facing quads and three cubes: at 1280×720 it is far below every
tier's budget, and all three tiers sit at the same ceiling. What the table does establish is
that **the selector works in a real player, every tier renders, and every tier still answers
the sorting probe correctly**; the tiers themselves cannot be separated until Stage 09's real
arena (sprites, highlights, HUD, particle juice) provides a load that exceeds a frame budget.
The 1% low of High being the lowest of the three is noise at this workload, not a signal.

**Sorting probe — the four questions, from the arena scene** (`sorting` in
`__projectJianghuArenaSpike`; the probe renders through URP `SubmitRenderRequest` into a
256×256 target and samples the centre pixel):

| Question | Measured |
| --- | --- |
| Sprite in front of an opaque prop draws over it | **true** |
| Prop occludes a sprite behind it | **true** |
| Of two overlapping sprites, the nearer is visible | **true** |
| Raising the far sprite's authored order swaps the pixel | **true** |
| Measured pixels per world unit vs projection | 17.067 vs 17.067 |
| Sorting orders of two sprites 0.6 units apart | −246 (near) / −266 (far) |
| Worst camera offset caused by the pixel snap | **0.667 px** |

## 4. Web payload

Unit: the five files the loader fetches (`index.html` + `Build/Web.{loader.js,framework.js,wasm,data}`),
bytes on disk, gzip level 9, Brotli quality 11 / lgwin 22 — the same measurement as
`docs/template/baseline.md`. **Release**, Unity 6000.3.9f1, `tools/build-web.ps1 -Configuration Release`
via `test.ps1 -Suite Full -Release`.

| File | This stage (B) | Template baseline (B) | Δ |
| --- | ---: | ---: | ---: |
| `index.html` | 4,264 | 5,586 | −1,322 |
| `Build/Web.loader.js` | 26,982 | 26,982 | 0 |
| `Build/Web.framework.js` | 430,945 | 430,574 | +371 |
| `Build/Web.wasm` | 50,211,786 | 50,004,375 | +207,411 |
| `Build/Web.data` | 13,094,924 | 12,971,609 | +123,315 |
| **initial payload** | **63,768,901 (60.81 MiB)** | 63,439,126 (60.50 MiB) | **+329,775 (+0.52 %)** |
| **gzip -9** | **18,191,216 (17.35 MiB)** | 18,919,549 (18.04 MiB) | **−0.69 MiB** |
| **Brotli q11** | **13,057,406 (12.45 MiB)** | 13,052,000 (12.45 MiB) | +0.01 MiB |
| whole build directory | 63,778,370 (60.82 MiB) | 63,462,420 (60.52 MiB) | +0.30 MiB |

- **Gate — gzip < 30 MiB:** 17.35 MiB → **PASS**.
- **Target — Brotli < 15 MiB:** 12.45 MiB → **PASS**.
- The `+123 KB` of `.data` is the four spike sprites, eight scene files and the new
  localization rows; the `+207 KB` of `.wasm` is the added presentation code plus the three
  URP assets' variants. The template is two text files and adds no payload (`index.html` is
  actually 1.3 KB smaller than the default template's).

## 5. Not tested, and risks

- **The quality tiers are indistinguishable at this workload.** All three hit the display
  ceiling; the table proves the selector and the rendering, not a performance difference.
  Stage 09/16 must re-measure with the real arena and must not reuse these numbers as a
  ranking (recorded in the report's own reading and in `web-fps.json`'s `tiers[]`).
- **144 Hz is this machine's display.** A 60 Hz machine would report ≈60 for every tier; the
  probe cannot distinguish "capped by refresh" from "fast enough" without a way to raise the
  load. Do not compare this table with a number from another machine.
- **The probe is headful Chrome with a real GPU path** (`--use-angle=default`), not
  swiftshader; it is not a phone, and WebGL2 on a phone is the real target.
- **Aspect ratios other than 16:9 are untested in a real browser.** The canvas fills any
  viewport by construction and the camera derives its size from the height (ADR 0110), but
  a 4:3 or ultrawide window was not exercised; Stage 09's camera bounds are where that gets
  answered.
- **The API for a broader canvas is new and only one test deep.** `title.spec.ts` is the
  only resize proof; a template edit that stops dispatching `resize` would be caught, but a
  change that only affects a HiDPI display would not.
- **`CharacterCreate` and `Bracket` are placeholders.** They start a run if the scene was
  opened directly (so a `?scene=` handover works) and go straight on; the real rules arrive
  in Stage 05/11 and 10. Their `StepSceneDirector` retries a departure refused with
  `transition.in_progress`, the same pattern as every other director.
- **The sorting probe lives in the scene**, so a Stage 09 rewrite of the Arena could delete
  it. ADR 0110 says it must survive into the real presentation; nothing enforces that yet
  except the ADR and the report.
- **The decorative glyph has no accessible text today.** `ui.spike.thai.banner` exists in the
  table but no screen reads it; when a banner becomes a real UI element it needs a real
  label, not just a PNG (ADR 0112 §5).
- **Rest/Arena are reached from a fresh character only once.** The loop test proves 20
  crossings and the Result screen's restart path, but CharacterCreate is a placeholder, so
  "make a character, fight, come back to the bracket" is not a flow yet (Stage 10/11).

## 6. Commands actually run

```powershell
pwsh -NoProfile -File tools/gen.ps1                                   # OK; config hash 674E4E85…
pwsh -NoProfile -File tools/verify-generated.ps1                      # PASS 8 | FAIL 0 | WARN 1 (uncommitted, expected)
pwsh -NoProfile -File tools/test.ps1 -Suite Fast                      # FAIL 2 (stale tables + 8 screen tests) -> fixed
pwsh -NoProfile -File tools/test.ps1 -Suite Full                      # Editor open: PASS 10 | FAIL 0 | NOT RUN 2
pwsh -NoProfile -File tools/test.ps1 -Suite Full -Release             # Editor closed: web PASS 1 + PLAYWRIGHT FAIL 4 (favicon, 960, rowCount) -> fixed
pwsh -NoProfile -File tools/test.ps1 -Suite Full -Release             # Editor closed: PASS 10 | FAIL 0 | NOT RUN 2 (42 s of web steps)
# Unity 6000.3.9f1, Editor open
#   unity EditMode: 42/42 passed (7 new IsometricTests)
#   unity PlayMode: 7/7 passed (20-crossing loop 18.85 s)
# Current verification after final documentation edits:
#   tools/test.ps1 -Suite Fast: PASS 9, FAIL 0, NOT RUN 3; EditMode 42/42
#   tools/test.ps1 -Suite Full -Release (Editor open): PASS 10, FAIL 0, NOT RUN 2; PlayMode 7/7, WebGL steps blocked by Editor lock
#   node tools/check-links.mjs: 306 links in 72 files resolve
node tests/web/scripts/probe-fps.mjs http://127.0.0.1:8080            # tiers Low/Medium/High -> artifacts/web-fps.json (recorded release run)
```

## 7. Decisions

- [ADR 0110](../adr/0110-hd2d-isometric-camera-pixel-snap-and-sorting.md) — orthographic
  45°/30°, 32 PPU, pixel snap in the camera basis, depth→sorting order, and the five defects
  the first probe measured instead of the engine.
- [ADR 0111](../adr/0111-webgl-canvas-fills-the-window.md) — the canvas is the window.
- [ADR 0112](../adr/0112-decorative-cjk-glyphs-are-sprites.md) — a decorative Chinese
  character is a sprite, with the atlas and byte counts that decided it.
