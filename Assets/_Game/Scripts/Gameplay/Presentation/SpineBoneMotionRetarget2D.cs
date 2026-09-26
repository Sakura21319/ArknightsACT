using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Motion source bridge for Arknights operator Spine assets.
    ///
    /// Default mode keeps the build skeleton hidden and copies matching bone deltas onto the
    /// combat skeleton. For skins whose BaseMotion changes slots/attachments (eyes, seated art,
    /// costume parts, etc.), full-source mode swaps the complete build Spine into view while
    /// Move / Relax / Interact / Sit / Sleep / Special are playing, then restores combat Spine.
    ///
    /// Full-source mode does not require combat/build bone coverage because it renders the authored
    /// BaseMotion skeleton directly; classic bone-retarget mode still requires sufficient coverage.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class SpineBoneMotionRetarget2D : MonoBehaviour
    {
        private const int MaxBindAttempts = 120;

        [SerializeField] private Transform targetPresentationRoot;
        [SerializeField] private Transform sourceMotionRoot;
        [SerializeField] private string preferredMoveAnimation = "Move";
        [SerializeField, Range(0.25f, 1f)] private float minimumBoneCoverage = 0.60f;
        [SerializeField] private string[] excludedBoneNameTokens = Array.Empty<string>();
        [SerializeField] private bool useFullSourceVisuals;
        [SerializeField, Min(0.1f)] private float fullSourceScaleMultiplier = 1f;
        [SerializeField] private Vector3 fullSourceLocalOffset = Vector3.zero;

        private readonly List<BonePair> _pairs = new();
        private Renderer[] _sourceRenderers = Array.Empty<Renderer>();
        private Renderer[] _targetRenderers = Array.Empty<Renderer>();
        private Component _targetSkeletonAnimation;
        private Component _sourceSkeletonAnimation;
        private object _targetSkeleton;
        private object _sourceSkeleton;
        private object _sourceAnimationState;
        private MethodInfo _sourceSetAnimationMethod;
        private MethodInfo _targetUpdateWorldTransformNoArgs;
        private MethodInfo _targetUpdateWorldTransformOneArg;
        private bool _moving;
        private bool _motionActionActive;
        private bool _motionActionLoop;
        private float _motionActionEndsAt;
        private string _activeMotionAction = string.Empty;
        private bool _bound;
        private bool _logged;
        private int _bindAttempts;
        private string _resolvedMoveAnimation = string.Empty;
        private BoneAccessors _boneAccessors;
        private BoneAccessors _dataAccessors;

        public bool IsCompatible => TryBind();
        public float BoneCoverage { get; private set; }
        public string ResolvedMoveAnimation => _resolvedMoveAnimation;
        public bool IsPlayingMotionAction => _motionActionActive;
        public string ActiveMotionAction => _activeMotionAction;

        public void ConfigureFullSourceAlignment(float scaleMultiplier, Vector3 localOffset)
        {
            fullSourceScaleMultiplier = scaleMultiplier > 0f ? Mathf.Max(0.1f, scaleMultiplier) : 1f;
            fullSourceLocalOffset = localOffset;
            if (_bound && useFullSourceVisuals && (_moving || _motionActionActive))
                SyncSourceVisualPose();
        }

        public void EnableFullSourceVisuals(bool enabled)
        {
            useFullSourceVisuals = enabled;
            _bindAttempts = 0;

            if (sourceMotionRoot != null && targetPresentationRoot != null)
            {
                sourceMotionRoot.localPosition = targetPresentationRoot.localPosition;
                sourceMotionRoot.localRotation = targetPresentationRoot.localRotation;
            }

            HideMotionSourceVisuals();
            if (_bound)
            {
                CacheVisualRenderers();
                SetSourceVisualMode(enabled && (_moving || _motionActionActive));
            }
        }

        public void Configure(
            Transform targetRoot,
            Transform sourceRoot,
            string moveAnimation = "Move",
            string[] excludedBoneTokens = null,
            bool showFullSourceVisuals = false)
        {
            targetPresentationRoot = targetRoot;
            sourceMotionRoot = sourceRoot;
            preferredMoveAnimation = string.IsNullOrWhiteSpace(moveAnimation) ? "Move" : moveAnimation;
            excludedBoneNameTokens = excludedBoneTokens ?? Array.Empty<string>();
            useFullSourceVisuals = showFullSourceVisuals;
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

            if (moving && _motionActionActive)
                ClearMotionActionState();

            if (_moving == moving)
            {
                if (useFullSourceVisuals)
                    SetSourceVisualMode(_moving || _motionActionActive);
                return;
            }

            _moving = moving;
            if (_moving)
            {
                PlaySourceMove();
                if (useFullSourceVisuals)
                    SetSourceVisualMode(true);
            }
            else if (useFullSourceVisuals && !_motionActionActive)
            {
                SetSourceVisualMode(false);
            }
        }

        public bool HasSourceAnimation(string animation)
        {
            if (!TryBind() || string.IsNullOrWhiteSpace(animation))
                return false;

            return !string.IsNullOrWhiteSpace(FindExact(GetAnimationNames(_sourceSkeleton), animation));
        }

        public bool PlayMotionAction(string animation, bool loop)
        {
            if (!TryBind() || string.IsNullOrWhiteSpace(animation))
                return false;

            var resolved = FindExact(GetAnimationNames(_sourceSkeleton), animation);
            if (string.IsNullOrWhiteSpace(resolved) || !PlaySourceAnimation(resolved, loop))
                return false;

            _moving = false;
            _motionActionActive = true;
            if (useFullSourceVisuals)
                SetSourceVisualMode(true);
            _motionActionLoop = loop;
            _activeMotionAction = resolved;
            if (loop)
            {
                _motionActionEndsAt = float.PositiveInfinity;
            }
            else if (TryGetSourceAnimationDuration(resolved, out var rawDuration))
            {
                _motionActionEndsAt = Time.time + rawDuration /
                    Mathf.Max(0.01f, SpineCharacterPresentation2D.ImportedAnimationPlaybackSpeed);
            }
            else
            {
                _motionActionEndsAt = Time.time + 1f;
            }

            return true;
        }

        public void StopMotionAction()
        {
            if (_motionActionActive)
                ClearMotionActionState();
        }

        private void ClearMotionActionState()
        {
            _motionActionActive = false;
            _motionActionLoop = false;
            _motionActionEndsAt = 0f;
            _activeMotionAction = string.Empty;
            if (useFullSourceVisuals && !_moving)
                SetSourceVisualMode(false);
        }

        private void Update()
        {
            if (!TryBind())
                return;

            if (_motionActionActive)
            {
                if (!_motionActionLoop && Time.time >= _motionActionEndsAt)
                {
                    ClearMotionActionState();
                    return;
                }

                if (useFullSourceVisuals)
                {
                    SyncSourceVisualPose();
                    return;
                }

                ApplyRetargetPose();
                return;
            }

            if (_moving)
            {
                if (useFullSourceVisuals)
                    SyncSourceVisualPose();
                else
                    ApplyRetargetPose();
            }
        }

        private void OnDisable()
        {
            if (useFullSourceVisuals)
                SetSourceVisualMode(false);
        }

        private bool TryBind()
        {
            if (_bound)
                return true;
            if (_bindAttempts >= MaxBindAttempts)
                return false;

            _bindAttempts++;
            if (targetPresentationRoot == null || sourceMotionRoot == null)
                return false;

            _targetSkeletonAnimation = FindSkeletonAnimation(targetPresentationRoot);
            _sourceSkeletonAnimation = FindSkeletonAnimation(sourceMotionRoot);
            if (_targetSkeletonAnimation == null || _sourceSkeletonAnimation == null)
                return false;

            TryInitialize(_targetSkeletonAnimation);
            TryInitialize(_sourceSkeletonAnimation);

            _targetSkeleton = GetPropertyValue(_targetSkeletonAnimation, "Skeleton");
            _sourceSkeleton = GetPropertyValue(_sourceSkeletonAnimation, "Skeleton");
            if (_targetSkeleton == null || _sourceSkeleton == null)
                return false;

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

            var boneRetargetAvailable = false;
            if (!useFullSourceVisuals)
            {
                boneRetargetAvailable = BuildBonePairs();
                if (!boneRetargetAvailable)
                {
                    // Once actual initialized skeletons were compared, low bone coverage is a
                    // structural incompatibility rather than an initialization race.
                    _bindAttempts = MaxBindAttempts;
                    return false;
                }
            }

            ResolveMoveAnimation();
            if (string.IsNullOrWhiteSpace(_resolvedMoveAnimation))
            {
                _bindAttempts = MaxBindAttempts;
                LogOnce("motion retarget unavailable: source skeleton has no Move/Move_Loop/Run_Loop animation");
                return false;
            }

            if (boneRetargetAvailable)
                ResolveWorldTransformMethods();
            CacheVisualRenderers();
            _bound = true;
            if (useFullSourceVisuals)
                SetSourceVisualMode(false);
            LogOnce(
                $"motion source ready: move='{_resolvedMoveAnimation}', fullVisual={useFullSourceVisuals}, " +
                $"matchedBones={_pairs.Count}, coverage={BoneCoverage:P0}");
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
                .GroupBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().bone, StringComparer.OrdinalIgnoreCase);
            var targetByName = targetBones
                .Select(bone => (bone, name: GetBoneName(bone)))
                .Where(x => !string.IsNullOrWhiteSpace(x.name))
                .GroupBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().bone, StringComparer.OrdinalIgnoreCase);

            _pairs.Clear();
            foreach (var item in sourceByName)
            {
                if (string.Equals(item.Key, "root", StringComparison.OrdinalIgnoreCase) ||
                    IsExcludedBone(item.Key))
                    continue;
                if (!targetByName.TryGetValue(item.Key, out var targetBone))
                    continue;

                var sourceData = GetPropertyOrFieldValue(item.Value, "Data", "data");
                var targetData = GetPropertyOrFieldValue(targetBone, "Data", "data");
                if (sourceData == null || targetData == null)
                    continue;

                _pairs.Add(new BonePair(item.Key, item.Value, targetBone, sourceData, targetData));
            }

            var comparableSourceCount = Mathf.Max(
                1,
                sourceByName.Keys.Count(name =>
                    !string.Equals(name, "root", StringComparison.OrdinalIgnoreCase) &&
                    !IsExcludedBone(name)));
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

        private bool IsExcludedBone(string boneName)
        {
            if (string.IsNullOrWhiteSpace(boneName) || excludedBoneNameTokens == null)
                return false;

            for (var i = 0; i < excludedBoneNameTokens.Length; i++)
            {
                var token = excludedBoneNameTokens[i];
                if (!string.IsNullOrWhiteSpace(token) &&
                    boneName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
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
            if (!PlaySourceAnimation(_resolvedMoveAnimation, true))
                _moving = false;
        }

        private bool PlaySourceAnimation(string animation, bool loop)
        {
            if (!_bound || string.IsNullOrWhiteSpace(animation))
                return false;

            try
            {
                var entry = _sourceSetAnimationMethod.Invoke(
                    _sourceAnimationState,
                    new object[] { 0, animation, loop });
                if (entry != null)
                {
                    var entryType = entry.GetType();
                    var property = entryType.GetProperty(
                        "TimeScale",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (property != null && property.CanWrite)
                    {
                        property.SetValue(
                            entry,
                            SpineCharacterPresentation2D.ImportedAnimationPlaybackSpeed);
                    }
                    else
                    {
                        var field = entryType.GetField(
                            "timeScale",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        field?.SetValue(
                            entry,
                            SpineCharacterPresentation2D.ImportedAnimationPlaybackSpeed);
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[ArknightsACT/Spine] Failed to play retarget source animation '{animation}': " +
                    exception.GetBaseException().Message,
                    this);
                return false;
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

            var sourceSkeleton = _sourceSkeletonAnimation ?? FindSkeletonAnimation(sourceMotionRoot);
            var sourceRenderer = sourceSkeleton != null ? sourceSkeleton.GetComponent<Renderer>() : null;
            _sourceRenderers = sourceRenderer != null
                ? new[] { sourceRenderer }
                : Array.Empty<Renderer>();
            if (sourceRenderer != null)
                sourceRenderer.enabled = false;

            foreach (var presentation in sourceMotionRoot.GetComponentsInChildren<SpineCharacterPresentation2D>(true))
                presentation.enabled = false;
            foreach (var layout in sourceMotionRoot.GetComponentsInChildren<SpineVisualAutoLayout2D>(true))
                layout.enabled = false;
        }

        private void CacheVisualRenderers()
        {
            // Only swap the actual Spine mesh renderers. Never toggle every Renderer below the
            // presentation roots: runtime character FX, weapon glows, trails and other authored
            // child renderers may live there and must remain independently controlled.
            var sourceRenderer = _sourceSkeletonAnimation != null
                ? _sourceSkeletonAnimation.GetComponent<Renderer>()
                : null;
            var targetRenderer = _targetSkeletonAnimation != null
                ? _targetSkeletonAnimation.GetComponent<Renderer>()
                : null;

            _sourceRenderers = sourceRenderer != null
                ? new[] { sourceRenderer }
                : Array.Empty<Renderer>();
            _targetRenderers = targetRenderer != null
                ? new[] { targetRenderer }
                : Array.Empty<Renderer>();

            if (!useFullSourceVisuals || sourceRenderer == null || targetRenderer == null)
                return;

            sourceRenderer.sortingLayerID = targetRenderer.sortingLayerID;
            sourceRenderer.sortingOrder = targetRenderer.sortingOrder;
        }

        private void SetSourceVisualMode(bool sourceVisible)
        {
            if (!useFullSourceVisuals)
                return;

            if (_sourceRenderers == null || _sourceRenderers.Length == 0 ||
                _targetRenderers == null || _targetRenderers.Length == 0)
                CacheVisualRenderers();

            for (var i = 0; i < _sourceRenderers.Length; i++)
                if (_sourceRenderers[i] != null)
                    _sourceRenderers[i].enabled = sourceVisible;
            for (var i = 0; i < _targetRenderers.Length; i++)
                if (_targetRenderers[i] != null)
                    _targetRenderers[i].enabled = !sourceVisible;

            if (sourceVisible)
                SyncSourceVisualPose();
        }

        private void SyncSourceVisualPose()
        {
            if (!useFullSourceVisuals || _sourceSkeletonAnimation == null || _targetSkeletonAnimation == null)
                return;

            var sourceVisual = _sourceSkeletonAnimation.transform;
            var targetVisual = _targetSkeletonAnimation.transform;
            if (sourceVisual == null || targetVisual == null)
                return;

            // Combat presentation is runtime-calibrated by SpineVisualAutoLayout2D, while the
            // hidden BaseMotion layout is intentionally disabled. Copy the combat visual's final
            // calibrated transform so switching to full build Spine does not make the character
            // suddenly shrink back to the BaseMotion prefab's safeInitialScale.
            var targetScale = targetVisual.localScale;
            var targetSign = targetScale.x < 0f ? -1f : 1f;
            // Newly added serialized floats are zero on older scene instances; zero means
            // neutral 1x here so migration never shrinks an existing character.
            var multiplier = fullSourceScaleMultiplier > 0f
                ? Mathf.Max(0.1f, fullSourceScaleMultiplier)
                : 1f;
            sourceVisual.localScale = new Vector3(
                Mathf.Abs(targetScale.x) * targetSign * multiplier,
                Mathf.Abs(targetScale.y) * multiplier,
                Mathf.Abs(targetScale.z) * multiplier);
            sourceVisual.localPosition = targetVisual.localPosition + fullSourceLocalOffset;
            sourceVisual.localRotation = targetVisual.localRotation;
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

        private bool TryGetSourceAnimationDuration(string animationName, out float duration)
        {
            duration = 0f;
            var data = GetPropertyValue(_sourceSkeleton, "Data");
            var animations = GetPropertyValue(data, "Animations") as IEnumerable;
            if (animations == null)
                return false;

            foreach (var animation in animations)
            {
                var name = GetPropertyOrFieldValue(animation, "Name", "name") as string;
                if (!string.Equals(name, animationName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = GetPropertyOrFieldValue(animation, "Duration", "duration");
                if (value == null)
                    return false;

                try
                {
                    duration = Mathf.Max(0.01f, Convert.ToSingle(value));
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
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
