using System.Linq;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypeEnemyCombatBrain25D), typeof(CombatEntity))]
    public sealed class EnemyPresentationDriver25D : MonoBehaviour
    {
        private PrototypeEnemyCombatBrain25D _brain;
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _presentation;
        private bool _dead;

        private void Awake()
        {
            _brain = GetComponent<PrototypeEnemyCombatBrain25D>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentsInChildren<SpineCharacterPresentation2D>(true)
                .FirstOrDefault(item => item != null && item.enabled);
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
            _presentation?.SetLocomotion(false, _brain != null ? _brain.FacingSign : -1);
        }

        private void Update()
        {
            if (_presentation == null || !_presentation.enabled)
            {
                _presentation = GetComponentsInChildren<SpineCharacterPresentation2D>(true)
                    .FirstOrDefault(item => item != null && item.enabled);
            }

            if (_dead || _brain == null || _presentation == null)
                return;
            if (_brain.IsAttacking)
                return;

            _presentation.SetLocomotion(_brain.IsMoving, _brain.FacingSign);
        }

        private void OnAttackStarted(int facing)
        {
            if (_dead)
                return;
            _presentation?.PlayAttack(0, facing);
        }

        private void OnDamaged(DamageContext _, DamageResult __)
        {
            if (_dead)
                return;
            _presentation?.PlayHit();
        }

        private void OnDied()
        {
            if (_dead)
                return;

            _dead = true;
            if (_brain != null)
                _brain.enabled = false;
            _presentation?.SetExternalLocomotionActive(false);
            _presentation?.PlayDie();
        }
    }
}
