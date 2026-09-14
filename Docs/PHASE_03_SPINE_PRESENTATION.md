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

The prototype uses the pinned multi-version-compatible Spine runtime recorded in `Packages/manifest.json` and `Packages/packages-lock.json`.

PRTS binaries, generated prefabs and generated prototype scenes remain local-only and are ignored by Git.

## Local setup

If the earlier experimental 2x atlas step was used, restore the raw local PRTS files first:

```text
ArknightsACT > Assets > PRTS > 2.7 Restore Original Local Atlases
```

Then use the normal path:

```text
ArknightsACT > Assets > PRTS > 2.5 Apply High Quality Texture Settings
ArknightsACT > Assets > PRTS > 3. Build Presentation Prefabs
ArknightsACT > Assets > PRTS > 4. Validate Presentation Setup
ArknightsACT > Build Prototype Scene
```

Texas also needs the base/dorm motion source for walking retargeting.

## Texas locomotion

The battle model keeps combat attachments/weapons. The base model is hidden and supplies only `Move` bone deltas through `SpineBoneMotionRetarget2D`.

## Texas basic attack: one visible swing = one gameplay attack

The battle skeleton contains:

```text
Attack_Start
Attack_Loop
Attack_End
```

They are phases of one attack state, not different combo attacks.

The authoritative prototype rule is:

```text
one complete Attack_Loop swing
= one gameplay attack cycle
= at most one damage pulse
```

`SpineAttackPlaybackSpeed2D` implements `IAttackTimingProvider` and reads the real `Attack_Loop` duration from Spine at runtime.

Default attack animation speed:

```text
2.0x
```

Runtime timing:

```text
cycleSeconds  = Attack_Loop duration / playbackSpeed
impactSeconds = cycleSeconds * impactNormalizedTime
```

The current impact marker starts at about 58% of the visible swing. This replaces the old fixed `0.095s` guess and keeps impact phase proportional if attack speed changes.

Expected diagnostic:

```text
[ArknightsACT/AttackTiming] Spine cycle=... raw, speed=2x, impact=58%.
```

Repeated J input uses a one-slot queue. Mashing cannot create invisible extra hits while the current sword animation is still playing; it only requests the next complete swing.

Future AttackSpeed upgrades must scale visible Spine playback and gameplay cadence together.

## Damage feedback

`DamageTintFlash2D` tints both players and enemies red on `CombatEntity.Damaged`.

It supports:

```text
Skeleton.R/G/B/A
Skeleton.Color.R/G/B/A
```

Base color is captured once. Rapid repeated hits extend the red window but never re-capture red as the new base color, fixing the permanent-red bug.

Enemies without a dedicated Hit clip also use a short physical fallback reaction.

## Death state

Death is higher priority than movement, dash, attack and skill input.

Player movement/dash/attack/skill controllers check `Health.IsDead`; presentation stops locomotion updates and keeps `Die` active.

Enemy presentation/AI locks on death. `DamageSystem` does not apply a knockback impulse after a hit has already become lethal, preventing corpses from sliding while `Die` plays.

## Enemy prototype behavior

- Soldier: chase + melee Attack.
- Hound: faster chase + fast melee Attack.
- Crossbowman: spacing + ranged Attack prototype.
- Attack windup/recovery locks horizontal movement.
- Damage interrupts attacks and exposes knockback/stagger.

## Texture/render quality investigation

Because the PRTS web viewer renders the same source clearly, the project no longer assumes the source atlas is the root problem.

Current raw-atlas import settings:

```text
Filter          Bilinear
MipMap          OFF
Compression     Uncompressed
Max Texture     8192
NPOT Scale      None
Wrap            Clamp
```

The prototype is now windowed `1600x900` by default and the camera uses orthographic size `3.40`, increasing the number of actual screen pixels devoted to each chibi compared with the old `1280x720 / 4.25` setup.

`PresentationQualityDiagnostics2D` logs the runtime truth after layout settles:

```text
[ArknightsACT/PresentationQuality]
Screen=...
Display=...
CameraPixels=...
Character≈...px high
Texture '...' WIDTHxHEIGHT, filter=...
```

Use these values before changing atlas data again. The local `2.6 Build 2x Sharp Local Atlases` tool remains available only as a reversible experiment, not the default path.

## Architecture rule

Spine does not own damage logic. Gameplay owns the authoritative attack and asks an optional timing provider for the visible animation cycle length and impact phase. This keeps combat testable and allows final art/runtime replacement later.
