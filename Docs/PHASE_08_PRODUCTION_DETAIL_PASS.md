# Phase 08 — Production Detail Pass

## Why this pass exists

The previous modular-kit milestone solved the architecture problem: map logic can now place persistent reusable environment modules instead of building the whole scene from transient Unity cubes.

The next visual problem was repetition. Even a beveled modular kit still looks procedural if every wall bay has the same orange light, every HVAC unit has the same face, and every service insert appears with the same rhythm.

This pass therefore focuses on **authored hard-surface layering + sparse visual rhythm** rather than adding more map markers.

## Editor-side production detail

`ChernobogProductionDetailPass` enriches the generated local prefabs after `ChernobogEnvironmentKitBuilder` has created the base kit.

The pass is automatically applied by `PrototypeStageRuntimeFactory` before the prototype scene serializes the environment-kit references.

Manual command:

`ArknightsACT > Assets > Apply Chernobog Production Detail Pass`

### WallVent upgrades

- raised structural shoulder and mid rail;
- physical angled louver blades with real depth/self-shadow;
- lower kick plate;
- small service bay;
- sparse ID strip;
- optional utility conduit and clamps;
- restrained fasteners;
- top cable channel for silhouette breakup.

### HVAC_M / HVAC_L upgrades

- four equipment feet and a lower shadow gap;
- framed side service door, hinges/handle language;
- rear intake/louver bank;
- one top fan on HVAC_M, two on HVAC_L;
- actual fan housing, well, hub and blades;
- optional service pipe;
- one small identification strip rather than a large orange roof accent.

### Other upgrades

- ElectricalCabinet gets a plinth, door rib, handle, cable drop and optional service box.
- Catwalk gets a real under-chord and cross-members so distant infrastructure has thickness.

All added hard-surface boxes use the same persistent beveled-mesh factory as the modular kit. New production meshes are saved under the ignored local `Assets/_Game/Data/ChernobogKit/Meshes/` folder.

## Runtime art direction

`RogueliteStageArtDirectionController` runs after modular placement and before the final surface/lighting pass.

It deliberately reduces repeated visual noise:

- hides leftover prototype `RoadStripe`, `FacilityAccent`, `ShopAccent` and `BlockMarker` renderers;
- keeps only roughly one third of already-sparse floor service inserts;
- wall practical lights appear only every several bays instead of on every module;
- wall ID strips / conduits / service bays are deterministic but intermittent;
- alternate HVAC service faces rotate 180 degrees for variation without moving colliders;
- HVAC orange stripes and service pipes are selectively hidden;
- the three far north pipe runs are reduced asymmetrically.

The purpose is to make the scene read in this order:

1. playable deck mass;
2. cover / wall silhouettes;
3. industrial material response;
4. selected maintenance details;
5. orange accents only as punctuation.

## Gameplay boundary

This pass does not change:

- GridCover collision;
- floor socket collision;
- pit opening/damage/reset logic;
- enemy navigation graph;
- Facility ramp / second-floor traversal;
- encounter placement;
- Active Originium or ballista behavior.

## Local validation

1. Pull `feat/phase-08-world-visuals`.
2. Wait for zero compiler errors.
3. Run `ArknightsACT > Assets > Rebuild Chernobog Modular Kit`.
4. Run `ArknightsACT > Build Prototype Scene`.
5. Enter Stage 1 from the same south-west camera framing.
6. Compare three areas carefully:
   - north/east vent walls: louvers should have visible depth and sparse lights;
   - HVAC_M/L: top fans, rear louvers, feet and service panels should break the box silhouette;
   - floor: broad areas should stay quiet with very few inserts/accents.
7. Confirm cover/jump collision, pits, Facility ramp, navigation and hazards are unchanged.

## Next production step

After this pass is validated in Unity, the highest-value next upgrade is no longer more procedural geometry. It is a small authored texture/mesh replacement set for the most camera-visible modules:

- WallVent hero module;
- HVAC_M / HVAC_L hero modules;
- FloorPlate edge/seam set;
- PitFrame;
- one large north-side industrial facade kit.

Those assets can replace the generated prefabs one-by-one while keeping `RogueliteStageModularKitController` and all map-generation logic intact.
