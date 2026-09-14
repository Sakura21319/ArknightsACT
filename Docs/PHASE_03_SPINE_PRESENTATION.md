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

## Spine runtime

Arknights chibi battle models are Spine 3.8 binary assets.

The project currently pins the optional multi-version runtime fork:

`ZeroFlyFly/WaifuSpineRuntime`

Pinned commit:

`f569ce2e8f5cbe5aed2c6023d6569efc5abc44af`

The dependency is recorded in `Packages/manifest.json` and `Packages/packages-lock.json` so another checkout can restore the same runtime revision.

Review Spine runtime licensing before distributing builds containing a Spine runtime.

---

## Local setup order

After switching to the Phase 3 branch:

```text
1. ArknightsACT > Assets > PRTS > Download Full Prototype Pack
2. ArknightsACT > Assets > PRTS > 3. Build Presentation Prefabs
3. ArknightsACT > Assets > PRTS > 4. Validate Presentation Setup
4. ArknightsACT > Build Prototype Scene
```

If the Spine runtime is not already restored through Package Manager, run:

```text
ArknightsACT > Assets > PRTS > 1. Install Spine 3.8-Compatible Runtime
```

Generated PRTS prefabs remain local-only. Re-run `3. Build Presentation Prefabs` after pulling presentation changes.

---

## Visual scale and alignment

PRTS raw Spine scale is not trusted as gameplay world scale.

Generated presentation prefabs start from conservative known-safe scales. During Play, `SpineVisualAutoLayout2D` waits for the Spine mesh to become valid, measures runtime renderer bounds and only applies a bounded correction. Invalid or extreme measurements keep the safe scale instead of hiding the character.

Target visual heights are approximately:

```text
Texas          1.62 world units
Soldier        1.50
Crossbowman    1.48
Hound          0.92
Originium Slug 0.72
Yokai Drone    1.05
Heavy Defender 1.72
```

Physics roots remain scale `1,1,1`; only the presentation child is scaled or flipped.

---

## Animation policy

### Texas battle model

Observed battle-model animation set:

```text
Attack_Start
Attack_Loop
Attack_End
Default
Die
Idle
Skill
Start
```

`Attack_Start / Attack_Loop / Attack_End` are phases of **one attack state**. They are not treated as three combo attacks.

The ACT prototype therefore uses:

```text
Basic attack gameplay definition: Texas_Basic
Basic attack Spine animation: Attack_Loop
Repeated presses: repeat Texas_Basic
Attack streak / proc counters: tracked independently from animation variety
```

This keeps Swift Blade and future effects such as "every N attacks" without inventing unsupported four-hit character animation.

### Texas movement

The combat model does not contain a weapon-preserving Move clip. The Base/Dorm model contains movement animation, but switching to that model removes the combat weapon and can introduce skeleton/attachment mismatches.

For Phase 3:

```text
Moving Texas
→ keep combat model
→ keep Idle animation and combat weapons
→ add subtle procedural bob/tilt as a locomotion cue
```

Do **not** swap the whole character to the Base/Dorm model during combat movement.

A later improvement can test bone-compatible animation retargeting from the Base/Dorm Move clip onto the combat skeleton. That should only be enabled after skeleton/bone compatibility is verified.

### Enemy locomotion

Persistent locomotion resolution now prioritizes:

```text
Move
Move_Loop
Run_Loop
Walk_Loop
```

Transition/directional clips such as these are not selected as persistent locomotion:

```text
Move_Begin
Move_End
Move_Up
Move_Down
Run_Begin
Run_End
```

### Current Texas wiring

Gameplay event | Presentation
---|---
Standing | Idle (Default only as fallback)
Horizontal movement | persistent Move/Run/Walk if available; otherwise combat Idle + procedural motion
Basic attack | one repeatable attack clip, preferring Attack_Loop, then Attack/Combat
Sword Rain cast | Skill / Ability / Special
Receive damage | Hit / Hurt / Stun / Damage candidate if available
Death | Die / Death / Dead
Facing | presentation child X scale only

Animation names are resolved twice: during local prefab generation and again at runtime from live `SkeletonData`.

Authoritative hit timing remains in `AttackDefinition`; Spine animation does **not** decide damage frames.

---

## Architecture rule

Correct:

```text
PlayerAttackController
→ authoritative damage timing
→ presentation event
→ Spine animation
```

Incorrect:

```text
Spine animation event
→ authoritative gameplay damage
```

Animation events may later drive optional VFX/SFX markers, but gameplay authority stays in Gameplay/Combat.

---

## Local-only resources

Ignored by Git:

```text
Assets/_Game/Art/**/PRTS/
Assets/_Game/Generated/PRTS/
Assets/_Game/Scenes/PrototypeRun.unity
```

The generated scene and PRTS binary/generated presentation files are reproducible local outputs, not source assets.
