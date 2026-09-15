#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Reflection-only Spine diagnostics. Keeps the editor assembly independent from a concrete
    /// Spine runtime version while exposing the local PRTS Texas skeleton layout needed for
    /// authoring procedural action poses.
    /// </summary>
    internal static class TexasSpineBoneDiagnostics
    {
        private const string MenuPath = "ArknightsACT/Diagnostics/Dump Texas Spine Bones";
        private const string PlayerName = "Player_Texas";
        private const string CombatPresentationPrefix = "Presentation_char_102_texas";

        [MenuItem(MenuPath)]
        private static void DumpTexasSpineBones()
        {
            var skeletonAnimation = FindTexasSkeletonAnimation();
            if (skeletonAnimation == null)
            {
                Debug.LogWarning(
                    "[ArknightsACT/TexasBones] Texas Spine SkeletonAnimation was not found. " +
                    "Build Prototype Scene first, make sure the PRTS Texas presentation prefab exists, " +
                    "then run this command again (Play Mode is recommended)." );
                return;
            }

            if (!TryInitialize(skeletonAnimation))
            {
                Debug.LogWarning(
                    "[ArknightsACT/TexasBones] Texas SkeletonAnimation was found but could not be initialized. " +
                    "Enter Play Mode and run the command again.",
                    skeletonAnimation);
                return;
            }

            var skeleton = GetPropertyValue(skeletonAnimation, "Skeleton");
            if (skeleton == null)
            {
                Debug.LogWarning(
                    "[ArknightsACT/TexasBones] Skeleton is null after initialization. " +
                    "Enter Play Mode and run the command again.",
                    skeletonAnimation);
                return;
            }

            var report = new StringBuilder(16384);
            report.AppendLine("================ TEXAS SPINE SKELETON REPORT ================");
            report.AppendLine($"GameObject: {skeletonAnimation.gameObject.name}");
            report.AppendLine($"Runtime Type: {skeletonAnimation.GetType().FullName}");
            report.AppendLine($"Application.isPlaying: {Application.isPlaying}");
            report.AppendLine();

            var boneRows = ReadBones(skeleton);
            report.AppendLine($"BONES ({boneRows.Count})");
            report.AppendLine("index | depth | name | parent | setup(x,y,rot,scaleX,scaleY) | runtime(x,y,rot,scaleX,scaleY)");
            report.AppendLine(new string('-', 118));
            for (var i = 0; i < boneRows.Count; i++)
            {
                var row = boneRows[i];
                report.Append(row.Index).Append(" | ")
                    .Append(row.Depth).Append(" | ")
                    .Append(new string(' ', row.Depth * 2)).Append(row.Name).Append(" | ")
                    .Append(row.ParentName).Append(" | ")
                    .Append(row.SetupPose).Append(" | ")
                    .Append(row.RuntimePose).AppendLine();
            }

            report.AppendLine();
            var slotRows = ReadSlots(skeleton);
            report.AppendLine($"SLOTS / ATTACHMENTS ({slotRows.Count})");
            report.AppendLine("index | slot | bone | attachment");
            report.AppendLine(new string('-', 86));
            for (var i = 0; i < slotRows.Count; i++)
            {
                var row = slotRows[i];
                report.Append(row.Index).Append(" | ")
                    .Append(row.SlotName).Append(" | ")
                    .Append(row.BoneName).Append(" | ")
                    .Append(row.AttachmentName).AppendLine();
            }

            report.AppendLine();
            report.AppendLine("LIKELY ACTION BONES (name heuristic only)");
            report.AppendLine(new string('-', 86));
            AppendHeuristicMatches(report, boneRows, "root/body/torso", "root", "body", "torso", "chest", "hip", "pelvis", "waist");
            AppendHeuristicMatches(report, boneRows, "head", "head", "face", "neck");
            AppendHeuristicMatches(report, boneRows, "arms/hands", "arm", "hand", "wrist", "shoulder", "elbow");
            AppendHeuristicMatches(report, boneRows, "weapon/sword", "weapon", "sword", "blade", "knife", "katana");
            AppendHeuristicMatches(report, boneRows, "legs/feet", "leg", "foot", "thigh", "knee", "ankle");
            AppendHeuristicMatches(report, boneRows, "hair/tail/clothes", "hair", "tail", "coat", "skirt", "cloth", "cape");
            report.AppendLine("==============================================================");

            var text = report.ToString();
            EditorGUIUtility.systemCopyBuffer = text;
            Debug.Log(
                "[ArknightsACT/TexasBones] Complete Texas Spine report copied to clipboard.\n" + text,
                skeletonAnimation);
        }

        private static Component FindTexasSkeletonAnimation()
        {
            var allBehaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            Component fallback = null;

            for (var i = 0; i < allBehaviours.Length; i++)
            {
                var behaviour = allBehaviours[i];
                if (behaviour == null || behaviour.GetType().FullName != "Spine.Unity.SkeletonAnimation")
                    continue;

                var go = behaviour.gameObject;
                if (!go.scene.IsValid())
                    continue;

                var path = GetTransformPath(go.transform);
                if (path.IndexOf(CombatPresentationPrefix, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    path.IndexOf(PlayerName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return behaviour;

                if (fallback == null &&
                    path.IndexOf("char_102_texas", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    path.IndexOf("MotionSource", StringComparison.OrdinalIgnoreCase) < 0)
                    fallback = behaviour;
            }

            return fallback;
        }

        private static bool TryInitialize(Component skeletonAnimation)
        {
            try
            {
                var type = skeletonAnimation.GetType();
                var initialize = type.GetMethod(
                    "Initialize",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(bool) },
                    null);
                initialize?.Invoke(skeletonAnimation, new object[] { false });
                return GetPropertyValue(skeletonAnimation, "Skeleton") != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[ArknightsACT/TexasBones] Initialize failed: " + exception.GetBaseException().Message,
                    skeletonAnimation);
                return false;
            }
        }

        private static List<BoneRow> ReadBones(object skeleton)
        {
            var rows = new List<BoneRow>();
            var bones = GetPropertyValue(skeleton, "Bones") as IEnumerable;
            if (bones == null)
                return rows;

            var index = 0;
            foreach (var bone in bones)
            {
                if (bone == null)
                    continue;

                var data = GetPropertyValue(bone, "Data");
                var name = ReadString(data, "Name", ReadString(bone, "Name", "<unnamed>"));
                var parent = GetPropertyValue(bone, "Parent");
                var parentData = parent != null ? GetPropertyValue(parent, "Data") : null;
                var parentName = parentData != null
                    ? ReadString(parentData, "Name", "<unknown>")
                    : parent != null ? ReadString(parent, "Name", "<unknown>") : "<root>";

                var depth = 0;
                var cursor = parent;
                while (cursor != null && depth < 64)
                {
                    depth++;
                    cursor = GetPropertyValue(cursor, "Parent");
                }

                rows.Add(new BoneRow
                {
                    Index = index++,
                    Depth = depth,
                    Name = name,
                    ParentName = parentName,
                    SetupPose = FormatPose(data),
                    RuntimePose = FormatPose(bone)
                });
            }

            return rows;
        }

        private static List<SlotRow> ReadSlots(object skeleton)
        {
            var rows = new List<SlotRow>();
            var slots = GetPropertyValue(skeleton, "Slots") as IEnumerable;
            if (slots == null)
                return rows;

            var index = 0;
            foreach (var slot in slots)
            {
                if (slot == null)
                    continue;

                var data = GetPropertyValue(slot, "Data");
                var bone = GetPropertyValue(slot, "Bone");
                var boneData = bone != null ? GetPropertyValue(bone, "Data") : null;
                var attachment = GetPropertyValue(slot, "Attachment");

                rows.Add(new SlotRow
                {
                    Index = index++,
                    SlotName = ReadString(data, "Name", "<unnamed>"),
                    BoneName = boneData != null
                        ? ReadString(boneData, "Name", "<unknown>")
                        : bone != null ? ReadString(bone, "Name", "<unknown>") : "<none>",
                    AttachmentName = attachment != null
                        ? ReadString(attachment, "Name", attachment.GetType().Name)
                        : "<none>"
                });
            }

            return rows;
        }

        private static string FormatPose(object source)
        {
            if (source == null)
                return "-";

            var x = ReadFloat(source, "X");
            var y = ReadFloat(source, "Y");
            var rotation = ReadFloat(source, "Rotation");
            var scaleX = ReadFloat(source, "ScaleX", 1f);
            var scaleY = ReadFloat(source, "ScaleY", 1f);
            return $"{x:0.###},{y:0.###},{rotation:0.###},{scaleX:0.###},{scaleY:0.###}";
        }

        private static void AppendHeuristicMatches(StringBuilder report, List<BoneRow> bones, string label, params string[] tokens)
        {
            var matches = new List<string>();
            for (var i = 0; i < bones.Count; i++)
            {
                var name = bones[i].Name;
                for (var t = 0; t < tokens.Length; t++)
                {
                    if (name.IndexOf(tokens[t], StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    matches.Add(name);
                    break;
                }
            }

            report.Append(label).Append(": ");
            report.AppendLine(matches.Count > 0 ? string.Join(", ", matches) : "<no obvious name matches>");
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null)
                return null;

            try
            {
                return target.GetType()
                    .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(target);
            }
            catch
            {
                return null;
            }
        }

        private static string ReadString(object target, string propertyName, string fallback)
        {
            var value = GetPropertyValue(target, propertyName);
            return value as string ?? fallback;
        }

        private static float ReadFloat(object target, string propertyName, float fallback = 0f)
        {
            var value = GetPropertyValue(target, propertyName);
            if (value is float single)
                return single;
            if (value is double dbl)
                return (float)dbl;
            return fallback;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            var stack = new Stack<string>();
            var cursor = transform;
            while (cursor != null)
            {
                stack.Push(cursor.name);
                cursor = cursor.parent;
            }
            return string.Join("/", stack);
        }

        private sealed class BoneRow
        {
            public int Index;
            public int Depth;
            public string Name;
            public string ParentName;
            public string SetupPose;
            public string RuntimePose;
        }

        private sealed class SlotRow
        {
            public int Index;
            public string SlotName;
            public string BoneName;
            public string AttachmentName;
        }
    }
}
#endif
