# 2.5D Map / 2D Character Combat Demo

This branch contains an isolated Don't-Starve-like 2.5D prototype.

## What it tests

- real 3D map geometry and collisions
- fixed lower oblique orthographic camera
- existing 2D Spine characters placed in 3D space as billboards
- free camera-relative movement on the XZ plane
- Space jump using CharacterController vertical velocity
- simple forward basic attack with J / left mouse
- simple enemy chase and melee attack
- enemy acquisition only inside a front-facing XZ vision cone
- buildings / props can block line of sight
- visible vision-cone outlines: yellow idle, red aggro

It does **not** replace the current horizontal ACT prototype or R3 roguelite scene.

## Build

After pulling the branch and waiting for Unity compilation:

1. run `ArknightsACT > Build 2.5D Demo Scene`
2. open `Assets/_Game/Scenes/Prototype25D.unity`
3. press Play

## Controls

- WASD / Arrow Keys: move
- Space: jump
- J / Left Mouse: basic attack

## Combat rules

Player basic attack is intentionally minimal: a short-range forward sector on the XZ plane dealing Physical damage.

Enemies begin idle. They do not acquire Ch'en from behind or from the side. Initial acquisition requires all of the following:

- target is within view distance
- target is inside the enemy's front-facing vision angle
- no 3D building / crate / barrier blocks line of sight

After acquisition the enemy chases and uses a simple melee hit at close range. If sight is lost long enough, it drops aggro.

Current demo enemies:

- Soldier
- Hound
- Crossbowman

They intentionally share the same simple melee prototype brain for now; the goal is to evaluate 2.5D movement, combat spacing and field-of-view behavior before porting the production enemy archetypes.

## Scene contents

The generated demo contains textured 3D ground, roads, sidewalks, buildings, crates, barriers, medians and invisible map bounds. Ch'en and enemies use existing Spine presentation prefabs when available, with flat fallback cards otherwise.

## Scope decision

The production `PrototypeRun.unity`, 2D combat physics, R3 route loop, rewards and collectibles remain untouched. If this demo feels good, the next step is to decide whether the production combat plane should migrate from XY/Physics2D to XZ/3D collision while keeping CombatEntity, DamageSystem and roguelite systems intact.
