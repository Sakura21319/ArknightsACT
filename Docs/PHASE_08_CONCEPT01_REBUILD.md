# Phase 08 - Concept 01 Unity Rebuild

## Chosen direction

The selected visual target is Concept 01: a clean early-Chernobog/mobile-city industrial combat deck.

The important characteristics are:

- broad, mostly quiet steel floor plates instead of a symbol on every tile;
- cold blue-grey metal with restrained muted-orange service accents;
- modular vented north/east boundary walls with visible frame depth;
- small HVAC / electrical utility covers instead of plain prototype cubes;
- occasional service grates and hatches, not repeated decoration across the whole board;
- strong industrial architecture outside the playable footprint to provide location and scale;
- readable ACT combat space first, environment detail concentrated at edges and selected service points.

## Runtime implementation

`RogueliteStageConceptOneController` is layered after the previous authenticity pass and before pit binding.

It deliberately leaves route, encounter, enemy navigation, rewards and physical block ownership unchanged.

### Floor

- Previous `[ArknightsTileSkin]` objects are disabled.
- `FloorSurface_*` renderers are retuned to one of two procedural steel-deck materials.
- Procedural deck textures use fine material noise and a plate rim only; no tactical icon is stamped on every socket.
- Only a small deterministic minority of sockets receive a maintenance grate or service hatch.
- Service hatches use one small orange tab rather than a full orange tile marker.

### Cover

- Existing collider/body remains authoritative.
- Old orange top/hazard-band presentation is hidden.
- A new layered skin adds inset vent faces, raised steel corner posts, a metal cap and a thin offset orange asset stripe.
- Some variants receive a compact top service box.

### Facility

- Existing two-floor traversal and ramp stay untouched.
- Added north facade band, multiple vent bays, steel cap, service light, pipes and an upper HVAC unit.

### Boundary

- Original `Bound_N/S/E/W` colliders remain.
- Their renderer is hidden.
- New boundary skin uses repeated framed wall modules with inset vent faces, thicker posts, top caps and sparse foot lights.
- Camera-near south/west guards remain lower than north/east walls.

### Block transitions

- Chunk seams are now dark mechanical joints with a small bridge plate at the center.
- They no longer read as bright floor decoration.

### Background architecture

Concept 01 uses one visually dominant north-side facility plus smaller north/east utility masses and a distant catwalk silhouette. This avoids the previous row of unrelated giant cuboids.

### Debug visual cleanup

`Prototype25DVisionCone` now keeps its enemy-perception logic but hides the orange line cone by default. The cone is only a debug presentation and can be enabled through the serialized `showDebugCone` field when needed.

### Pit compatibility

`RoguelitePitFloorSyncController` now disables a nearby Concept-01 service grate/hatch whenever its socket becomes a real pit, preventing floating floor detail above an open shaft.

## Local validation

After pulling the branch:

1. Run `ArknightsACT > Build Prototype Scene` again.
2. Enter Play Mode.
3. Check Stage 1 first at the same camera framing used in the previous screenshots.
4. Confirm there are no orange vision arcs during normal play.
5. Confirm most floor plates are visually quiet; only occasional grates/hatches should appear.
6. Confirm cover collision and jump behavior did not change.
7. Confirm real holes do not keep a floating grate/hatch.
8. Confirm the Facility ramp and second floor still navigate correctly.
9. Check Stage 2 and Stage 3 for edge architecture overlap with enemies/chests.

The next iteration should be driven from a fresh Unity screenshot of this Concept-01 pass rather than another image-generation pass.
