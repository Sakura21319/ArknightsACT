# Phase 05 — Roguelite R3 Route Layer

## Goal

Turn the Phase 04 endless combat-room loop into a small playable route loop without introducing multiple combat scenes yet.

Current flow:

```text
First normal combat
-> combat reward
-> choose next route node
-> execute combat or non-combat node
-> choose route again
-> after 4 non-boss combat clears, route converges to Boss
-> Boss reward
-> new route cycle
```

The existing combat arena, player, damage pipeline, collectible inventory, pause service, and room-clear action-settle rules are reused.

## Branch

`feat/phase-05-roguelite-r3-routing`

Base: `feat/phase-04-roguelite-r1-r2` at `a651ede`.

## Route nodes

### 普通作战 / Combat

- standard room tuning
- +2 Originium Ingots on clear
- 2-choice collectible reward, Common or better

### 紧急作战 / Emergency Combat

- +1 enemy
- enemy max HP x1.35 in addition to normal room progression
- ranged enemy pool enabled immediately
- +4 Originium Ingots on clear
- 3-choice collectible reward, Rare or better

### 不期而遇 / Encounter

Prototype event with two choices:

- search transport wreckage: +5 Ingots
- field preparation: heal 20% max HP

### 安全屋 / Safe House

Prototype rest node with two choices:

- full rest: heal 40% max HP
- organize supplies: heal 15% max HP and gain 4 Ingots

### 诡意行商 / Rogue Trader

Starts the run with 6 Ingots so the first trader can be meaningful.

Current offers:

- field medical kit: 3 Ingots, heal 30% max HP
- collectible crate: 6 Ingots, open a 3-choice Common-or-better collectible reward
- leave without purchase

This is intentionally a minimal economy. Hope / recruitment currency is not added because the current ACT prototype has one playable operator.

### 险路恶敌 / Boss

After 4 non-boss combat clears, the next route selection is forced to Boss.

Prototype Boss:

- one Heavy Defender template
- base Heavy Defender HP is 180 before room progression
- route tuning applies x4 HP
- +8 Ingots on clear
- 3-choice Rare-or-better collectible reward

The Heavy Defender is stored as the fourth hidden enemy template and is not included in normal room random selection.

## Architecture

### `RogueliteRouteController`

Owns route progression and immediate-mode prototype route/event/shop UI.

It does not spawn enemies itself. Combat nodes call the existing `PrototypeRoomLoopController` with a `CombatRoomTuning`.

### `CombatRoomTuning`

Generic room-level tuning currently supports:

- enemy count bonus
- enemy count override
- health multiplier
- early ranged enable
- forced enemy template index
- debug label

This keeps room spawning independent from Roguelite node names.

### `RogueliteRewardController`

Phase 04 reward UI was refactored into a reusable service.

The caller now supplies:

- title
- minimum rarity
- choice count
- completion callback

This allows combat nodes, Boss rewards, and Trader purchases to share one reward screen.

### `RogueliteRunState`

Currently owns only run-scoped values needed by R3:

- Originium Ingots
- route depth
- combat clears since last Boss

Do not add Hope or operator recruitment state until the game actually supports multiple recruitable operators.

## Input

Route, reward, event, and trader prototype UI use mouse or 1 / 2 / 3.

Route overlays apply a one-frame input lock whenever one overlay hands off to another. This prevents the key used to select a collectible from also selecting a route node in the same frame.

## Local rebuild required

R3 changes the generated scene composition and enemy template list.

After pulling the branch:

1. wait for Unity compilation
2. fix only the first red compiler error if any
3. run `ArknightsACT > Build Prototype Scene`
4. open / play `Assets/_Game/Scenes/PrototypeRun.unity`

## Validation checklist

### Base flow

- first room is a normal combat room
- clearing it waits for the player's attack / dash / skill to finish
- reward UI appears
- normal combat reward shows at most 2 choices
- choosing a collectible opens route selection instead of immediately spawning the next room
- 1 / 2 / 3 used on reward does not auto-select a route choice

### Route selection

- route screen shows route depth and current Originium Ingots
- normally shows 3 unique choices
- at least one choice is Combat or Emergency Combat
- selecting a combat node resumes gameplay and spawns the next room

### Emergency

- has one more enemy than the equivalent normal room, limited by spawn points
- enemy HP is visibly higher
- ranged enemies can appear before room 4
- clear grants 4 Ingots
- reward only contains Rare / Epic eligible collectibles

### Encounter

- +5 Ingots option works
- 20% max-HP recovery option works
- event completes without spawning a combat room
- route selection opens again

### Safe House

- 40% heal option works
- 15% heal + 4 Ingots option works
- route selection opens again

### Trader

- run begins with 6 Ingots
- medical kit costs 3 and heals 30%
- collectible crate costs 6 and opens reward UI
- insufficient funds keeps trader open and shows a message
- leave costs nothing

### Boss

- after 4 non-boss combat clears, route selection converges to a single Boss node
- Boss spawns one Heavy Defender / Heavy Defender placeholder
- Boss has much higher HP than normal enemies
- Boss clear grants 8 Ingots
- Boss reward is Rare / Epic only
- after Boss, normal route generation resumes

## Known prototype limitations

- route UI is IMGUI, not final presentation
- all combat nodes still reuse one arena
- Encounter has one prototype event set
- Trader inventory is fixed and has no refresh / discount system
- Boss AI currently reuses melee archetype behavior; only presentation / HP / room role differ
- route graph is generated choice-by-choice rather than rendered as a full floor map
- Unity Editor / PlayMode was not run by the assistant

## Recommended R4

After R3 is stable locally, the next useful layer is a real floor map / route graph plus more encounter content, not more raw collectible count.

Suggested R4 scope:

- pre-generated floor graph with visible connected nodes
- node history and current position
- 2-3 encounter event definitions
- trader stock generated from data
- first proper Boss behavior module
- run-end / death / victory summary
