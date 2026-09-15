using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Action-prototyping animator for the PRTS Texas combat skeleton.
    ///
    /// The first prototype drove IK targets and F_Weapon directly. Sampling the authored
    /// Attack_Start / Attack_Loop clips showed that Texas' real attacks do the opposite:
    /// hand IK targets stay almost static, F_Weapon local pose is effectively static, while
    /// Arm -> Forearm -> Hand bones carry the actual slash. This version follows that authored
    /// rig logic and uses Idle as a neutral base so our action pose does not fight Attack_Loop.
    ///
    /// Gameplay movement, hitboxes and damage remain authoritative outside this component.
    /// Final hand-authored Spine clips can replace this class without changing combat code.
    /// </summary>
    [DefaultExecutionOrder(1600)]
    [RequireComponent(typeof(PlayerAttackController), typeof(PlayerMotor2D))]
    public sealed class TexasProceduralActionAnimator2D : MonoBehaviour
    {
        private const int MaxBindAttempts = 120;

        // Deliberately use the same direct chains that the authored PRTS attacks animate.
        // IK hand targets and F_Weapon are intentionally NOT controlled here.
        private static readonly string[] RequiredBoneNames =
        {
            "F_Waist", "F_Chest", "F_Head",
            "F_L_Arm", "F_L_Forearm", "F_L_Hand",
            "F_R_Arm", "F_R_Forearm", "F_R_Hand",
            "F_L_Leg", "F_L_Calf", "F_L_Foot",
            "F_R_Leg", "F_R_Calf", "F_R_Foot"
        };

        [Header("Prototype pose tuning")]
        [Tooltip("Global multiplier for authored pose deltas. Keep at 1 while validating silhouettes.")]
        [SerializeField, Range(0f, 1.35f)] private float poseStrength = 1f;

        [Tooltip("How much of the pre-impact time is used to visibly coil before accelerating into the hit.")]
        [SerializeField, Range(0.35f, 0.8f)] private float anticipationShare = 0.58f;

        [Tooltip("Heavy finisher holds anticipation longer to read as a deliberate finisher.")]
        [SerializeField, Range(0.45f, 0.88f)] private float heavyAnticipationShare = 0.70f;

        private PlayerAttackController _attack;
        private PlayerMotor2D _motor;
        private SpineCharacterPresentation2D _presentation;
        private SpineBoneMotionRetarget2D _motionRetarget;
        private IAttackTimingProvider _timingProvider;

        private Component _skeletonAnimation;
        private object _skeleton;
        private object _animationState;
        private PropertyInfo _timeScaleProperty;
        private FieldInfo _timeScaleField;
        private MethodInfo _updateWorldTransformNoArgs;
        private MethodInfo _updateWorldTransformOneArg;

        private readonly Dictionary<string, BoneHandle> _bones = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BoneSnapshot> _actionBase = new(StringComparer.OrdinalIgnoreCase);

        private bool _bound;
        private bool _logged;
        private int _bindAttempts;
        private bool _active;
        private bool _pendingCapture;
        private int _captureNotBeforeFrame;
        private PlayerAttackActionType _action;
        private float _actionStartedAt;

        private void Awake()
        {
            _attack = GetComponent<PlayerAttackController>();
            _motor = GetComponent<PlayerMotor2D>();
            _motionRetarget = GetComponent<SpineBoneMotionRetarget2D>();

            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IAttackTimingProvider provider)
                {
                    _timingProvider = provider;
                    break;
                }
            }

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

            _active = false;
            _pendingCapture = false;
            _actionBase.Clear();
            SetAnimationTimeScale(1f);
        }

        private void OnActionStarted(PlayerAttackActionType action)
        {
            _action = action;
            _actionStartedAt = Time.time;
            _active = true;
            _pendingCapture = true;

            // AttackStarted fires before AttackActionStarted. The generic presentation driver may
            // briefly start Attack_Loop; immediately replace it with Idle so our authored pose has
            // one stable source instead of two attack animations fighting over the same bones.
            PrepareNeutralBase();

            // Idle is selected now but SkeletonAnimation has already evaluated this frame on many
            // runtime orders. Wait one frame so the captured base really is the neutral Idle pose.
            _captureNotBeforeFrame = Time.frameCount + 1;
        }

        /// <summary>
        /// Runs after normal SkeletonAnimation.Update and the movement retargeter, but before the
        /// SkeletonRenderer builds its LateUpdate mesh.
        /// </summary>
        private void Update()
        {
            if (!TryBind())
                return;

            if (_attack == null || !_attack.IsAttacking)
            {
                if (_active)
                {
                    _active = false;
                    _pendingCapture = false;
                    _actionBase.Clear();
                    SetAnimationTimeScale(1f);
                }
                return;
            }

            if (!_active)
            {
                _action = _attack.CurrentActionType;
                _actionStartedAt = Time.time;
                _active = true;
                _pendingCapture = true;
                _captureNotBeforeFrame = Time.frameCount + 1;
            }

            PrepareNeutralBase();

            if (_pendingCapture)
            {
                if (Time.frameCount < _captureNotBeforeFrame)
                    return;

                CaptureActionBase();
                _pendingCapture = false;
            }

            ResolveTiming(_action, out var impactSeconds, out var cycleSeconds);
            var elapsed = Mathf.Max(0f, Time.time - _actionStartedAt);
            ApplyPose(EvaluatePose(_action, elapsed, impactSeconds, cycleSeconds));
            UpdateWorldTransform();
        }

        private void PrepareNeutralBase()
        {
            if (!TryBind())
                return;

            // Do not allow the hidden dorm/base Move retarget to mix walking limbs into an attack.
            _motionRetarget?.SetMoving(false);

            if (_presentation != null)
            {
                _presentation.CancelBasicAttackPresentation();
                _presentation.SetExternalLocomotionActive(false);
                _presentation.SetLocomotion(false, _motor != null ? _motor.FacingSign : 1);
            }

            // SpineAttackPlaybackSpeed2D boosts AnimationState on AttackStarted. Our procedural
            // actions use gameplay timing directly, so keep the neutral Idle source at 1x.
            SetAnimationTimeScale(1f);
        }

        private bool TryBind()
        {
            if (_bound)
                return true;
            if (_bindAttempts >= MaxBindAttempts)
                return false;

            _bindAttempts++;
            var presentations = GetComponentsInChildren<SpineCharacterPresentation2D>(true);
            _presentation = presentations.FirstOrDefault(item => item != null && item.enabled);
            if (_presentation == null)
                return false;

            _skeletonAnimation = _presentation.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component != null && component.GetType().FullName == "Spine.Unity.SkeletonAnimation");
            if (_skeletonAnimation == null)
                return false;

            TryInitialize(_skeletonAnimation);
            _skeleton = GetPropertyValue(_skeletonAnimation, "Skeleton");
            _animationState = GetPropertyValue(_skeletonAnimation, "AnimationState");
            if (_skeleton == null)
                return false;

            ResolveTimeScaleAccessors();

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
                    LogOnce("Texas sampled-chain animator unavailable. Missing: " + string.Join(", ", missing));
                return false;
            }

            ResolveWorldTransformMethods();
            _bound = true;
            LogOnce(
                "Texas sampled-chain animator ready. Direct chains: torso, L/R arm, L/R leg. " +
                "Hand IK and F_Weapon intentionally left to the authored rig.");
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

        private void ResolveTiming(PlayerAttackActionType action, out float impactSeconds, out float cycleSeconds)
        {
            impactSeconds = 0.22f;
            cycleSeconds = 0.42f;

            if (_timingProvider != null &&
                _timingProvider.TryGetBasicAttackTiming(out var baseImpact, out var baseCycle) &&
                baseCycle > 0.01f)
            {
                impactSeconds = baseImpact;
                cycleSeconds = baseCycle;
            }

            switch (action)
            {
                case PlayerAttackActionType.DashSlash:
                    cycleSeconds = Mathf.Max(0.14f, cycleSeconds * 0.92f);
                    impactSeconds = cycleSeconds * 0.50f;
                    break;

                case PlayerAttackActionType.AirSlash:
                    cycleSeconds = Mathf.Max(0.14f, cycleSeconds * 0.86f);
                    impactSeconds = cycleSeconds * 0.44f;
                    break;

                case PlayerAttackActionType.Plunge:
                    // Plunge damage is authoritative on actual landing, not a fixed timer.
                    impactSeconds = 0.07f;
                    cycleSeconds = 1.5f;
                    break;
            }
        }

        private ActionPose EvaluatePose(
            PlayerAttackActionType action,
            float elapsed,
            float impactSeconds,
            float cycleSeconds)
        {
            if (action == PlayerAttackActionType.Plunge)
                return EvaluatePlunge(elapsed);

            ActionPose anticipation;
            ActionPose strike;
            ActionPose follow;
            var share = anticipationShare;

            switch (action)
            {
                case PlayerAttackActionType.GroundLight1:
                    anticipation = PoseLight1Anticipation();
                    strike = PoseLight1Strike();
                    follow = PoseLight1Follow();
                    break;

                case PlayerAttackActionType.GroundLight2:
                    anticipation = PoseLight2Anticipation();
                    strike = PoseLight2Strike();
                    follow = PoseLight2Follow();
                    break;

                case PlayerAttackActionType.GroundHeavy3:
                    anticipation = PoseHeavyAnticipation();
                    strike = PoseHeavyStrike();
                    follow = PoseHeavyFollow();
                    share = heavyAnticipationShare;
                    break;

                case PlayerAttackActionType.DashSlash:
                    anticipation = PoseDashAnticipation();
                    strike = PoseDashStrike();
                    follow = PoseDashFollow();
                    break;

                case PlayerAttackActionType.AirSlash:
                    anticipation = PoseAirAnticipation();
                    strike = PoseAirStrike();
                    follow = PoseAirFollow();
                    break;

                default:
                    return ActionPose.Zero;
            }

            return EvaluateFourPhase(
                elapsed,
                impactSeconds,
                cycleSeconds,
                Mathf.Clamp01(share),
                anticipation,
                strike,
                follow);
        }

        private static ActionPose EvaluateFourPhase(
            float elapsed,
            float impactSeconds,
            float cycleSeconds,
            float anticipationShareValue,
            ActionPose anticipation,
            ActionPose strike,
            ActionPose follow)
        {
            impactSeconds = Mathf.Max(0.02f, impactSeconds);
            cycleSeconds = Mathf.Max(impactSeconds + 0.04f, cycleSeconds);

            var anticipationEnd = Mathf.Clamp(
                impactSeconds * anticipationShareValue,
                0.02f,
                impactSeconds - 0.015f);
            var followEnd = Mathf.Clamp(
                impactSeconds + Mathf.Max(0.06f, (cycleSeconds - impactSeconds) * 0.48f),
                impactSeconds + 0.02f,
                cycleSeconds - 0.015f);

            if (elapsed <= anticipationEnd)
            {
                var t = elapsed / anticipationEnd;
                return ActionPose.Lerp(ActionPose.Zero, anticipation, Smooth01(t));
            }

            if (elapsed <= impactSeconds)
            {
                var t = (elapsed - anticipationEnd) / Mathf.Max(0.001f, impactSeconds - anticipationEnd);
                // Accelerate hard into the actual gameplay hit frame.
                return ActionPose.Lerp(anticipation, strike, EaseInCubic(t));
            }

            if (elapsed <= followEnd)
            {
                var t = (elapsed - impactSeconds) / Mathf.Max(0.001f, followEnd - impactSeconds);
                return ActionPose.Lerp(strike, follow, Smooth01(t));
            }

            if (elapsed <= cycleSeconds)
            {
                var t = (elapsed - followEnd) / Mathf.Max(0.001f, cycleSeconds - followEnd);
                return ActionPose.Lerp(follow, ActionPose.Zero, Smooth01(t));
            }

            return ActionPose.Zero;
        }

        private ActionPose EvaluatePlunge(float elapsed)
        {
            var tuck = PosePlungeTuck();
            var dive = PosePlungeDive();
            var landed = _attack != null && _attack.IsPlunging && _motor != null && _motor.IsGrounded;

            if (elapsed < 0.07f)
                return ActionPose.Lerp(ActionPose.Zero, tuck, Smooth01(elapsed / 0.07f));

            if (landed)
                return PosePlungeLanding();

            var t = Mathf.Clamp01((elapsed - 0.07f) / 0.11f);
            return ActionPose.Lerp(tuck, dive, EaseInCubic(t));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float EaseInCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value;
        }

        // ---------------------------------------------------------------------
        // Action poses
        // ---------------------------------------------------------------------
        // These use the authored Texas clips as motion grammar rather than copying one frame:
        // torso rotates first, upper arm follows, forearm accelerates, wrist finishes the line.
        // Small local translations mirror the ranges seen in Attack_Start/Attack_Loop sampling.
        // F_Weapon is never touched because the authored clips keep its local transform static.

        // Light 1: fast low-to-mid horizontal cut led by the left chain.
        private static ActionPose PoseLight1Anticipation() => new ActionPose()
            .Move("F_Waist", -0.02f, 0.07f).Rot("F_Waist", -5f)
            .Rot("F_Chest", -8f).Rot("F_Head", 2f)
            .Move("F_L_Arm", 0.02f, 0.02f).Rot("F_L_Arm", -22f)
            .Rot("F_L_Forearm", -28f).Rot("F_L_Hand", -10f)
            .Move("F_R_Arm", -0.02f, -0.01f).Rot("F_R_Arm", 12f)
            .Rot("F_R_Forearm", -12f).Rot("F_R_Hand", -6f)
            .Rot("F_L_Leg", -5f).Rot("F_R_Leg", 4f);

        private static ActionPose PoseLight1Strike() => new ActionPose()
            .Move("F_Waist", 0.01f, -0.04f).Rot("F_Waist", 7f)
            .Rot("F_Chest", 13f).Rot("F_Head", -3f)
            .Move("F_L_Arm", 0.03f, 0.04f).Rot("F_L_Arm", 86f)
            .Rot("F_L_Forearm", 68f).Rot("F_L_Hand", 34f)
            .Move("F_R_Arm", -0.03f, 0.02f).Rot("F_R_Arm", -28f)
            .Rot("F_R_Forearm", -34f).Rot("F_R_Hand", -13f)
            .Rot("F_L_Leg", 5f).Rot("F_R_Leg", -4f);

        private static ActionPose PoseLight1Follow() => new ActionPose()
            .Rot("F_Waist", 5f).Rot("F_Chest", 8f).Rot("F_Head", -2f)
            .Rot("F_L_Arm", 62f).Rot("F_L_Forearm", 48f).Rot("F_L_Hand", 24f)
            .Rot("F_R_Arm", -18f).Rot("F_R_Forearm", -18f).Rot("F_R_Hand", -7f)
            .Rot("F_L_Leg", 3f).Rot("F_R_Leg", -2f);

        // Light 2: reverse cut. The right chain takes the lead so the silhouette flips clearly.
        private static ActionPose PoseLight2Anticipation() => new ActionPose()
            .Move("F_Waist", 0.01f, -0.05f).Rot("F_Waist", 6f)
            .Rot("F_Chest", 10f).Rot("F_Head", -2f)
            .Rot("F_L_Arm", 48f).Rot("F_L_Forearm", 34f).Rot("F_L_Hand", 18f)
            .Move("F_R_Arm", 0.01f, 0.02f).Rot("F_R_Arm", -24f)
            .Rot("F_R_Forearm", -30f).Rot("F_R_Hand", -14f)
            .Rot("F_L_Leg", 4f).Rot("F_R_Leg", -3f);

        private static ActionPose PoseLight2Strike() => new ActionPose()
            .Move("F_Waist", -0.02f, 0.07f).Rot("F_Waist", -9f)
            .Rot("F_Chest", -16f).Rot("F_Head", 4f)
            .Move("F_L_Arm", -0.02f, 0.01f).Rot("F_L_Arm", -44f)
            .Rot("F_L_Forearm", -52f).Rot("F_L_Hand", -22f)
            .Move("F_R_Arm", -0.03f, -0.02f).Rot("F_R_Arm", 82f)
            .Rot("F_R_Forearm", 64f).Rot("F_R_Hand", 31f)
            .Rot("F_L_Leg", -5f).Rot("F_R_Leg", 5f);

        private static ActionPose PoseLight2Follow() => new ActionPose()
            .Rot("F_Waist", -6f).Rot("F_Chest", -10f).Rot("F_Head", 3f)
            .Rot("F_L_Arm", -28f).Rot("F_L_Forearm", -30f).Rot("F_L_Hand", -12f)
            .Rot("F_R_Arm", 58f).Rot("F_R_Forearm", 44f).Rot("F_R_Hand", 20f)
            .Rot("F_L_Leg", -3f).Rot("F_R_Leg", 3f);

        // Heavy 3: low cross-draw into a two-arm X finisher, not a slow great-sword swing.
        private static ActionPose PoseHeavyAnticipation() => new ActionPose()
            .Move("F_Waist", -0.03f, 0.10f).Rot("F_Waist", -8f)
            .Rot("F_Chest", -14f).Rot("F_Head", 3f)
            .Move("F_L_Arm", -0.03f, -0.02f).Rot("F_L_Arm", -38f)
            .Rot("F_L_Forearm", -34f).Rot("F_L_Hand", -16f)
            .Move("F_R_Arm", -0.03f, 0.02f).Rot("F_R_Arm", 34f)
            .Rot("F_R_Forearm", 24f).Rot("F_R_Hand", 13f)
            .Rot("F_L_Leg", -13f).Rot("F_L_Calf", 10f)
            .Rot("F_R_Leg", 11f).Rot("F_R_Calf", -9f);

        private static ActionPose PoseHeavyStrike() => new ActionPose()
            .Move("F_Waist", 0.02f, -0.08f).Rot("F_Waist", 11f)
            .Rot("F_Chest", 19f).Rot("F_Head", -4f)
            .Move("F_L_Arm", 0.04f, 0.03f).Rot("F_L_Arm", 108f)
            .Rot("F_L_Forearm", 82f).Rot("F_L_Hand", 42f)
            .Move("F_R_Arm", -0.04f, -0.01f).Rot("F_R_Arm", -88f)
            .Rot("F_R_Forearm", -70f).Rot("F_R_Hand", -34f)
            .Rot("F_L_Leg", 9f).Rot("F_L_Calf", -7f)
            .Rot("F_R_Leg", -8f).Rot("F_R_Calf", 7f);

        private static ActionPose PoseHeavyFollow() => new ActionPose()
            .Rot("F_Waist", 7f).Rot("F_Chest", 11f).Rot("F_Head", -3f)
            .Rot("F_L_Arm", 78f).Rot("F_L_Forearm", 56f).Rot("F_L_Hand", 29f)
            .Rot("F_R_Arm", -58f).Rot("F_R_Forearm", -46f).Rot("F_R_Hand", -22f)
            .Rot("F_L_Leg", 5f).Rot("F_R_Leg", -5f);

        // Dash slash: compact draw, low torso, one decisive line through the target.
        private static ActionPose PoseDashAnticipation() => new ActionPose()
            .Move("F_Waist", -0.03f, 0.08f).Rot("F_Waist", -9f)
            .Rot("F_Chest", -15f).Rot("F_Head", 3f)
            .Rot("F_L_Arm", -34f).Rot("F_L_Forearm", -28f).Rot("F_L_Hand", -13f)
            .Rot("F_R_Arm", 28f).Rot("F_R_Forearm", 18f).Rot("F_R_Hand", 10f)
            .Rot("F_L_Leg", -14f).Rot("F_L_Calf", 9f)
            .Rot("F_R_Leg", 13f).Rot("F_R_Calf", -8f);

        private static ActionPose PoseDashStrike() => new ActionPose()
            .Move("F_Waist", 0.03f, -0.09f).Rot("F_Waist", 12f)
            .Rot("F_Chest", 21f).Rot("F_Head", -5f)
            .Rot("F_L_Arm", 112f).Rot("F_L_Forearm", 76f).Rot("F_L_Hand", 38f)
            .Rot("F_R_Arm", -72f).Rot("F_R_Forearm", -56f).Rot("F_R_Hand", -27f)
            .Rot("F_L_Leg", 10f).Rot("F_L_Calf", -6f)
            .Rot("F_R_Leg", -9f).Rot("F_R_Calf", 6f);

        private static ActionPose PoseDashFollow() => new ActionPose()
            .Rot("F_Waist", 8f).Rot("F_Chest", 14f).Rot("F_Head", -3f)
            .Rot("F_L_Arm", 86f).Rot("F_L_Forearm", 56f).Rot("F_L_Hand", 28f)
            .Rot("F_R_Arm", -48f).Rot("F_R_Forearm", -36f).Rot("F_R_Hand", -17f)
            .Rot("F_L_Leg", 6f).Rot("F_R_Leg", -6f);

        // Air slash: compact legs and a fast torso-driven spin cut.
        private static ActionPose PoseAirAnticipation() => new ActionPose()
            .Rot("F_Waist", -8f).Rot("F_Chest", -13f).Rot("F_Head", 3f)
            .Rot("F_L_Arm", -26f).Rot("F_L_Forearm", -22f).Rot("F_L_Hand", -10f)
            .Rot("F_R_Arm", 22f).Rot("F_R_Forearm", 18f).Rot("F_R_Hand", 8f)
            .Rot("F_L_Leg", 24f).Rot("F_L_Calf", -28f).Rot("F_L_Foot", 10f)
            .Rot("F_R_Leg", -22f).Rot("F_R_Calf", 26f).Rot("F_R_Foot", -10f);

        private static ActionPose PoseAirStrike() => new ActionPose()
            .Rot("F_Waist", 13f).Rot("F_Chest", 23f).Rot("F_Head", -5f)
            .Rot("F_L_Arm", 104f).Rot("F_L_Forearm", 74f).Rot("F_L_Hand", 36f)
            .Rot("F_R_Arm", -70f).Rot("F_R_Forearm", -52f).Rot("F_R_Hand", -25f)
            .Rot("F_L_Leg", 31f).Rot("F_L_Calf", -36f).Rot("F_L_Foot", 13f)
            .Rot("F_R_Leg", -28f).Rot("F_R_Calf", 32f).Rot("F_R_Foot", -12f);

        private static ActionPose PoseAirFollow() => new ActionPose()
            .Rot("F_Waist", 7f).Rot("F_Chest", 13f).Rot("F_Head", -3f)
            .Rot("F_L_Arm", 72f).Rot("F_L_Forearm", 48f).Rot("F_L_Hand", 24f)
            .Rot("F_R_Arm", -42f).Rot("F_R_Forearm", -30f).Rot("F_R_Hand", -14f)
            .Rot("F_L_Leg", 19f).Rot("F_L_Calf", -22f)
            .Rot("F_R_Leg", -17f).Rot("F_R_Calf", 20f);

        // Plunge: tuck -> blade-first dive -> compact landing. No sideways great-sword pose.
        private static ActionPose PosePlungeTuck() => new ActionPose()
            .Rot("F_Waist", -6f).Rot("F_Chest", -10f).Rot("F_Head", 3f)
            .Rot("F_L_Arm", -30f).Rot("F_L_Forearm", -24f).Rot("F_L_Hand", -12f)
            .Rot("F_R_Arm", 26f).Rot("F_R_Forearm", 20f).Rot("F_R_Hand", 10f)
            .Rot("F_L_Leg", 30f).Rot("F_L_Calf", -35f).Rot("F_L_Foot", 12f)
            .Rot("F_R_Leg", -28f).Rot("F_R_Calf", 33f).Rot("F_R_Foot", -12f);

        private static ActionPose PosePlungeDive() => new ActionPose()
            .Rot("F_Waist", 10f).Rot("F_Chest", 18f).Rot("F_Head", -6f)
            .Rot("F_L_Arm", 72f).Rot("F_L_Forearm", 50f).Rot("F_L_Hand", 26f)
            .Rot("F_R_Arm", -58f).Rot("F_R_Forearm", -42f).Rot("F_R_Hand", -21f)
            .Rot("F_L_Leg", -15f).Rot("F_L_Calf", 13f).Rot("F_L_Foot", -5f)
            .Rot("F_R_Leg", 14f).Rot("F_R_Calf", -12f).Rot("F_R_Foot", 5f);

        private static ActionPose PosePlungeLanding() => new ActionPose()
            .Rot("F_Waist", -11f).Rot("F_Chest", -17f).Rot("F_Head", 4f)
            .Rot("F_L_Arm", 40f).Rot("F_L_Forearm", 28f).Rot("F_L_Hand", 14f)
            .Rot("F_R_Arm", -30f).Rot("F_R_Forearm", -22f).Rot("F_R_Hand", -11f)
            .Rot("F_L_Leg", -22f).Rot("F_L_Calf", 28f).Rot("F_L_Foot", -9f)
            .Rot("F_R_Leg", 21f).Rot("F_R_Calf", -26f).Rot("F_R_Foot", 9f);

        private void ResolveTimeScaleAccessors()
        {
            if (_animationState == null)
                return;

            var type = _animationState.GetType();
            _timeScaleProperty = type.GetProperty("TimeScale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_timeScaleProperty == null || _timeScaleProperty.PropertyType != typeof(float) || !_timeScaleProperty.CanWrite)
                _timeScaleProperty = null;

            _timeScaleField = type.GetField("timeScale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_timeScaleField == null || _timeScaleField.FieldType != typeof(float) || _timeScaleField.IsInitOnly)
                _timeScaleField = null;
        }

        private void SetAnimationTimeScale(float value)
        {
            if (_animationState == null)
                return;

            try
            {
                if (_timeScaleProperty != null)
                    _timeScaleProperty.SetValue(_animationState, value);
                else
                    _timeScaleField?.SetValue(_animationState, value);
            }
            catch
            {
                // Presentation-only fallback; failure should never affect gameplay.
            }
        }

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
                // Some runtime forks rebuild later; local values still remain valid.
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
