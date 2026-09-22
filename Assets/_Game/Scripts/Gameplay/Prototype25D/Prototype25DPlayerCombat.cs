using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Prototype25D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity), typeof(Prototype25DPlayerMotor))]
    public sealed class Prototype25DPlayerCombat : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float attackRange = 1.45f;
        [SerializeField, Range(10f, 180f)] private float attackAngle = 105f;
        [SerializeField, Min(0.1f)] private float damage = 24f;
        [SerializeField, Min(0.05f)] private float cooldown = 0.38f;

        private CombatEntity _entity;
        private Prototype25DPlayerMotor _motor;
        private float _nextAttackTime;

        public bool IsAttacking { get; private set; }

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _motor = GetComponent<Prototype25DPlayerMotor>();
        }

        private void Update()
        {
            if (_entity == null || _entity.Health == null || _entity.Health.IsDead)
                return;
            if (GameplayInputBlocker.IsBlocked)
            {
                IsAttacking = false;
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var pressed = (keyboard != null && keyboard.jKey.wasPressedThisFrame) ||
                          (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (!pressed || Time.time < _nextAttackTime)
                return;

            Attack();
        }

        private void Attack()
        {
            _nextAttackTime = Time.time + cooldown;
            IsAttacking = true;

            var forward = _motor != null ? _motor.WorldFacingDirection : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            var hits = Physics.OverlapSphere(transform.position + forward * (attackRange * 0.55f) + Vector3.up * 0.7f, attackRange);
            for (var i = 0; i < hits.Length; i++)
            {
                var target = hits[i] != null ? hits[i].GetComponentInParent<CombatEntity>() : null;
                if (target == null || target == _entity || target.Team != Team.Enemy || target.Health == null || target.Health.IsDead)
                    continue;

                var flat = target.transform.position - transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > attackRange * attackRange || flat.sqrMagnitude < 0.001f)
                    continue;
                if (Vector3.Angle(forward, flat.normalized) > attackAngle * 0.5f)
                    continue;

                DamageSystem.Apply(new DamageContext(
                    _entity,
                    _entity,
                    target,
                    damage,
                    DamageType.Physical,
                    Vector2.zero,
                    sourceId: "Prototype25D_Basic"));
            }

            IsAttacking = false;
        }
    }
}
