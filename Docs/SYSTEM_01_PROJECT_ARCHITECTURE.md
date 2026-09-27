# SYSTEM 01 — Project Architecture / Current State

Updated: 2026-09-26

This is a long-lived system document. Prefer this file over dated MASTER/PROJECT_STATUS handoffs.

## 1. Project identity

Project root: `D:\WorkSpace\ArknightsACT`

Primary prototype scene:

`Assets/_Game/Scenes/PrototypeRun.unity`

Unity:

`6000.0.23f1`

Current product direction:

- 2.5D ACT combat;
- continuous Chernobog mobile-city exploration;
- extraction / scavenging loop;
- light in-run roguelite construction;
- Arknights operator/collectible data used as prototype source;
- current priority is system completion, consistency and polish rather than adding unrelated subsystems.

Core operation loop:

```text
Home
  -> Start Operation
  -> Explore / Combat / Search / Facilities
  -> Continue to deeper Stage OR return to extraction
  -> Real Extraction
  -> Settlement
  -> Warehouse / Trade
  -> Home
```

Crossing to another Stage is not extraction. Unsecured normal loot remains at risk until a real extraction succeeds.

## 2. Architecture rule

Dependency direction:

```text
Core
  -> Combat
  -> Gameplay
  -> Character / Roguelite / Presentation adapters
```

Hard rules:

- Core does not know individual characters or UI.
- Combat does not branch on operator name.
- Generic Gameplay must not use `if OperatorId == ...` to implement character mechanics.
- Character-specific skills, FX, animation and audio stay under `Gameplay/Characters`.
- FX and animation do not directly settle HP/damage.
- Runtime does not read external unpack directories.
- Gameplay does not depend on PRTS URLs, skel/atlas filenames or external tool paths.
- Do not rebuild a giant monolithic PlayerController.

## 3. Main runtime systems

### Combat

Key directory:

`Assets/_Game/Scripts/Combat`

Responsibilities:

- DamageContext / DamageSystem;
- physical / Arts / true damage;
- DEF / RES / penetration;
- CombatStats and layered modifiers;
- status and target-facing combat settlement.

The formal combat pipeline supersedes historical compatibility approximations.

### Characters

Key directory:

`Assets/_Game/Scripts/Gameplay/Characters`

Shared runtime:

- `PlayableOperatorDefinition`
- `PlayableOperatorIdentity`
- `OperatorRuntimeStats`
- `OperatorProgressionController`
- `OperatorSkillMasteryController`
- `OperatorRangeCatalog / OperatorRangeUtility`
- `PlayableOperatorAudioProfile`

Character-specific implementations remain separate.

### Roguelite / Extraction

Key directory:

`Assets/_Game/Scripts/Gameplay/Roguelite`

Major areas:

- Routing / Run State / Shell;
- Scavenging and backpack;
- Collectibles;
- progression and skill upgrades;
- city/world generation;
- facilities and exploration;
- warehouse/meta state.

## 4. Player switching boundary

The codebase still contains:

- `PlayerRuntimeContext`
- `PlayableOperatorSwitchController`
- `PrototypeOperatorSwitchInput`

These remain useful for prototype testing and old-scene compatibility.

**Formal release gameplay currently does not use in-run character switching.**

Do not design new production systems around TAB switching. Character switching support must not force shared systems to depend on individual operators.

## 5. Current playable operator scope

Current maintained roster:

- Chen
- Schwarz
- Skadi
- Wisadel
- FrostNova Winter / 冬痕 only

FrostNova legacy skin enum values may remain for serialized-scene compatibility, but runtime/editor selection converges on Winter.

## 6. State ownership

Run-only:

- Source Ingots;
- current run collectibles;
- unsecured backpack loot;
- run upgrades;
- current map exploration/facility state.

Meta/persistent:

- LMD;
- warehouse inventory;
- operator external progression state.

New run must reset all run-owned state and must not leak temporary modifiers from the previous operation.

## 7. Editor composition

Shared editor infrastructure:

- `PlayableOperatorPrototypeComposer`
- `PrototypeOperatorRegistry`
- per-operator builders
- local asset import infrastructure
- PRTS progression/mastery importers

A new operator should extend character-local gameplay/presentation and use shared composition rather than adding branches to Stage, HUD, Camera, Shop or Reward code.

## 8. Current validation tools

### P9 — numeric structure validator

Menu:

`ArknightsACT/角色调试/数值校验器`

Checks data completeness, Blackboard fields and suspicious official-number hardcodes.

### P10 — numeric runtime acceptance

Menu:

`ArknightsACT/角色调试/数值自动验收`

Compares official Definition/Mastery input against resolved runtime results.

Target:

`Fail 0`

### City validation

Editor validation remains under:

- `MobileCityGenerationValidation`
- `CityExplorationValidation`

## 9. Documentation source of truth

Read in this order:

1. `README.md`
2. this file
3. `SYSTEM_02_CHARACTERS_COMBAT_NUMERICS.md`
4. `SYSTEM_03_SCAVENGING_META_UI.md`
5. `SYSTEM_04_CITY_WORLD_EXPLORATION.md`
6. `SYSTEM_05_ASSET_IMPORT_PRESENTATION_AUDIO.md`
7. `CHARACTER_IMPORT_WORKFLOW.md` when importing a character

Dated handoffs marked OUT are historical only.

## 10. Validation boundary

FolderBridge `build/test` may be validation-only.

Authoritative acceptance remains:

- Unity C# compilation;
- EditMode tests;
- Play Mode behavior;
- actual visual/audio verification where applicable.
