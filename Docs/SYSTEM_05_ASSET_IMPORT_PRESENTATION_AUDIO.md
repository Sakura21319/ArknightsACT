# SYSTEM 05 — Asset Import / Presentation / FX / Audio

Updated: 2026-09-26

This document replaces most character-specific import/FX/audio handoffs. For actual character onboarding steps, also read `CHARACTER_IMPORT_WORKFLOW.md`.

## 1. Ownership boundary

External source assets are Editor-time inputs only.

```text
External unpack/export
  -> Editor import/copy/build
  -> Assets/_Game Art / Generated presentation
  -> character presentation/FX/audio adapter
  -> gameplay events
```

Runtime must not read:

- `D:\Ark\_Unpacked`
- PRTS URLs
- exporter temp directories
- arbitrary local user paths.

Gameplay/Combat must not depend on atlas/skel filenames.

## 2. Current local source root

Default local unpack root:

`D:\Ark\_Unpacked`

Shared editor infrastructure:

- `LocalOperatorAssetImportUtility`
- `LocalOperatorPresentationImporter`
- `LocalOperatorPackageScanner`
- `LocalOperatorQuickImportWindow`
- per-character thin bootstrap/import plan where special handling is necessary.

When a character package is supplied, inspect its manifests before binding.

## 3. Character import workflow

The stable division of responsibility is:

1. user/export tool creates the unpack package;
2. agent/editor code inspects package manifests;
3. copy only required Spine/FX/audio/UI assets;
4. bind directly into the character runtime/presentation;
5. Unity refresh/compile;
6. Play Mode acceptance.

Do not send users through large manual importer workflows when the package already contains enough binding metadata.

`CHARACTER_IMPORT_WORKFLOW.md` remains the detailed source of truth for this procedure.

## 4. Spine / motion

General pattern:

- combat Spine = `char_*` or enemy battle Spine;
- BaseMotion/build Spine = `build_char_*` where available.

Imported character animation playback convention:

**2x playback speed**

This is a character animation convention. Do not blindly double FX timeline speed.

When BaseMotion changes attachments/slots required by the action, prefer full-source BaseMotion rather than trying to retarget only bones.

Common non-combat motions may include:

- Interact
- Relax
- Sit
- Sleep
- Special

Missing source animation remains missing; do not fabricate fallbacks that imply an animation exists.

## 5. Extracted frame FX

Current production-friendly FX path:

```text
external exporter
 -> PNG frames + timing metadata
 -> ExtractedFrameFxImporter
 -> Sprite/Animation/Prefab
 -> character FX controller
```

Runtime FX controllers listen to gameplay events; they do not own damage settlement.

Binding rules:

- `*_trail` = projectile/travel visual;
- hit FX follows actual resolved target;
- actor buff/start FX attaches to actor;
- tuning UI edits binding transforms, not gameplay.

Skin-specific FX must not be mixed across skins.

## 6. OHMS

OHMS Structured importer remains useful for:

- research;
- dependency inspection;
- reconstructing standard Unity object/material hierarchy.

It is **not** the current primary runtime FX path for maintained characters.

Do not reconnect historical OriginalClient/OHMS prefabs to runtime merely because the assets still exist.

## 7. Character-specific presentation notes

### Chen

Uses the established extracted-frame/custom FX event path. Historical OriginalClient/OHMS experiments remain reference-only unless explicitly revived.

### Schwarz

Maintains separate skin FX and ranged/trail behavior. Do not mix Snow/Striker/Default assets.

### Wisadel

Original and game skin FX binding remains separate. Trail assets represent projectile travel.

### Skadi

Current maintained skins:

- default
- marthe#5
- summer#3

Combat FX includes basic attack and P8 S2/S3 lifecycle binding.

### FrostNova

Only Winter is maintained as formal playable presentation.

Skill3 visual chain and persistent back buff behavior remain character-specific.

## 8. Audio

Character audio should be declared through `PlayableOperatorAudioProfile` plus character-local cue components when generic skill-cast timing is insufficient.

Examples:

- basic attack swing;
- impact;
- skill activation;
- character voice pools.

Do not guess missing source audio or reuse another character's voice as fallback.

## 9. BGM

Current playlist file:

`Assets/_Game/Data/Audio/BgmPlaylist.json`

Runtime BGM:

`Assets/_Game/Art/Audio/BGM`

Current groups:

UI/home:
- 生命流
- Ghost Hunter
- City

In-map:
- 蔓延
- 深渊梦呓
- 深层迷醉

Playback policy:

- random within current group;
- avoid immediate repeat;
- Intro -> Loop when source is authored that way.

## 10. UI image/audio asset rule

Runtime UI reads local imported assets.

Do not add runtime network downloads for icons/audio.

Editor-time download/import can be used for prototype source acquisition only when the resulting runtime asset is local.

## 11. Refresh behavior

Script reload/current-character refresh should:

- refresh assets/references;
- avoid destructive full-scene rebuilds;
- avoid automatically saving unrelated user scene edits;
- refresh only the relevant current operator where possible.

Old scene instances may be migrated by Builder `RefreshExisting` when new required runtime components are added.

## 12. Acceptance

Bridge/source validation is not enough.

For imported characters, final acceptance includes:

- Unity compile;
- correct visible Spine;
- correct skin;
- action playback;
- facing/mirroring;
- FX timing/placement;
- hit/trail target relation;
- audio timing;
- TAB/test-switch compatibility only where still used in prototype;
- no loss of FX after refresh/reselection.
