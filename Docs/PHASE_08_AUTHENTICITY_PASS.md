# Phase 08 — Arknights stage authenticity pass

This pass responds directly to the first Unity screenshot of the Phase-08 branch. The gameplay layout was readable, but the world still looked like a grey prototype arena: a flat board, continuous box walls, oversized cuboid buildings inside the combat space, simple orange-top cover and weak foreground/background separation.

The goal of this iteration is **not** to turn the ACT prototype back into a tower-defense grid. It keeps the current 2.5D action movement, enemy navigation, route/encounter ownership and procedural 4/6/9-block structure, while making the physical stage read much more like an actual Arknights mobile-city combat space.

## Visual reference direction

Primary direction remains early Chernobog / mobile-city industrial stages. PRTS references already collected by Phase 08 include Chernobog story backgrounds and the Chernobog 6 District Ruins map reference. The 6 District Ruins setting explicitly uses Active Originium and a ruined Chernobog core-city battlefield, which matches the current prototype's first environment family.

Important visual rules for this pass:

- cold blue-grey metal rather than neutral bright grey;
- clear modular tactical deck cells;
- dark recessed seams, maintenance plates and grates;
- restrained dirty-orange safety identifiers rather than large orange slabs;
- segmented vented industrial boundary panels;
- architecture mass sits mostly **outside** the playable board;
- gameplay cover reads as engineered RIIC/mobile-city utility equipment, not Minecraft-like boxes;
- pits expose a real shaft;
- Active Originium reads as an iconic stage tile rather than a purple rectangle;
- distant Chernobog massing should create depth behind the board without obscuring gameplay.

## Implemented

### `RogueliteStagePaletteController`

Retunes the generated material assets at runtime while preserving material names for downstream systems:

- darker/cooler ground;
- deeper road asphalt;
- colder facility wall/deck metal;
- less plastic cover response;
- muted orange tactical accent;
- lower smoothness and stronger material separation.

This is intentionally a runtime clone pass, so the generated base materials remain reusable and no shader lookup is required.

### `RogueliteStageAuthenticityController`

Runs after the segmented-floor layout pass.

- disables the first-pass `ChernobogLowRise_*` blockout buildings inside playable chunks;
- adds maintenance/grate/ID detail directly to segmented floor sockets;
- upgrades `GridCover` with inset faces, vent slats, posts, metal top cap, restrained orange lip and occasional utility box;
- keeps original outer-bound colliders but hides their single stretched renderer;
- rebuilds the visible boundary as short repeating industrial wall modules;
- far north/east walls use taller vented panels;
- camera-near south/west guards stay lower to preserve ACT readability;
- adds internal deck-joint seams and bridge plates at procedural block boundaries;
- adds assembled Chernobog utility structures outside the north/east stage edges;
- retunes ambient/fog/directional lighting for stronger depth and less flat grey wash.

### `RogueliteStageTerrainPresentationController`

Runs after real-hole binding and upgrades environment-created terrain without changing gameplay scripts/colliders.

- Active Originium: recessed base, orange safety rim and denser uneven crystal cluster;
- pit: visible shaft walls and warning tabs below the removed physical floor socket;
- ballista: industrial feet, shield, rails and arm structure around the existing hazard logic;
- next-stage marker: blue industrial frame while keeping existing activation/interaction behavior.

## Expected screenshot difference

Compared with the first Phase-08 screenshot, the next build should no longer have the tall rectangular `ChernobogLowRise` block sitting in the middle of the combat area. The board should have visibly segmented mobile-city deck plates, richer service detail, much more structured perimeter walls, stronger shadow/value separation and a denser Chernobog skyline concentrated behind the playable area.

## Local validation

1. Pull `feat/phase-08-world-visuals`.
2. In Unity run `ArknightsACT > Build Prototype Scene` again. Rebuilding is required because `PrototypeRun.unity` is generated.
3. Enter Play Mode on Stage 1 and capture one full-screen screenshot.
4. Check Console for compile/runtime errors.
5. Verify:
   - player/enemies still traverse all cardinal block connections;
   - the lower near-camera guards do not block the view;
   - no `ChernobogLowRise_*` cube is visible inside playable space;
   - covers remain collidable and jumpable as before;
   - real pits still remove one floor socket and trigger reset/damage;
   - Active Originium effect remains unchanged mechanically;
   - ballista behavior is unchanged;
   - Facility ramp/navigation remains intact;
   - no magenta materials appear;
   - Stage 2/3 wider maps do not over-clutter the skyline.

## Next visual iteration after screenshot validation

Once this pass is visually confirmed in Unity, the next highest-value work is to replace remaining primitive geometry with a small reusable authored environment kit:

- Chernobog deck tile A/B/C;
- vent wall A/B/end/corner;
- RIIC utility crate long/square/tall;
- maintenance grate and cable trench;
- ruined facade / scaffold / pipe modules;
- dedicated Active Originium mesh/material;
- more faithful stage entrance/goal hardware.

Use PRTS/game references first. Only generate new art for ACT-specific assets that do not have a suitable original reference/source asset.
