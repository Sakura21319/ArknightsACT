# Phase 08 — Persistent Chernobog Modular Asset Kit

## Goal

Move the selected Concept-01 direction away from transient `CreatePrimitive(Cube)` presentation and toward a reusable environment-production workflow that can later be replaced piece-by-piece with hand-authored meshes and textures without changing roguelite map logic.

The target remains the clean early-Chernobog / mobile-city industrial deck language selected from Concept-01:

- broad quiet steel deck surfaces;
- thick modular vent walls;
- HVAC / electrical utility cover;
- exposed pipes and maintenance infrastructure;
- restrained orange identification strips and practical lights;
- readable ACT traversal first, environment detail second.

## New asset pipeline

`ChernobogEnvironmentKitBuilder` creates a local persistent asset library under:

`Assets/_Game/Data/ChernobogKit/`

The folder is deterministic and ignored by Git. The builder code is the source of truth, so a fresh checkout can regenerate the full kit automatically during `Build Prototype Scene`.

Manual rebuild menu:

`ArknightsACT > Assets > Rebuild Chernobog Modular Kit`

Generated content:

- persistent beveled Mesh assets;
- PBR-style deck / wall / cover / grate materials;
- persistent albedo, normal and AO Texture2D assets;
- reusable environment prefabs;
- one `ChernobogEnvironmentKit.asset` manifest referenced by the generated prototype scene.

The generated kit folder and its root `.meta` are ignored intentionally. During this phase, do not treat those generated binaries as the hand-authored source of truth; rebuild them from code until a module is deliberately replaced with an authored FBX/texture asset.

## Current module set

### Floor

- `FloorPlate`
- `FloorPlateHeavy`
- `Floor_Grate`
- `Floor_ServiceHatch`

The physical floor sockets remain authoritative for collision and pits. The modular pass only replaces their render mesh/material and adds sparse service inserts.

### Walls

- `WallVent`
- `WallSolid`
- `WallLow`
- `WallCorner`

The original `Bound_N/S/E/W` BoxColliders remain authoritative. These prefabs replace only the presentation layer.

### Gameplay cover

- `HVAC_S`
- `HVAC_M`
- `HVAC_L`
- `ElectricalCabinet`

`GridCover` keeps its original collider and gameplay transform. The old body renderer is hidden and a modular utility-equipment visual is attached.

### Infrastructure

- `PipeRun`
- `Catwalk`
- `SupportBeam`
- `PitFrame`

These are visual modules. Pit damage/reset behavior stays in the existing hazard scripts and the physical hole still comes from `RogueliteFloorSocket25D.Open()`.

## Runtime placement

`RogueliteStageModularKitController` runs after the Concept-01 blockout skin and before the mesh/PBR quality passes.

It:

1. disables the old Concept-01 dynamic floor-detail / utility-cover / perimeter roots;
2. replaces `FloorSurface_*` render meshes with persistent floor meshes;
3. places only rare floor grates/service hatches;
4. replaces `GridCover` renderers with modular HVAC units while retaining the old colliders;
5. rebuilds north/east full-height and south/west low perimeter walls from reusable prefabs;
6. dresses Facility blocks with reusable equipment and pipe modules;
7. adds background catwalk / pipe infrastructure;
8. swaps old primitive pit edging for the modular pit frame when a hazard appears.

The route graph, encounter ownership, floor sockets, Facility ramp and hazard behavior are intentionally unchanged.

## Why this matters

Previous visual passes improved color and shape, but most objects were still transient primitive geometry. This made the scene read as a polished blockout rather than a production environment.

The modular kit creates a stable boundary between:

- **map rules** — where gameplay objects belong; and
- **environment assets** — what those objects look like.

That means future hand-authored FBX / Blender meshes, PRTS-derived reference textures, decals or Substance-style PBR sets can replace individual kit prefabs without rewriting procedural map generation.

## Local validation

1. Pull the branch and wait for zero compiler errors.
2. Run `ArknightsACT > Assets > Rebuild Chernobog Modular Kit` once.
3. Run `ArknightsACT > Build Prototype Scene`.
4. Enter Stage 1.
5. Verify:
   - floor is mostly visually quiet;
   - repeated orange tile markers are gone;
   - cover looks like HVAC / electrical equipment rather than plain cubes;
   - perimeter wall modules have visible depth and bevel highlights;
   - pits keep their existing gameplay behavior but use the new industrial frame;
   - Facility ramp and upper-floor navigation remain usable;
   - no magenta materials;
   - no new collider blockers from visual prefabs.

## Next art-production step

After this generated kit is visually stable, replace the generated modules one-by-one with authored production assets in this priority order:

1. `HVAC_M` / `HVAC_L`;
2. `WallVent` / `WallCorner`;
3. `FloorPlate` + `Floor_Grate`;
4. `PitFrame`;
5. pipes / catwalk / support structure;
6. decals, warning text, grime and environment-specific variation.

The placement controller should not need to change when those authored assets arrive.
