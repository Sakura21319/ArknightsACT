# Phase 07 — Exploration Progression Prototype

The active run is now a runtime-generated 2.5D exploration region rather than the legacy isolated-room loop.

## Run flow

```text
Stage 1: 2x2 = 4 blocks -> Boss -> next-stage entrance
Stage 2: 3x2 = 6 blocks -> Boss -> next-stage entrance
Stage 3: 3x3 = 9 blocks -> final Boss -> run complete
```

Start is fixed bottom-left and Boss/exit top-right. Intermediate blocks are cardinally connected and may be explored in any order.

## Block types and themes

Types: Start, Combat, Emergency Combat, Shop, Boss.

Runtime chunk themes are Open, Street, Cover Lane, Two-floor Facility, Safe Plaza and Boss Arena. Chunks are 14 x 11 world units. Exactly one non-shop block per stage is promoted to the two-floor facility theme when possible.

## Exploration behavior

A block activates its contents on first entry. Activated enemies persist when the player leaves, so encounters can cross block boundaries. The player does not need to clear every block before reaching the Boss.

Normal / spike / monster chests are rolled as block-internal points of interest. Monster chests can continue their permanent pursuit across the shared generated navigation graph.

## Rewards

- Normal combat: +2 Ingots and 20% Common+ collectible 2-choice.
- Emergency combat: +4 Ingots, 1.5x enemy EXP, guaranteed Rare+ collectible 2-choice.
- Boss: +8 Ingots and Rare+ collectible 3-choice.

Reward selection is serialized through `RewardSelectionCoordinator` so level-up, skill specialization and collectible screens do not overlap.

## Stage transition

Boss reward completion activates an exit marker. On stages 1 and 2, approach it and press `E` to generate the next stage while preserving run progression. Stage 3 ends the prototype run.

## Shop

The Shop block currently has a physical safe-plaza location and randomized position. Purchase inventory and refresh behavior are intentionally deferred until exploration pacing is validated.

## Local validation

1. Rebuild with `ArknightsACT > Build Prototype Scene`.
2. Confirm Stage 1 physically has four connected chunks.
3. Cross boundaries and verify enemies/treasure activate once and persist.
4. Find the single two-floor facility and test player/enemy ramp traversal.
5. Reach Boss without clearing every optional block.
6. Resolve Boss reward, approach exit and press `E`.
7. Confirm Stage 2 becomes 3x2 and Stage 3 becomes 3x3 while upgrades/collectibles/EXP persist.

Repository changes remain source/static edits until validated in the local Unity Editor.
