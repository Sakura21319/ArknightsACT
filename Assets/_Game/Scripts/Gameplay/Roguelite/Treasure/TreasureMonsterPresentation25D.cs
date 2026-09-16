using System.Linq;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Treasure
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TreasureMonsterBrain25D), typeof(CombatEntity))]
    public sealed class TreasureMonsterPresentation25D : MonoBehaviour
    {
        private TreasureMonsterBrain25D _brain;
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _presentation;
        private BillboardPresentation25D _billboard;
        private bool _dead;

        private void Awake()
        {
            _brain = GetComponent<TreasureMonsterBrain25D>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentsInChildren<SpineCharacterPresentation2D>(true)
                .FirstOrDefault(item => item != null && item.enabled);
            _billboard = GetComponentInChildren<BillboardPresentation25D>(true);
        }

        private void OnEnable()
        {
            if (_brain != null)
                _brain.AttackStarted += OnAttackStarted;
            if (_entity != null)
            {
                _entity.Damaged += OnDamaged;
                if (_entity.Health != null)
                    _entity.Health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (_brain != null)
                _brain.AttackStarted -= OnAttackStarted;
            if (_entity != null)
            {
                _entity.Damaged -= OnDamaged;
                if (_entity.Health != null)
                    _entity.Health.Died -= OnDied;
            }
        }

        private void Start()
        {
            _presentation?.SetLocomotion(false, -1);
        }

        private void Update()
        {
            if (_dead || _brain == null || !_brain.IsActivated)
                return;

            _billboard?.SetPlanarDirection(_brain.LogicForward, _brain.FacingSign, _brain.IsAttacking ? 1f : 0.7f);
            if (!_brain.IsAttacking)
                _presentation?.SetLocomotion(_brain.IsMoving, _brain.FacingSign);
        }

        private void OnAttackStarted(int facing)
        {
            if (!_dead)
                _presentation?.PlayAttack(0, facing);
        }

        private void OnDamaged(DamageContext _, DamageResult __)
        {
            if (!_dead)
                _presentation?.PlayHit();
        }

        private void OnDied()
        {
            _dead = true;
            _presentation?.PlayDie();
        }
    }
}
