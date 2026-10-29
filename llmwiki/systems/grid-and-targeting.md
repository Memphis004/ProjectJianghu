# Grid and targeting domain

Stage 06's grid rules live in `lib/Runtime/Domain/Arena/` next to the ArenaBattle contract
(`ArenaGrid.cs`, `ArenaPathfinder.cs`, `ArenaArea.cs`, `ArenaFacing.cs`) — see
[ADR 0113](../../docs/adr/0113-grid-geometry-anchor-direction-and-size.md) for why the anchor,
the direction and the meaning of `AreaSize` are the way they are. They are pure C#,
reference nothing but `System`, and depend on no config row: the numbers they take
(`moveRange`, `jumpRange`, `AreaSize`) arrive resolved from `CombatStatBlock` and
`ArenaSkillLoadout`, and the balance numbers stay in `data/`.

## `ArenaGrid` — the board

- Built from an `arena_map` row's `mask` (`ArenaGrid.FromMask(width, height, mask)`), one
  string per row, `.` walkable and `#` blocked. The shape and the characters are validated
  here as well as in `Data.Validator`, so a mask built in code or restored from a save fails
  loudly instead of becoming an invisible wall.
- `Width`, `Height`, `TileCount` (`Width × Height`), capped by `ArenaConstants.MaxGridSize`
  (32) and `MaxGridTiles` (1024).
- `GridCoord` ↔ index is row-major: `ToIndex` returns `-1` outside the grid, `FromIndex`
  returns `GridCoord.Invalid`. The index is internal plumbing, never saved.
- Occupancy is a mirror of the fighter positions: `TryOccupy`, `TryVacate`,
  `ClearOccupancy`, `IsOccupied`, `OccupantAt`, `IsFree`. `NoFighter` (255) means empty, and
  occupying a blocked, out-of-grid or already-taken tile is refused rather than overwritten.
  Stage 07 keeps the fighters' tiles and updates the mirror, so every query sees one board.

## Walks — `ArenaPathfinder`

- `GetReachable(grid, start, moveRange, span)` — breadth-first search of the four Manhattan
  neighbours, limited to `moveRange` steps. Blocked tiles and occupied tiles are never
  entered; the start tile is never reported. The result is written row-major, so the same
  inputs always produce the same list in the same order.
- `FindPath(grid, start, goal, moveRange, span)` — the same search, then the parent chain is
  reversed into the caller's span: the start is excluded, the goal is the last entry, and
  every entry is one adjacent tile from the previous one. It answers `0` for every refusal
  (occupied or unwalkable goal, out-of-grid tile, unreachable walk, more steps than
  `moveRange`, more steps than the span, goal equal to start); the command layer decides
  which `CommandResult` that maps to.
- There is no diagonal step, so there is no corner-cutting rule to break.
- `GetJumpTargets(grid, start, jumpRange, span)` — every tile 1..`jumpRange` Manhattan away
  whose landing tile is walkable and empty. Tiles in between are irrelevant, which is what
  lets a jump clear a fighter standing in the way (GDD §1.6).

## Areas — `ArenaArea`

`Resolve(shape, anchor, direction, size, grid, span)` writes the covered tiles. The anchor is
the aimed tile, not the caster; the direction is the caster's facing, so line and cone grow
**away** from the caster through the anchor, while cross, diamond and square are symmetric.

| Shape | `AreaSize` means | `size = 2` at (5,5) facing SE |
| --- | --- | --- |
| `Single` | ignored | (5,5) |
| `Line` | total tiles, anchor included | (5,5), (6,5) |
| `Cross` | arm length | plus (4,5), (6,5), (7,5), (5,4), (5,3), (5,6), (5,7) |
| `Diamond` | Manhattan radius | 13 tiles at Manhattan distance ≤ 2 from the anchor |
| `Square` | half extent | 25 tiles, 5×5 |
| `Cone` | length in rows | (5,5); (6,4), (6,5), (6,6) |

Tiles outside the grid are dropped (the highlight a player sees is clipped exactly like the
damage will be). Tiles inside the grid but blocked by the mask are **kept**: the shape is
geometry, and whether a wall stops a skill is a Stage 07 rule reading the same set.

## Facing — `ArenaFacing`

Movement is four-directional Manhattan and a fighter only ever faces one of four `IsoDir`
diagonals (ADR 0102), so the two spaces are one bijection:

| `IsoDir` | grid step |
| --- | --- |
| `NE` | (0, +1) |
| `SE` | (+1, 0) |
| `SW` | (0, −1) |
| `NW` | (−1, 0) |

That is the mapping `FakeArenaSession` already used, so Stage 07 can replace the fake without
changing which way anyone turns. An offset that is not axis-aligned resolves by its dominant
axis, ties going to the X axis — `(1,1)` faces `SE`. A zero offset answers `SE` (the default
facing), so no caller has to handle "no direction".

`Relative(defenderTile, facing, attackerTile)` compares quarter turns: the direction from the
defender to the attacker is its facing (`Front`), its opposite (`Back`, the backstab) or a
perpendicular (`Flank`). The attacker's own facing never matters. The damage bonuses behind
`Back`/`Flank` are `constant` rows (`back_damage_bonus_x100`, `flank_damage_bonus_x100`) and
belong to Stage 07; this enum is already the answer `ArenaEventFlags.Backstab`/`Flank` needs.

## Buffers and budget

Every query writes only into the caller's `Span<GridCoord>` and returns the number of entries
written; a span that is too small truncates the result instead of throwing or allocating. The
search's bookkeeping is `stackalloc` memory sized to the board (144 ints for the 12×12 map),
so it is reclaimed when the call returns, two queries cannot observe each other, and the
measured allocation of a query is **0 bytes** (`GC.GetAllocatedBytesForCurrentThread`).

Stage 06's measurements, from `GridDomainTests` on the authored 12×12 map: reachability, path,
jump and a cone resolved back to back average **4.7 µs per query** against the 0.05 ms budget,
and a 10,000-case property run compares the search against an independent flood fill over
random 1..6 × 1..6 boards with random blockers.

## Who calls this today

`FakeArenaSession` does, for every geometric question: `GetReachableTiles`,
`GetJumpTiles`, `PreviewSkillArea`, the Move and Jump validation (a walk must have a path,
not just a Manhattan distance) and the facing after a move. It keeps an `ArenaGrid`'s
occupant mirror current from the fighters that are still standing, and a skill's resolve
hits every opposing fighter in the same tile set its preview showed. That is why a
highlight can be trusted before Stage 07 exists; `FakeArenaGeometryTests` pins each of
those agreements, including the contrast that a tile a walk cannot reach is still a legal
jump landing.

## What Stage 07 still owes

- Populate the occupant mirror from the real simulation and map `FindPath`'s `0` to the
  right `CommandResult` per reason.
- Read `moveRange`/`jumpRange` from `CombatStatBlock` and `AreaSize`/`Area` from
  `ArenaSkillLoadout`, and build the grid from the `arena_map` row instead of the fake's
  empty rectangle (`ArenaMatchSetup` carries only `ArenaMapId`).
- Decide whether a blocked tile stops a skill's area (the geometry keeps it today).
