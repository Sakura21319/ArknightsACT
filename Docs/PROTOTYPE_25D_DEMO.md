# 2.5D Map / 2D Character Demo

This branch contains an isolated visual prototype for a Don't-Starve-like presentation direction.

## What it tests

- real 3D map geometry and collisions
- fixed oblique orthographic camera
- existing 2D Spine character presentation placed in 3D space
- camera-facing billboard characters
- free WASD / arrow-key movement on the XZ ground plane
- camera-relative controls
- CharacterController collision against 3D buildings, crates, barriers and map bounds

It does **not** replace the current horizontal ACT prototype or R3 roguelite scene.

## Build

After pulling the branch and waiting for Unity compilation:

1. run `ArknightsACT > Build 2.5D Demo Scene`
2. open `Assets/_Game/Scenes/Prototype25D.unity`
3. press Play
4. move with WASD or Arrow Keys

If the generated Ch'en / enemy Spine presentation prefabs are available, the demo uses them. Otherwise it falls back to flat placeholder cards.

## Scene contents

The generated demo contains:

- a 3D ground block
- intersecting road surfaces
- raised sidewalks
- multiple 3D building blocks
- crates, barriers and concrete medians
- invisible outer collision bounds
- Ch'en as the controllable 2D billboard actor
- Soldier, Hound and Crossbowman as static 2D billboard references at different world depths
- a fixed-angle orthographic follow camera

## Scope decision

This is intentionally a presentation experiment. The production `PrototypeRun.unity`, 2D combat physics, R3 route loop, rewards and collectibles are untouched.

If this visual direction feels right, the next step should be a second prototype that ports one basic attack and one enemy chase/attack interaction onto the XZ plane before changing the main combat architecture.
