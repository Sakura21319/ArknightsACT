# Phase 08 · PRTS Gameplay Audio

This pass adds a small **local-only** Arknights audio layer to the 2.5D prototype. Downloaded audio is intentionally stored under `Assets/_Game/Art/Audio/PRTS/`, which is already excluded by the repository-wide `Assets/_Game/Art/**/PRTS/` ignore rule. The repository contains the downloader/catalog/runtime wiring, not redistributed audio files.

## Canonical sources used

- Game BGM: Chapter 16 main-menu track `sys_act16main` / `Sound_Beta_2/Music/act16main/m_sys_act16main`, titled **反常光谱** on PRTS.
- Chen skill slot 1 (`赤霄·拔刀`): verified Chixiao/story presentation SFX `d_avg_chixiaosword`.
- Chen skill slot 2 (`赤霄·绝影`): verified Chixiao/story presentation SFX `d_avg_chixiaotiancheng`.
- Chen battle voice pool: **Japanese only**, CN_025 .. CN_028 from `voice/char_010_chen`.

The downloader normalizes PRTS media paths and writes a `_SOURCE.txt` sidecar recording the PRTS page and the media URL that succeeded. Japanese voice files use `Chen_Voice_JP_*` local names so previously downloaded Chinese clips cannot be reused accidentally.

## Simplified Unity menu

The visible `ArknightsACT` production workflow is intentionally small:

- `ArknightsACT > Build Prototype Scene`
- `ArknightsACT > Assets > PRTS > Download Prototype Models`
- `ArknightsACT > Assets > PRTS > Build Presentation Prefabs`
- `ArknightsACT > Assets > PRTS > Download Gameplay Audio`
- `ArknightsACT > Assets > PRTS > Verify Gameplay Audio`

Legacy demo builders, individual Chernobog material passes, diagnostic commands, per-character download commands, the old Spine installer entry, and split audio download commands are hidden from the normal menu. Their implementation remains available to the editor code where still useful.

## Unity workflow

1. Pull `feat/phase-08-world-visuals` and wait for zero compiler errors.
2. Run `ArknightsACT > Assets > PRTS > Download Gameplay Audio`.
3. Run `ArknightsACT > Assets > PRTS > Verify Gameplay Audio`.
4. Run `ArknightsACT > Build Prototype Scene`.
5. Enter Play mode.

## Runtime behavior

`RoguelitePrototypeAudioController` is added to `[StageRuntime]` by `PrototypeStageRuntimeFactory`.

- `反常光谱` is loaded as the single looping gameplay BGM; the old Chernobog intro/loop pair is no longer wired.
- Skill audio listens to `PlayerSkillController.SkillCastSucceeded`, so cooldown/invalid button presses never play audio.
- Slot 1 randomly alternates between Japanese CN_025 / CN_026; slot 2 alternates between Japanese CN_027 / CN_028, avoiding immediate repetition when possible.
- Voice briefly ducks the BGM so the spoken line remains readable without making the skill SFX too loud.
- Missing clips do not break the prototype. The scene can still build and the audio verifier reports what is missing.

## Validation

Confirm:

- `反常光谱` starts and loops during gameplay.
- No Chinese Ch'en voice is used; skill voices are Japanese.
- Skill 1 only plays its SFX/voice after a successful cast.
- Skill 2 only plays its SFX/voice after a successful cast.
- Repeated casts vary the voice line rather than always selecting the same clip.
- BGM returns to its normal volume after the voice finishes.
- No downloaded PRTS `.mp3` or `_SOURCE.txt` files appear in Git status.
