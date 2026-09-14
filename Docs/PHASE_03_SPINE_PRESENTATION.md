# Phase 03 — PRTS Spine Presentation

## Goal

Replace graybox presentation with local PRTS Spine battle models while keeping gameplay independent from a concrete Spine runtime.

```text
Combat / Gameplay
      ↓ events / interfaces
Presentation adapters
      ↓ reflection
Spine runtime
      ↓
PRTS local-only assets
```

## Current runtime

The prototype uses the pinned multi-version-compatible Spine runtime already recorded in `Packages/manifest.json` and `Packages/packages-lock.json`.

PRTS binaries, generated prefabs and generated prototype scenes remain local-only and are ignored by Git.

## Local setup

```text
ArknightsACT > Assets > PRTS > Download Full Prototype Pack
ArknightsACT > Assets > PRTS > 2.5 Apply High Quality Texture Settings
ArknightsACT > Assets > PRTS > 3. Build Presentation Prefabs
ArknightsACT > Assets > PRTS > 4. Validate Presentation Setup
ArknightsACT > Build Prototype Scene
```

Texas also needs the base/dorm motion source for walking retargeting.

## Texas locomotion

The battle model keeps the combat attachments/weapons. The base model is hidden and supplies only `Move` bone deltas through `SpineBoneMotionRetarget2D`.

This avoids swapping to a weaponless base model during movement.

## Texas basic attack: one visible swing = one gameplay attack

The battle skeleton contains:

```text
Attack_Start
Attack_Loop
Attack_End
```

They are phases of one attack state, not different combo attacks.

The prototype now uses a single authoritative rule:

```text
one complete Attack_Loop swing
= one gameplay attack cycle
= at most one damage pulse
```

`SpineAttackPlaybackSpeed2D` implements `IAttackTimingProvider` and reads the real `Attack_Loop` duration from Spine at runtime.

Default prototype attack animation speed:

```text
2.0x
```

The runtime attack cycle is:

```text
cycleSeconds = Attack_Loop duration / playbackSpeed
impactSeconds = cycleSeconds * impactNormalizedTime
```

Current impact marker starts at about 58% of the visible swing and is intentionally data-driven rather than a fixed `0.095s` guess. The runtime prints:

```text
[ArknightsACT/AttackTiming] Spine cycle=... raw, speed=2x, impact=58%.
```

Repeated J input uses a one-slot queue. Mashing cannot create hidden extra hits while the current sword animation is still playing; it only requests the next complete swing.

Future AttackSpeed upgrades must scale both the visible Spine playback and the gameplay cadence together.

## Damage feedback

`DamageTintFlash2D` tints both players and enemies red on `CombatEntity.Damaged`.

It supports Spine runtimes exposing either:

```text
Skeleton.R/G/B/A
Skeleton.Color.R/G/B/A
```

Base color is captured once. Rapid repeated hits extend the red window but never re-capture red as the new base color, fixing the permanent-red bug.

Enemies without a dedicated Hit clip also use a short physical fallback reaction.

## Death state

Death is higher priority than movement, dash, attack and skill input.

Player movement/dash/attack/skill controllers check `Health.IsDead`; presentation stops locomotion updates and keeps `Die` active.

Enemies stop AI and physics control during death. `DamageSystem` does not apply a later knockback impulse after a hit has already become lethal, preventing corpses from sliding during `Die`.

## Enemy prototype behavior

- Soldier: chase + melee Attack.
- Hound: faster chase + fast melee Attack.
- Crossbowman: spacing + ranged Attack prototype.
- Attack windup/recovery locks horizontal movement.
- Damage interrupts an attack and exposes knockback/stagger.

## Texture/render quality investigation

The PRTS web viewer can display the same source clearly, so the project no longer assumes the source atlas itself is the only problem.

Current import settings mirror a conventional web Spine path:

```text
Filter          Bilinear
MipMap          OFF
Compression     Uncompressed
Max Texture     8192
NPOT Scale      None
Wrap            Clamp
```

The prototype player is now windowed `1600x900` by default and the camera uses orthographic size `3.40`, giving a 1.6-unit chibi materially more screen pixels than the previous `1280x720 / 4.25` setup.

`PresentationQualityDiagnostics2D` logs the real runtime values after Spine layout settles:

```text
[ArknightsACT/PresentationQuality]
Screen=...
Display=...
CameraPixels=...
Character≈...px high
Texture '...' WIDTHxHEIGHT, filter=...
```

If Unity is still visibly softer than the PRTS viewer, use this log before changing atlas data again.

### Local 2x atlas experiment

`2.6 Build 2x Sharp Local Atlases` remains available as a reversible experiment, but it is no longer the default recommendation. If it was previously applied and you want to compare the raw PRTS path again, run:

```text
ArknightsACT > Assets > PRTS > 2.7 Restore Original Local Atlases
```

Then re-run `2.5`, rebuild PRTS prefabs, and rebuild the prototype scene.

## Architecture rule

Spine does not own damage logic. Gameplay owns the authoritative attack and asks an optional timing provider for the visible animation's cycle length/impact phase. This keeps combat testable and allows the final art/runtime to be replaced later.
