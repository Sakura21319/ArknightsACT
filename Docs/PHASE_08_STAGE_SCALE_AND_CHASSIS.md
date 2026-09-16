# Phase 08 — Larger Combat Blocks and Mobile-City Chassis

## Stage scale

Generated block footprint is enlarged from **14 x 11** to **18 x 14** world units.

Goals:

- create more ACT movement distance before enemies immediately enter melee range;
- leave room for future terrain devices / tactical features without turning every block into prop clutter;
- let Open / Street / CoverLane blocks read as distinct spaces rather than compact rooms;
- preserve the existing 4 / 6 / 9 logical block progression.

`RogueliteStageRuntimeController` owns the enlarged physical coordinates, block resolution, outer bounds, navigation cardinal nodes, cover layouts, treasure positions and encounter spawn offsets.

`RogueliteStageExpansionController` bridges legacy Phase-08 presentation authored against 14 x 11 blocks:

- expands `[FloorSockets]` to the new footprint;
- updates `RogueliteFloorSocket25D.Footprint` so Active Originium still fits a whole socket;
- expands broad merged floor plates after they appear;
- moves visual perimeter and north/east backdrop architecture to the enlarged boundary;
- hides the old compact block-connection joint caps rather than stretching them into oversized floor markings;
- does not scale gameplay cover or Facility traversal colliders.

## Mobile-city chassis

`RogueliteMobileCityChassisController` gives the stage a visible understructure rather than leaving a thin deck floating over a black void.

The chassis is presentation-only and consists of:

- upper armored belly plate directly below the combat deck;
- narrower deep service hull;
- central service spine;
- south/west camera-facing armor skirts with recessed vents/service panels;
- lower edge rails;
- longitudinal and cross girders;
- diagonal truss braces;
- several underside machinery pods;
- sparse warm maintenance-light strips.

No chassis object owns gameplay collision or navigation.

## Canon-compatible gameplay candidates

The following should be considered as future authored block features, not all enabled simultaneously:

1. **Active Originium** — already present; risk/reward damage plus offensive buff terrain.
2. **Heat-pump passages** — periodic high-damage lanes inspired by Chernobog `Broken Avenue`; useful as telegraphed timing hazards in ACT.
3. **Special tactical points** — areas that increase push/pull force; best paired with enemies near barriers or heat-pump lanes.
4. **Deployable / movable roadblocks** — let the player temporarily alter enemy routes or create cover, adapted from Arknights obstacle play.
5. **Vent / exhaust grates** — intermittent steam bursts that hide, stagger or displace units; visually fits the mobile-city service deck.
6. **Maintenance switches** — short interaction objectives that open shutters, disable a hazard lane or power a shortcut.
7. **Service shutters / blast doors** — timed lane changes that create combat-phase topology changes rather than static decoration.
8. **Damaged deck / rubble fields** — movement-slowing zones with intact safe lanes; use sparingly so ACT movement stays readable.
9. **Originium-contaminated pockets** — smaller mineral-crust areas that alter movement/skill timing without replacing every hazard with Active Originium.
10. **Vertical service platforms** — short ramps / upper catwalks that support ranged enemies, jump attacks and route choice without becoming full platforming sections.

Reference direction checked against PRTS Chernobog stage material such as `切尔诺伯格 6区废墟`, `59区废墟`, `破碎大道`, and the PRTS special-terrain catalog. Exact ACT mechanics should remain adaptations rather than literal tower-defense deployment rules.

## Validation

1. Rebuild Prototype Scene and confirm 0 compiler errors.
2. Confirm each block is visibly larger and block entry/encounter triggering still follows the correct logical cell.
3. Confirm enemies can navigate across E/W and N/S block connections on all stage sizes.
4. Confirm Facility ramp and upper floor still work.
5. Confirm treasure / boss exit / enemies do not spawn outside the floor.
6. Confirm Active Originium still occupies one complete floor socket.
7. Inspect south/west deck edges from the gameplay camera: underside armor, girders, braces and machinery should remain visible instead of collapsing into pure black.
8. Confirm chassis has no colliders and cannot interfere with combat/navigation.
9. Check lighting cost after the larger stage footprint; only the directional key should cast shadows.
