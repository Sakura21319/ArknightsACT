using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Experimental motion-only retargeter for Arknights operator Spine assets.
    ///
    /// Example use case: Texas' combat skeleton has weapons but no Move animation, while
    /// build_char_102_texas has Move but no combat weapons. The base skeleton is kept hidden,
    /// plays Move, and this component copies matching bone DELTAS onto the visible combat
    /// skeleton. Attachments/slots still come from the combat skeleton, so weapons remain.
    ///
    /// The system is deliberately runtime-agnostic and reflection based. If bone coverage is
    /// insufficient it disables itself and the normal procedural locomotion fallback remains.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class SpineBoneMotionRetarget2D : MonoBehaviour
    {
        [SerializeField] private Transform targetPresentationRoot;
        [SerializeField] private Transform sourceMotionRoot;
        [SerializeField] private string preferredMoveAnimation = "Move";
        [SerializeField, Range(0.25f, 1f)] private float minimumBoneCoverage = 0.60f;

        private readonly List<BonePair> _pairs = new();
        private Component _targetSkeletonAnimation;
        private Component _sourceSkeletonAnimation;
        private object _targetSkeleton;
        private object _sourceSkeleton;
        private object _sourceAnimationState;
        private MethodInfo _sourceSetAnimationMethod;
        private MethodInfo _targetUpdateWorldTransformNoArgs;
        private MethodInfo _targetUpdateWorldTransformOneArg;
        private bool _moving;
        private bool _bound;
        private bool _bindAttempted;
        private bool _logged;
        private string _resolvedMoveAnimation = string.Empty;
        private BoneAccessors _boneAccessors;
        private BoneAccessors _dataAccessors;

        public bool IsCompatible => _bound && _pairs.Count > 0;
        public float BoneCoverage { get; private set; }
        public string ResolvedMoveAnimation => _resolvedMoveAnimation;

        public void Configure(Transform targetRoot, Transform sourceRoot, string moveAnimation = "Move")
        {
            targetPresentationRoot = targetRoot;
            sourceMotionRoot = sourceRoot;
            preferredMoveAnimation = string.IsNullOrWhiteSpace(moveAnimation) ? "Move" : moveAnimation;
            HideMotionSourceVisuals();
        }

        private void Awake()
        {
            HideMotionSourceVisuals();
            TryBind();
        }

        public void SetMoving(bool moving)
        {
            if (!TryBind())
                return;

            if (_moving == moving)
                return;

            _moving = moving;
            if (_moving)
                PlaySourceMove();
        }

        private void Update()
        {
            if (!_moving || !TryBind())
                return;

            ApplyRetargetPose();
        }

        private bool TryBind()
        {
            if (_bound)
                return true;
            if (_bindAttempted)
                return false;

            _bindAttempted = true;
            if (targetPresentationRoot == null || sourceMotionRoot == null)
                return false;

            _targetSkeletonAnimation = FindSkeletonAnimation(targetPresentationRoot);
            _sourceSkeletonAnimation = FindSkeletonAnimation(sourceMotionRoot);
            if (_targetSkeletonAnimation == null || _sourceSkeletonAnimation == null)
            {
                LogOnce("motion retarget unavailable: target/source SkeletonAnimation missing");
                return false;
            }

            TryInitialize(_targetSkeletonAnimation);
            TryInitialize(_sourceSkeletonAnimation);

            _targetSkeleton = GetPropertyValue(_targetSkeletonAnimation, "Skeleton");
            _sourceSkeleton = GetPropertyValue(_sourceSkeletonAnimation, "Skeleton");
            if (_targetSkeleton == null || _sourceSkeleton == null)
            {
                LogOnce("motion retarget unavailable: target/source Skeleton not initialized");
                return false;
            }

            _sourceAnimationState = GetPropertyValue(_sourceSkeletonAnimation, "AnimationState");
            if (_sourceAnimationState == null)
                return false;

            _sourceSetAnimationMethod = _sourceAnimationState.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "SetAnimation") return false;
                    var args = method.GetParameters();
                    return args.Length == 3 &&
                           args[0].ParameterType == typeof(int) &&
                           args[1].ParameterType == typeof(string) &&
                           args[2].ParameterType == typeof(bool);
                });
            if (_sourceSetAnimationMethod == null)
                return false;

            if (!BuildBonePairs())
                return false;

            ResolveMoveAnimation();
            if (string.IsNullOrWhiteSpace(_resolvedMoveAnimation))
            {
                LogOnce("motion retarget unavailable: source skeleton has no Move/Move_Loop/Run_Loop animation");
                return false;
            }

            ResolveWorldTransformMethods();
            _bound = true;
            LogOnce(
                $"motion retarget ready: move='{_resolvedMoveAnimation}', matchedBones={_pairs.Count}, " +
                $"coverage={BoneCoverage:P0}");
            return true;
        }

        private bool BuildBonePairs()
        {
            var sourceBones = EnumerateBones(_sourceSkeleton);
            var targetBones = EnumerateBones(_targetSkeleton);
            if (sourceBones.Count == 0 || targetBones.Count == 0)
                return false;

            var sourceByName = sourceBones
                .Select(bone => (bone, name: GetBoneName(bone)))
                .Where(x => !string.IsNullOrWhiteSpace(x.name))
                .ToDictionary(x => x.name, x => x.bone, StringComparer.OrdinalIgnoreCase);
            var targetByName = targetBones
                .Select(bone => (bone, name: GetBoneName(bone)))
                .Where(x => !string.IsNullOrWhiteSpace(x.name))
                .ToDictionary(x => x.name, x => x.bone, StringComparer.OrdinalIgnoreCase);

            _pairs.Clear();
            foreach (var item in sourceByName)
            {
                if (string.Equals(item.Key, "root", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!targetByName.TryGetValue(item.Key, out var targetBone))
                    continue;

                var sourceData = GetPropertyOrFieldValue(item.Value, "Data", "data");
                var targetData = GetPropertyOrFieldValue(targetBone, "Data", "data");
                if (sourceData == null || targetData == null)
                    continue;

                _pairs.Add(new BonePair(item.Key, item.Value, targetBone, sourceData, targetData));
            }

            var comparableSourceCount = Mathf.Max(1, sourceByName.Keys.Count(name => !string.Equals(name, "root", StringComparison.OrdinalIgnoreCase)));
            BoneCoverage = (float)_pairs.Count / comparableSourceCount;
            if (BoneCoverage < minimumBoneCoverage)
            {
                LogOnce($"motion retarget rejected: bone coverage {BoneCoverage:P0} < required {minimumBoneCoverage:P0}");
                _pairs.Clear();
                return false;
            }

            var sampleBone = _pairs[0];
            _boneAccessors = new BoneAccessors(sampleBone.SourceBone.GetType(), true);
            _dataAccessors = new BoneAccessors(sampleBone.SourceData.GetType(), false);
            return _boneAccessors.CanReadCore && _dataAccessors.CanReadCore;
        }

        private void ResolveMoveAnimation()
        {
            var names = GetAnimationNames(_sourceSkeleton);
            _resolvedMoveAnimation = FindExact(names, preferredMoveAnimation)
                                     ?? FindExact(names, "Move")
                                     ?? FindExact(names, "Move_Loop")
                                     ?? FindExact(names, "Run_Loop")
                                     ?? names.FirstOrDefault(name =>
                                         name.IndexOf("move", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                         name.IndexOf("loop", StringComparison.OrdinalIgnoreCase) >= 0)
                                     ?? string.Empty;
        }

        private void PlaySourceMove()
        {
            if (!_bound || string.IsNullOrWhiteSpace(_resolvedMoveAnimation))
                return;

            try
            {
                _sourceSetAnimationMethod.Invoke(_sourceAnimationState, new object[] { 0, _resolvedMoveAnimation, true });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ArknightsACT/Spine] Failed to play retarget source Move: " + exception.GetBaseException().Message, this);
                _moving = false;
            }
        }

        private void ApplyRetargetPose()
        {
            if (_boneAccessors == null || _dataAccessors == null)
                return;

            for (var i = 0; i < _pairs.Count; i++)
            {
                var pair = _pairs[i];
                CopyAdditive(pair, _boneAccessors.X, _dataAccessors.X);
                CopyAdditive(pair, _boneAccessors.Y, _dataAccessors.Y);
                CopyAngle(pair, _boneAccessors.Rotation, _dataAccessors.Rotation);
                CopyScale(pair, _boneAccessors.ScaleX, _dataAccessors.ScaleX);
                CopyScale(pair, _boneAccessors.ScaleY, _dataAccessors.ScaleY);
                CopyAdditive(pair, _boneAccessors.ShearX, _dataAccessors.ShearX);
                CopyAdditive(pair, _boneAccessors.ShearY, _dataAccessors.ShearY);
            }

            UpdateTargetWorldTransform();
        }

        private static void CopyAdditive(BonePair pair, FloatMember boneMember, FloatMember dataMember)
        {
            if (!boneMember.CanReadWrite || !dataMember.CanRead)
                return;

            var sourceCurrent = boneMember.Get(pair.SourceBone);
            var sourceSetup = dataMember.Get(pair.SourceData);
            var targetSetup = dataMember.Get(pair.TargetData);
            boneMember.Set(pair.TargetBone, targetSetup + (sourceCurrent - sourceSetup));
        }

        private static void CopyAngle(BonePair pair, FloatMember boneMember, FloatMember dataMember)
        {
            if (!boneMember.CanReadWrite || !dataMember.CanRead)
                return;

            var sourceCurrent = boneMember.Get(pair.SourceBone);
            var sourceSetup = dataMember.Get(pair.SourceData);
            var targetSetup = dataMember.Get(pair.TargetData);
            boneMember.Set(pair.TargetBone, targetSetup + Mathf.DeltaAngle(sourceSetup, sourceCurrent));
        }

        private static void CopyScale(BonePair pair, FloatMember boneMember, FloatMember dataMember)
        {
            if (!boneMember.CanReadWrite || !dataMember.CanRead)
                return;

            var sourceCurrent = boneMember.Get(pair.SourceBone);
            var sourceSetup = dataMember.Get(pair.SourceData);
            var targetSetup = dataMember.Get(pair.TargetData);
            if (Mathf.Abs(sourceSetup) < 0.0001f)
                return;
            boneMember.Set(pair.TargetBone, targetSetup * (sourceCurrent / sourceSetup));
        }

        private void ResolveWorldTransformMethods()
        {
            var methods = _targetSkeleton.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.Name == "UpdateWorldTransform")
                .ToArray();
            _targetUpdateWorldTransformNoArgs = methods.FirstOrDefault(method => method.GetParameters().Length == 0);
            _targetUpdateWorldTransformOneArg = methods.FirstOrDefault(method => method.GetParameters().Length == 1);
        }

        private void UpdateTargetWorldTransform()
        {
            try
            {
                if (_targetUpdateWorldTransformNoArgs != null)
                {
                    _targetUpdateWorldTransformNoArgs.Invoke(_targetSkeleton, null);
                    return;
                }

                if (_targetUpdateWorldTransformOneArg != null)
                {
                    var parameterType = _targetUpdateWorldTransformOneArg.GetParameters()[0].ParameterType;
                    var defaultValue = parameterType.IsEnum ? Enum.ToObject(parameterType, 0) : null;
                    _targetUpdateWorldTransformOneArg.Invoke(_targetSkeleton, new[] { defaultValue });
                }
            }
            catch
            {
                // Some runtime forks calculate world transforms later in their own LateUpdate.
                // The retargeted local values are still useful, so failure here is non-fatal.
            }
        }

        private void HideMotionSourceVisuals()
        {
            if (sourceMotionRoot == null)
                return;

            foreach (var renderer in sourceMotionRoot.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;

            foreach (var presentation in sourceMotionRoot.GetComponentsInChildren<SpineCharacterPresentation2D>(true))
                presentation.enabled = false;
            foreach (var layout in sourceMotionRoot.GetComponentsInChildren<SpineVisualAutoLayout2D>(true))
                layout.enabled = false;
        }

        private static Component FindSkeletonAnimation(Transform root)
        {
            if (root == null)
                return null;
            return root.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component != null && component.GetType().FullName == "Spine.Unity.SkeletonAnimation");
        }

        private static void TryInitialize(Component skeletonAnimation)
        {
            var method = skeletonAnimation.GetType().GetMethod(
                "Initialize",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(bool) },
                null);
            try
            {
                method?.Invoke(skeletonAnimation, new object[] { false });
            }
            catch
            {
                // Best effort only. Skeleton may already be initialized by its own Awake.
            }
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
            var dataName = GetPropertyOrFieldValue(data, "Name", "name") as string;
            if (!string.IsNullOrWhiteSpace(dataName))
                return dataName;
            return GetPropertyOrFieldValue(bone, "Name", "name") as string;
        }

        private static List<string> GetAnimationNames(object skeleton)
        {
            var result = new List<string>();
            var data = GetPropertyValue(skeleton, "Data");
            var animations = GetPropertyValue(data, "Animations") as IEnumerable;
            if (animations == null)
                return result;
            foreach (var animation in animations)
            {
                var name = GetPropertyOrFieldValue(animation, "Name", "name") as string;
                if (!string.IsNullOrWhiteSpace(name))
                    result.Add(name);
            }
            return result;
        }

        private static string FindExact(IEnumerable<string> names, string wanted)
        {
            if (string.IsNullOrWhiteSpace(wanted))
                return null;
            return names.FirstOrDefault(name => string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase));
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null)
                return null;
            return target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);
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

        private void LogOnce(string message)
        {
            if (_logged)
                return;
            _logged = true;
            Debug.Log("[ArknightsACT/Spine] " + message, this);
        }

        private sealed class BonePair
        {
            public readonly string Name;
            public readonly object SourceBone;
            public readonly object TargetBone;
            public readonly object SourceData;
            public readonly object TargetData;

            public BonePair(string name, object sourceBone, object targetBone, object sourceData, object targetData)
            {
                Name = name;
                SourceBone = sourceBone;
                TargetBone = targetBone;
                SourceData = sourceData;
                TargetData = targetData;
            }
        }

        private sealed class BoneAccessors
        {
            public readonly FloatMember X;
            public readonly FloatMember Y;
            public readonly FloatMember Rotation;
            public readonly FloatMember ScaleX;
            public readonly FloatMember ScaleY;
            public readonly FloatMember ShearX;
            public readonly FloatMember ShearY;

            public bool CanReadCore => X.CanRead && Y.CanRead && Rotation.CanRead;

            public BoneAccessors(Type type, bool requireWrite)
            {
                X = FloatMember.Create(type, "X", "x", requireWrite);
                Y = FloatMember.Create(type, "Y", "y", requireWrite);
                Rotation = FloatMember.Create(type, "Rotation", "rotation", requireWrite);
                ScaleX = FloatMember.Create(type, "ScaleX", "scaleX", requireWrite);
                ScaleY = FloatMember.Create(type, "ScaleY", "scaleY", requireWrite);
                ShearX = FloatMember.Create(type, "ShearX", "shearX", requireWrite);
                ShearY = FloatMember.Create(type, "ShearY", "shearY", requireWrite);
            }
        }

        private sealed class FloatMember
        {
            private readonly PropertyInfo _property;
            private readonly FieldInfo _field;

            public bool CanRead => _property?.CanRead == true || _field != null;
            public bool CanWrite => _property?.CanWrite == true || (_field != null && !_field.IsInitOnly);
            public bool CanReadWrite => CanRead && CanWrite;

            private FloatMember(PropertyInfo property, FieldInfo field)
            {
                _property = property;
                _field = field;
            }

            public static FloatMember Create(Type type, string propertyName, string fieldName, bool requireWrite)
            {
                var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.PropertyType == typeof(float) && (!requireWrite || property.CanWrite))
                    return new FloatMember(property, null);

                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(float) && (!requireWrite || !field.IsInitOnly))
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
