using System.Collections;
using ArknightsACT.Combat;
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
        [SerializeField] private float comboResetSeconds = 0.55f;
        [SerializeField] private float inputBufferSeconds = 0.18f;

        private CombatEntity _entity;
        private PlayerMotor2D _motor;
        private IPlayerInputSource _input;
        private Coroutine _attackRoutine;
        private int _comboIndex;
        private float _lastAttackFinishedAt = -999f;
        private float _bufferUntil = -999f;
        private float _attackStartedAt;
        private AttackDefinition _currentDefinition;

        public bool IsAttacking => _attackRoutine != null;

        public bool CanDashCancel
        {
            get
            {
                if (!IsAttacking || _currentDefinition == null)
                    return true;

                var elapsed = Time.time - _attackStartedAt;
                var normalized = _currentDefinition.TotalDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / _currentDefinition.TotalDuration);

                return normalized >= _currentDefinition.dashCancelNormalizedTime;
            }
        }

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _motor = GetComponent<PlayerMotor2D>();
            _input = GetComponent<IPlayerInputSource>();
        }

        private void Update()
        {
            if (_input == null || !_input.AttackPressedThisFrame)
                return;

            if (IsAttacking)
            {
                _bufferUntil = Time.time + inputBufferSeconds;
                return;
            }

            TryBeginAttack();
        }

        public void Configure(AttackDefinition[] definitions, float attackValue = 10f)
        {
            combo = definitions;
            baseAttack = Mathf.Max(0f, attackValue);
        }

        public void CancelCurrentAttack()
        {
            if (_attackRoutine == null)
                return;

            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
            _currentDefinition = null;
        }

        private void TryBeginAttack()
        {
            if (combo == null || combo.Length == 0)
                return;

            if (Time.time - _lastAttackFinishedAt > comboResetSeconds)
                _comboIndex = 0;

            _attackRoutine = StartCoroutine(AttackRoutine(combo[_comboIndex]));
        }

        private IEnumerator AttackRoutine(AttackDefinition definition)
        {
            _currentDefinition = definition;
            _attackStartedAt = Time.time;
            _bufferUntil = -999f;

            yield return WaitScaled(definition.startup);
            PerformHit(definition);
            yield return WaitScaled(definition.active);
            yield return WaitScaled(definition.recovery);

            _lastAttackFinishedAt = Time.time;
            _comboIndex = (_comboIndex + 1) % combo.Length;
            _attackRoutine = null;
            _currentDefinition = null;

            if (Time.time <= _bufferUntil)
                TryBeginAttack();
        }

        private void PerformHit(AttackDefinition definition)
        {
            var facing = _motor.FacingSign;
            var localOffset = definition.hitboxOffset;
            localOffset.x *= facing;

            var center = (Vector2)transform.position + localOffset;
            var hits = Physics2D.OverlapBoxAll(center, definition.hitboxSize, 0f);
            var hitAny = false;

            foreach (var hit in hits)
            {
                if (hit == null)
                    continue;

                var target = hit.GetComponentInParent<CombatEntity>();
                if (target == null || target == _entity || target.Team == _entity.Team)
                    continue;

                var knockback = definition.knockback;
                knockback.x *= facing;

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
                target.GetComponent<HitFlash2D>()?.Flash();
            }

            if (!hitAny)
                return;

            HitStopService.Instance?.Request(definition.hitStopSeconds);
            CameraShake2D.Instance?.Shake(definition.cameraShakeAmplitude, 0.08f);
        }

        private static IEnumerator WaitScaled(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_currentDefinition == null)
                return;

            var facing = Application.isPlaying && _motor != null ? _motor.FacingSign : 1;
            var offset = _currentDefinition.hitboxOffset;
            offset.x *= facing;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube((Vector2)transform.position + offset, _currentDefinition.hitboxSize);
        }
#endif
    }
}
