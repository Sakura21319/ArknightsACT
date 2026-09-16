# Phase 08 · PRTS Gameplay Audio

This pass adds a small **local-only** Arknights audio layer to the 2.5D prototype. Downloaded audio is intentionally stored under `Assets/_Game/Art/Audio/PRTS/`, which is already excluded by the repository-wide `Assets/_Game/Art/**/PRTS/` ignore rule. The repository contains the downloader/catalog/runtime wiring, not redistributed audio files.

## Canonical sources used

- Battle BGM: `bat_chernobog` / `m_bat_chernobog_intro` + `m_bat_chernobog_loop`.
- Chen skill slot 1 (`赤霄·拔刀`): `p_skill_chixiaobadao`.
- Chen skill slot 2 (`赤霄·绝影`): `p_skill_jueying_1`.
- Chen Chinese battle voice pool: CN_025 .. CN_028 from the operator voice set.

The downloader keeps fallback URL candidates for PRTS media-path casing/root differences. Every downloaded file receives a `_SOURCE.txt` sidecar recording the PRTS page and the actual media URL that succeeded.

## Unity workflow

1. Pull `feat/phase-08-world-visuals` and wait for zero compiler errors.
2. Run `ArknightsACT > Assets > PRTS > Download Gameplay Audio (BGM + Chen)`.
3. Wait for Unity to import all audio clips.
4. Run `ArknightsACT > Build Prototype Scene`.
5. Enter Play mode.

## Runtime behavior

`RoguelitePrototypeAudioController` is added to `[StageRuntime]` by `PrototypeStageRuntimeFactory`.

- BGM intro is scheduled once, then the loop clip is scheduled immediately after it for a cleaner transition than repeatedly playing a combined intro.
- Skill audio listens to `PlayerSkillController.SkillCastSucceeded`, so cooldown/invalid button presses never play audio.
- Slot 1 and slot 2 use separate canonical Chen skill SFX.
- Slot 1 randomly alternates between CN_025 / CN_026; slot 2 alternates between CN_027 / CN_028, avoiding immediate repetition when possible.
- Voice briefly ducks the BGM so the spoken line remains readable without making the skill SFX too loud.
- Missing clips do not break the prototype. One warning explains how to download and rebuild the scene.

## Validation

Confirm:

- Chernobog BGM starts with the intro and continues into the loop.
- Skill 1 only plays its SFX/voice after a successful cast.
- Skill 2 only plays its SFX/voice after a successful cast.
- Repeated casts vary the voice line rather than always selecting the same clip.
- BGM returns to its normal volume after the voice finishes.
- No downloaded PRTS `.mp3` or `_SOURCE.txt` files appear in Git status.
