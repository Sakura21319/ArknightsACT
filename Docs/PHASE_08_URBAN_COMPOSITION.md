# Phase 08 Urban Composition Pass

## Goal

Move the generated 18x14 cells away from flat steel rectangles and toward readable mobile-city districts with real obstacle silhouettes, vertical architecture and safe ACT traversal.

## Gameplay containment

`RogueliteStageContainmentController` adds four tall renderer-free BoxCollider walls around the complete generated stage. The low visible perimeter can stay visually believable while the invisible shell prevents the player from jumping or dashing onto the decorative chassis/outside void.

## Obstacle composition

`RogueliteStageUrbanCompositionController` waits for the deterministic set-dressing pass, then snaps the major blocking features into four extreme corner pockets. These pockets stay outside the broad central E/W + N/S navigation cross and leave room around the current enemy/treasure spawn offsets.

Blocking dressing types:

- scaffold;
- rubble cluster;
- black Originium growth;
- broken deck slab;
- cargo stack.

`RogueliteStageDressingCollisionController` then installs physical colliders after composition:

- aggregate BoxCollider for rubble / crystal / broken slab / cargo roots;
- mesh-bounds BoxCollider for scaffold poles, platforms and braces;
- `Physics.SyncTransforms()` after the collision pass.

## Architecture vocabulary

The same composition controller builds solid mid-edge architecture so tall forms enrich the district without cutting the cardinal cross:

- multi-storey service buildings with floor bands, facade inserts and roof utility units;
- ruined building shells with partial second-floor slabs and exposed beams;
- utility warehouses with service doors and roof caps;
- relay/utility towers with stacked vent panels and antenna masts;
- maintenance kiosks and open service canopies;
- overhead service gates that preserve walk-under clearance;
- pipe bridges with solid posts/decks and visual service pipes.

Theme usage is deterministic but varied:

- Open: service block + ruined shell + pipe bridge;
- Street: taller service building + ruined shell + overhead gate;
- CoverLane: warehouse + relay tower + pipe bridge;
- Facility: existing traversable `TwoFloorFacility` remains authoritative, framed by utility annex/tower/service infrastructure;
- SafePlaza: lower kiosk/canopy architecture;
- BossArena: stronger vertical relay-tower silhouettes + overhead gate while the arena center stays open.

## Navigation rule

The current enemy navigation remains waypoint based, so this pass does not attempt arbitrary maze generation. The central cardinal cross remains the guaranteed route between cells. Architecture and dressing blockers are deliberately kept to edge/corner pockets until navigation is upgraded to obstacle-aware routing/NavMesh.

## Validation

1. Rebuild the Prototype Scene.
2. Verify player cannot jump/dash through any outer edge.
3. Walk into rubble, black crystal, cargo and scaffold parts; they must block.
4. Jump onto scaffold platforms and confirm stable grounding.
5. Enter several themes and confirm architecture differs materially between blocks.
6. Verify enemy E/W/N/S routes and Facility ramp still work.
7. Check tall near-camera architecture for excessive occlusion; keep the south/west sightline readable.
