# Phase 08 — Lighting and Fine Surface Pass

## Goal

Move the selected Concept-01 Chernobog deck away from flat prototype shading without adding more floor markings or decorative clutter.

The pass focuses on two things that should be visible from the normal gameplay camera:

1. metal surfaces react differently to light at broad and micro scales;
2. the stage has an authored cool-key / dark-ambient / warm-practical lighting hierarchy.

## Fine material layer

`ChernobogMaterialFineDetailPass` runs after `ChernobogMaterialProductionPass`.

The production pass remains responsible for broad albedo, normal, AO and metallic/smoothness maps. The fine pass adds optional shader detail maps only when the current shader exposes the relevant properties.

- Deck / DeckHeavy / DeckSecondary / Steel use subtle horizontal brushed-metal micro detail.
- Wall uses a restrained vertical painted-metal micro structure.
- Inset uses darker, lower-contrast micro breakup.
- Detail albedo stays close to neutral 0.5 so it does not repaint the stage or create visible repeating symbols.
- Detail normals are deliberately weak; their job is to break broad specular highlights rather than make the surface look noisy.
- Specular highlights and environment reflections are kept enabled where supported.
- The pass references no URP package classes. All shader properties are guarded with `Material.HasProperty`.

Manual rebuild menu:

`ArknightsACT > Assets > Apply Chernobog Fine Material Detail`

`Build Prototype Scene` also applies the pass automatically.

## Runtime lighting rig

`RogueliteStageLightingController` runs after the quality and per-module material-variation passes.

It uses only UnityEngine `Light` and `RenderSettings`:

- one cold directional key with soft shadows;
- a broad cool deck fill from the south-west gameplay-camera side;
- a weaker cool industrial rim from the north/east side;
- three warm spot practicals near north/east infrastructure;
- two small local warm point lights for controlled metal highlights;
- tri-light ambient colours so deck tops, vertical walls and under-deck structure no longer receive the same flat ambient level;
- subtle linear fog and controlled reflection intensity;
- HDR remains enabled on the main camera.

Only the directional key owns shadows. Additional spots/points do not cast shadows, keeping the first lighting pass relatively inexpensive.

The controller disables the old `[Concept01_QualityLights]` runtime practical-light root so the two systems do not stack.

## Art direction

The intended hierarchy is:

`cold key -> readable deck mass -> dark cavities / undersides -> cool rim on hard-surface edges -> sparse warm service-light pools`

Orange should be expressed primarily by real local light and a few authored accents, not by repeating orange floor markers.

## Validation

1. Pull the branch and wait for zero compiler errors.
2. Run `ArknightsACT > Build Prototype Scene`.
3. Enter Stage 1 and compare the same south-west camera framing used in earlier screenshots.
4. Check that floor / WallVent / HVAC highlights break up under movement rather than reading as one smooth plastic lobe.
5. Check that under-deck structure and wall recesses are darker than upward-facing deck surfaces without becoming crushed black.
6. Check that warm service lights form a few local pools and do not wash the full map orange.
7. Confirm the directional light is the only visibly shadow-casting authored stage light and that performance remains acceptable.
8. Confirm combat, navigation, Active Originium, cover collision and Facility traversal are unchanged.
