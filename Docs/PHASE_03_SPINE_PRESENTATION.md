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

### 2.5 / 2.6 Texture quality

Original PRTS chibi atlas regions are relatively low-resolution. Mipmaps, platform compression and downscaling are disabled. Raw pages use Point filtering to avoid extra bilinear blur.

For a smoother local prototype, run:

```text
ArknightsACT
→ Assets
→ PRTS
→ 2.6 Build 2x Sharp Local Atlases
```

This tool keeps untouched `*.original` backups, creates a 2x bilinear-upscaled + mildly sharpened PNG, and scales the Spine atlas pixel coordinates (`size / xy / orig / offset / split / pad`) by the same factor. Generated HD pages then use Bilinear filtering for smoother sub-pixel movement.

This is reversible and local-only. Restore with:

```text
ArknightsACT
→ Assets
→ PRTS
→ 2.7 Restore Original Local Atlases
```

This does not create true new art detail. Production/public art should still use original/licensed high-resolution assets.

### 3. Generate / regenerate presentation prefabs

```text
ArknightsACT
→ Assets
→ PRTS
→ 3. Build Presentation Prefabs
```

Run this again after pulling presentation changes or after generating/restoring local HD atlases.

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

Repeated attack input does not restart `Attack_Loop`; gameplay damage pulses remain independent from Spine playback. Hit timing is owned by `AttackDefinition`, not animation events.

---

## Texas movement and weapon preservation

PRTS provides Texas as separate model groups:

```text
combat front: char_102_texas
base/dorm:    build_char_102_texas
```

The combat model has weapons but no Move clip. The base/dorm model has Move but does not render the combat weapons.

The prototype therefore does **not** swap rendered models while moving. `SpineBoneMotionRetarget2D` uses the hidden base model as a motion source and copies matching-bone deltas onto the visible combat skeleton. Attachments still come from the combat skeleton, so weapons stay visible.

Expected runtime log:

```text
[ArknightsACT/Spine] motion retarget ready: move='Move', matchedBones=..., coverage=...%
```

---

## Damage readability

`DamageTintFlash2D` gives player and enemies an Arknights-style red flash on damage. Spine runtimes expose tint differently, so the adapter supports both layouts:

```text
Skeleton.R / G / B / A
Skeleton.Color.R / G / B / A
```

Current flash duration is about `0.16s`, with a strong red tint. Enemies without a dedicated Hit clip also use a short generic punch/squash reaction.

---

## Jump tuning

The previous prototype only increased gravity while falling, causing a floaty ascent. Current Texas settings use strong gravity on both halves while compensating launch velocity:

```text
jumpVelocity          9.6
riseGravityMultiplier 2.0
fallGravityMultiplier 3.2
```

The target is roughly similar practical jump height with clearly shorter airtime.

---

## Enemy prototype combat

Soldier / Hound / Crossbowman use their persistent Move loops and Attack clips. Enemy attack windup and recovery lock horizontal movement. Damage interrupts the attack and allows knockback to remain visible.

Death immediately stops AI/collision/physics movement, plays Die, and delays destruction long enough to see the animation.

---

## Important architecture rule

Do not move gameplay rules into Spine animation events.

Correct:

```text
Gameplay controller
→ authoritative timing / damage
→ presentation event
→ Spine animation
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

PRTS binaries, locally generated HD derivatives and backups remain local-only.
