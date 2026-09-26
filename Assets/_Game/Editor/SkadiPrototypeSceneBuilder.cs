#if UNITY_EDITOR
using ArknightsACT.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class SkadiPrototypeSceneBuilder
    {
        private const string DataDir = "Assets/_Game/Data/Attacks/Skadi";

        internal static AttackDefinition[] BuildAttackDefinitions()
        {
            EnsureFolder(DataDir);
            return new[]
            {
                GetOrCreateAttack(
                    "Skadi_Basic_1",
                    startup: 0.15f,
                    active: 0.04f,
                    recovery: 0.28f,
                    damageMultiplier: 1.00f,
                    hitboxOffset: new Vector2(0.90f, 0.04f),
                    hitboxSize: new Vector2(1.70f, 1.20f),
                    knockback: new Vector2(3.0f, 0.55f),
                    dashCancel: 0.46f,
                    hitStop: 0.025f,
                    shake: 0.06f)
            };
        }

        private static AttackDefinition GetOrCreateAttack(
            string name,
            float startup,
            float active,
            float recovery,
            float damageMultiplier,
            Vector2 hitboxOffset,
            Vector2 hitboxSize,
            Vector2 knockback,
            float dashCancel,
            float hitStop,
            float shake)
        {
            var path = $"{DataDir}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AttackDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.name = name;
            asset.startup = startup;
            asset.active = active;
            asset.recovery = recovery;
            asset.damageMultiplier = damageMultiplier;
            asset.hitboxOffset = hitboxOffset;
            asset.hitboxSize = hitboxSize;
            asset.knockback = knockback;
            asset.dashCancelNormalizedTime = dashCancel;
            asset.hitStopSeconds = hitStop;
            asset.cameraShakeAmplitude = shake;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
