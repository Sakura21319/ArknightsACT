using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity), typeof(CharacterController))]
    public sealed class EnemyDeathCleanup25D : MonoBehaviour
    {
        private CombatEntity _entity;
        private CharacterController _controller;
        private EnemyVisionCone25D _visionCone;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _controller = GetComponent<CharacterController>();
            _visionCone = GetComponent<EnemyVisionCone25D>();
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

        private void OnDied()
        {
            if (_controller != null)
                _controller.enabled = false;
            if (_visionCone != null)
                _visionCone.enabled = false;
        }
    }
}
