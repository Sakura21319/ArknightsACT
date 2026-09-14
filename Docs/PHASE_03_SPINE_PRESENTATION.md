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

## Runtime

Arknights operator battle assets are legacy Spine data. The prototype uses a pinned multi-version-compatible runtime fork:

`ZeroFlyFly/WaifuSpineRuntime`

Pinned commit:

`f569ce2e8f5cbe5aed2c6023d6569efc5abc44af`

`Packages/manifest.json` and `Packages/packages-lock.json` are committed so another checkout can restore the same dependency.

Review Spine runtime licensing before distributing builds that contain a Spine runtime.

---

## Local setup

After switching to the Phase 3 branch:

### 1. Runtime

```text
ArknightsACT
→ Assets
→ PRTS
→ 1. Install Spine 3.8-Compatible Runtime
```

If the package is already restored through `manifest.json`, this is not needed again.

### 2. Download PRTS models

```text
ArknightsACT
→ Assets
→ PRTS
→ Download Full Prototype Pack
```

The current full pack contains 8 local-only skeleton sources:

- Texas combat model
- Texas base/dorm model used only as a Move motion source
- Originium Slug
- Soldier
- Crossbowman
- Hound
- Yokai Drone
- Heavy Defender

If the original 7-model pack was already downloaded, only run:

```text
ArknightsACT
→ Assets
→ PRTS
→ Download Texas Base Motion Source
```

PRTS binary assets remain local and are ignored by Git.

### 3. Generate / regenerate presentation prefabs

```text
ArknightsACT
→ Assets
→ PRTS
→ 3. Build Presentation Prefabs
```

Run this again after pulling presentation changes because generated PRTS prefabs are local-only.

The builder:

1. locates imported `SkeletonDataAsset` objects through reflection;
2. creates `SkeletonAnimation` instances without a compile-time Spine dependency;
3. scans the real animation list;
4. resolves Idle / Move / Attack / Skill / Hit / Die candidates;
5. starts from a conservative visible scale;
6. lets `SpineVisualAutoLayout2D` perform bounded runtime correction after meshes exist;
7. writes local generated prefabs to `Assets/_Game/Generated/PRTS/Prefabs/`.

### 4. Validate

```text
ArknightsACT
→ Assets
→ PRTS
→ 4. Validate Presentation Setup
```

Expected current result:

```text
Spine runtime: OK
PRTS source models: 8/8
Generated presentation prefabs: 8/8
```

### 5. Rebuild prototype scene

```text
ArknightsACT → Build Prototype Scene
```

`PrototypeRun.unity` is generated locally and is ignored by Git, because it can reference local-only generated PRTS prefabs.

---

## Texas attack presentation

The combat skeleton contains:

```text
Attack_Start
Attack_Loop
Attack_End
```

These are phases of one attack state, not three different combo attacks.

The authoritative gameplay attack is one repeatable `Texas_Basic`. Current prototype timing is intentionally faster than the earlier build:

```text
startup  0.045 s
active   0.035 s
recovery 0.105 s
```

The Spine playback is now decoupled from damage pulses:

```text
first J
→ start/enter attack presentation
→ Attack_Loop keeps running

more J presses
→ refresh attack-chain grace
→ DO NOT restart Attack_Loop
→ gameplay continues producing individual hits

stop attacking
→ Attack_End
→ Idle
```

This prevents rapid J input from repeatedly resetting the animation to its first one or two frames.

Hit timing remains owned by `AttackDefinition`; Spine does not decide authoritative damage frames.

---

## Texas movement and weapon preservation

PRTS provides Texas as separate model groups:

```text
combat front: char_102_texas
base/dorm:    build_char_102_texas
```

The combat model has weapons but no Move clip. The base/dorm model has Move but does not render the combat weapons.

The prototype therefore does **not** swap rendered models while moving.

Instead:

1. the visible object is always the combat skeleton;
2. `build_char_102_texas` is instantiated as a hidden motion source;
3. the source plays `Move`;
4. `SpineBoneMotionRetarget2D` matches bones by name;
5. it copies animation deltas relative to each skeleton's own setup pose;
6. slots/attachments still come from the combat skeleton, so weapons stay visible.

The retargeter requires at least 60% source-bone coverage. If compatibility is too low or the source asset is missing, it automatically falls back to the existing combat-Idle + procedural bob/lean locomotion rather than breaking the character.

Expected runtime log when it works:

```text
[ArknightsACT/Spine] motion retarget ready: move='Move', matchedBones=..., coverage=...%
```

If compatibility is insufficient, the Console reports the coverage and the fallback remains active.

---

## Enemy animation mapping

Persistent locomotion prefers loop clips and avoids transition clips:

```text
Move
Move_Loop
Run_Loop
Walk_Loop
```

Clips such as the following are not used as persistent movement:

```text
Move_Begin
Move_End
Move_Up
Move_Down
Run_Begin
Run_End
```

This is why Soldier/Hound/Crossbowman should now bind to their loop locomotion instead of a transition pose.

---

## Important architecture rule

Do not move gameplay rules into Spine animation events.

Correct:

```text
PlayerAttackController
→ authoritative timing / damage
→ presentation event
→ Spine animation
```

Incorrect:

```text
Spine animation event
→ decides game damage
```

Animation may later provide optional VFX/SFX markers, but gameplay remains authoritative.

---

## Local-only resources

Ignored by Git:

```text
Assets/_Game/Art/**/PRTS/
Assets/_Game/Generated/PRTS/
Assets/_Game/Scenes/PrototypeRun.unity
```

This prevents third-party PRTS binaries and generated references from entering the repository.
