#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArknightsACT.Gameplay.Characters.Chen;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.PRTS
{
    /// <summary>
    /// Local-only catalog for Ch'en's independent battle effect prefabs.
    /// These are NOT character Spine attachments. The client gamedata references them by the
    /// chen_skill_02_* and chen_skill_03_* keys and the source bundles live under
    /// battle/prefabs/effects in the original game client.
    /// </summary>
    internal static class ChenOriginalSkillFxCatalog
    {
        public const string Root = "Assets/_Game/Art/FX/OriginalClient/Chen";

        public const string DrawStart = "chen_skill_02_start";
        public const string DrawHit = "chen_skill_02_hit";
        public const string DrawBuff = "chen_skill_02_buff";
        public const string JueyingStart = "chen_skill_03_start";
        public const string JueyingStart02 = "chen_skill_03_start_02";

        public static readonly string[] JueyingHits = Enumerable.Range(1, 10)
            .Select(index => $"chen_skill_03_hit_{index:00}")
            .ToArray();

        public static readonly string[] ExpectedKeys = new[]
            {
                DrawStart,
                DrawHit,
                DrawBuff,
                JueyingStart,
                JueyingStart02
            }
            .Concat(JueyingHits)
            .ToArray();

        public static ChenOriginalSkillFxController Configure(GameObject owner)
        {
            if (owner == null)
                return null;

            var controller = owner.GetComponent<ChenOriginalSkillFxController>() ??
                             owner.AddComponent<ChenOriginalSkillFxController>();
            controller.Configure(
                ResolveGameObject(DrawStart),
                ResolveGameObject(DrawHit),
                ResolveGameObject(DrawBuff),
                ResolveGameObject(JueyingStart),
                ResolveGameObject(JueyingStart02),
                JueyingHits.Select(ResolveGameObject).ToArray());

            var available = ExpectedKeys.Count(key => ResolveGameObject(key) != null);
            if (available == ExpectedKeys.Length)
            {
                Debug.Log(
                    $"[ArknightsACT/ChenSkillFX] Loaded all {available}/{ExpectedKeys.Length} original client skill FX from {Root}.",
                    owner);
            }
            else
            {
                var missing = ExpectedKeys.Where(key => ResolveGameObject(key) == null).ToArray();
                Debug.LogWarning(
                    $"[ArknightsACT/ChenSkillFX] Original client skill FX available: {available}/{ExpectedKeys.Length}. " +
                    $"Missing: {string.Join(", ", missing)}.\n" +
                    "Source category: game client battle/prefabs/effects. No generated slash fallback is used.",
                    owner);
            }

            return controller;
        }

        private static GameObject ResolveGameObject(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            var directPath = $"{Root}/{key}.prefab";
            var direct = AssetDatabase.LoadAssetAtPath<GameObject>(directPath);
            if (direct != null)
                return direct;

            if (!AssetDatabase.IsValidFolder(Root))
                return null;

            // AssetRipper/ArkStudio output can preserve a nested source-bundle directory. Search
            // recursively by the authoritative client key so the user does not need to flatten it.
            var guids = AssetDatabase.FindAssets(key, new[] { Root });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var name = Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null)
                    return asset;
            }

            return null;
        }
    }
}
#endif
