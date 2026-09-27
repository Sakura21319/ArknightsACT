# SYSTEM 04 — Chernobog City / World / Exploration

Updated: 2026-09-26

This document replaces the dated mobile-city, zone, minimap, facility and building/container handoffs.

## 1. World direction

The game currently stays focused on Chernobog mobile-city exploration.

World system directory:

`Assets/_Game/Scripts/Gameplay/Roguelite/World`

Routing/minimap:

`Assets/_Game/Scripts/Gameplay/Roguelite/Routing`

Current map design uses continuous city blocks rather than isolated roguelite rooms.

Typical stage footprints remain based around:

- 4×3
- 4×4
- 5×4

Seeded generation should remain reproducible.

## 2. Current layout rule

**Normal buildings use rule-based aligned urban layout.**

Do not restore the superseded global “randomly rotated / heavily staggered houses” direction from the 2026-09-22 interaction handoff.

Variety should come from:

- building use/type;
- frontage;
- facade modules;
- dimensions;
- props;
- district rules;
- controlled setbacks;
- landmarks.

Road/navigation clarity takes priority over arbitrary rotation.

## 3. Main zones

Current district language includes:

- core / municipal;
- ruins;
- industrial;
- perimeter/defensive areas.

Zone-specific systems may affect:

- palette/materials;
- building mix;
- risk;
- enemy density/strength;
- container opportunity;
- facilities and landmarks.

Unknown areas must not leak hidden loot/building details through UI.

## 4. Building / container system

Building profiles and container profiles are data-driven in the existing world/scavenging code.

Important files:

- `ChernobogSearchBuildingProfiles.cs`
- `SalvageContainerProfiles.cs`
- `SearchableContainer25D.cs`

Existing design includes:

- multiple building-use profiles;
- approximately 28 container variants;
- five container quality tiers;
- four current item rarity bands.

Container quality controls rarity weighting; it does not guarantee top rarity.

Visual container shape and reward logic remain separate concerns.

## 5. Enterable buildings

`EnterableBuilding25D` owns building visit/search state.

Entering a building:

- may reveal visit state;
- must not automatically roll/open all containers;
- must not expose hidden loot early.

Building states should distinguish:

- not visited;
- visited with loot remaining;
- cleared;
- intentionally empty.

## 6. Facilities

Core:

- `CityFacility25D`
- `CityFacilityController`

Current facility concepts include:

- emergency healing;
- survey/reveal;
- power unlock;
- risk/reward tower interactions.

Hold interaction remains separate from:

- F search;
- E extraction/route interaction.

Facility state is map/run-owned unless explicitly designed otherwise.

Network/authority fields are future-facing only; actual multiplayer transport is not implemented.

## 7. Landmarks / verticality

Current world includes major core/industrial landmarks such as:

- municipal/core structures;
- tower/maintenance landmark;
- vertical routes and roof access.

Relevant runtime includes:

- `CityTowerLandmark`
- `CityVerticalRoute`
- tower/vertical partials on the city streets controller.

Landmarks may carry real searchable resources. Their visual height/antenna does not imply every visible top surface is gameplay-accessible.

## 8. Minimap and exploration guide

Core:

- `RogueliteMinimapController`
- `RogueliteMinimapGraphic`
- `CityExplorationGuide`

Current controls:

- M: expand/collapse map;
- N: cycle exploration / extraction / next-stage guidance.

Principles:

- fog/unknown state is respected;
- player location/direction shown;
- extraction and available next-stage target can be shown;
- known uncleared buildings can be highlighted;
- guidance must not auto-move or auto-extract.

## 9. Visual composition architecture

The world stack contains specialized controllers for:

- roads/markings;
- facade modules;
- infrastructure;
- chassis/deep base;
- district density;
- set dressing;
- terrain;
- environment;
- distant districts/backdrops;
- quality pass;
- composition cleanup.

Shared StageRoot / visual-root access should be used rather than each controller independently creating duplicate roots.

Avoid reintroducing high-frequency Update initialization where generation/event initialization already exists.

## 10. Wheelchair interaction

`Wheelchair25D` / `WheelchairLocomotion25D` remain a world interaction.

Current character-seat offset was tuned upward during integration. Treat it as presentation interaction tuning, not part of operator numeric progression.

## 11. Validation

Editor validation:

- `MobileCityGenerationValidation`
- `CityExplorationValidation`

Validation covers seeded layout, navigation/search positions, container/building constraints and exploration logic.

These tests do not replace:

- full-scene performance;
- collision feel;
- lighting;
- player readability;
- real Play Mode traversal.
