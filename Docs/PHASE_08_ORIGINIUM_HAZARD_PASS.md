# Phase 08 — Active Originium and Hazard Policy

## Decision

Pit hazards are removed from the current prototype direction.

They were adding traversal noise without contributing enough to the selected early-Chernobog / mobile-city combat identity. The segmented floor system remains in place because it is still useful for authored terrain replacement, but generated stages no longer create `Hazard_Hole` objects and the prototype scene no longer installs `RoguelitePitFloorSyncController`.

`RogueliteStageHazardPolicyController` remains as a defensive cleanup pass so stale scenes or future environment code cannot silently reintroduce hole hazards.

## Active Originium reference

PRTS stage 4-3 `人工制冷` documents classic **Active Originium** terrain: units standing on / crossing the tile continuously take true damage while receiving a large offensive benefit.

Reference:

- https://prts.wiki/w/4-3_%E4%BA%BA%E5%B7%A5%E5%88%B6%E5%86%B7
- https://prts.wiki/w/S4-1_%E6%99%B6%E7%B0%87-1

The ACT prototype keeps its existing gameplay adaptation for now (`ActiveOriginiumZone25D`) and changes the visual language only in this pass.

## Visual rebuild

`RogueliteActiveOriginiumPresentationController` now:

- snaps Active Originium to one side/corner physical floor socket;
- replaces that socket's normal floor surface visually;
- normalizes the trigger footprint to the floor module;
- hides the old purple/orange prototype tile and crystal skin;
- builds a dark-framed **red / crimson hazardous floor tile**;
- adds a hotter red core, emissive fissures and only a few low crystal fins;
- uses cached beveled hard-surface meshes so the tile belongs to the same environment kit as the deck.

The result should read as a special Arknights terrain tile first, and as decorative crystal clutter second.

## Validation

1. Rebuild the prototype scene.
2. Enter combat blocks until an Active Originium tile appears.
3. Confirm no pit / hole terrain is generated anywhere.
4. Confirm the Active Originium tile replaces a normal floor module instead of floating over it.
5. Confirm the tile is predominantly red/crimson with dark framing and hot red fissures.
6. Confirm touching the tile still triggers the existing ACT Active Originium damage/buff behaviour.
7. Confirm cover collision, Facility navigation, enemies and ballista hazards are unchanged.
