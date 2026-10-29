# ADR 0112 — Decorative Chinese characters are sprites, never font glyphs

- **Status:** Accepted
- **Date:** 2026-10-29
- **Stage:** 03 (the spike); binding for every screen that draws a decorative character
- **Supersedes:** nothing
- **Related:** [ADR 0008](0008-thai-text-ui-stack.md),
  `llmwiki/gotchas/tmp-font-assets-in-builds.md`

## Context

The game is a wuxia: banners, seals, signboards and skill cut-ins carry Chinese characters
(武 on the arena's banner is the spike's example). The player-facing language is Thai, and
every player-facing string comes from the localization table ([ADR 0008](0008-thai-text-ui-stack.md)).
A decorative character is neither: it is **art with a reading**, drawn at a size and in a
style a text atlas would not give it anyway.

The obvious implementation — put 武 in a label and let TMP find a fallback font — has a cost
that is paid by every build:

- The shipped Thai atlas is built for the characters the table uses. Measured on this
  checkout: `Sarabun-Regular SDF.asset` is **2.2 MB** and `Sarabun-Regular FontAsset.asset`
  is **2.2 MB** for **182 baked glyphs** — about 12 KB per glyph of atlas in the project, and
  that is for a script with a few hundred characters.
- A font that covers Chinese has tens of thousands of glyphs. Adding it as a fallback adds a
  second atlas (and its font asset) to the build for a handful of decorative characters, and
  — worse — makes the *set of glyphs baked* depend on whether a decorative label happened to
  be active when the font was baked. That is exactly the failure
  `gotchas/tmp-font-assets-in-builds.md` records: a build whose font asset covers fewer
  glyphs than the Editor's, discovered only in the browser.
- A CJK character in a Thai text atlas also competes for AtlasPopulationMode space at bake
  time and can silently trigger a dynamic atlas texture at runtime — a texture upload on the
  frame it first draws.

## Decision

1. **A decorative Chinese character is a sprite asset**, imported with the project's sprite
   preset (point filter, PPU 32, no compression), and placed like any other 2D art. The
   spike's banner is `Assets/Main/Art/Sprites/banner_wu.png`, 72×72 px = 2.25 world units,
   **518 bytes**.
2. **No CJK font is added to the project** for decorative text, and no decorative character
   goes through `LocalizationService`. Localized text stays Thai/English; a decorative
   character is a picture.
3. **The art owns its style.** A banner character is brush-drawn at the size it is shown; a
   font glyph would be a scaled, anti-aliased SDF of a typeface the game does not otherwise
   ship.
4. **It is generated, not hand-edited, like the rest of the spike art**
   (`SpikeArtSetup.EnsureSpikeArt`, `ProjectJianghu/Stage 03/1`), so a checkout with no
   binary art still builds the scene.
5. **A screen that needs to *say* something about the banner** — a tooltip, a screen-reader
   style description — uses a localization row for the description; the character itself
   stays a sprite.

## Evidence

| Asset | Size on disk | Glyphs covered |
| --- | --- | --- |
| `banner_wu.png` (the decorative 武) | **518 B** | 1 drawn character |
| `Sarabun-Regular SDF.asset` (TH+Latin, TMP) | **2,197,914 B** | 182 baked |
| `Sarabun-Regular FontAsset.asset` (UI Toolkit) | **2,197,181 B** | 182 baked |

The bake log after adding the Stage 03 art: `baked 182 characters … table=182 coversAll=True`
for both stacks — the number did not move when the banner was added, which is the point: a
sprite does not touch the font atlas.

## Alternatives considered

- **A CJK fallback font on the TMP asset.** Rejected: megabytes and a second atlas for a
  handful of decorative characters, and it makes the baked glyph set depend on what was
  active at bake time.
- **A dynamic TMP atlas that grows at runtime.** Rejected: a texture upload on the frame the
  banner first appears, on the weakest target (WebGL2, no threads), for art that is known at
  build time.
- **Render the character with the UI Toolkit TextCore stack.** Rejected: same font cost,
  plus a second text stack for one picture.
- **Draw the character with a mesh/Shape.** Rejected: more moving parts than a PNG and no
  style; a brush stroke is an image.

## Consequences

- **Positive:** the font atlas is unaffected by decorative art; the build carries a 518-byte
  PNG instead of a CJK font; the character can be drawn at any size and style the art
  direction wants, including outside text metrics.
- **Negative:** a decorative character cannot be searched, copied or translated — it is a
  picture, and a screen that needs those things needs real text.
- **Negative:** the art pipeline now has one more asset kind to author (a PNG per character
  or per banner), which is a content cost, not an engineering one.
- **Verification:** `smoke.spec.ts` asserts the Thai atlas covers every spike glyph at
  1280×720 (so the atlas has not quietly grown or shrunk to accommodate decoration), and the
  Stage 03 report compares the Release payload with `docs/template/baseline.md`.
