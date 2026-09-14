# Phase 03 — PRTS Spine Presentation

## Goal

Replace graybox-only presentation with real PRTS Spine battle models while keeping gameplay independent from Spine.

Dependency direction remains:

```text
Combat / Gameplay
      ↓ events
Presentation Driver
      ↓
SpineCharacterPresentation2D
      ↓ reflection
Spine.Unity.SkeletonAnimation
      ↓
PRTS local assets
```

Gameplay assemblies do **not** reference Spine assemblies at compile time.

---

## Why not use official Spine 3.8 runtime directly?

Arknights chibi battle models are Spine 3.8 binary assets.

The official Spine 3.8 Unity runtime only officially supports old Unity versions and is not suitable as a direct Unity 6 dependency. The current official 4.x runtime supports Unity 6 but does not directly load 3.8 binary skeletons.

For prototype evaluation, the project provides an **optional** installer for a pinned multi-version runtime fork:

`ZeroFlyFly/WaifuSpineRuntime`

Pinned commit:

`f569ce2e8f5cbe5aed2c6023d6569efc5abc44af`

The fork advertises binary import support for Spine 3.5–4.2, including Arknights Spine 3.8.

This is intentionally optional. Removing/replacing it does not require changing Combat, Skills, AI or Build code.

Review the Spine runtime license before distributing builds containing a Spine runtime.

---

## Local setup order

After switching to the Phase 3 branch, run these Unity menu commands in order.

### 1. Runtime

```text
ArknightsACT
→ Assets
→ PRTS
→ 1. Install Spine 3.8-Compatible Runtime
```

Wait for Package Manager and Unity compilation/domain reload to finish.

### 2. Download PRTS models

```text
ArknightsACT
→ Assets
→ PRTS
→ Download Full Prototype Pack
```

The downloaded third-party assets remain local and are ignored by Git.

### 3. Generate / regenerate presentation prefabs

```text
ArknightsACT
→ Assets
→ PRTS
→ 3. Build Presentation Prefabs
```

Run this again after pulling any Phase 3 presentation changes because generated PRTS prefabs are local-only and are not replaced by Git pull.

The builder:

1. locates imported `SkeletonDataAsset` objects through reflection;
2. creates `SkeletonAnimation` instances without a compile-time Spine dependency;
3. scans all real animation names contained in the skeleton;
4. resolves Idle / Move / Run / Attack / Combat / Skill / Hit / Die candidates;
5. measures the rendered model bounds;
6. scales the Spine visual child to a configured ACT world height;
7. centers the model horizontally and aligns its rendered lowest point to the gameplay foot line;
8. adds `SpineCharacterPresentation2D`;
9. writes local generated prefabs to:

```text
Assets/_Game/Generated/PRTS/Prefabs/
```

Generated prefabs are also ignored by Git because they reference local PRTS files.

Current target visual heights are approximately:

```text
Texas          1.62 world units
Soldier        1.50
Crossbowman    1.48
Hound          0.92
Originium Slug 0.72
Yokai Drone    1.05
Heavy Defender 1.72
```

Physics roots remain scale `1,1,1`; only the presentation child is scaled/flipped.

### 4. Validate

```text
ArknightsACT
→ Assets
→ PRTS
→ 4. Validate Presentation Setup
```

Expected result:

```text
Spine runtime: OK
PRTS source models: 7/7
Generated presentation prefabs: 7/7
```

### 5. Rebuild prototype scene

```text
ArknightsACT → Build Prototype Scene
```

`PrototypeFactory` automatically uses generated PRTS prefabs when present and falls back to graybox visuals otherwise.

The Phase 3 prototype camera is deliberately wider than the earlier combat lab and now includes a simple graybox environment backdrop so character scale can be judged against the room instead of against an empty screen.

---

## Current animation wiring

### Texas

Gameplay event | Presentation
---|---
Standing | Idle / Relax / Default / Stand
Horizontal movement | Move / Run / Walk
Attack 1–4 | available Attack / Combat / Atk animations
Sword Rain cast | Skill / Ability / Special
Receive damage | Hit / Hurt / Stun / Damage candidate if available
Death | Die / Death / Dead
Facing | presentation child X scale only

Animation names are resolved twice:

1. in the editor when the local prefab is generated;
2. again at runtime from the live `SkeletonData` before playback.

The second pass prevents a stale or imperfect editor mapping from leaving a character frozen in setup pose. The adapter also verifies a requested animation exists before calling `SetAnimation`.

The ACT hitbox timing is still controlled by `AttackDefinition`; Spine animation does **not** own damage frames.

### Enemies

Phase 3 initially wires:

- Idle
- Hit
- Die

When enemy AI is implemented, Move and Attack events will use the same presentation adapter.

---

## Important architecture rule

Do not move gameplay rules into Spine animation events.

Correct:

```text
PlayerAttackController
→ damage timing
→ AttackStarted presentation event
→ Spine attack animation
```

Incorrect:

```text
Spine animation event
→ decides game damage
```

Animation may later provide optional VFX/SFX timing markers, but authoritative combat timing stays in Gameplay/Combat.

---

## Local-only resources

Ignored by Git:

```text
Assets/_Game/Art/**/PRTS/
Assets/_Game/Generated/PRTS/
```

This prevents third-party PRTS binaries and generated references from entering the repository.
