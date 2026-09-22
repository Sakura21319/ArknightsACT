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
- J / Left Mouse: basic combo
- K / Shift: dash
- L: Ch'en Skill 1
- I / Right Mouse: Ch'en Skill 2
- E: extract the unsecured haul and enter the next stage after defeating the stage Boss
- F: open the nearby container window; its entries unseal one at a time while you stand still
- 1 / 2 / 3 / 4: pick up the identified entry in that slot
- T: take every identified entry that still fits
- B: open the field pack (unsecured haul + collectible archive)
- Tab: switch between the unsecured and archive tabs while the pack is open
- X: drop the most recently picked up entry while the unsecured tab is open
- Esc: close the container window or the field pack

## Run structure

The current Phase 07 exploration flow generates the physical stage at runtime:

```text
Stage 1: 2x2 = 4 blocks
    -> fixed Boss endpoint
Stage 2: 2x2 = 4 blocks
    -> fixed Boss endpoint
Stage 3: 2x2 = 4 blocks
    -> final Boss
```

Start is fixed at the bottom-left and Boss/exit at the top-right. Intermediate blocks are randomized between normal combat, emergency combat and a possible shop location. Physical chunk themes include open tactical ground, streets, cover lanes and exactly one walkable two-floor facility per stage.

Entering a block for the first time activates its content. Enemies persist if the player leaves, so encounters can spill across block boundaries. Treasure can also be rolled inside combat blocks.

The shop block currently has a physical safe-plaza location only. Purchase items and refresh UI are intentionally deferred until exploration pacing is validated.

## City exploration update (2026-09-20)

Each district is now 36 × 30 units. Foreground houses have walk-in interiors with actor-aware roof/wall fading. Nine searchable containers per stage draw from 22 Arknights-named collectibles adapted for this ACT. A four-slot unsecured bag is settled at stage exits; death loses the unsecured haul. Class-specific and future effects are explicitly labeled, and new entries can be added in `Assets/_Game/Resources/ScavengingCatalog.json`.

Search is a two-step loop modelled on extraction shooters. Pressing F beside a container opens a search window whose entries are unsealed one at a time by a circular sweep; each identified entry pops its placeholder out of the case and has to be picked up deliberately. Container flavour sets the entry count (residential/service/commercial 3, industrial/checkpoint 4), standing still is required, and moving or taking damage aborts the pass while keeping everything already identified. The unsecured bag and the collectible archive now live behind B instead of a permanent on-screen strip.

See [the search window handoff](Docs/SCAVENGING_SEARCH_UI_HANDOFF_2026_09_20.md) for this iteration, and [the city exploration handoff](Docs/CITY_EXPLORATION_HANDOFF_2026_09_20.md) for the district architecture, extension interfaces, validation and remaining playtest work.

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
