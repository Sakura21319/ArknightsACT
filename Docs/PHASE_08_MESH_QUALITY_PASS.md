# Phase 08 — Concept-01 Mesh Quality Pass

## Why this pass exists

The selected Concept-01 direction is a clean early-Chernobog / mobile-city industrial deck. The previous Unity screenshots still read as prototype geometry because most environment pieces were raw Unity cube primitives. Material tuning alone cannot create the edge highlights, silhouette breaks, under-deck depth and hard-surface response visible in the target concept.

This pass keeps gameplay/navigation/collision ownership unchanged and upgrades only presentation geometry.

## Implemented

### Reusable chamfered hard-surface mesh

`ChernobogBeveledMeshFactory` builds and caches runtime box meshes with:

- six main hard-surface faces;
- twelve chamfer strips;
- eight clipped corner faces;
- explicit normals and UVs;
- generated tangents for the existing normal-map quality pass.

The bevel is expressed in local metric size rather than by scaling a unit cube, so long wall panels do not receive absurdly wide chamfers.

### Concept-01 geometry upgrade

`RogueliteStageMeshUpgradeController` runs after `RogueliteStageConceptOneController` and before `RogueliteStageQualityPassController`.

It converts visual Concept-01 cube modules into chamfered meshes, including:

- perimeter wall frames and caps;
- utility/HVAC cover skins;
- facade modules and roof machinery;
- catwalk pieces;
- small floor surface plates.

The original `GridCover` body and selected facility walls/rails/columns are also upgraded. Their existing BoxColliders are resized when transform scale is baked into the visual mesh, so gameplay collision dimensions remain unchanged.

### Under-deck structure

The camera-facing south and west sides now receive a visual-only structural layer below the playable slab:

- dark fascia beams;
- lower steel chords;
- repeated vertical supports;
- alternating diagonal truss braces;
- sparse warm service-light strips.

This is specifically intended to remove the previous “flat grey slab floating over a black void” appearance without adding obstacles to the playable surface.

## Validation

After pulling the branch:

1. Wait for Unity to compile with zero errors.
2. Rebuild `ArknightsACT > Build Prototype Scene` once.
3. Enter Stage 1 and inspect the same south-west camera framing used in the previous screenshots.
4. Confirm covers and perimeter modules have visible edge highlights instead of perfectly sharp cube silhouettes.
5. Confirm the south/west platform underside now shows beams and truss structure.
6. Confirm cover collision, Facility walls/rails/ramp, pits, enemy navigation and jumping behave exactly as before.

## Next visual step

Once the mesh pass is validated, the next quality jump should be authored/reusable environment assets rather than more procedural markers: dedicated deck, vent-wall, HVAC, pipe, support-beam and catwalk modules with persistent PBR texture sets and controlled variants.
