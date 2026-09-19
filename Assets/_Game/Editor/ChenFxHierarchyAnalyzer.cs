using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Dumps Ch'en skill 3 original prefab hierarchy and particle activation data.
    /// Used to reconstruct the original client FX event order instead of blindly playing all emitters.
    /// </summary>
    public static class LegacyChenFxHierarchyAnalyzer
    {
        // Disabled legacy analyzer. The OHMS analyzer owns this menu now.
        public static void Analyze()
        {
            const string root = "Assets/_Game/Art/FX/OriginalClient/Chen/Prefabs";
            var output = "ChenSkill03FxAnalysis.txt";
            using var writer = new StreamWriter(output, false);

            for (var i = 1; i <= 10; i++)
            {
                var path = $"{root}/chen_skill_03_hit_{i:00}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    writer.WriteLine($"MISSING {path}");
                    continue;
                }

                writer.WriteLine($"==== {prefab.name} ====");
                Dump(prefab.transform, writer, 0);
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ArknightsACT/ChenFX] Analysis written: {output}");
        }

        private static void Dump(Transform node, StreamWriter writer, int depth)
        {
            writer.WriteLine(
                $"{new string(' ', depth * 2)}{node.name} active={node.gameObject.activeSelf} " +
                $"pos={node.localPosition} rot={node.localEulerAngles}");

            foreach (var ps in node.GetComponents<ParticleSystem>())
            {
                var main = ps.main;
                var emission = ps.emission;
                writer.WriteLine(
                    $"{new string(' ', depth * 2 + 2)}PS " +
                    $"playAwake={main.playOnAwake} loop={main.loop} duration={main.duration} " +
                    $"max={main.maxParticles} emission={emission.enabled} bursts={emission.burstCount}");
            }

            foreach (Transform child in node)
                Dump(child, writer, depth + 1);
        }
    }
}
