# Phase 08 — Active Originium and Hazard Policy

## Decision

Pit and ballista hazards are removed from the current prototype direction.

They were adding traversal / visual noise without contributing enough to the selected early-Chernobog / mobile-city combat identity. The segmented floor system remains in place because it is still useful for authored terrain replacement, but generated stages no longer create `Hazard_Hole` or `Hazard_Ballista` objects and the prototype scene no longer installs `RoguelitePitFloorSyncController`.

`RogueliteStageHazardPolicyController` remains as a defensive cleanup pass so stale scenes or future environment code cannot silently reintroduce hole or ballista hazards.

`BallistaHazard25D` remains dormant source code only; it is not spawned by the current generated stages.

## Active Originium reference direction

The gameplay hazard remains `ActiveOriginiumZone25D`; this pass is presentation-only.

The previous red/crimson floor treatment was rejected after in-engine review because it read as a painted plastic hazard plate. The new art direction follows the supplied Originium mineral reference instead:

- mineral mass is predominantly black / smoke-brown;
- exposed facets read amber / dark gold rather than saturated red;
- emissive energy is restrained to thin amber veins and a very small minority of fragments;
- the terrain should look like fragmented Originium embedded into a damaged industrial deck, not a clean colored tile;
- shard silhouettes stay low enough to preserve ACT movement readability.

PRTS stage references for Active Originium gameplay context remain:

- https://prts.wiki/w/4-3_%E4%BA%BA%E5%B7%A5%E5%88%B6%E5%86%B7
- https://prts.wiki/w/S4-1_%E6%99%B6%E7%B0%87-1

## Visual rebuild

`RogueliteActiveOriginiumPresentationController` now:

- snaps Active Originium to one side/corner physical floor socket;
- replaces that socket's normal floor surface visually;
- removes nearby maintenance inserts so the special terrain has a clean silhouette;
- normalizes the trigger footprint to the floor module;
- suppresses the legacy purple/orange skin and the rejected red-tile skin;
- builds a dark industrial frame plus a very rough scorched mineral bed;
- adds several irregular carbonized ore patches rather than one smooth colored plate;
- adds only a few thin amber veins instead of a broad emissive core;
- generates three cached faceted crystal meshes at runtime and scatters deterministic clusters of 18 low shards/chips;
- mixes black ore, amber, gold-facet and rare emissive-amber materials so the mineral reads black/yellow from the gameplay camera;
- strips inherited deck albedo/normal/AO maps from crystal materials so metal-panel texture detail is not projected onto the mineral faces;
- uses deliberately low smoothness on the bed/frame to remove the previous plastic response while keeping controlled highlights on crystal faces.

`RogueliteStageEnvironmentController` also uses a subdued amber placeholder and no longer spawns temporary red crystal spikes before the production presentation appears.

## Validation

1. Rebuild the prototype scene.
2. Enter combat blocks until an Active Originium tile appears.
3. Confirm no pit / hole or ballista terrain hazard is generated anywhere.
4. Confirm the Active Originium terrain replaces one normal floor module instead of floating over it.
5. From the normal gameplay camera, confirm the dominant read is black/brown mineral debris with amber/yellow facets — not a red plate.
6. Check that the base is rough/matte while individual shard faces catch harder highlights without looking glossy-plastic.
7. Confirm shards remain low enough that they do not look like physical blockers.
8. Confirm touching the tile still triggers the existing ACT Active Originium damage/buff behaviour.
9. Confirm cover collision, Facility navigation and enemies are unchanged.
