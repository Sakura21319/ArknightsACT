using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Input;
using UnityEngine;

namespace ArknightsACT.Gameplay.Combat
{
    [RequireComponent(typeof(CombatEntity), typeof(PlayerMotor2D))]
    public sealed class PlayerAttackController : MonoBehaviour
    {
        [SerializeField] private AttackDefinition[] combo;
        [SerializeField] private float baseAttack = 10f;
        [SerializeField] private float comboResetSeconds = 0.60f;
        [SerializeField, Min(0f)] private float movementUnlockAfterImpactSeconds = 0.04f;

        private CombatEntity _entity;
        private PlayerMotor2D _motor;
        private IPlayerInputSource _input;
        private PlayerSkillController _skills;
        private Coroutine _attackRoutine;
        private AttackDefinition _currentDefinition;
        private int _comboIndex;
        private bool _attackQueued;
        private float _lastAttackFinishedAt = -999f;
        private float _attackStartedAt;
        private float _currentImpactSeconds;
        private float _currentCycleSeconds;

        public bool IsAttacking => _attackRoutine != null;
        public int CurrentComboIndex { get; private set; }

        public bool IsMovementLocked
        {
            get
            {
                if (!IsAttacking)
                    return false;
                var unlockAt = Mathf.Min(
                    Mathf.Max(0f, _currentCycleSeconds),
                    Mathf.Max(0f, _currentImpactSeconds) + movementUnlockAfterImpactSeconds);
                return Time.time - _attackStartedAt < unlockAt;
            }
        }

        public bool CanDashCancel
        {
            get
            {
                if (!IsAttacking || _currentDefinition == null)
                    return true;
                var elapsed = Time.time - _attackStartedAt;
                var duration = Mathf.Max(0.01f, _currentCycleSeconds);
                return Mathf.Clamp01(elapsed / duration) >= _currentDefinition.dashCancelNormalizedTime;
            }
        }

        public event Action<int> AttackStarted;
        public event Action<CombatEntity> AttackHit;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _motor = GetComponent<PlayerMotor2D>();
            _input = GetComponent<IPlayerInputSource>();
            _skills = GetComponent<PlayerSkillController>();
        }

        private void OnEnable()
        {
            if (_entity?.Health != null)
                _entity.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_entity?.Health != null)
                _entity.Health.Died -= OnDied;
        }

        private void Update()
        {
            if (_entity?.Health == null || _entity.Health.IsDead || _input == null)
                return;
            if (!_input.AttackPressedThisFrame)
                return;
            if (_skills != null && _skills.IsCasting)
                return;
            if (_motor != null && !_motor.IsGrounded)
                return;

            if (IsAttacking)
            {
                _attackQueued = true;
                return;
            }

            BeginNextComboAttack();
        }

        public void Configure(AttackDefinition[] definitions, float attackValue = 10f)
        {
            combo = definitions;
            baseAttack = Mathf.Max(0f, attackValue);
        }

        public void CancelCurrentAttack()
        {
            if (_attackRoutine != null)
                StopCoroutine(_attackRoutine);
            _attackRoutine = null;
            _currentDefinition = null;
            _currentImpactSeconds = 0f;
            _currentCycleSeconds = 0f;
            _attackQueued = false;
        }

        private void OnDied() => CancelCurrentAttack();

        private void BeginNextComboAttack()
        {
            if (combo == null || combo.Length == 0 || _entity?.Health == null || _entity.Health.IsDead)
                return;
            if (_motor != null && !_motor.IsGrounded)
                return;

            if (Time.time - _lastAttackFinishedAt > comboResetSeconds)
                _comboIndex = 0;

            CurrentComboIndex = Mathf.Clamp(_comboIndex, 0, combo.Length - 1);
            _attackRoutine = StartCoroutine(AttackRoutine(combo[CurrentComboIndex], CurrentComboIndex));
        }

        private IEnumerator AttackRoutine(AttackDefinition definition, int presentationIndex)
        {
            _currentDefinition = definition;
            _attackStartedAt = Time.time;
            _attackQueued = false;
            _currentImpactSeconds = Mathf.Max(0f, definition.startup);
            _currentCycleSeconds = Mathf.Max(0.01f, definition.TotalDuration);
            AttackStarted?.Invoke(presentationIndex);

            if (_currentImpactSeconds > 0f)
                yield return new WaitForSeconds(_currentImpactSeconds);

            if (_entity?.Health != null && !_entity.Health.IsDead)
                PerformHit(definition);

            var remainder = Mathf.Max(0f, _currentCycleSeconds - _currentImpactSeconds);
            if (remainder > 0f)
                yield return new WaitForSeconds(remainder);

            _lastAttackFinishedAt = Time.time;
            _comboIndex = (_comboIndex + 1) % combo.Length;
            _attackRoutine = null;
            _currentDefinition = null;
            _currentImpactSeconds = 0f;
            _currentCycleSeconds = 0f;

            var continueChain = _attackQueued &&
                                _entity?.Health != null &&
                                !_entity.Health.IsDead &&
                                (_motor == null || _motor.IsGrounded) &&
                                (_skills == null || !_skills.IsCasting);
            _attackQueued = false;
            if (continueChain)
                BeginNextComboAttack();
        }

        private void PerformHit(AttackDefinition definition)
        {
            var facing = _motor != null ? _motor.FacingSign : 1;
            var offset = definition.hitboxOffset;
            offset.x *= facing;
            var knockback = definition.knockback;
            knockback.x *= facing;

            var center = (Vector2)transform.position + offset;
            var hits = Physics2D.OverlapBoxAll(center, definition.hitboxSize, 0f);
            var processed = new HashSet<CombatEntity>();
            var hitAny = false;

            foreach (var hit in hits)
            {
                if (hit == null)
                    continue;
                var target = hit.GetComponentInParent<CombatEntity>();
                if (target == null || target == _entity || target.Team == _entity.Team ||
                    target.Health == null || target.Health.IsDead || !processed.Add(target))
                    continue;

                var context = new DamageContext(
                    _entity,
                    _entity,
                    target,
                    baseAttack * definition.damageMultiplier,
                    DamageType.Physical,
                    knockback,
                    sourceId: definition.name);
                var result = DamageSystem.Apply(context);
                if (!result.Applied)
                    continue;

                hitAny = true;
                target.GetComponentInChildren<HitFlash2D>()?.Flash();
                AttackHit?.Invoke(target);
            }

            if (!hitAny)
                return;
            if (definition.hitStopSeconds > 0f)
                HitStopService.Instance?.Request(definition.hitStopSeconds);
            if (definition.cameraShakeAmplitude > 0f)
                CameraShake2D.Instance?.Shake(definition.cameraShakeAmplitude, 0.06f);
        }
    }
}
