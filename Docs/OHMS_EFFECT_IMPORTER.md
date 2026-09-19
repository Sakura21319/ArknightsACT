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

`ArknightsACT > Assets > OHMS > Import staged Ch'en combat FX`

The repair-only command is next to it:

`ArknightsACT > Assets > OHMS > Repair Ch'en skill 3 blade textures`

The same two commands are also available directly under `ArknightsACT > OHMS`.

This command targets `Library/ArknightsACT/OHMS/Normalized` and refreshes the `Chen` package.

Choose the export folder, Scan it, then use include/exclude tokens to choose root GameObjects. Tokens are comma-separated and use case-insensitive substring matching.

For Ch'en's current combat FX use the built-in **Preset: Ch'en combat**. It selects base `chen_skill_02_*`, `chen_skill_03_*` and `chen_attack_01_*` roots while excluding `sale#10` and `nian#2` variants. Use package name `Chen`, so the generated prefabs land under:

`Assets/_Game/Art/FX/OriginalClient/Chen/Prefabs/`

`ChenOriginalSkillFxCatalog` can search this package recursively for analysis, but it is currently
reference-only: the generated Prototype Scene deliberately does **not** mount the imported attack or
skill FX at runtime. This avoids treating incomplete cross-bundle dependencies as production assets.
The current hand-off and the future authored-FX mount contract are documented in
[`Docs/CHEN_CUSTOM_FX_HANDOFF.md`](CHEN_CUSTOM_FX_HANDOFF.md).

## What is reconstructed

The importer is intentionally generic and name-agnostic. It rebuilds:

- GameObject/Transform hierarchy
- ParticleSystem main-module and emission/Burst settings, plus ParticleSystemRenderer materials
- MeshFilter / MeshRenderer
- TrailRenderer
- OHMS meshes
- Material scalar/color/texture settings using the local `ArknightsACT/ImportedClientFX` fallback shader. The shader is deliberately pipeline-neutral: this project currently has no active URP asset (`GraphicsSettings.m_CustomRenderPipeline` is empty), so tagging it as URP-only would make every particle silently disappear in the built-in renderer.
- Internal Texture2D PNG payloads
- root prefabs

Original game `MonoBehaviour` scripts are not fabricated. OHMS also does not currently write payload files for `Animator` and `AnimationClip` in this export mode, so those are reported in `OHMS_IMPORT_REPORT.txt` instead of silently guessed.

## External references

A material can point to textures/materials in another bundle through a non-zero Unity `m_FileID`. When the referenced `PathID` is present in the same combined structured export, the staging pass normalizes that pointer so the importer can resolve it. The importer shows the normalized-pointer count after Scan and includes it in the final summary.

A single-bundle export cannot contain dependency objects. References whose `PathID` is still absent after staging remain external and are recorded as `FileID + PathID` entries in `OHMS_IMPORT_REPORT.txt`.

For Ch'en skill 3 specifically, the minimal complete AssetStudio load set is:

```
battle/prefabs/effects/chen.ab
refs/fx/texture/trail.ab
refs/fx/texture/flow.ab
```

`chen.ab` owns the `daoguang` mesh and materials; `trail.ab` owns `trail_52_C`/`trail_51_C`, and `flow.ab` owns `flow_02_ab_02` (the dissolve map). Do not substitute `chen2.ab` or `chen3.ab`; those are a different Ch'en asset set. Export all three bundles into one Structured export and import it with the Ch'en combat preset. The report should show a non-zero `NormalizedPointers` count and no unresolved entries for `chen_skill_03_hit_1._MainTex`, `chen_skill_03_hit_1._DissolveTex`, or `trail_51_C_add._MainTex`.

If a bundle is unavailable, the local batch command can use the staged copies of those three textures under `Chen/Textures`; it will rebind the original skill-3 materials after import. The tool is designed so the same workflow can later import effects for other characters/enemies without adding character-specific importer code.

The staged Ch'en combat import also applies a semantic fallback for the known slash nodes: when the dependency material is absent, `daofeng_01_add` is bound to `rotation_y`, `rotation_z`, `fixed`, and `static_offset` (and to unresolved slots in the two normal-attack prefabs) instead of leaving the red diagnostic material visible.
