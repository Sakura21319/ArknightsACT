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
- E: enter the next stage after defeating the stage Boss

## Run structure

The current Phase 07 exploration flow generates the physical stage at runtime:

```text
Stage 1: 2x2 = 4 blocks
    -> fixed Boss endpoint
Stage 2: 3x2 = 6 blocks
    -> fixed Boss endpoint
Stage 3: 3x3 = 9 blocks
    -> final Boss
```

Start is fixed at the bottom-left and Boss/exit at the top-right. Intermediate blocks are randomized between normal combat, emergency combat and a possible shop location. Physical chunk themes include open tactical ground, streets, cover lanes and exactly one walkable two-floor facility per stage.

Entering a block for the first time activates its content. Enemies persist if the player leaves, so encounters can spill across block boundaries. Treasure can also be rolled inside combat blocks.

The shop block currently has a physical safe-plaza location only. Purchase items and refresh UI are intentionally deferred until exploration pacing is validated.

## Progression

The run currently has three separate growth layers:

- EXP / level upgrades: generic combat growth such as all/physical/Arts damage, burn and chain lightning.
- Collectibles: long-run build modifiers and synergies.
- Character skill specializations: Ch'en-specific skill mutations from the monster chest reward chain.

Reward selection screens share a single coordinator so level-up, skill-specialization and collectible choices queue instead of overlapping.

## Treasure prototypes

The prototype uses locally downloaded PRTS presentation sources for:

- normal chest (`trap_065_normbox`),
- spike chest (`trap_066_rarebox`),
- Chest Seaborn / monster chest (`enemy_2035_sybox`).

Normal chest gives low-value resources. Spike chest reflects direct damage and grants a collectible choice. Monster chest initially looks like a normal chest; the first hit reveals the monster form, permanently activates pursuit and grants character skill specialization plus a collectible reward on defeat.

## Important development note

The legacy `PrototypeRoomLoopController` remains in the repository as a fallback implementation, but the current generated `PrototypeRun` no longer creates or starts the old isolated-room loop. Stage exploration is driven by `RogueliteStageMapController` + `RogueliteStageRuntimeController`.

## Validation

Repository changes are source/static edits until tested in a local Unity Editor. Rebuild the prototype scene after pulling changes that modify editor factories, generated scene composition or PRTS presentation setup.
