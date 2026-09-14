using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    [RequireComponent(typeof(CombatEntity))]
    public sealed class EnemyPresentationDriver2D : MonoBehaviour
    {
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _spine;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _spine = GetComponentInChildren<SpineCharacterPresentation2D>(true);
        }

        private void OnEnable()
        {
            if (_entity == null)
                return;

            _entity.Damaged += OnDamaged;
            if (_entity.Health != null)
                _entity.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_entity == null)
                return;

            _entity.Damaged -= OnDamaged;
            if (_entity.Health != null)
                _entity.Health.Died -= OnDied;
        }

        private void Start()
        {
            _spine?.SetLocomotion(false, -1);
        }

        private void OnDamaged(DamageContext _, DamageResult __)
        {
            _spine?.PlayHit();
        }

        private void OnDied()
        {
            _spine?.PlayDie();
        }
    }
}
