using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor.OHMS
{
    public static class ChenFxHierarchyAnalyzer
    {
        [InitializeOnLoadMethod]
        private static void VerifyLoaded()
        {
            Debug.Log("[ArknightsACT/OHMS] ChenFxHierarchyAnalyzer loaded");
        }

        [UnityEditor.MenuItem("ArknightsACT/OHMS/Analyze Ch'en Skill 03 FX")]
        [UnityEditor.MenuItem("Tools/ArknightsACT/Analyze Ch'en Skill 03 FX")]
        public static void Analyze()
        {
            const string root = "Assets/_Game/Art/FX/OriginalClient/Chen/Prefabs";
            var output = "ChenSkill03FxAnalysis.txt";
            using var writer = new StreamWriter(output, false);

            for (var i = 1; i <= 10; i++)
            {
                var path = $"{root}/chen_skill_03_hit_{i:00}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                writer.WriteLine($"==== {path} ====");
                if (prefab != null)
                    Dump(prefab.transform, writer, 0);
                else
                    writer.WriteLine("MISSING");
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ArknightsACT/ChenFX] Analysis written: {output}");
        }

        private static void Dump(Transform node, StreamWriter writer, int depth)
        {
            writer.WriteLine($"{new string(' ', depth * 2)}{node.name} active={node.gameObject.activeSelf} pos={node.localPosition} rot={node.localEulerAngles}");
            foreach (var ps in node.GetComponents<ParticleSystem>())
            {
                var main = ps.main;
                var emission = ps.emission;
                writer.WriteLine($"{new string(' ', depth * 2 + 2)}PS awake={main.playOnAwake} loop={main.loop} duration={main.duration} max={main.maxParticles} bursts={emission.burstCount}");
            }
            foreach (Transform child in node)
                Dump(child, writer, depth + 1);
        }
    }
}
