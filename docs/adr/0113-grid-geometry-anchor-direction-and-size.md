# ADR 0113 — Grid geometry: anchor, direction, size, and which tiles an area keeps

- **Status:** Accepted
- **Date:** 2026-10-30
- **Stage:** 06 (binding from Stage 07, which implements `IArenaQuery` against it)
- **Supersedes:** nothing
- **Related:** [ADR 0102](0102-four-facing-sprites-two-drawn-mirrored.md) (four facings),
  [ADR 0101](0101-no-ecs-struct-array-fighters.md), GDD §1.6, `llmwiki/systems/grid-and-targeting.md`

## Context

The contract (`ArenaSkillLoadout`) fixes what an area is *called* — six `AreaShape` values and
an `AreaSize` byte — but not what the byte measures, where the area is anchored, or which way it
points. Three of the six shapes are symmetric and three are not, so a single meaning for the
direction would have been either useless or wrong. Nothing in the contract says whether a wall
stops a skill, and `GridCoord` says nothing about which grid axis each `IsoDir` means, though
`FakeArenaSession` had already grown a private answer.

These are the questions every later stage would otherwise answer differently, which is what
makes them an ADR rather than a comment.

## Decision

1. **The anchor is the aimed tile, never the caster.** `ArenaCommand.Target` is where the area
   is centred, which is the tile the player clicked and the tile `SkillWindUp.Tile` carries.
2. **The direction is the caster's facing**, `ArenaFacing.Toward(caster, anchor)`. It rotates
   the shapes that have a front: `Line` and `Cone` grow **away** from the caster, through the
   anchor. `Single`, `Cross`, `Diamond` and `Square` are symmetric; they still take the
   parameter so one signature resolves every shape.
3. **`AreaSize` means what the shape measures:**

   | Shape | `AreaSize` |
   | --- | --- |
   | `Single` | ignored; one tile |
   | `Line` | total tiles, anchor included; 0 counts as 1 |
   | `Cross` | arm length from the anchor |
   | `Diamond` | Manhattan radius (1 = anchor plus four neighbours) |
   | `Square` | half extent (1 = 3×3) |
   | `Cone` | length in rows; row *i* is *2i+1* tiles wide |

   A size may also be read as "how far past the anchor the skill reaches" by subtracting one;
   the chosen reading keeps the smallest legal area (one tile) usable with size 0.
4. **Tiles outside the grid are dropped; tiles the mask blocks are kept.** The tile set is
   geometry. A highlight clipped by the grid edge matches the damage that will resolve, while a
   wall is a *rule* question (does a wall stop a thrust?) that Stage 07 answers by reading the
   same set. Deciding it here would have hidden a rules decision inside a geometry function.
5. **The four `IsoDir` values are the four grid axes**, one bijection, stated once:

   | `IsoDir` | step |
   | --- | --- |
   | `NE` | (0, +1) |
   | `SE` | (+1, 0) |
   | `SW` | (0, −1) |
   | `NW` | (−1, 0) |

   A non-axis-aligned offset resolves by its dominant axis, ties to the X axis; a zero offset
   answers `SE`, the default facing. The mapping matches what the fake already did, so replacing
   the fake in Stage 07 cannot change which way anyone turns. The fake's private copy is deleted,
   not maintained.
6. **Occupancy is a mirror inside `ArenaGrid`.** The simulation keeps the fighters' tiles and
   writes `TryOccupy`/`TryVacate`, so walk search, jump and rendering all read one board. The
   grid refuses to overwrite an occupant rather than silently replacing it, and
   `ComputeStateHash` should include the mirror so a desync surfaces as a hash mismatch.
7. **Queries write into caller spans.** A result that does not fit is truncated, not regrown;
   the search's bookkeeping is stack memory sized to the board. Output order is row-major (or
   generation order for shapes) so equal inputs always produce an equal list.

## Alternatives considered

- **Anchor the area on the caster and extend toward the aim tile.** Rejected: it makes a
  ground-targeted skill impossible to place on a far tile, which the ranged skills in `skill.csv`
  need.
- **Give each shape its own method/overload.** Rejected: six call sites in Stage 07 where one
  will do, and the contract resolves shapes dynamically from a loadout.
- **Treat a blocked tile as out of the area.** Rejected for Stage 06 as above; it is a one-line
  rule in Stage 07 if the design wants it, and it should then apply to every shape at once.
- **A separate `GridDir` enum next to `IsoDir`.** Rejected: two four-value enums that must stay
  in sync is exactly the drift ADR 0102 removed for sprites.
- **Return a `List<GridCoord>` or an array from the queries.** Rejected by the zero-allocation
  requirement: a query runs on the highlight path every frame during aiming.

## Consequences

- **Positive:** the geometry is decided once and tested as goldens (20 cases); the search is
  allocation-free and measured at 4.7 µs per query, far inside the 0.05 ms budget; Stage 07
  consumes functions instead of inventing formulas.
- **Negative:** a designer reading `AreaSize` without this ADR will guess wrong for `Line`
  (`Line`, size 2 = two tiles, not three); the shape's numeric meaning lives in a document and a
  table, not in a name.
- **Negative:** an area can span a wall, so a skill preview may highlight a tile nobody can be
  pushed into. Stage 07 settles it explicitly.
- **Verification:** `GridDomainTests` — 20 shape goldens over all four directions, corner and
  edge clipping, a 10,000-board property run against an independent flood fill, and zero
  allocated bytes across 500 rounds of all four query kinds.
