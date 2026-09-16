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
        private const float IdleDirectionStrength = 0.30f;
        private const float MoveDirectionStrength = 0.65f;
        private const float AttackDirectionStrength = 1.00f;

        private PrototypeEnemyCombatBrain25D _brain;
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _presentation;
        private BillboardPresentation25D _billboard;
        private bool _dead;

        private void Awake()
        {
            _brain = GetComponent<PrototypeEnemyCombatBrain25D>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentsInChildren<SpineCharacterPresentation2D>(true)
                .FirstOrDefault(item => item != null && item.enabled);
            _billboard = GetComponentInChildren<BillboardPresentation25D>(true);
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
            _billboard?.ResetDirectionalCue();
        }

        private void Start()
        {
            var facing = _brain != null ? _brain.FacingSign : -1;
            _presentation?.SetLocomotion(false, facing);
            if (_brain != null)
                _billboard?.SetPlanarDirection(_brain.LogicForward, facing, IdleDirectionStrength);
        }

        private void Update()
        {
            if (_presentation == null || !_presentation.enabled)
            {
                _presentation = GetComponentsInChildren<SpineCharacterPresentation2D>(true)
                    .FirstOrDefault(item => item != null && item.enabled);
            }

            if (_dead || _brain == null)
                return;

            var facing = _brain.FacingSign;
            _billboard?.SetPlanarDirection(
                _brain.LogicForward,
                facing,
                _brain.IsAttacking
                    ? AttackDirectionStrength
                    : (_brain.IsMoving ? MoveDirectionStrength : IdleDirectionStrength));

            if (_presentation == null || _brain.IsAttacking)
                return;

            _presentation.SetLocomotion(_brain.IsMoving, facing);
        }

        private void OnAttackStarted(int facing)
        {
            if (_dead)
                return;
            if (_brain != null)
                _billboard?.SetPlanarDirection(_brain.LogicForward, facing, AttackDirectionStrength);
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
            _billboard?.ResetDirectionalCue();
            _presentation?.SetExternalLocomotionActive(false);
            _presentation?.PlayDie();
        }
    }
}
