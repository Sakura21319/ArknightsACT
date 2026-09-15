using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Prototype action animator for the PRTS Texas combat skeleton.
    ///
    /// Gameplay movement, hitboxes and damage remain authoritative elsewhere. This component
    /// only overrides a small set of Texas Spine bones after SkeletonAnimation has evaluated in
    /// Update, then rebuilds world transforms before SkeletonRenderer creates its mesh in
    /// LateUpdate. This timing is important: applying the pose in LateUpdate would be too late
    /// for the current frame and the source animation would overwrite it again next frame.
    ///
    /// It is a fast prototyping layer that can later be replaced by final hand-authored Spine
    /// clips without changing combat gameplay code.
    /// </summary>
    [DefaultExecutionOrder(1600)]
    [RequireComponent(typeof(PlayerAttackController), typeof(PlayerMotor2D))]
    public sealed class TexasProceduralActionAnimator2D : MonoBehaviour
    {
        private const int MaxBindAttempts = 120;

        private static readonly string[] RequiredBoneNames =
        {
            "F_Waist",
            "F_Chest",
            "F_Head",
            "Ik_F_L_Hand",
            "Ik_F_R_Hand",
            "Ik_F_L_Leg",
            "Ik_F_R_Leg",
            "Ik_F_L_Foot",
            "Ik_F_R_Foot",
            "F_Weapon"
        };

        [Header("Prototype pose tuning")]
        [Tooltip("Global multiplier for procedural bone offsets. Keep at 1 for the authored prototype; reduce after the action silhouette is validated.")]
        [SerializeField, Range(0f, 1.5f)] private float poseStrength = 1f;

        private PlayerAttackController _attack;
        private PlayerMotor2D _motor;
        private Component _skeletonAnimation;
        private object _skeleton;
        private readonly Dictionary<string, BoneHandle> _bones = new(StringComparer.OrdinalIgnoreCase);
        private MethodInfo _updateWorldTransformNoArgs;
        private MethodInfo _updateWorldTransformOneArg;
        private bool _bound;
        private bool _logged;
        private int _bindAttempts;

        private bool _active;
        private bool _pendingCapture;
        private PlayerAttackActionType _action;
        private float _actionStartedAt;
        private readonly Dictionary<string, BoneSnapshot> _actionBase = new(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            _attack = GetComponent<PlayerAttackController>();
            _motor = GetComponent<PlayerMotor2D>();
            TryBind();
        }

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackActionStarted += OnActionStarted;
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackActionStarted -= OnActionStarted;
            RestoreActionBase();
            if (_bound)
                UpdateWorldTransform();
            _active = false;
            _pendingCapture = false;
        }

        private void OnActionStarted(PlayerAttackActionType action)
        {
            _action = action;
            _actionStartedAt = Time.time;
            _active = true;
            _pendingCapture = true;
        }

        /// <summary>
        /// Intentionally Update, not LateUpdate.
        /// DefaultExecutionOrder(1600) keeps this after the normal SkeletonAnimation.Update pass,
        /// while SkeletonRenderer still consumes the modified skeleton later in LateUpdate.
        /// </summary>
        private void Update()
        {
            if (!TryBind())
                return;

            if (_attack == null || !_attack.IsAttacking)
            {
                if (_active)
                {
                    RestoreActionBase();
                    UpdateWorldTransform();
                }
                _active = false;
                _pendingCapture = false;
                return;
            }

            if (!_active)
            {
                _action = _attack.CurrentActionType;
                _actionStartedAt = Time.time;
                _active = true;
                _pendingCapture = true;
            }

            if (_pendingCapture)
            {
                CaptureActionBase();
                _pendingCapture = false;
            }

            var elapsed = Mathf.Max(0f, Time.time - _actionStartedAt);
            ApplyPose(EvaluatePose(_action, elapsed));
            UpdateWorldTransform();
        }

        private bool TryBind()
        {
            if (_bound)
                return true;
            if (_bindAttempts >= MaxBindAttempts)
                return false;

            _bindAttempts++;
            var presentations = GetComponentsInChildren<SpineCharacterPresentation2D>(true);
            var combatPresentation = presentations.FirstOrDefault(item => item != null && item.enabled);
            if (combatPresentation == null)
                return false;

            _skeletonAnimation = combatPresentation.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component != null && component.GetType().FullName == "Spine.Unity.SkeletonAnimation");
            if (_skeletonAnimation == null)
                return false;

            TryInitialize(_skeletonAnimation);
            _skeleton = GetPropertyValue(_skeletonAnimation, "Skeleton");
            if (_skeleton == null)
                return false;

            var allBones = EnumerateBones(_skeleton);
            if (allBones.Count == 0)
                return false;

            _bones.Clear();
            for (var i = 0; i < allBones.Count; i++)
            {
                var bone = allBones[i];
                var name = GetBoneName(bone);
                if (string.IsNullOrWhiteSpace(name) || !RequiredBoneNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    continue;

                var access = new BoneAccessors(bone.GetType());
                if (!access.CanReadWriteCore)
                    continue;
                _bones[name] = new BoneHandle(name, bone, access);
            }

            var missing = RequiredBoneNames.Where(name => !_bones.ContainsKey(name)).ToArray();
            if (missing.Length > 0)
            {
                if (_bindAttempts >= MaxBindAttempts)
                    LogOnce("Texas procedural action bones unavailable. Missing: " + string.Join(", ", missing));
                return false;
            }

            ResolveWorldTransformMethods();
            _bound = true;
            LogOnce("Texas procedural bone animator ready: " + string.Join(", ", RequiredBoneNames));
            return true;
        }

        private void CaptureActionBase()
        {
            _actionBase.Clear();
            foreach (var item in _bones)
            {
                var handle = item.Value;
                _actionBase[item.Key] = new BoneSnapshot(
                    handle.Access.X.Get(handle.Bone),
                    handle.Access.Y.Get(handle.Bone),
                    handle.Access.Rotation.Get(handle.Bone));
            }
        }

        private void RestoreActionBase()
        {
            if (_actionBase.Count == 0)
                return;

            foreach (var item in _actionBase)
            {
                if (!_bones.TryGetValue(item.Key, out var handle))
                    continue;
                handle.Access.X.Set(handle.Bone, item.Value.X);
                handle.Access.Y.Set(handle.Bone, item.Value.Y);
                handle.Access.Rotation.Set(handle.Bone, item.Value.Rotation);
            }
            _actionBase.Clear();
        }

        private void ApplyPose(ActionPose pose)
        {
            var strength = Mathf.Max(0f, poseStrength);
            foreach (var baseItem in _actionBase)
            {
                if (!_bones.TryGetValue(baseItem.Key, out var handle))
                    continue;

                var delta = pose.Get(baseItem.Key);
                var basePose = baseItem.Value;
                handle.Access.X.Set(handle.Bone, basePose.X + delta.X * strength);
                handle.Access.Y.Set(handle.Bone, basePose.Y + delta.Y * strength);
                handle.Access.Rotation.Set(handle.Bone, basePose.Rotation + delta.Rotation * strength);
            }
        }

        private ActionPose EvaluatePose(PlayerAttackActionType action, float elapsed)
        {
            return action switch
            {
                PlayerAttackActionType.GroundLight1 => EvaluateThreePhase(
                    elapsed, 0.070f, 0.125f, 0.245f,
                    PoseLight1Anticipation(), PoseLight1Strike()),

                PlayerAttackActionType.GroundLight2 => EvaluateThreePhase(
                    elapsed, 0.085f, 0.150f, 0.275f,
                    PoseLight2Anticipation(), PoseLight2Strike()),

                PlayerAttackActionType.GroundHeavy3 => EvaluateThreePhase(
                    elapsed, 0.135f, 0.230f, 0.410f,
                    PoseHeavyAnticipation(), PoseHeavyStrike()),

                PlayerAttackActionType.DashSlash => EvaluateThreePhase(
                    elapsed, 0.055f, 0.145f, 0.275f,
                    PoseDashAnticipation(), PoseDashStrike()),

                PlayerAttackActionType.AirSlash => EvaluateThreePhase(
                    elapsed, 0.080f, 0.165f, 0.300f,
                    PoseAirAnticipation(), PoseAirStrike()),

                PlayerAttackActionType.Plunge => EvaluatePlunge(elapsed),
                _ => ActionPose.Zero
            };
        }

        private static ActionPose EvaluateThreePhase(
            float elapsed,
            float anticipationEnd,
            float strikeEnd,
            float recoveryEnd,
            ActionPose anticipation,
            ActionPose strike)
        {
            if (elapsed <= anticipationEnd)
                return ActionPose.Lerp(ActionPose.Zero, anticipation, Smooth01(elapsed / Mathf.Max(0.001f, anticipationEnd)));

            if (elapsed <= strikeEnd)
            {
                var t = (elapsed - anticipationEnd) / Mathf.Max(0.001f, strikeEnd - anticipationEnd);
                return ActionPose.Lerp(anticipation, strike, Smooth01(t));
            }

            if (elapsed <= recoveryEnd)
            {
                var t = (elapsed - strikeEnd) / Mathf.Max(0.001f, recoveryEnd - strikeEnd);
                return ActionPose.Lerp(strike, ActionPose.Zero, Smooth01(t));
            }

            return ActionPose.Zero;
        }

        private ActionPose EvaluatePlunge(float elapsed)
        {
            var tuck = PosePlungeTuck();
            var dive = PosePlungeDive();
            var landed = _attack != null && _attack.IsPlunging && _motor != null && _motor.IsGrounded;

            if (elapsed < 0.075f)
                return ActionPose.Lerp(ActionPose.Zero, tuck, Smooth01(elapsed / 0.075f));

            if (landed)
                return PosePlungeLanding();

            var diveBlend = Mathf.Clamp01((elapsed - 0.075f) / 0.10f);
            return ActionPose.Lerp(tuck, dive, Smooth01(diveBlend));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        // Texas' top-level combat bone is rotated 90 degrees, so local X/Y do not map directly
        // to intuitive world horizontal/vertical. Rotations therefore carry most silhouette change;
        // IK/weapon offsets stay deliberately small to keep the first prototype robust.

        private static ActionPose PoseLight1Anticipation() => new ActionPose()
            .Rot("F_Waist", -5f).Rot("F_Chest", -10f).Rot("F_Head", 4f)
            .Move("Ik_F_L_Hand", -0.03f, 0.09f).Move("Ik_F_R_Hand", -0.02f, 0.14f)
            .Move("F_Weapon", -0.02f, 0.10f).Rot("F_Weapon", 28f)
            .Move("Ik_F_L_Leg", -0.02f, 0.03f).Move("Ik_F_R_Leg", 0.02f, -0.03f);

        private static ActionPose PoseLight1Strike() => new ActionPose()
            .Rot("F_Waist", 7f).Rot("F_Chest", 16f).Rot("F_Head", -5f)
            .Move("Ik_F_L_Hand", 0.06f, -0.12f).Move("Ik_F_R_Hand", 0.08f, -0.25f)
            .Move("F_Weapon", 0.07f, -0.23f).Rot("F_Weapon", -68f)
            .Move("Ik_F_L_Leg", 0.03f, -0.05f).Move("Ik_F_R_Leg", -0.02f, 0.05f);

        private static ActionPose PoseLight2Anticipation() => new ActionPose()
            .Rot("F_Waist", 6f).Rot("F_Chest", 13f).Rot("F_Head", -4f)
            .Move("Ik_F_L_Hand", -0.02f, -0.16f).Move("Ik_F_R_Hand", 0.02f, -0.22f)
            .Move("F_Weapon", 0.01f, -0.20f).Rot("F_Weapon", -48f);

        private static ActionPose PoseLight2Strike() => new ActionPose()
            .Rot("F_Waist", -9f).Rot("F_Chest", -20f).Rot("F_Head", 6f)
            .Move("Ik_F_L_Hand", 0.07f, 0.18f).Move("Ik_F_R_Hand", 0.10f, 0.28f)
            .Move("F_Weapon", 0.08f, 0.25f).Rot("F_Weapon", 82f)
            .Move("Ik_F_L_Leg", -0.03f, 0.06f).Move("Ik_F_R_Leg", 0.04f, -0.06f);

        private static ActionPose PoseHeavyAnticipation() => new ActionPose()
            .Rot("F_Waist", -13f).Rot("F_Chest", -25f).Rot("F_Head", 9f)
            .Move("Ik_F_L_Hand", -0.08f, 0.19f).Move("Ik_F_R_Hand", -0.10f, 0.31f)
            .Move("F_Weapon", -0.08f, 0.28f).Rot("F_Weapon", 62f)
            .Move("Ik_F_L_Leg", -0.10f, 0.05f).Move("Ik_F_R_Leg", -0.08f, -0.05f)
            .Move("Ik_F_L_Foot", -0.07f, 0.04f).Move("Ik_F_R_Foot", -0.05f, -0.04f);

        private static ActionPose PoseHeavyStrike() => new ActionPose()
            .Rot("F_Waist", 15f).Rot("F_Chest", 31f).Rot("F_Head", -10f)
            .Move("Ik_F_L_Hand", 0.11f, -0.21f).Move("Ik_F_R_Hand", 0.16f, -0.36f)
            .Move("F_Weapon", 0.15f, -0.34f).Rot("F_Weapon", -108f)
            .Move("Ik_F_L_Leg", 0.07f, -0.08f).Move("Ik_F_R_Leg", -0.03f, 0.10f)
            .Move("Ik_F_L_Foot", 0.04f, -0.07f).Move("Ik_F_R_Foot", -0.02f, 0.08f);

        private static ActionPose PoseDashAnticipation() => new ActionPose()
            .Rot("F_Waist", -7f).Rot("F_Chest", -13f).Rot("F_Head", 4f)
            .Move("Ik_F_L_Hand", -0.04f, 0.10f).Move("Ik_F_R_Hand", -0.05f, 0.18f)
            .Move("F_Weapon", -0.04f, 0.16f).Rot("F_Weapon", 38f)
            .Move("Ik_F_L_Leg", -0.02f, 0.08f).Move("Ik_F_R_Leg", 0.03f, -0.11f);

        private static ActionPose PoseDashStrike() => new ActionPose()
            .Rot("F_Waist", 13f).Rot("F_Chest", 27f).Rot("F_Head", -8f)
            .Move("Ik_F_L_Hand", 0.13f, -0.20f).Move("Ik_F_R_Hand", 0.18f, -0.38f)
            .Move("F_Weapon", 0.17f, -0.36f).Rot("F_Weapon", -96f)
            .Move("Ik_F_L_Leg", 0.08f, -0.12f).Move("Ik_F_R_Leg", -0.03f, 0.13f)
            .Move("Ik_F_L_Foot", 0.05f, -0.10f).Move("Ik_F_R_Foot", -0.02f, 0.11f);

        private static ActionPose PoseAirAnticipation() => new ActionPose()
            .Rot("F_Waist", -8f).Rot("F_Chest", -16f).Rot("F_Head", 5f)
            .Move("Ik_F_L_Hand", -0.04f, 0.12f).Move("Ik_F_R_Hand", -0.04f, 0.19f)
            .Move("F_Weapon", -0.03f, 0.17f).Rot("F_Weapon", 42f)
            .Move("Ik_F_L_Leg", -0.16f, 0.10f).Move("Ik_F_R_Leg", -0.13f, -0.10f)
            .Move("Ik_F_L_Foot", -0.14f, 0.07f).Move("Ik_F_R_Foot", -0.12f, -0.07f);

        private static ActionPose PoseAirStrike() => new ActionPose()
            .Rot("F_Waist", 10f).Rot("F_Chest", 23f).Rot("F_Head", -7f)
            .Move("Ik_F_L_Hand", 0.09f, -0.17f).Move("Ik_F_R_Hand", 0.13f, -0.31f)
            .Move("F_Weapon", 0.12f, -0.29f).Rot("F_Weapon", -86f)
            .Move("Ik_F_L_Leg", -0.10f, -0.10f).Move("Ik_F_R_Leg", -0.17f, 0.12f)
            .Move("Ik_F_L_Foot", -0.08f, -0.08f).Move("Ik_F_R_Foot", -0.14f, 0.09f);

        private static ActionPose PosePlungeTuck() => new ActionPose()
            .Rot("F_Waist", -10f).Rot("F_Chest", -20f).Rot("F_Head", 8f)
            .Move("Ik_F_L_Hand", -0.06f, 0.11f).Move("Ik_F_R_Hand", -0.08f, 0.20f)
            .Move("F_Weapon", -0.06f, 0.18f).Rot("F_Weapon", 48f)
            .Move("Ik_F_L_Leg", -0.18f, 0.08f).Move("Ik_F_R_Leg", -0.16f, -0.08f)
            .Move("Ik_F_L_Foot", -0.15f, 0.06f).Move("Ik_F_R_Foot", -0.14f, -0.06f);

        private static ActionPose PosePlungeDive() => new ActionPose()
            .Rot("F_Waist", 18f).Rot("F_Chest", 34f).Rot("F_Head", -12f)
            .Move("Ik_F_L_Hand", 0.16f, -0.12f).Move("Ik_F_R_Hand", 0.20f, -0.22f)
            .Move("F_Weapon", 0.18f, -0.20f).Rot("F_Weapon", -112f)
            .Move("Ik_F_L_Leg", 0.10f, 0.07f).Move("Ik_F_R_Leg", 0.12f, -0.07f)
            .Move("Ik_F_L_Foot", 0.14f, 0.05f).Move("Ik_F_R_Foot", 0.15f, -0.05f);

        private static ActionPose PosePlungeLanding() => new ActionPose()
            .Rot("F_Waist", -14f).Rot("F_Chest", -26f).Rot("F_Head", 9f)
            .Move("Ik_F_L_Hand", 0.02f, -0.08f).Move("Ik_F_R_Hand", 0.03f, -0.13f)
            .Move("F_Weapon", 0.02f, -0.12f).Rot("F_Weapon", -74f)
            .Move("Ik_F_L_Leg", -0.13f, 0.12f).Move("Ik_F_R_Leg", -0.11f, -0.12f)
            .Move("Ik_F_L_Foot", -0.10f, 0.10f).Move("Ik_F_R_Foot", -0.09f, -0.10f);

        private void ResolveWorldTransformMethods()
        {
            var methods = _skeleton.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.Name == "UpdateWorldTransform")
                .ToArray();
            _updateWorldTransformNoArgs = methods.FirstOrDefault(method => method.GetParameters().Length == 0);
            _updateWorldTransformOneArg = methods.FirstOrDefault(method => method.GetParameters().Length == 1);
        }

        private void UpdateWorldTransform()
        {
            try
            {
                if (_updateWorldTransformNoArgs != null)
                {
                    _updateWorldTransformNoArgs.Invoke(_skeleton, null);
                    return;
                }

                if (_updateWorldTransformOneArg != null)
                {
                    var parameterType = _updateWorldTransformOneArg.GetParameters()[0].ParameterType;
                    var defaultValue = parameterType.IsEnum ? Enum.ToObject(parameterType, 0) : null;
                    _updateWorldTransformOneArg.Invoke(_skeleton, new[] { defaultValue });
                }
            }
            catch
            {
                // Runtime forks may choose to rebuild transforms later in their own loop.
            }
        }

        private static void TryInitialize(Component skeletonAnimation)
        {
            try
            {
                var initialize = skeletonAnimation.GetType().GetMethod(
                    "Initialize",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(bool) },
                    null);
                initialize?.Invoke(skeletonAnimation, new object[] { false });
            }
            catch
            {
                // Best effort only.
            }
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null)
                return null;
            return target.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static object GetPropertyOrFieldValue(object target, string propertyName, string fieldName)
        {
            if (target == null)
                return null;
            var type = target.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
                return property.GetValue(target);
            return type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);
        }

        private static List<object> EnumerateBones(object skeleton)
        {
            var result = new List<object>();
            var bones = GetPropertyValue(skeleton, "Bones") as IEnumerable;
            if (bones == null)
                return result;
            foreach (var bone in bones)
            {
                if (bone != null)
                    result.Add(bone);
            }
            return result;
        }

        private static string GetBoneName(object bone)
        {
            if (bone == null)
                return null;
            var data = GetPropertyOrFieldValue(bone, "Data", "data");
            var name = GetPropertyOrFieldValue(data, "Name", "name") as string;
            if (!string.IsNullOrWhiteSpace(name))
                return name;
            return GetPropertyOrFieldValue(bone, "Name", "name") as string;
        }

        private void LogOnce(string message)
        {
            if (_logged)
                return;
            _logged = true;
            Debug.Log("[ArknightsACT/TexasBones] " + message, this);
        }

        private readonly struct BoneSnapshot
        {
            public readonly float X;
            public readonly float Y;
            public readonly float Rotation;

            public BoneSnapshot(float x, float y, float rotation)
            {
                X = x;
                Y = y;
                Rotation = rotation;
            }
        }

        private readonly struct BoneDelta
        {
            public readonly float X;
            public readonly float Y;
            public readonly float Rotation;

            public BoneDelta(float x, float y, float rotation)
            {
                X = x;
                Y = y;
                Rotation = rotation;
            }

            public static BoneDelta Lerp(BoneDelta a, BoneDelta b, float t)
            {
                return new BoneDelta(
                    Mathf.LerpUnclamped(a.X, b.X, t),
                    Mathf.LerpUnclamped(a.Y, b.Y, t),
                    Mathf.LerpAngle(a.Rotation, b.Rotation, t));
            }
        }

        private sealed class ActionPose
        {
            private readonly Dictionary<string, BoneDelta> _values = new(StringComparer.OrdinalIgnoreCase);
            public static readonly ActionPose Zero = new();

            public ActionPose Move(string bone, float x, float y)
            {
                var current = Get(bone);
                _values[bone] = new BoneDelta(current.X + x, current.Y + y, current.Rotation);
                return this;
            }

            public ActionPose Rot(string bone, float rotation)
            {
                var current = Get(bone);
                _values[bone] = new BoneDelta(current.X, current.Y, current.Rotation + rotation);
                return this;
            }

            public BoneDelta Get(string bone)
            {
                return _values.TryGetValue(bone, out var value) ? value : default;
            }

            public static ActionPose Lerp(ActionPose a, ActionPose b, float t)
            {
                var result = new ActionPose();
                for (var i = 0; i < RequiredBoneNames.Length; i++)
                {
                    var bone = RequiredBoneNames[i];
                    result._values[bone] = BoneDelta.Lerp(a.Get(bone), b.Get(bone), t);
                }
                return result;
            }
        }

        private sealed class BoneHandle
        {
            public readonly string Name;
            public readonly object Bone;
            public readonly BoneAccessors Access;

            public BoneHandle(string name, object bone, BoneAccessors access)
            {
                Name = name;
                Bone = bone;
                Access = access;
            }
        }

        private sealed class BoneAccessors
        {
            public readonly FloatMember X;
            public readonly FloatMember Y;
            public readonly FloatMember Rotation;
            public bool CanReadWriteCore => X.CanReadWrite && Y.CanReadWrite && Rotation.CanReadWrite;

            public BoneAccessors(Type type)
            {
                X = FloatMember.Create(type, "X", "x");
                Y = FloatMember.Create(type, "Y", "y");
                Rotation = FloatMember.Create(type, "Rotation", "rotation");
            }
        }

        private sealed class FloatMember
        {
            private readonly PropertyInfo _property;
            private readonly FieldInfo _field;
            public bool CanReadWrite => (_property?.CanRead == true && _property.CanWrite) ||
                                        (_field != null && !_field.IsInitOnly);

            private FloatMember(PropertyInfo property, FieldInfo field)
            {
                _property = property;
                _field = field;
            }

            public static FloatMember Create(Type type, string propertyName, string fieldName)
            {
                var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.PropertyType == typeof(float) && property.CanRead && property.CanWrite)
                    return new FloatMember(property, null);

                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(float) && !field.IsInitOnly)
                    return new FloatMember(null, field);

                return new FloatMember(null, null);
            }

            public float Get(object target)
            {
                if (_property != null)
                    return (float)_property.GetValue(target);
                if (_field != null)
                    return (float)_field.GetValue(target);
                return 0f;
            }

            public void Set(object target, float value)
            {
                if (_property?.CanWrite == true)
                    _property.SetValue(target, value);
                else if (_field != null && !_field.IsInitOnly)
                    _field.SetValue(target, value);
            }
        }
    }
}
