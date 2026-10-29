# ADR 0111 — The WebGL canvas is the window, not a 960×600 box

- **Status:** Accepted
- **Date:** 2026-10-29
- **Stage:** 03
- **Supersedes:** nothing
- **Related:** `llmwiki/gotchas/webgl-template-canvas-960x600.md`,
  `llmwiki/gotchas/canvas-scaler-follows-aspect.md`, [ADR 0110](0110-hd2d-isometric-camera-pixel-snap-and-sorting.md),
  [ADR 0003](0003-performance-baseline-machine-and-browser.md)

## Context

Unity's default desktop WebGL template writes `canvas.style.width = "960px"` and
`height = "600px"`. The player therefore renders 960×600 inside whatever window it is
given, and **the game is never told how big the window is**:

- `Screen.width` is 960 and `Screen.height` is 600, whatever the viewport is;
- a window resize is a layout change only — the player keeps rendering 960×600;
- the template's own fullscreen button is the only thing that changes the canvas element,
  and it changes the *element*, not the window.

This is a measurement problem before it is a visual one. Every number this project quotes
about the player — the shortlisted FPS per quality tier, where a control is on screen when a
browser test clicks it, how much of the arena a resize reveals — is a number of the canvas
the player actually has. At the template's default, all of them are numbers about 960×600,
and the viewport the page reports is a different, unrelated size.

The spike's own need is concrete: an HD-2D stage has to be *looked at*, and a 960×600
letterbox in a 1920×1080 window shows a third of the screen for no reason.

## Decision

1. **The project ships its own WebGL template**: `Assets/WebGLTemplates/Jianghu`, selected in
   Player Settings as `PROJECT:Jianghu` (`WebGlTemplate.PlayerSettingValue`). The folder name
   is the template name; `WebGlTemplate` holds the three strings so the settings write, the
   EditMode test and the build check cannot disagree.
2. **The canvas fills the viewport.** `#unity-container` is `position: fixed; inset: 0` and
   `#unity-canvas` is `width: 100%; height: 100%; display: block`. There is no letterbox and
   no fixed aspect ratio on the canvas element: a 16:9 window fills it, and so does a 4:3
   one.
3. **A resize reaches the game.** The page keeps the runtime's canvas element matched to the
   container on `resize` (including a `devicePixelRatio` change, e.g. a window dragged to a
   HiDPI display). The WebGL runtime's own canvas-to-element matching is what makes
   `Screen.width`/`Screen.height` follow.
4. **The game does the adapting, and each half does it in its own units:**
   - **UI**: every canvas is `ScaleWithScreenSize`, 1920×1080 reference, `matchWidthOrHeight
     = 0.5`, so a corner-anchored panel keeps its proportions and a centre-anchored control
     stays centred at any aspect.
   - **World**: the isometric camera derives its orthographic size from the viewport
     *height* ([ADR 0110](0110-hd2d-isometric-camera-pixel-snap-and-sorting.md) §1), so a
     wider window reveals more floor on the sides instead of shrinking the sprites. Pixels
     per world unit stays 32 screen pixels × zoom, at every window size.
5. **Nothing is cropped and nothing is stretched by the page.** The page owns layout only.
   A page that scaled the canvas with a CSS transform would blur the pixel art, and a page
   that put a fixed-aspect box in the middle would be the 960×600 problem with different
   numbers.

## Evidence

The measurement is the player's own `Screen.width`/`Screen.height`, published in each
screen's report (`canvasWidth`/`canvasHeight`), read from the built Release player in Chrome
(`tools/versions.lock.json` records the browser).

| What was done | Player reports | Test |
| --- | --- | --- |
| Player booted in the test browser | canvas 1280×720, and the player's own report agrees | `smoke.spec.ts` (Thai spike, 1280×720) |
| Canvas element set to 1280×720 and `resize` dispatched | `canvasWidth = 1280`, `canvasHeight = 720`, start control at x = 640 — i.e. the player re-mapped its own coordinates to the new size | `title.spec.ts` |
| Demand for a 1280×720 measurement of the longest Thai skill name | taken at 1280×720, not at 960×600 | `smoke.spec.ts` |
| Editor play mode at 1920×1080 | canvas 1920×1080 | the spike report, editor run |

Before this ADR the same probe would have reported 960×600 while the window was 1280×720 —
which is exactly the failure the gotcha documents.

## Alternatives considered

- **Keep the default template and tell authors to press fullscreen.** Rejected: every
  measurement is then conditional on a button press, and the first thing most sessions do
  (resize, or open at a small window) is unsupported.
- **A fixed 16:9 letterboxed canvas centred in the page.** Rejected: it is the same defect
  with a nicer number — the canvas element stops being the window, the wasted area stays,
  and a 4:3 or ultrawide screen gets bars the game cannot react to.
- **Scale the canvas element with a CSS transform to fill the window.** Rejected: it
  upscales the render target, so the pixel art is bilinear-blurred and `Screen.width`
  describes a size nothing was drawn at.
- **Fix the game to a 16:9 render and crop.** Rejected: it changes what the game is, not how
  the page shows it, and would put the arena's edge tiles off screen on a 4:3 window.
- **Set the canvas size from JavaScript on every resize instead of using CSS.** The runtime
  keeps the element and the render target matched; a second, hand-written resize path is a
  bug waiting to disagree with it. The page sizes the element, the runtime follows it.

## Consequences

- **Positive:** the player is told the truth about the window; a browser test's click
  coordinates, a per-tier FPS number and a Thai-text fit measurement are all taken at the
  size they name; and the arena is not letterboxed on any screen.
- **Negative:** the page is now the game's layout, so a change to the template is a change to
  what every player sees and must be measured again. It is one file plus one stylesheet, and
  `WebGlTemplate.Verify()` fails a build whose settings stopped pointing at it.
- **Negative:** an odd aspect ratio changes how much world is visible, so a future feature
  that assumes "the arena always fits the screen" needs its own rule (Stage 09's camera
  limits, likely).
- **Verification:** `WebGlTemplate` + the EditMode check that Player Settings select
  `PROJECT:Jianghu` and the template's page exists; `title.spec.ts`'s resize leg;
  `smoke.spec.ts`'s 1280×720 assertion; and the size comparison against
  `docs/template/baseline.md` in the Stage 03 report (the template is two text files and adds
  no payload).
