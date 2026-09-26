# ArknightsACT

Arknights-inspired 2.5D ACT roguelite prototype built in Unity.

## Current playable prototype

Generate the scene from Unity:

```text
ArknightsACT > Build Prototype Scene
```

Then open:

```text
Assets/_Game/Scenes/PrototypeRun.unity
```

The current prototype uses a low-oblique orthographic 2.5D presentation with a true 3D XZ world and camera-facing Spine characters.

### Controls

- WASD / arrow keys: move
- Space: jump
- J / Left Mouse: basic attack
- K / Shift: dash
- L: Skill slot 1
- I / Right Mouse: Skill slot 2
- Tab: open/close the in-run operator switch panel; 1-9 select a registered operator
- E: interact with the extraction point or an unlocked next-stage entrance
- F: search a nearby container / pick up a world salvage item
- G (hold): use the nearest city facility
- B: open the field backpack
- U: upgrade backpack capacity while the backpack is open
- R: auto-arrange backpack items while the backpack is open
- 1-9: take an identified container entry when the search window is open
- T: take all identified entries that still fit
- M: expand/collapse the exploration map
- N: cycle exploration / return / next-stage guidance
- Esc: close the active modal UI or operator switch panel

## Run structure

The current mobile-city generator builds one continuous seeded district per stage:

```text
Stage 1: 4x3 sectors
Stage 2: 4x4 sectors
Stage 3: 5x4 sectors
```

The safe start/extraction area remains in the south-west and the Boss/next-stage direction remains toward the north-east. The map is continuous and backtrackable rather than a sequence of isolated rooms. Encounters, buildings, searchable containers, city facilities, extraction and stage progression are assembled by the current Roguelite stage runtime.

The city is divided into outskirts, core, ruins and industrial roles. Current landmark content includes the civic emergency-command complex and the second-stage power-dispatch tower, plus medical, survey, power and risk/reward facilities.

## City exploration update (2026-09-23)

The current runtime uses the curated 118-item scavenging database and a real 2D backpack starting at 4×5 cells. Search buildings are selected from 12 building profiles and use 28 container types across five loot tiers. Acquired collectibles apply during the current run; ordinary extracted goods can be secured into the persistent warehouse only through a real extraction.

Exploration tracks entered, searched, uncleared and empty buildings without revealing unknown loot. The minimap supports fog-of-war, district coloring, an expanded view and navigation guidance. City generation, container distribution and the current landmark/facility layer are documented in the current Docs index.

Start with [Docs/README.md](Docs/README.md) and the [current progress and roadmap](Docs/PROJECT_STATUS_AND_ROADMAP_2026_09_24.md). Use the detailed handoff and focused technical notes linked from the index when you need implementation context.

## Progression

The run currently has three separate growth layers:

- EXP / level upgrades: generic combat growth such as all/physical/Arts damage, burn and chain lightning.
- Collectibles: long-run build modifiers and synergies.
- Character skill specializations: Ch'en-specific skill mutations from the monster chest reward chain.

Reward selection screens share a single coordinator so level-up, skill-specialization and collectible choices queue instead of overlapping.

## Treasure prototypes

The prototype uses locally downloaded PRTS presentation sources for normal chest (`trap_065_normbox`), spike chest (`trap_066_rarebox`), and Chest Seaborn / monster chest (`enemy_2035_sybox`).

Normal chest gives low-value resources. Spike chest reflects direct damage and grants a collectible choice. Monster chest initially looks like a normal chest; the first hit reveals the monster form, permanently activates pursuit and grants character skill specialization plus a collectible reward on defeat.

## Important development note

The legacy `PrototypeRoomLoopController` remains in the repository as a fallback implementation, but the current generated `PrototypeRun` no longer creates or starts the old isolated-room loop. Stage exploration is driven by `RogueliteStageMapController` + `RogueliteStageRuntimeController`.

## Validation

Repository changes are source/static edits until tested in a local Unity Editor. Rebuild the prototype scene after pulling changes that modify editor factories, generated scene composition or PRTS presentation setup.

Two ways to run the exploration smoke test:

- `ArknightsACT > Validate City Exploration` — interactive, for when an Editor already holds the project. Runs against the currently open `PrototypeRun.unity`, exits Play Mode when done, and reports to the Console and `Logs/CityExploration/result.txt`.
- `-executeMethod ArknightsACT.Editor.CityExplorationValidation.Run` — batch, opens the scene itself and exits the process.

While a Unity Editor already holds the project lock, a batch Unity run cannot start at all. Use `Tools/compile_check.py` for a source-level check first: it reuses the response files Unity wrote under `Library/Bee/artifacts/` (same references and defines) and rebuilds `Game.Gameplay` and `Game.Editor` with Unity's bundled Roslyn, so new files are compiled before the editor refreshes.

## Extracted effect workflow

Extract effects with `D:\Effect\EffectExtractor.exe`, then import and apply the generated PNG frames from Unity:

```text
ArknightsACT > Assets > Import Extracted Frame FX
```

The importer creates Sprite animations and Prefabs under `Assets/_Game/Art/FX/Extracted`. For the current Ch'en prototype, use “导入并应用到当前陈原型”; the generated controller listens to the existing skill events and uses `CustomFxMountPoint`. Details are documented in `Docs/EXTRACTED_FX_PIPELINE.md`.
