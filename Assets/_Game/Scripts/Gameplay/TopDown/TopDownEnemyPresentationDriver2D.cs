using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    public sealed class TopDownEnemyPresentationDriver2D : MonoBehaviour
    {
        private TopDownEnemyBrain2D _brain;
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _presentation;

        private void Awake()
        {
            _brain = GetComponent<TopDownEnemyBrain2D>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentInChildren<SpineCharacterPresentation2D>(true);
        }

        private void OnEnable()
        {
            if (_brain != null)
                _brain.AttackStarted += OnAttackStarted;
            if (_entity != null && _entity.Health != null)
                _entity.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_brain != null)
                _brain.AttackStarted -= OnAttackStarted;
            if (_entity != null && _entity.Health != null)
                _entity.Health.Died -= OnDied;
        }

        private void Update()
        {
            if (_presentation == null || _brain == null || _entity == null || _entity.Health == null || _entity.Health.IsDead)
                return;
            _presentation.SetLocomotion(_brain.IsMoving && !_brain.IsAttacking, _brain.FacingSign);
        }

        private void OnAttackStarted(int facing)
        {
            _presentation?.PlayAttack(0, facing);
        }

        private void OnDied()
        {
            _presentation?.PlayDie();
        }
    }
}
