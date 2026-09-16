# Phase 07 — Exploration Progression Prototype

## Goal

Move the run loop from isolated combat rooms to explorable 2.5D regions while keeping the Phase 06 combat/presentation stack intact.

## Run flow

```text
Stage 1: 2x2 = 4 blocks
    -> Boss at fixed top-right endpoint
    -> next-stage entrance
Stage 2: 3x2 = 6 blocks
    -> Boss
    -> next-stage entrance
Stage 3: 3x3 = 9 blocks
    -> final Boss
    -> run complete
```

Start is fixed bottom-left. Intermediate blocks are cardinally connected and may be explored in any order.

## Block types

- Start
- Combat
- Emergency Combat
- Shop
- Boss

Shop position is randomized. Emergency-combat frequency increases by stage.

## Physical chunk themes

Runtime chunks are 14 x 11 world units. Current themes:

- Open tactical ground
- Street
- Cover lane
- Two-floor facility
- Safe plaza / shop
- Boss arena

Exactly one non-shop block per stage is promoted to the two-floor facility theme when possible. The other chunks remain low-profile to preserve readability and the earlier constraint of not filling the map with repeated buildings.

## Exploration behavior

A block activates its contents the first time the player enters it. Activated enemies persist if the player leaves. The player does not need to clear every block before reaching the Boss.

This intentionally allows avoiding ordinary patrols, pulling encounters across block boundaries, taking optional treasure risk, carrying an activated monster chest pursuer into another block, and rushing the Boss with fewer upgrades or exploring for more growth first.

## Encounter rewards

- Normal combat: +2 Ingots, 20% Common+ collectible 2-choice.
- Emergency combat: +4 Ingots, 1.5x enemy EXP, guaranteed Rare+ collectible 2-choice.
- Boss: +8 Ingots, Rare+ collectible 3-choice.

Reward selection is serialized through `RewardSelectionCoordinator` so it cannot overlap level-up or character-skill specialization screens.

## Treasure

Treasure is rolled inside combat blocks rather than consuming a whole block:

- Normal chest: low-value currency / healing / temporary damage buff.
- Spike chest: reflects direct damage and grants collectible 2-choice.
- Monster chest: appears as normal chest, transforms after first hit, permanently pursues the player, and grants Ch'en skill specialization followed by collectible choice.

## Navigation

Each generated stage creates one `PrototypeNavigationGraph25D` spanning all block centers and block entrances. The facility adds extra ramp / second-floor nodes. Normal enemies and activated monster chests therefore share the same cross-block and multi-floor waypoint graph.

## Stage transition

Boss reward completion enables the exit marker in the Boss block. On stages 1 and 2, approach it and press `E` to generate the next stage while preserving player run progression. Stage 3 completes the prototype run.

## Current shop state

The Shop block is physically generated as a safe plaza with a shop counter and randomized map location. Purchase items and refresh behavior are intentionally deferred until the exploration pacing is validated; the current UI labels this as a prototype shop area rather than pretending the economy is finished.

## Local validation checklist

1. Rebuild `PrototypeRun` with `ArknightsACT > Build Prototype Scene`.
2. Confirm Stage 1 physically contains four connected chunks.
3. Cross a chunk boundary and confirm enemies/treasure spawn only on first entry.
4. Leave an activated enemy block and confirm enemies persist rather than despawn/reset.
5. Locate the single two-floor facility and confirm player + enemies can use its ramp.
6. Reach the top-right Boss block without clearing every optional block.
7. Defeat Boss, resolve queued rewards, then confirm the exit marker enables.
8. Press `E` near the exit and confirm Stage 2 rebuilds as 3x2 while EXP/upgrades/collectibles persist.
9. Repeat for Stage 3 (3x3) and final Boss.

## Validation status

Source/static inspection only. Unity Editor compilation and PlayMode behavior still require local validation.
