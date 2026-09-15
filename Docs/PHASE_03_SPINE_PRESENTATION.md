# Phase 03 — Ch'en PRTS Spine Presentation

## Current direction

The active prototype is a horizontal 2D ACT with Ch'en as the player character.

Abandoned experiments have been removed from the active branch:

- Texas-specific build / sword-rain code
- procedural Texas action animation
- survivor prototype
- top-down prototype
- auto melee
- extra basic-attack slash VFX
- HD atlas upscale experiment

## PRTS animation catalog

The local `char_010_chen` combat skeleton currently exposes:

```text
Attack        1.5000s
Attack_End    0.6000s
Attack_Pre    0.6000s
Default       0
Die           1.0000s
Idle          2.0000s
Skill         1.0000s
Skill_2       1.1667s
Skill_3       2.9333s
Skill_End     0.6000s
Skill_End_2   0.6000s
Skill_End_3   0.3000s
Start         0.8333s
```

The current ACT mapping is intentionally based on the authored clips instead of inventing extra actions:

```text
Basic 1 -> Attack 0%..50%
Basic 2 -> Attack 50%..100%
Basic 3 -> Skill

Skill 1 -> Skill_2 + Skill_End_2
Skill 2 -> Skill_3 + Skill_End_3
```

No air slash, plunge or post-dash attack exists in the current moveset.

## Presentation ownership

`ChenPresentationDriver2D` owns clip names and playback speed. Generic combat code does not know that the clips are named `Attack`, `Skill_2`, etc.

```text
PlayerAttackController / ChenSkill1 / ChenSkill2
        ↓ gameplay events
ChenPresentationDriver2D
        ↓ reflection adapter
Spine SkeletonAnimation
```

Gameplay remains authoritative for damage and timing. Spine is visual presentation only.

## Locomotion

The visible model is always `char_010_chen` so combat weapons/attachments remain visible.

`build_char_010_chen` is attached as a hidden motion source. `SpineBoneMotionRetarget2D` transfers its `Move` bone motion onto the combat skeleton when compatible.

## Basic attack feedback

The old generated slash-line / sword-wave basic-attack VFX has been removed.

Current hit feedback is deliberately limited to:

- enemy hit tint/flash
- HitStop
- small Camera Shake
- knockback from the gameplay attack definition

This keeps the authored Ch'en sword animation readable while still providing impact feedback.

## Skills

### Skill 1 — 赤霄·拔刀

Presentation: `Skill_2` + `Skill_End_2`.

Prototype gameplay: wide frontal hit, physical + Arts damage, short cooldown.

### Skill 2 — 赤霄·绝影

Presentation: `Skill_3` + `Skill_End_3`.

Prototype gameplay: multiple strikes against nearby valid targets, then a stronger final hit, longer cooldown.

These numbers are prototype-only and should be tuned after animation/gameplay feel is validated.

## Local setup

```text
ArknightsACT > Assets > PRTS > Download Full Prototype Pack
ArknightsACT > Assets > PRTS > 2.5 Apply High Quality Texture Settings
ArknightsACT > Assets > PRTS > 3. Build Presentation Prefabs
ArknightsACT > Assets > PRTS > 4. Validate Presentation Setup
ArknightsACT > Build Prototype Scene
```

To inspect Ch'en clips again in Play Mode:

```text
ArknightsACT > Diagnostics > Dump Ch'en Animation Catalog
```

## Architecture rule

Do not add character-specific clip names or PRTS paths to generic movement/combat systems. New Ch'en-only behavior belongs under:

```text
Assets/_Game/Scripts/Gameplay/Characters/Chen/
```

Presentation must never directly own HP changes.
