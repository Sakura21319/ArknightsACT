#if UNITY_EDITOR
using ArknightsACT.Gameplay.Combat;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class WisadelPrototypeSceneBuilder
    {
        private const string DataDirectory = "Assets/_Game/Data/Attacks/Wisadel";

        internal static AttackDefinition[] BuildAttackDefinitions()
        {
            return new[]
            {
                GetOrCreateAttack("Wisadel_Basic_A", 0.24f, 0.04f, 0.31f, 1.00f),
                GetOrCreateAttack("Wisadel_Basic_B", 0.23f, 0.04f, 0.32f, 1.10f),
                GetOrCreateAttack("Wisadel_Basic_C", 0.26f, 0.04f, 0.36f, 1.20f)
            };
        }

        private static AttackDefinition GetOrCreateAttack(
            string name,
            float startup,
            float active,
            float recovery,
            float damageMultiplier)
        {
            EnsureFolder(DataDirectory);
            var path = DataDirectory + "/" + name + ".asset";
            var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);
            if (attack == null)
            {
                attack = ScriptableObject.CreateInstance<AttackDefinition>();
                AssetDatabase.CreateAsset(attack, path);
            }

            attack.name = name;
            attack.startup = startup;
            attack.active = active;
            attack.recovery = recovery;
            attack.damageMultiplier = damageMultiplier;
            attack.hitboxOffset = new Vector2(4f, 0.75f);
            attack.hitboxSize = new Vector2(8f, 3f);
            attack.knockback = Vector2.zero;
            attack.dashCancelNormalizedTime = 0.55f;
            attack.hitStopSeconds = 0.01f;
            attack.cameraShakeAmplitude = 0.015f;
            EditorUtility.SetDirty(attack);
            return attack;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var slash = path.LastIndexOf('/');
            if (slash <= 0)
                return;
            var parent = path.Substring(0, slash);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
#endif
