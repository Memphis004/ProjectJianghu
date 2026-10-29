# ADR 0110 — HD-2D isometric: orthographic 45°/30°, a pixel-constant PPU, snap in the camera basis, order from depth

- **Status:** Accepted
- **Date:** 2026-10-29
- **Stage:** 03 (the spike); binding from Stage 09 (arena presentation)
- **Supersedes:** nothing
- **Related:** [GDD §1.8](../gdd/GDD.md), [ADR 0102](0102-four-facing-sprites-two-drawn-mirrored.md),
  `llmwiki/gotchas/unity-yaml-not-hand-editable.md`

## Context

The game is HD-2D: pixel-art sprites standing in a 3D room, seen through a fixed isometric
camera. Stage 03 has to settle four things before any arena art exists, because every later
asset, collider and hit test inherits them:

1. **Projection.** Perspective or orthographic, and at what angles.
2. **Scale.** How many pixels a world unit is worth, and what that makes a tile.
3. **Pixel snap.** Whether the camera ever sits between texels, and what that costs.
4. **Sorting.** How a sprite is ordered against the 3D props around it and against another
   sprite standing a fraction of a tile in front.

These are exactly the decisions that are expensive to change later (the camera angle changes
every sprite; the PPU changes every sprite's world size; the sorting rule changes every
prop), and cheap to decide now with a placeholder stage. `docs/template/examples/adr/0018`
is the model followed here: **a decision with a measurement behind it, not a preference**.

The spike is `Assets/Main/Scenes/Arena.unity`, built by `ArenaSceneBuilder`: a 12×12 wood
floor, three props, three 32×48 fighter sprites, a shadow decal and a banner carrying 武 as a
sprite. `ArenaSpikeDirector` runs the measurements and publishes them on
`__projectJianghuArenaSpike`, so the same numbers can be read from the Editor, from a WebGL
build, and from a browser.

## Decision

### 1. Orthographic, 45° yaw, 30° pitch, size derived from the viewport

`IsoCameraRig` sets `orthographic = true`, rotation `Quaternion.Euler(30, 45, 0)`, and

```
orthographicSize = Screen.height / (2 × PixelsPerWorldUnit × zoom)
```

with `zoom = 1.5` screen pixels per sprite pixel (`IsometricLayout.OrthographicSizeFor`).
At 1080p that is 11.25 world units of half-height; at 720p, 7.5.

- **Orthographic** because a perspective camera draws the same character larger at the
  bottom of the screen than at the top. An isometric grid whose rows have different scales
  cannot be read as a grid, and a hit test that used world distance would disagree with what
  the player sees.
