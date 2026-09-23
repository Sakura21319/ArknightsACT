#if UNITY_EDITOR
using ArknightsACT.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    public static class SchwarzPrototypeSceneBuilder
    {
        private const string DataDir = "Assets/_Game/Data/Attacks/Schwarz";
        public static void BuildDefault() => PrototypeRunSceneBuilder.Build("Schwarz", "default");

        public static void BuildSnow() => PrototypeRunSceneBuilder.Build("Schwarz", "snow#1");

        public static void BuildStriker() => PrototypeRunSceneBuilder.Build("Schwarz", "striker#1");

        internal static AttackDefinition[] BuildAttackDefinitions()
        {
            return new[]
            {
                GetOrCreateAttack("Schwarz_Basic_1", 0.24f, 0.02f, 0.30f, 1.00f, 5.75f, 11.00f, 0.62f, 0.46f, 0.018f, 0.035f),
                GetOrCreateAttack("Schwarz_Basic_2", 0.22f, 0.02f, 0.31f, 1.10f, 6.00f, 11.50f, 0.64f, 0.44f, 0.020f, 0.040f),
                GetOrCreateAttack("Schwarz_Basic_3", 0.28f, 0.02f, 0.36f, 1.30f, 6.25f, 12.00f, 0.68f, 0.50f, 0.026f, 0.055f)
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

    }
}
#endif
