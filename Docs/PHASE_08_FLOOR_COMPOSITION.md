# Phase 08 — Broad Floor Composition Pass

## Why this pass exists

The previous environment kit improved material response and hard-surface silhouettes, but the battlefield still exposed the original 6x5 physical floor-socket rhythm too clearly. That made the stage read like a procedural editor grid rather than a deliberate Arknights-style industrial deck.

This pass keeps the 6x5 physical socket system for gameplay, pits and traversal, but decouples visual floor composition from that socket grid.

## Persistent floor assets

`ChernobogFloorProductionPass` extends the generated local kit with:

- `FloorPlate_LongX` — one broad plate spanning three physical cells;
- `FloorPlate_LongZ` — one broad plate spanning two physical cells;
- `FloorJoint_X` — mechanical connection cap for E/W block links;
- `FloorJoint_Z` — mechanical connection cap for N/S block links;
- `Kit_DeckSecondary` — a restrained second steel response for large tonal masses.

Manual rebuild menu:

`ArknightsACT > Assets > Apply Chernobog Floor Composition Assets`

A normal `Build Prototype Scene` also ensures these assets exist.

## Runtime composition rules

`RogueliteStageFloorCompositionController` runs after the modular kit is placed.

### Pit safety

The central E/W row and central N/S columns are already guaranteed pit-ineligible by `RogueliteStageLayoutController`. Only those safe lanes are visually merged into broad plates.

Pit-eligible corner/side cells remain one visual plate per physical socket, so opening a pit still removes exactly the correct floor cell.

### Broad masses

For every 14x11 block:

- the central row becomes two 3-cell-wide plates;
- the central columns become paired 2-cell-tall plates above and below the center row;
- side/corner cells remain individual modules;
- broad material changes happen by block/theme rather than checkerboard per tile.

This reduces the visible small-cell seam count substantially while preserving all physical gameplay geometry.

### Block connections

The old long prototype seam overlays are disabled. Instead, only real block-to-block traversal links receive a compact mechanical joint cap. This makes the stage connection language functional rather than decorative.

### Prototype overlay cleanup

The pass hides the old `RoadStrip`, `RoadStripe`, `PlazaPad`, `ArenaPad` and Concept-01 `DeckTransitions` render overlays. Theme identity now comes from broad deck tone, cover layout, equipment and architecture rather than flat debug-like slabs.

## Art direction target

The floor should now read in this order:

1. one broad industrial deck mass;
2. a few large plate changes;
3. sparse mechanical block joints;
4. only a handful of maintenance inserts;
5. pits / hazards / gameplay cover as the main points of interest.

The player should no longer perceive every physical socket as a separate decorated tile.

## Validation

After pulling the branch:

1. wait for zero compiler errors;
2. run `ArknightsACT > Assets > Rebuild Chernobog Modular Kit`;
3. run `ArknightsACT > Build Prototype Scene`;
4. enter Stage 1;
5. verify the central cross reads as larger plates rather than a 6x5 grid;
6. verify pits still remove exactly one side/corner physical floor socket;
7. verify no large visual plate covers a pit;
8. verify Facility ramp/navigation and cover collision remain unchanged.
