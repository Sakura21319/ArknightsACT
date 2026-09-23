#if UNITY_EDITOR
using ArknightsACT.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class FrostNovaPrototypeSceneBuilder
    {
        private const string DataDir = "Assets/_Game/Data/Attacks/FrostNova";

        internal static AttackDefinition[] BuildAttackDefinitions()
        {
            EnsureFolder(DataDir);
            return new[]
            {
                GetOrCreateAttack(
                    "FrostNova_Basic_1",
                    0.30f,
                    0.03f,
                    0.54f,
                    1.00f,
                    5.25f,
                    10.50f,
                    0.80f,
                    0.48f,
                    0.018f,
                    0.040f)
            };
        }

        private static AttackDefinition GetOrCreateAttack(
            string name,
            float startup,
            float active,
            float recovery,
            float damageMultiplier,
            float forwardOffset,
            float forwardSize,
            float lateralSize,
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
            asset.hitboxOffset = new Vector2(forwardOffset, 0.04f);
            asset.hitboxSize = new Vector2(forwardSize, lateralSize);
            asset.knockback = Vector2.zero;
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
