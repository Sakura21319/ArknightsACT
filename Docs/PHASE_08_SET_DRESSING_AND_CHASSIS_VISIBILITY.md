# Phase 08 - Set Dressing and Chassis Visibility

## Why this pass exists
The enlarged 18 x 14 combat blocks improved movement space, but the first result still read as a clean empty steel plane. The camera-facing south/west underside also merged into the dark background, so the battlefield still looked like a thin floating board.

This pass addresses both problems without moving combat, navigation, route, reward, or encounter ownership.

## Stage set dressing
`RogueliteStageSetDressingController` adds deterministic visual clusters near block edges while preserving a clear central ACT movement cross.

Current clusters:
- multi-level maintenance scaffold with poles, grating, rails and diagonal braces;
- black Originium-like faceted crystal growths;
- broken deck slabs and scattered rubble;
- stacked industrial cargo blocks with structural X braces;
- darker repair/deck patches and occasional service grates.

Theme rhythm:
- Facility: scaffold + rubble + cargo;
- Street: scaffold + damaged slab + rubble, with occasional crystal growth;
- Cover Lane: cargo + rubble + crystals;
- Safe Plaza: lighter cargo/scaffold/rubble dressing;
- Boss Arena: stronger crystal growth + broken slabs + tall scaffold;
- Open: rubble + damaged deck + occasional crystals.

All current dressing is visual-only. Existing gameplay cover remains the authoritative collidable obstacle system until a later gameplay pass deliberately promotes selected props into navigation-aware mechanics.

## Mobile-city underside visibility
`RogueliteMobileCityChassisController` now builds a deeper, camera-readable body below the deck:
- upper belly armor;
- mid service hull;
- lower city body and central service spine;
- south/west protruding service terraces;
- visible support towers and vents;
- deep braces/girders;
- machinery pods and large drive housings;
- sparse warm service lights.

The south/west structures protrude beyond the playable footprint so they remain visible from the normal gameplay camera instead of being hidden directly under the deck.

`RogueliteStageLightingController` also adds a broad shadowless `CoolUndersideLift` light. It is intentionally dimmer than the deck fill: the underside should read as structure, not become equally bright as the battlefield.

## Validation
1. Rebuild the Prototype Scene.
2. From the normal gameplay camera, confirm the south/west lower half is filled by terraces, support towers, girders and drive housings instead of pure black.
3. Confirm every block now contains multiple readable dressing types and no longer reads as a uniform steel floor.
4. Confirm the central cardinal movement lanes remain clear.
5. Confirm dressing and chassis objects do not introduce gameplay colliders/navigation changes.
6. Check black crystal silhouettes under the new lighting; they should read as faceted mineral growth rather than smooth plastic spikes.
