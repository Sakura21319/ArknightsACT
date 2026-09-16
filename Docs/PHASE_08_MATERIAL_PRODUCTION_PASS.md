# Phase 08 — Production Material Pass

## Goal

The selected Concept-01 geometry was reading too much like uniformly tinted Unity primitives. The main causes were low-frequency 256px prototype maps, nearly constant smoothness, repeated material response across every module and base-colour tint being multiplied over already-coloured albedo.

This pass moves the environment toward a colder, rougher Arknights mobile-city industrial material language without changing gameplay geometry.

## Persistent material maps

`ChernobogMaterialProductionPass` runs after the modular kit and broad-floor extensions are available.

For Deck, DeckHeavy, DeckSecondary, Wall, Inset, Steel and Grate it generates 512x512 persistent maps under:

`Assets/_Game/Data/ChernobogKit/Textures/Production/`

Each surface receives:

- authored cool-steel albedo with broad tonal variation rather than speckled per-pixel noise;
- restrained directional brushing / rolling marks on steel surfaces;
- sparse chips and grime rather than markings on every tile;
- dedicated micro-normal response;
- cavity / dirt AO;
- metallic + smoothness variation map, with smoothness stored in alpha;
- neutral material tint so the albedo is not multiplied dark twice.

The pass does not reference URP package classes. It only assigns shader properties when those properties exist on the current material.

Manual rebuild menu:

`ArknightsACT > Assets > Apply Chernobog Material Production Pass`

`Build Prototype Scene` also applies the pass automatically.

## Per-module variation

`RogueliteStageMaterialVariationController` uses `MaterialPropertyBlock` to add very small deterministic tint / metallic / roughness differences to repeated `Kit_*` renderers.

The variation is intentionally subtle. It should break copy-paste repetition without reintroducing the checkerboard appearance that earlier tile-by-tile colour variation created.

Shared material assets are not cloned at runtime.

## Hazard cleanup

Ballista hazards are retired alongside pits in the current environment direction.

- `RogueliteStageEnvironmentController` no longer creates `Hazard_Ballista`.
- `RogueliteStageHazardPolicyController` removes stale pit or ballista objects from older generated scenes.
- `BallistaHazard25D` is left in source control only as dormant prototype code; it is not part of generated play spaces.

## Validation

1. Pull the branch and wait for zero compiler errors.
2. Run `ArknightsACT > Build Prototype Scene`.
3. Confirm no pit or ballista hazard appears in any block.
4. Compare floor / wall / HVAC close-ups against the previous build: broad colour should stay quiet while scratches, brushing and roughness appear mainly through lighting.
5. Move the camera view across WallVent and HVAC edges and confirm highlights break up naturally instead of forming a single plastic sheen.
6. Check repeated wall bays / covers: subtle per-module response variation should be visible only on comparison, not as obvious checkerboard colour changes.
7. Verify Active Originium remains the amber-black shard terrain and its gameplay trigger still functions.
