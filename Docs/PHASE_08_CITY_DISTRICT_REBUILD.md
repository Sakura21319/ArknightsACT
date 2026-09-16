# Phase 08 - City District Rebuild

## Goal
Move the stage from an industrial combat platform toward a believable Chernobog/mobile-city district: continuous roads, dense camera-far facades, a mix of enterable and sealed buildings, street furniture, debris pockets and a layered horizon.

## Stage scale
The physical cell footprint remains 18 x 14 for navigation stability, but the logical map is enlarged:

- Stage 1: 3 x 3 = 9 blocks
- Stage 2: 4 x 3 = 12 blocks
- Stage 3: 4 x 4 = 16 blocks

This increases the actual city footprint without stretching every cover/nav placement again.

## Street composition
`RogueliteStageCityStreetsController` adds a continuous E/W + N/S road cross to each block, sidewalk/service bands, drains and restrained road joints. The central road cross remains free for the current waypoint graph.

The north side of each block receives a dense street wall of sealed buildings/storefronts. These are not enterable and use solid building-mass colliders. The map's right-most column receives additional east-facing buildings so the gameplay camera reads a continuous urban edge rather than a platform ending in void.

Street furniture includes service cabinets, lamps, bollards and occasional utility piping. These are placed outside the protected road cross.

## Enterable vs sealed buildings
- `RogueliteStagePlayableArchitectureController`: enterable service rooms, raised loading decks and covered checkpoints.
- `RogueliteStageCityStreetsController`: sealed city buildings/storefronts used for density, silhouette and route boundaries.
- Existing `TwoFloorFacility`: remains the authoritative enemy-navigable vertical building.

## Background / right-side void
`RogueliteStageHorizonCityController` adds a broad visual-only north/east/north-east city wrap behind the existing distant district. It includes lower terraces, low-detail tower rows and a large north-east megastructure. A dark-cool far foundation catches downward sightlines.

The camera clear/fog colour is lifted from near-black to a dark blue-grey so any residual gap reads as urban haze rather than empty void.

If local PRTS Chernobog story backdrops are installed, `RogueliteStageBackdropReferenceController` now overscans the story image according to the enlarged 3x3/4x3/4x4 city footprint so its edges are not exposed.

## Overlap policy
`RogueliteStageCompositionCleanupController` now considers colliders from:
- urban shell architecture;
- playable architecture;
- city street/sealed-building architecture.

Bulky dressing is moved into street-safe pockets. If no non-overlapping pocket exists, that single dressing feature is disabled instead of knowingly leaving visible interpenetration.

## Validation
1. Rebuild Prototype Scene.
2. Verify Stage 1 is now a 3x3 city footprint.
3. Traverse the E/W and N/S roads between all adjacent blocks.
4. Confirm north-side sealed buildings block entry while playable service rooms remain enterable.
5. Check the right-most/east edge from the normal gameplay camera; no large pure-black hole should remain.
6. Inspect scaffolds/rubble/crystals near buildings for interpenetration.
7. Confirm the outer containment still prevents jumping/dashing off the stage.
