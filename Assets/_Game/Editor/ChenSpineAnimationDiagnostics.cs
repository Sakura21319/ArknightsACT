#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Dumps the complete authored animation catalog from the local PRTS Ch'en combat skeleton.
    /// We deliberately discover clips first instead of assuming Texas-style animation names.
    /// </summary>
    internal static class ChenSpineAnimationDiagnostics
    {
        private const string MenuPath = "ArknightsACT/Diagnostics/Dump Ch'en Animation Catalog";

        [MenuItem(MenuPath)]
        private static void Dump()
        {
            var skeletonAnimation = FindChenSkeletonAnimation();
            if (skeletonAnimation == null)
            {
                Debug.LogWarning(
                    "[ArknightsACT/ChenAnim] Ch'en combat SkeletonAnimation was not found. " +
                    "Download Ch'en, build PRTS presentation prefabs, rebuild Prototype Scene, then run this command again.");
                return;
            }

            TryInitialize(skeletonAnimation);
            var skeleton = GetMember(skeletonAnimation, "Skeleton", "skeleton");
            var skeletonData = GetMember(skeleton, "Data", "data");
            if (skeletonData == null)
            {
                Debug.LogWarning("[ArknightsACT/ChenAnim] SkeletonData is unavailable.", skeletonAnimation);
                return;
            }

            var animations = GetMember(skeletonData, "Animations", "animations") as IEnumerable;
            if (animations == null)
            {
                Debug.LogWarning("[ArknightsACT/ChenAnim] Animation list is unavailable.", skeletonAnimation);
                return;
            }

            var rows = new List<AnimationRow>();
            foreach (var animation in animations)
            {
                if (animation == null)
                    continue;
                var name = ReadString(animation, "Name", "name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                rows.Add(new AnimationRow(name, ReadFloat(animation, "Duration", "duration")));
            }
            rows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            var boneCount = CountEnumerable(GetMember(skeleton, "Bones", "bones") as IEnumerable);
            var slotCount = CountEnumerable(GetMember(skeleton, "Slots", "slots") as IEnumerable);

            var report = new StringBuilder(16384);
            report.AppendLine("================ CH'EN PRTS ANIMATION CATALOG ================");
            report.AppendLine($"GameObject: {skeletonAnimation.gameObject.name}");
            report.AppendLine($"Runtime Type: {skeletonAnimation.GetType().FullName}");
            report.AppendLine($"Application.isPlaying: {Application.isPlaying}");
            report.AppendLine($"Bones: {boneCount}   Slots: {slotCount}   Animations: {rows.Count}");
            report.AppendLine();
            report.AppendLine("ALL AUTHORED CLIPS");
            report.AppendLine("index | name | duration(s)");
            report.AppendLine(new string('-', 72));
            for (var i = 0; i < rows.Count; i++)
                report.AppendLine($"{i} | {rows[i].Name} | {rows[i].Duration:0.####}");

            AppendGroup(report, "ATTACK / COMBAT CANDIDATES", rows,
                "attack", "atk", "skill", "special", "ability", "combo", "slash", "strike", "shoot");
            AppendGroup(report, "MOVEMENT CANDIDATES", rows,
                "move", "run", "walk", "start", "enter", "retreat", "idle");
            AppendGroup(report, "REACTION CANDIDATES", rows,
                "hit", "hurt", "stun", "die", "death", "down");

            report.AppendLine();
            report.AppendLine("NEXT STEP");
            report.AppendLine("Paste this report into ChatGPT. We will map Ch'en's real authored clips to ACT actions before changing gameplay timing.");
            report.AppendLine("===============================================================");

            var text = report.ToString();
            EditorGUIUtility.systemCopyBuffer = text;
            Debug.Log("[ArknightsACT/ChenAnim] Complete Ch'en animation catalog copied to clipboard.\n" + text, skeletonAnimation);
        }

        private static void AppendGroup(StringBuilder report, string title, List<AnimationRow> rows, params string[] tokens)
        {
            report.AppendLine();
            report.AppendLine(title);
            report.AppendLine(new string('-', 72));
            var any = false;
            foreach (var row in rows)
            {
                if (!tokens.Any(token => row.Name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;
                any = true;
                report.AppendLine($"{row.Name} | {row.Duration:0.####}s");
            }
            if (!any)
                report.AppendLine("<none matched by name heuristic>");
        }

        private static Component FindChenSkeletonAnimation()
        {
            var behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            Component fallback = null;
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null || behaviour.GetType().FullName != "Spine.Unity.SkeletonAnimation")
                    continue;
                if (!behaviour.gameObject.scene.IsValid())
                    continue;

                var path = GetTransformPath(behaviour.transform);
                if (path.IndexOf("MotionSource", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                if (path.IndexOf("Player_Chen", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    path.IndexOf("char_010_chen", StringComparison.OrdinalIgnoreCase) >= 0)
                    return behaviour;

                if (fallback == null && path.IndexOf("char_010_chen", StringComparison.OrdinalIgnoreCase) >= 0)
                    fallback = behaviour;
            }
            return fallback;
        }

        private static void TryInitialize(Component component)
        {
            if (component == null)
                return;
            try
            {
                var method = component.GetType().GetMethod(
                    "Initialize",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(bool) },
                    null);
                method?.Invoke(component, new object[] { false });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ArknightsACT/ChenAnim] Initialize failed: " + exception.GetBaseException().Message, component);
            }
        }

        private static object GetMember(object target, string propertyName, string fieldName)
        {
            if (target == null)
                return null;
            var type = target.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
                return property.GetValue(target);
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? type.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(target);
        }

        private static string ReadString(object target, string propertyName, string fieldName)
        {
            return GetMember(target, propertyName, fieldName) as string ?? string.Empty;
        }

        private static float ReadFloat(object target, string propertyName, string fieldName)
        {
            var value = GetMember(target, propertyName, fieldName);
            if (value is float f) return f;
            if (value is double d) return (float)d;
            return 0f;
        }

        private static int CountEnumerable(IEnumerable values)
        {
            if (values == null)
                return 0;
            var count = 0;
            foreach (var _ in values) count++;
            return count;
        }

        private static string GetTransformPath(Transform transform)
        {
            var stack = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
                stack.Push(current.name);
            return string.Join("/", stack);
        }

        private readonly struct AnimationRow
        {
            public readonly string Name;
            public readonly float Duration;

            public AnimationRow(string name, float duration)
            {
                Name = name;
                Duration = duration;
            }
        }
    }
}
#endif
