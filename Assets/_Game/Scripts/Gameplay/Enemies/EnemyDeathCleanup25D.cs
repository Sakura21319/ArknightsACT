using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity), typeof(CharacterController))]
    public sealed class EnemyDeathCleanup25D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float corpseLifetime = 1.25f;

        private CombatEntity _entity;
        private CharacterController _controller;
        private EnemyVisionCone25D _visionCone;
        private LineRenderer _visionLine;
        private bool _deathHandled;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _controller = GetComponent<CharacterController>();
            _visionCone = GetComponent<EnemyVisionCone25D>();
        }

        private void OnEnable()
        {
            _deathHandled = false;
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
            if (_deathHandled)
                return;
            _deathHandled = true;

            if (_controller != null)
                _controller.enabled = false;

            // EnemyVisionCone25D creates its LineRenderer dynamically. Disabling only the
            // behaviour leaves that renderer alive, so explicitly hide it before disabling the
            // cone. This removes the warning outline immediately while the Die clip finishes.
            _visionLine ??= GetComponent<LineRenderer>();
            if (_visionLine != null)
            {
                _visionLine.positionCount = 0;
                _visionLine.enabled = false;
            }
            if (_visionCone != null)
                _visionCone.enabled = false;

            // Keep the root alive briefly so EnemyPresentationDriver25D can show the authored
            // death animation, then remove the corpse from the scene entirely.
            Destroy(gameObject, Mathf.Max(0.05f, corpseLifetime));
        }
    }
}
