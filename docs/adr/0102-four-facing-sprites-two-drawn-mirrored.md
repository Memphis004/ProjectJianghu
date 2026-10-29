# ADR 0102 — Four iso facings, two drawn sets, mirrored at render time

- **Status:** Accepted
- **Date:** 2026-10-29
- **Stage:** 00 (binding from Stage 03, the HD-2D spike, and Stage 09, the arena
  presentation)
- **Supersedes:** nothing
- **Related:** [GDD §1.8](../gdd/GDD.md), `IsoDir` in [GDD §2](../gdd/GDD.md)

## Context

The arena is an isometric grid whose movement is four-directional Manhattan, so a
character only ever faces a diagonal: `NE`, `SE`, `SW`, `NW` (`IsoDir` in the contract,
values 0–3). The camera is orthographic and fixed at 45° yaw, and it never rotates.

Eight-direction art would mean four drawn sets; four-direction art would mean four
drawn sets too (two front-ish, two back-ish). Pixel art is the longest-lead item in the
project (GDD's closing note), and a drawn set is not one image: it is seven animations —
idle 4, walk 6, attack 6, cast 4, jump 4, hit 2, death 4 = **30 frames** at 64×80 px.

Because the two front facings are mirror images of each other on an iso grid, and so are
the two back facings, half the work is redundant. The GDD already says so
("วาดจริงแค่ 2 ชุด (หันหน้ากับหันหลัง) แล้วกลับด้านภาพเอา"); this ADR makes it a rule with
a code-visible mapping, so Stage 03 and Stage 09 do not each invent it.

## Decision

1. **Draw two sets: `SE` (front) and `NE` (back).** The other two are produced at render
   time:

   | `IsoDir` | Drawn set | Transform |
   | --- | --- | --- |
   | `SE` (1) | `front` | none |
   | `SW` (2) | `front` | flip horizontally |
   | `NE` (0) | `back` | none |
   | `NW` (3) | `back` | flip horizontally |

   One function owns the mapping (`IsoDir` → set + flip) and it is unit-tested for all
   four values. A `FighterView` carries `Facing`; the view resolves the sprite, never the
   other way round.
2. **Gameplay never reads a sprite.** Flank/backstab is computed from the `IsoDir`
   enum's relative angle (front / flank / back), stated once in Stage 06's targeting
   rules. Mirroring is therefore incapable of changing damage: the enum is the truth and
   the art follows it.
3. **Flip the sprite renderer only.** The `flipX` (or negative local scale) applies to
   the character's sprite renderer, not to its root transform: the shadow, the status
   icons and the world-space HP bar must not mirror, and a negative scale on a collider
   or a lit material is a second bug class.
4. **Art constraint: asymmetry is a cost, not a detail.** A left-hand-only weapon,
   a one-sided sash or a scar reads correctly in one facing and wrong in its mirror. The
   rule for the prototype is: **all characters carry the weapon in the same hand and
   wear asymmetric detail symmetrically.** A character that genuinely needs asymmetry
   gets four drawn sets of its own — an asset exception, no code change.
5. **Frame budget is stated with the decision:** 30 frames per set × 2 sets = **60
   frames per character**, against 120 for four drawn sets. Import stays 64×80 px,
   point filter, one fixed pixels-per-unit and the pivot at the feet (GDD §1.8); the
   footing, not the sprite centre, is the y-sort key.
6. **Cut-ins and portraits are separate assets.** The skill cut-in (brush-stroke name
   banner) and any portrait are not mirrored sprites; none of them may be used as a
   facing frame.

## Alternatives considered

- **Four drawn sets (true 4-direction art).** Rejected for the prototype: doubles the
  longest-lead asset work — 120 frames per character — to fix a mirror that the iso
  projection makes nearly invisible.
- **Eight directions.** Rejected: the camera never rotates and movement is Manhattan;
  four facings already cover every legal move, so four more have no state to display.
- **Mirror in the texture at import (bake flipped sheets).** Rejected: doubles the atlas
  and the build payload for something a per-renderer flag does for free; it also hides
  which frames are authored from which.
- **A shader that flips UVs.** Rejected as more moving parts than `flipX` for the same
  result, and it would interact with the lit HD-2D materials for no benefit.

## Consequences

- **Positive:** the asset list is halved and unambiguous; the mapping is one tested
  function; the camera and gameplay are untouched by art decisions.
- **Negative:** mirrored frames are visibly mirrored for asymmetric detail — a real
  limitation the art rules above accept rather than solve.
- **Negative:** every character shares one weapon hand. A future skin or NPC with a
  different handedness needs its own four sets, and that must be a content decision, not
  a shader trick.
- **Verification:** Stage 03's spike must show all four facings walking on the iso grid
  (screenshot in the stage report); Stage 09 implements the mapping and the flip on the
  sprite renderer, with a unit test over the four `IsoDir` values and a PlayMode check
  that facing changes do not move the shadow.
