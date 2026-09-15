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
    /// Samples the authored Texas PRTS Spine clips at fixed normalized times so new ACT actions
    /// can reuse the original animator's coordinated torso / arm / weapon / leg relationships
    /// instead of guessing local bone offsets by hand.
    /// </summary>
    internal static class TexasSpineAnimationSampler
    {
        private const string MenuPath = "ArknightsACT/Diagnostics/Dump Texas Attack Pose Samples";
        private static readonly string[] ClipNames = { "Attack_Start", "Attack_Loop", "Attack_End", "Skill" };
        private static readonly float[] Samples = { 0f, 0.125f, 0.25f, 0.375f, 0.5f, 0.625f, 0.75f, 0.875f, 1f };
        private static readonly string[] BoneNames =
        {
            "F_Hip", "F_Waist", "F_Chest", "F_Head",
            "F_L_Arm", "F_L_Forearm", "F_L_Hand", "Ik_F_L_Hand",
            "F_R_Arm", "F_R_Forearm", "F_R_Hand", "Ik_F_R_Hand",
            "F_L_Leg", "F_L_Calf", "F_L_Foot", "Ik_F_L_Leg", "Ik_F_L_Foot",
            "F_R_Leg", "F_R_Calf", "F_R_Foot", "Ik_F_R_Leg", "Ik_F_R_Foot",
            "F_Weapon"
        };

        [MenuItem(MenuPath)]
        private static void Dump()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ArknightsACT/TexasAnimSample] Enter Play Mode first, then run this command.");
                return;
            }

            var skeletonAnimation = FindTexasSkeletonAnimation();
            if (skeletonAnimation == null)
            {
                Debug.LogWarning("[ArknightsACT/TexasAnimSample] Texas combat SkeletonAnimation was not found. Rebuild Prototype Scene and enter Play Mode.");
                return;
            }

            TryInitialize(skeletonAnimation);
            var skeleton = GetPropertyOrFieldValue(skeletonAnimation, "Skeleton", "skeleton");
            var state = GetPropertyOrFieldValue(skeletonAnimation, "AnimationState", "animationState");
            if (skeleton == null || state == null)
            {
                Debug.LogWarning("[ArknightsACT/TexasAnimSample] Skeleton or AnimationState is unavailable.", skeletonAnimation);
                return;
            }

            var setAnimation = FindMethod(state.GetType(), "SetAnimation", 3);
            var updateState = FindMethod(state.GetType(), "Update", 1);
            var applyState = state.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "Apply" && m.GetParameters().Length == 1);
            if (setAnimation == null || updateState == null || applyState == null)
            {
                Debug.LogWarning("[ArknightsACT/TexasAnimSample] Required AnimationState methods were not found.", skeletonAnimation);
                return;
            }

            var setToSetupPose = skeleton.GetType().GetMethod("SetToSetupPose", BindingFlags.Instance | BindingFlags.Public);
            var bones = EnumerateBones(skeleton)
                .Select(b => new { Bone = b, Name = GetBoneName(b) })
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToDictionary(x => x.Name, x => x.Bone, StringComparer.OrdinalIgnoreCase);

            var skeletonData = GetPropertyOrFieldValue(skeleton, "Data", "data");
            var animations = GetPropertyOrFieldValue(skeletonData, "Animations", "animations") as IEnumerable;
            var durations = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (animations != null)
            {
                foreach (var animation in animations)
                {
                    if (animation == null) continue;
                    var name = GetPropertyOrFieldValue(animation, "Name", "name") as string;
                    var duration = ReadFloat(animation, "Duration", "duration");
                    if (!string.IsNullOrWhiteSpace(name)) durations[name] = duration;
                }
            }

            var sb = new StringBuilder(65536);
            sb.AppendLine("================ TEXAS AUTHORED ANIMATION POSE SAMPLES ================");
            sb.AppendLine("Format: bone local[x,y,rot] world[x,y,rotX]");
            sb.AppendLine();

            foreach (var requestedClip in ClipNames)
            {
                var clip = durations.Keys.FirstOrDefault(x => string.Equals(x, requestedClip, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(clip))
                {
                    sb.AppendLine($"CLIP {requestedClip}: <missing>");
                    sb.AppendLine();
                    continue;
                }

                var duration = Mathf.Max(0.0001f, durations[clip]);
                sb.AppendLine($"CLIP {clip} duration={duration:0.####}s");
                sb.AppendLine(new string('-', 96));

                try
                {
                    setToSetupPose?.Invoke(skeleton, null);
                    setAnimation.Invoke(state, new object[] { 0, clip, false });
                    applyState.Invoke(state, new[] { skeleton });
                    UpdateWorldTransform(skeleton);

                    var previousTime = 0f;
                    for (var i = 0; i < Samples.Length; i++)
                    {
                        var normalized = Samples[i];
                        var time = duration * normalized;
                        var delta = Mathf.Max(0f, time - previousTime);
                        if (delta > 0f)
                            updateState.Invoke(state, new object[] { delta });
                        applyState.Invoke(state, new[] { skeleton });
                        UpdateWorldTransform(skeleton);

                        sb.AppendLine($"t={normalized:0.###}  sec={time:0.####}");
                        for (var b = 0; b < BoneNames.Length; b++)
                        {
                            var boneName = BoneNames[b];
                            if (!bones.TryGetValue(boneName, out var bone))
                            {
                                sb.AppendLine($"  {boneName}: <missing>");
                                continue;
                            }

                            var x = ReadFloat(bone, "X", "x");
                            var y = ReadFloat(bone, "Y", "y");
                            var rot = ReadFloat(bone, "Rotation", "rotation");
                            var worldX = ReadFloat(bone, "WorldX", "worldX");
                            var worldY = ReadFloat(bone, "WorldY", "worldY");
                            var worldRot = ReadWorldRotationX(bone);
                            sb.AppendLine($"  {boneName}: local[{x:0.###},{y:0.###},{rot:0.###}] world[{worldX:0.###},{worldY:0.###},{worldRot:0.###}]");
                        }
                        sb.AppendLine();
                        previousTime = time;
                    }
                }
                catch (Exception exception)
                {
                    sb.AppendLine("  SAMPLE FAILED: " + exception.GetBaseException().Message);
                }

                sb.AppendLine();
            }

            // Return to a safe idle state after diagnostics.
            try
            {
                setToSetupPose?.Invoke(skeleton, null);
                if (durations.Keys.FirstOrDefault(x => string.Equals(x, "Idle", StringComparison.OrdinalIgnoreCase)) is { } idle)
                    setAnimation.Invoke(state, new object[] { 0, idle, true });
                applyState.Invoke(state, new[] { skeleton });
                UpdateWorldTransform(skeleton);
            }
            catch
            {
                // Diagnostics must not fail just because restore differs across Spine forks.
            }

            sb.AppendLine("=======================================================================");
            var report = sb.ToString();
            EditorGUIUtility.systemCopyBuffer = report;
            Debug.Log("[ArknightsACT/TexasAnimSample] Complete authored animation pose report copied to clipboard.\n" + report, skeletonAnimation);
        }

        private static Component FindTexasSkeletonAnimation()
        {
            var behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            Component fallback = null;
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null || behaviour.GetType().FullName != "Spine.Unity.SkeletonAnimation") continue;
                if (!behaviour.gameObject.scene.IsValid()) continue;
                var path = GetTransformPath(behaviour.transform);
                if (path.IndexOf("Player_Texas", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    path.IndexOf("MotionSource", StringComparison.OrdinalIgnoreCase) < 0)
                    return behaviour;
                if (fallback == null && path.IndexOf("char_102_texas", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    path.IndexOf("MotionSource", StringComparison.OrdinalIgnoreCase) < 0)
                    fallback = behaviour;
            }
            return fallback;
        }

        private static void TryInitialize(Component component)
        {
            try
            {
                var method = component.GetType().GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(bool) }, null);
                method?.Invoke(component, new object[] { false });
            }
            catch { }
        }

        private static MethodInfo FindMethod(Type type, string name, int parameterCount) =>
            type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameterCount);

        private static List<object> EnumerateBones(object skeleton)
        {
            var result = new List<object>();
            if (GetPropertyOrFieldValue(skeleton, "Bones", "bones") is not IEnumerable bones) return result;
            foreach (var bone in bones) if (bone != null) result.Add(bone);
            return result;
        }

        private static string GetBoneName(object bone)
        {
            var data = GetPropertyOrFieldValue(bone, "Data", "data");
            return GetPropertyOrFieldValue(data, "Name", "name") as string
                   ?? GetPropertyOrFieldValue(bone, "Name", "name") as string;
        }

        private static float ReadFloat(object target, string propertyName, string fieldName)
        {
            var value = GetPropertyOrFieldValue(target, propertyName, fieldName);
            if (value is float f) return f;
            if (value is double d) return (float)d;
            return 0f;
        }

        private static float ReadWorldRotationX(object bone)
        {
            var value = GetPropertyOrFieldValue(bone, "WorldRotationX", "worldRotationX");
            if (value is float f) return f;
            if (value is double d) return (float)d;

            var a = ReadFloat(bone, "A", "a");
            var c = ReadFloat(bone, "C", "c");
            return Mathf.Atan2(c, a) * Mathf.Rad2Deg;
        }

        private static object GetPropertyOrFieldValue(object target, string propertyName, string fieldName)
        {
            if (target == null) return null;
            var type = target.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null) return property.GetValue(target);
            return type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);
        }

        private static void UpdateWorldTransform(object skeleton)
        {
            if (skeleton == null) return;
            var methods = skeleton.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.Name == "UpdateWorldTransform").ToArray();
            var noArgs = methods.FirstOrDefault(m => m.GetParameters().Length == 0);
            if (noArgs != null)
            {
                noArgs.Invoke(skeleton, null);
                return;
            }
            var oneArg = methods.FirstOrDefault(m => m.GetParameters().Length == 1);
            if (oneArg == null) return;
            var type = oneArg.GetParameters()[0].ParameterType;
            var value = type.IsEnum ? Enum.ToObject(type, 0) : null;
            oneArg.Invoke(skeleton, new[] { value });
        }

        private static string GetTransformPath(Transform transform)
        {
            var stack = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                stack.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", stack);
        }
    }
}
#endif
