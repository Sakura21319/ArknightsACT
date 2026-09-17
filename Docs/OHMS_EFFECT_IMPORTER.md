# OHMS Structured Effect Importer

This project can rebuild standard Unity battle-effect assets from an AssetStudio-Arknights/OHMS **Structured (JSON list)** export.

## Export side

In AssetStudio-Arknights load the desired game AssetBundle(s), then use:

`[OHMS] Export -> Structured (JSON list) -> All assets`

The selected export must contain:

```
<package>/
  assets.json
  things/
    0.ttbin
    1.ttbin
    ...
```

`.ttbin` is only a container extension. OHMS writes different payloads by Unity type: normal Unity objects are JSON, Texture2D payloads are PNG bytes, and Mesh payloads are OHMS text meshes.

The importer never edits this source export. Scan and Import build a disposable normalized copy under `Library/ArknightsACT/OHMS/Normalized`.

## Unity side

Open:

`ArknightsACT > Assets > OHMS Effect Importer`

If the importer window is inconvenient, the last staged export can be rebuilt directly with:

`ArknightsACT > OHMS > Import staged Ch'en combat FX`

This command targets `Library/ArknightsACT/OHMS/Normalized` and refreshes the `Chen` package.

Choose the export folder, Scan it, then use include/exclude tokens to choose root GameObjects. Tokens are comma-separated and use case-insensitive substring matching.

For Ch'en's current combat FX use the built-in **Preset: Ch'en combat**. It selects base `chen_skill_02_*`, `chen_skill_03_*` and `chen_attack_01_*` roots while excluding `sale#10` and `nian#2` variants. Use package name `Chen`, so the generated prefabs land under:

`Assets/_Game/Art/FX/OriginalClient/Chen/Prefabs/`

`ChenOriginalSkillFxCatalog` searches this package recursively, so rebuilding Prototype Scene automatically wires imported skill prefabs.

## What is reconstructed

The importer is intentionally generic and name-agnostic. It rebuilds:

- GameObject/Transform hierarchy
- ParticleSystem main-module and emission/Burst settings, plus ParticleSystemRenderer materials
- MeshFilter / MeshRenderer
- TrailRenderer
- OHMS meshes
- Material scalar/color/texture settings using the local `ArknightsACT/ImportedClientFX` fallback shader
- Internal Texture2D PNG payloads
- root prefabs

Original game `MonoBehaviour` scripts are not fabricated. OHMS also does not currently write payload files for `Animator` and `AnimationClip` in this export mode, so those are reported in `OHMS_IMPORT_REPORT.txt` instead of silently guessed.

## External references

A material can point to textures/materials in another bundle through a non-zero Unity `m_FileID`. When the referenced `PathID` is present in the same combined structured export, the staging pass normalizes that pointer so the importer can resolve it. The importer shows the normalized-pointer count after Scan and includes it in the final summary.

A single-bundle export cannot contain dependency objects. References whose `PathID` is still absent after staging remain external and are recorded as `FileID + PathID` entries in `OHMS_IMPORT_REPORT.txt`.

For effects that still miss textures after import, load the effect bundle together with its dependency bundles in AssetStudio-Arknights and run the structured export again. The tool is designed so the same workflow can later import effects for other characters/enemies without adding character-specific importer code.