- **45° yaw** is what turns a square tile into the diamond the whole diagonal-movement
  contract assumes (Stage 06's grid is Manhattan; see [ADR 0102](0102-four-facing-sprites-two-drawn-mirrored.md)).
- **30° pitch** is the classic 2:1 read with room above a sprite's head. The strict 2:1
  angle is 26.565°; 30° trades 3.4° of exactness for headroom, which matters because a
  sprite is 1.5 units tall and a prop is allowed to be taller.
- **Size from the viewport, not authored**, so one world unit is a constant number of
  pixels at every window size. The failure this replaces is the usual one: a sprite that is
  32 px at 720p and 43 px after a resize, because the camera kept an authored size.

### 2. 32 pixels per world unit, point filter, feet pivot — project-wide

`IsometricLayout.PixelsPerUnit = 32` is a constant, not a per-scene value, and it is the
same number the importer writes (`SpikeArtSetup.ApplySpriteImportSettings`). Consequences
that are part of the decision:

- One tile is **one world unit**, so the 12×12 `arena_map` is a 12×12 metre floor and a
  grid coordinate converts to world space by addition, not by a scale factor.
- A 32×48 fighter is **1 × 1.5 units**; a 72×72 banner glyph is 2.25 units square.
- **Point filter, uncompressed**, because any bilinear sample of a 32 px sprite is a
  blurred sprite; the pixel look is not a post effect.
- **Bottom-centre pivot**: a sprite's transform is the tile it stands on. A centre pivot
  would make every placement subtract half a sprite height, and the y-sort key (the feet)
  and the transform would disagree.

### 3. Pixel snap in the camera's own basis

`IsometricLayout.SnapToPixel` projects a position onto the camera's right/up/forward axes,
rounds the right and up components to whole pixels, and leaves depth alone.
`IsoCameraRig.ApplyPose` calls it on every move, including a follow target, a sweep and a
resize.

- Snapping **in the camera basis**, not in world space, is the point: a world-space snap
  leaves a 45°-yawed camera sampling halfway between texels, which is exactly the shimmer.
- **Depth is not snapped** because an orthographic projection does not change size with
  depth; moving the camera along its own forward axis moves nothing on screen.
- **The measured cost:** over a sweep of 256 positions, the largest distance the snap moved
  the camera is **0.667 px** (`pixelSnap.maxOffsetPixels` in the spike report) — under half
  a pixel on each axis, which is why the sweep reads as smooth instead of steppy.

### 4. Depth decides sprite-vs-prop; authored order decides sprite-vs-sprite

- A sprite's material is in the transparent queue with `ZTest LEqual` and `ZWrite Off`. An
  **opaque prop that writes depth** therefore occludes a sprite behind it, and a sprite in
  front of it is drawn over it. No render-queue tricks are needed, but **props must be
  opaque**: a transparent prop cannot occlude, and the sorting rule silently changes.
- Two sprites write no depth, so depth cannot order them. `BillboardSprite` writes
  `sortingOrder = round(-dot(position, cameraForward) × 32)` + an authored bias
  (`IsometricLayout.SortingOrderFor`): one sorting step per pixel of depth, so two sprites a
  third of a tile apart still order differently instead of flickering at a tile boundary.
- **Yaw-only billboarding.** The sprite faces the camera's yaw and stays vertical; a
  full billboard would tilt it by the 30° pitch and lean the character into the floor.

### 5. The spike must not cover the stage with UI

The template's screen centred a 1200×900 panel over everything. An HD-2D spike has to be
looked at, so the arena's UI is a top bar, a bottom row of two controls and a corner
settings panel, all translucent. This is a layout rule for Stage 09 too.

## Evidence

`ArenaSortingProbe` answers the four questions in **pixels**, not by inspection: it builds a
throwaway opaque 1-unit cube and two 2-unit sprite quads around the scene's own camera,
renders through URP's `SubmitRenderRequest` into a 256×256 target, samples the centre pixel
of each arrangement, and destroys everything it made. Sizes and offsets were chosen from a
first, failed run (see "What the first probe measured" below).

Editor, Unity 6000.3.9f1, URP, arena camera orthographic size 11.25 (1080p, zoom 1.5),
probe target 256×256:

| Question | Answer the ADR needs | Measured | Value |
| --- | --- | --- | --- |
| Sprite held *in front of* the prop | sprite drawn over prop | `spriteInFrontDrawsOverProp` | **true** |
| Sprite held *behind* the same prop | prop occludes sprite | `propOccludesSpriteBehind` | **true** |
| Two fully overlapping sprites, 0.6 units apart in depth | nearer one visible | `nearerSpriteDrawsFirst` | **true** |
| Far sprite's order raised by 100 above the near one | the pixel follows the order | `authoredOrderDecides` | **true** |
| Measured screen pixels per world unit | equals the projection's | Editor probe at 256×256, ortho size 11.25: 11.378 vs 11.378; WebGL at 1280×720, ortho size 7.5, same 256×256 probe target: 17.067 vs 17.067 | **agrees in both** |
| Orders `BillboardSprite` gave the two sprites | one order per pixel of depth | −246 (near) / −266 (far) | 20 orders, 0.6 units |
| Largest camera move caused by the pixel snap | < 1 px | `maxOffsetPixels` | **0.667 px** |

The whole report is `sorting.passed = true`, and it is published from the real arena scene in
both the Editor and the WebGL build. Measured pixels per world unit differs between those
reports because `MeasurePixelsPerUnit` uses the camera's active render-target height: the
Editor camera used orthographic size 11.25, while the 720p WebGL viewport derives size 7.5.
Both readings equal their own projection's expected value.

### What the first probe measured — and why the answer changed

The first version returned `false` for all four questions, and every one was a defect in the
probe rather than in the game:

1. **A 64×64 render target** drew one world unit at 2.8 px, so a probe sprite was smaller
   than a pixel and the centre sample missed it. 256 px wide fixes the measurement, not the
   game.
2. **An 8×8 sprite at 32 PPU** is 0.25 units — and its centre sample landed exactly on the
   bottom edge of a feet-pivoted sprite. The probe now uses 2-unit sprites with a centre
   pivot: it is measuring sorting, not footing.
3. **A sprite 0.4 units in front of a 1-unit cube is *inside* the cube.** Seen down a
   45°/30° ray an axis-aligned unit cube reaches 0.5 × (|fx|+|fy|+|fz|) ≈ 0.73 units toward
   the camera, so "in front" was 0.03 units behind the front face. The offset is now 1.5.
4. **The far probe sprite was deactivated for the prop test and never re-enabled**, so the
   sprite-vs-sprite answer was "the only visible sprite". Found by the `authoredOrderDecides`
   question staying `false` while `nearerSpriteDrawsFirst` was `true`.
5. **A lit cube is not the colour it was authored.** The prop is now URP/Unlit: both sides
   of the comparison are unlit, so a shaded face cannot fail the sorting test.

The lesson kept in the code: a probe that answers "false" is a claim about the probe first
and the engine second.

## Alternatives considered

- **Perspective camera.** Rejected: per-row scale makes an isometric grid unreadable and
  breaks world-distance hit tests against what the player sees.
- **A camera that rotates with the board.** Rejected for the prototype: the four-facing
  sprite rule (ADR 0102) and the camera's fixed yaw are the same decision; a rotating camera
  needs eight drawn sets.
- **World-space pixel snap.** Rejected: it leaves a 45°-yawed camera between texels, the
  exact shimmer it is supposed to remove.
- **ZWrite-on sprites, sorted by geometry alone.** Rejected: sprites would punch holes in
  each other's antialiased edges and depend on sub-pixel geometry for what is an authored
  question; the order is written from depth and a bias instead.
- **Per-tile sorting order** (one order per grid cell). Rejected: sprites a third of a tile
  apart would flicker as they crossed a boundary. One order per pixel of depth is cheap and
  continuous.
- **A 2:1 (26.565°) camera.** Rejected: 3.4° of exact tile ratio is not worth losing the
  headroom above a 1.5-unit-tall sprite.

## Consequences

- **Positive:** the projection, the scale, the pivot and both sorting rules are one tested
  class (`IsometricLayout`) plus one component (`BillboardSprite`), so Stage 09 places
  fighters and props without re-deciding any of them; the four answers are measured in a
  real frame, so a URP upgrade that changes sorting fails the spike instead of the player.
- **Negative:** props are now constrained to be opaque; a translucent prop (a paper screen,
  a water surface) needs its own rule and is not covered by this ADR.
- **Negative:** a sprite and a prop at *exactly* the same depth are decided by the depth
  buffer's precision, not by the authored order. Content keeps props off the tile a fighter
  stands on.
- **Verification:** `ArenaSortingProbe` (runtime, published in the report), `IsometricLayout`
  unit tests (snap bounds, order monotonicity, PPU), and a PlayMode check that the arena's
  sprites load with point filters and bottom-centre pivots. Stage 09 keeps the same probe in
  the build so the evidence does not rot.
