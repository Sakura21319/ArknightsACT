# Phase 08 — Chernobog World Visuals / Map Generation

> Active implementation branch: `feat/phase-08-world-visuals`
>
> This phase follows `HANDOFF_PHASE_07_EXPLORATION.md`. Gameplay, rewards and route ownership remain unchanged; the focus is physical block generation and the environment/background presentation layer.

## 1. Visual target

The current prototype is anchored to early-story **Chernobog / mobile-city urban ruins** rather than a generic cyberpunk city.

Primary visual rules:

- cold blue-gray steel/concrete floor panels,
- modular industrial slabs and low-rise utility buildings,
- dark heavy mobile-city silhouettes,
- sparse orange/yellow hazard identifiers,
- exposed machinery, roof units and antennae,
- readable broken/open floor sections,
- Active Originium as a recognizable tactical terrain feature,
- enough negative space to preserve ACT movement and the four cardinal block connections.

The goal is to reproduce the original game's visual grammar and setting logic while keeping the 2.5D ACT map playable. We are **not** copying a complete Arknights stage geometry 1:1.

## 2. PRTS references

Environment references are local-prototype assets, following the same rule as the existing PRTS Spine workflow.

### Story backgrounds

PRTS image dataset entries used by this phase:

- `bg_cher_0` → `Avg_bg_bg_cher_0.png`
- `bg_cher_2` → `Avg_bg_bg_cher_2.png`
- `bg_cher_5` → `Avg_bg_bg_cher_5.png`

PRTS background resource index:

```text
https://prts.wiki/w/剧情资源概览/背景
```

The current PRTS media URLs are cataloged editor-side in:

```text
Assets/_Game/Editor/PRTS/PrtsEnvironmentReferenceCatalog.cs
```

### Stage layout reference

Chernobog 6 District Ruins:

```text
https://prts.wiki/w/切尔诺伯格_6区废墟
https://torappu.prts.wiki/assets/map_preview/level_rune_05-02.png
```

The PRTS stage description identifies the area as a core Chernobog urban district turned battlefield and uses **Active Originium** as special terrain. This matches the current ACT adaptation's environmental direction.

Chernobog 59 District Ruins is also a useful secondary reference for compact abandoned urban blocks and Active Originium:

```text
https://prts.wiki/w/59区废墟
```

## 3. Local asset workflow

Download the environment reference pack:

```text
ArknightsACT > Assets > PRTS > Download Chernobog Environment References
```

Files are written to:

```text
Assets/_Game/Art/Environment/PRTS/Chernobog/
```

That path is already covered by the repository's PRTS `.gitignore` rule. Do not commit or redistribute downloaded game art through this repository.

After downloading, rebuild:

```text
ArknightsACT > Build Prototype Scene
```

The generated scene receives the locally available Chernobog backgrounds. If they are absent, the project falls back to the procedural mobile-city skyline and remains playable.

## 4. Map-generation changes

### Segmented floor

`RogueliteStageLayoutController` replaces each generated 14 x 11 monolithic floor slab with a 6 x 5 set of floor modules after the runtime stage is created.

Important constraints:

- the central east-west corridor is never eligible for pit removal,
- the central north-south corridor is never eligible for pit removal,
- route generation and navigation graph ownership remain in `RogueliteStageRuntimeController`,
- environment mechanics do not rebuild the map.

`RogueliteFloorSocket25D` is the physical interface between the map and environment layers.

### Real pit openings

The existing environment controller continues to decide **where/when** a pit exists.

`RoguelitePitFloorSyncController` runs after the environment pass:

```text
Environment creates Hazard_Hole
-> find nearest eligible floor socket in the same block
-> snap pit presentation/trigger to that socket
-> disable that floor module's renderer and collider
```

This converts the Phase 07 dark-floor illusion into an actual opening while preserving the architecture boundary.

### Block variants

Open / Street / Cover Lane chunks now receive deterministic internal variants based on stage/block/theme. The variants alter existing physical cover positions and, for Street, road orientation/marking.

The controller also adds low-rise Chernobog-style industrial shells near block edges. Cardinal connections remain intentionally open so these details do not sever exploration routes.

Facility traversal geometry is intentionally **not** randomized yet because its ramp/upper-floor navigation nodes are authored to fixed positions.

## 5. Background integration

`RogueliteStageBackdropReferenceController` optionally adds one dimmed, camera-facing Chernobog reference card behind the procedural skyline.

Principles:

- official/reference image = distant mood layer only,
- procedural/3D geometry = playable world and silhouette depth,
- no PRTS image is required for gameplay,
- no new runtime shader is created with `Shader.Find`; the backdrop clones an already-verified stage material to avoid the Phase 07 magenta-material issue,
- stage 1/2/3 prefer separate Chernobog background references when locally available.

This is intentionally subtler than replacing the entire world with a flat screenshot.

## 6. What is deliberately not generated by Image2.5 yet

Image generation is held as fallback only. PRTS already contains suitable Chernobog background references, so this phase does not generate an imitation of artwork that already exists.

Use generated art only when a required view/prop has no usable PRTS source, for example:

- a new ACT-specific side-facing industrial facade,
- a parallax layer that does not exist in the original assets,
- a unique destroyed-building variant required by the procedural map,
- a bespoke catastrophe sky/dust layer.

Generated assets should still follow the Chernobog material/color/scale rules above rather than inventing a new art direction.

## 7. Local validation checklist

After pulling the branch:

```text
[ ] Unity compiles with no red errors.
[ ] Download Chernobog Environment References succeeds.
[ ] Build Prototype Scene succeeds with and without downloaded PRTS environment files.
[ ] Stage 1 floor is visibly segmented but has no movement seams/snags.
[ ] Street/Open/Cover Lane blocks show multiple layouts across a run.
[ ] Four cardinal block connections remain traversable.
[ ] Enemies can still navigate across block boundaries.
[ ] Facility ramp and second floor are unchanged and navigable.
[ ] At least one pit appears where expected.
[ ] Pit removes a real floor module instead of drawing a dark rectangle on top of solid ground.
[ ] Player can jump across the opening.
[ ] Entering pit still costs 20% max HP and resets safely.
[ ] Enemy entering pit still dies.
[ ] Active Originium behavior remains unchanged.
[ ] Ballista behavior remains unchanged.
[ ] No new geometry renders magenta/purple.
[ ] With PRTS backgrounds downloaded, the distant image reads as Chernobog but does not overpower characters/combat readability.
[ ] Without PRTS backgrounds, procedural skyline fallback still works.
```

## 8. Next pass after validation

Do not increase chunk dimensions yet. First validate density and readability, then continue in this order:

1. tune shell placement against chest/enemy spawn positions,
2. add dedicated Street-A/B/C and Industrial-A/B/C theme data instead of only variant transforms,
3. add damaged facade / scaffolding / pipe modular props,
4. add an ACT-specific Active Originium visual based on PRTS terrain reference,
5. replace primitive ballista presentation with an appropriate source asset if PRTS has one; otherwise generate/author a compatible prop,
6. implement the first catastrophe visual pass (Dust Front) with separate near/mid/far layers.

## 9. Rights / distribution note

PRTS itself states that game images, animation, audio and original game text remain copyrighted by Hypergryph and its affiliates. Keep downloaded game assets local to the prototype workflow and review content rights before any public/commercial distribution.
