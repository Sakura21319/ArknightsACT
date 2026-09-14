using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Enemies;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    [RequireComponent(typeof(CombatEntity))]
    public sealed class EnemyPresentationDriver2D : MonoBehaviour
    {
        private CombatEntity _entity;
        private PrototypeEnemyCombatBrain2D _brain;
        private Rigidbody2D _body;
        private SpineCharacterPresentation2D _spine;
        private bool _dead;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _brain = GetComponent<PrototypeEnemyCombatBrain2D>();
            _body = GetComponent<Rigidbody2D>();
            _spine = GetComponentInChildren<SpineCharacterPresentation2D>(true);
        }

        private void OnEnable()
        {
            if (_entity != null)
            {
                _entity.Damaged += OnDamaged;
                if (_entity.Health != null)
                    _entity.Health.Died += OnDied;
            }

            if (_brain != null)
                _brain.AttackStarted += OnAttackStarted;
        }

        private void OnDisable()
        {
            if (_entity != null)
            {
                _entity.Damaged -= OnDamaged;
                if (_entity.Health != null)
                    _entity.Health.Died -= OnDied;
            }

            if (_brain != null)
                _brain.AttackStarted -= OnAttackStarted;
        }

        private void Start()
        {
            _spine?.SetLocomotion(false, _brain != null ? _brain.FacingSign : -1);
        }

        private void Update()
        {
            if (_spine == null || !_spine.enabled)
                _spine = GetComponentInChildren<SpineCharacterPresentation2D>(true);
            if (_spine == null || _dead)
                return;

            // Do not let Move/Idle overwrite the attack clip during windup/recovery.
            // The brain owns this lock and only releases it when the whole attack finishes.
            if (_brain != null && _brain.IsAttacking)
                return;

            var moving = _brain != null
                ? _brain.IsMoving
                : _body != null && Mathf.Abs(_body.linearVelocity.x) > 0.08f;
            var facing = _brain != null ? _brain.FacingSign : -1;
            _spine.SetLocomotion(moving, facing);
        }

        private void OnAttackStarted(int facing)
        {
            _spine?.PlayAttack(0, facing);
        }

        private void OnDamaged(DamageContext _, DamageResult __)
        {
            if (_dead)
                return;
            _spine?.PlayHit();
        }

        private void OnDied()
        {
            _dead = true;
            _spine?.PlayDie();
        }
    }
}
