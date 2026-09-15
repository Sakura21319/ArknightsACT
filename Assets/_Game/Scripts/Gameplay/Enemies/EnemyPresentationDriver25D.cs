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
            if (_dead || _brain == null || _presentation == null)
                return;
            _presentation.SetLocomotion(_brain.IsMoving, _brain.FacingSign);
        }

        private void OnDied()
        {
            _dead = true;
            _presentation?.PlayDie();
        }
    }
}
