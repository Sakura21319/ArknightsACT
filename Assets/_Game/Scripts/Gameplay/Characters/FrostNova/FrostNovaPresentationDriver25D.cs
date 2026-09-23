using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Presentation;
using ArknightsACT.Gameplay.Roguelite;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.FrostNova
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor25D), typeof(PlayerAttackController), typeof(PlayerSkillController))]
    public sealed class FrostNovaPresentationDriver25D :
        MonoBehaviour,
        IPlayerRunResettable,
        IPlayerControlLockSource,
        IPlayerBasicAttackMovementLockProvider
    {
        private const float FallbackAttackDuration = 0.50f;
        private const float FallbackSkillDuration = 1.00f;

        private PlayerMotor25D _motor;
        private PlayerAttackController _attack;
        private PlayerSkillController _skills;
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _presentation;
        private BillboardPresentation25D _billboard;
        private FrostNovaTuningProfile _tuning;

        private float _visualLockUntil;
        private float _attackVisualLockUntil;
        private bool _actionVisualWasActive;
        private bool _dead;
        [SerializeField] private FrostNovaSkinVariant _skin;

        public bool BlocksMovement => !_dead && Time.time < _visualLockUntil;
        public bool BlocksDash => !_dead && Time.time < _visualLockUntil;
        public bool BlocksBasicAttack => !_dead && Time.time < _visualLockUntil;
        public bool IsBasicAttackMovementLocked => !_dead && Time.time < _attackVisualLockUntil;
        public float RemainingActionVisualSeconds =>
            _dead ? 0f : Mathf.Max(0f, _visualLockUntil - Time.time);

        public void Configure(FrostNovaSkinVariant skin)
        {
            _skin = skin;
        }

        private void SyncSkinFromIdentity()
        {
            var identity = GetComponent<PlayableOperatorIdentity>();
            if (identity == null ||
                !string.Equals(
                    identity.OperatorId,
                    "FrostNova",
                    System.StringComparison.OrdinalIgnoreCase))
                return;

            _skin = FrostNovaSkinVariantExtensions.FromSkinId(identity.SkinId);
        }

        public void RefreshTuning()
        {
            _tuning = Resources.Load<FrostNovaTuningProfile>(
                FrostNovaTuningProfile.ResourcePath);
        }

        private float MoveSpeed => _tuning != null ? _tuning.MoveAnimationSpeed : 2f;
        private float AttackSpeed => _tuning != null ? _tuning.BasicAttackAnimationSpeed : 2f;

        private void Awake()
        {
            SyncSkinFromIdentity();
            _motor = GetComponent<PlayerMotor25D>();
            _attack = GetComponent<PlayerAttackController>();
            _skills = GetComponent<PlayerSkillController>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentInChildren<SpineCharacterPresentation2D>(true);
            _billboard = GetComponentInChildren<BillboardPresentation25D>(true);
            RefreshTuning();
        }

        private void OnEnable()
        {
            SyncSkinFromIdentity();
            RefreshTuning();

            if (_attack != null)
                _attack.AttackStarted += OnAttackStarted;
            if (_skills != null)
                _skills.SkillCastSucceeded += OnSkillCast;
            if (_entity?.Health != null)
                _entity.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackStarted -= OnAttackStarted;
            if (_skills != null)
                _skills.SkillCastSucceeded -= OnSkillCast;
            if (_entity?.Health != null)
                _entity.Health.Died -= OnDied;

            _visualLockUntil = 0f;
            _attackVisualLockUntil = 0f;
        }

        private void Update()
        {
            if (_dead || _presentation == null || _motor == null)
                return;

            var facing = _motor.FacingSign;
            var actionVisualActive =
                Time.time < _visualLockUntil;

            if (actionVisualActive)
            {
                _actionVisualWasActive = true;
                _presentation.SetExternalLocomotionActive(false);
                _presentation.SetFacingImmediate(facing);
                _billboard?.SetPlanarDirection(_motor.PlanarForward, facing, 1f);
                return;
            }

            if (_actionVisualWasActive)
                _actionVisualWasActive = false;

            _presentation.SetExternalLocomotionActive(false);
            _presentation.SetLocomotion(_motor.IsMoving, facing);
            _presentation.SetCurrentAnimationSpeed(_motor.IsMoving ? MoveSpeed : 1f);
            _billboard?.SetPlanarDirection(
                _motor.PlanarForward,
                facing,
                _motor.IsMoving ? 0.65f : 0.25f);
        }

        private void OnAttackStarted(int comboIndex)
        {
            if (_dead || _presentation == null)
                return;

            var facing = _motor != null ? _motor.FacingSign : 1;
            var speed = AttackSpeed;
            _presentation.PlayAttack(comboIndex, facing);
            _presentation.SetCurrentAnimationSpeed(speed);

            var duration = ResolveSpineDuration("Attack", FallbackAttackDuration) /
                           Mathf.Max(0.05f, speed);
            _visualLockUntil = Time.time + duration;
            _attackVisualLockUntil = _visualLockUntil;
            _actionVisualWasActive = true;
        }

        private void OnSkillCast(int slot)
        {
            if (_dead || _presentation == null)
                return;

            var facing = _motor != null ? _motor.FacingSign : 1;
            var speed = _tuning != null
                ? _tuning.GetSkillAnimationSpeedForSlot(slot)
                : slot == 2 ? 0.70f : 1f;

            var animation = ResolveSkillAnimation(slot);
            if (string.IsNullOrWhiteSpace(animation))
                return;

            _presentation.PlayNamedAnimation(animation, facing, false);
            _presentation.SetCurrentAnimationSpeed(speed);

            var duration = ResolveSpineDuration(animation, FallbackSkillDuration) /
                           Mathf.Max(0.05f, speed);
            _visualLockUntil = Time.time + duration;
            _attackVisualLockUntil = 0f;
            _actionVisualWasActive = true;
        }

        private float ResolveSpineDuration(string animation, float fallback)
        {
            return _presentation != null &&
                   _presentation.TryGetAnimationDuration(animation, out var duration)
                ? Mathf.Max(0.01f, duration)
                : Mathf.Max(0.01f, fallback);
        }

        private string ResolveSkillAnimation(int slot)
        {
            if (_skin.UsesWinterSkillSet())
                return slot == 1 ? "Skill_1" : slot == 2 ? "Skill_3" : string.Empty;

            return slot == 1 ? "Skill_1" : slot == 2 ? "Skill_2" : string.Empty;
        }

        private void OnDied()
        {
            _dead = true;
            _visualLockUntil = 0f;
            _attackVisualLockUntil = 0f;
            _billboard?.ResetDirectionalCue();
            _presentation?.PlayDie();
            _presentation?.SetCurrentAnimationSpeed(1f);
        }

        public void ResetForNewRun()
        {
            _dead = false;
            _visualLockUntil = 0f;
            _attackVisualLockUntil = 0f;
            _actionVisualWasActive = false;
            RefreshTuning();
            _billboard?.ResetDirectionalCue();
            _presentation?.SetExternalLocomotionActive(false);
            _presentation?.SetLocomotion(false, _motor != null ? _motor.FacingSign : 1);
            _presentation?.SetCurrentAnimationSpeed(1f);
        }
    }
}
