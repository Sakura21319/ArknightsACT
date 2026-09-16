# Phase 08 - Playable Architecture and Distant City Pass

This pass addresses three Unity-side observations from the latest playtest:

1. the north/east horizon behind the stage still reads as empty black;
2. some dressing geometry visually protrudes beyond its collider or intersects neighboring generated architecture;
3. new buildings should participate in movement/combat rather than exist only as skyline decoration.

## Distant mobile-city background

`RogueliteStageDistantDistrictController` adds a visual-only district beyond the playable north/east edge:

- stepped background decks and armor edges;
- multiple depth layers of industrial building masses;
- facade vent bands and roof caps;
- distant antenna towers;
- a long service skybridge;
- a far service gantry / industrial silhouette.

The pass deliberately uses no colliders and casts no realtime shadows. Fog and the existing cold environment lighting provide depth separation. The geometry is an ACT-oriented interpretation of a large mobile city, not a claim about an official Chernobog underside or district layout.

## Playable architecture

`RogueliteStagePlayableArchitectureController` introduces architecture intended to affect player movement:

- walk-in service rooms with a readable doorway and partial roof;
- internal service cabinets that function as cover;
- raised loading decks with a real sloped ramp;
- railings and deck cover;
- covered checkpoints / alcoves.

These objects use explicit `BoxCollider`s. They are placed outside the broad central E/W + N/S route reserve so the existing waypoint navigation remains valid.

The existing `TwoFloorFacility` remains the authoritative enemy-navigable vertical structure. The new raised decks are currently side opportunities for player positioning rather than replacements for the navigation graph.

## Interpenetration cleanup

`RogueliteStageCompositionCleanupController` runs after the urban and playable architecture passes. Bulky dressing such as scaffolds, rubble, black Originium, cargo and broken slabs is tested against architecture collider bounds and moved among deterministic safe pockets until it no longer overlaps.

`RogueliteStageDressingCollisionController` now derives aggregate blocker bounds from the actual rendered geometry instead of hard-coded sizes. Scaffold rails are also solid in addition to poles, platforms and braces. This closes the common case where the player could visually enter part of a rubble pile or scaffold even though another part already had collision.

## Validation

After rebuilding `PrototypeRun`, verify:

- the north/east horizon contains visible distant city layers rather than a flat black void;
- distant background geometry never blocks gameplay;
- walk-in service rooms can be entered through their doorway and their walls/cabinet block correctly;
- loading ramps are traversable and deck rails block correctly;
- rubble/crystal/cargo collision matches visible volume more closely;
- scaffold rails/poles/platforms/braces all collide;
- no generated dressing is visibly embedded into a service building or playable room;
- cardinal inter-block routes remain open.
