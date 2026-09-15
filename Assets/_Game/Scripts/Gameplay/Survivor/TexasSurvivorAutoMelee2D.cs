using System.Reflection;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Survivor
{
    /// <summary>
    /// Experimental survivor-mode melee trigger for Texas.
    ///
    /// Important design rule: this component NEVER moves, pulls, snaps or magnetizes the player.
    /// It only asks PlayerAttackController to begin an attack when a live enemy is already inside
    /// the current-facing melee envelope. Player movement remains 100% authoritative.
    ///
    /// The reflection bridge intentionally stays inside this experimental component so the normal
    /// ACT combat API does not need to change before the survivor direction is validated.
    /// </summary>
    [RequireComponent(typeof(PlayerAttackController), typeof(PlayerMotor2D), typeof(PlayerDashController))]
    public sealed class TexasSurvivorAutoMelee2D : MonoBehaviour
    {
        [SerializeField] private Vector2 attackProbeOffset = new(0.86f, 0.04f);
        [SerializeField] private Vector2 attackProbeSize = new(1.55f, 1.28f);
        [SerializeField, Min(0.01f)] private float scanInterval = 0.035f;

        private PlayerAttackController _attack;
        private PlayerMotor2D _motor;
        private PlayerDashController _dash;
        private CombatEntity _self;
        private MethodInfo _tryBeginAttack;
        private float _nextScanAt;
        private bool _warned;

        public Vector2 AttackProbeSize => attackProbeSize;

        private void Awake()
        {
            _attack = GetComponent<PlayerAttackController>();
            _motor = GetComponent<PlayerMotor2D>();
            _dash = GetComponent<PlayerDashController>();
            _self = GetComponent<CombatEntity>();

            _tryBeginAttack = typeof(PlayerAttackController).GetMethod(
                "TryBeginAttack",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private void Update()
        {
            if (_attack == null || _motor == null || _self == null ||
                _self.Health == null || _self.Health.IsDead)
                return;

            if (!_motor.IsGrounded || _attack.IsAttacking || (_dash != null && _dash.IsDashing))
                return;

            if (Time.time < _nextScanAt)
                return;
            _nextScanAt = Time.time + scanInterval;

            if (!HasEnemyInsideCurrentFacingMeleeRange())
                return;

            if (_tryBeginAttack == null)
            {
                WarnOnce("PlayerAttackController.TryBeginAttack could not be resolved. Auto melee disabled.");
                enabled = false;
                return;
            }

            // No transform/rigidbody writes here: attack starts exactly where the player stands.
            _tryBeginAttack.Invoke(_attack, null);
        }

        private bool HasEnemyInsideCurrentFacingMeleeRange()
        {
            var facing = _motor.FacingSign < 0 ? -1f : 1f;
            var offset = attackProbeOffset;
            offset.x *= facing;
            var center = (Vector2)transform.position + offset;
            var hits = Physics2D.OverlapBoxAll(center, attackProbeSize, 0f);

            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i];
                if (collider == null)
                    continue;

                var target = collider.GetComponentInParent<CombatEntity>();
                if (target == null || target == _self || target.Team == _self.Team ||
                    target.Health == null || target.Health.IsDead)
                    continue;

                return true;
            }

            return false;
        }

        private void WarnOnce(string message)
        {
            if (_warned)
                return;
            _warned = true;
            Debug.LogWarning("[ArknightsACT/Survivor] " + message, this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var motor = Application.isPlaying ? GetComponent<PlayerMotor2D>() : null;
            var facing = motor != null && motor.FacingSign < 0 ? -1f : 1f;
            var offset = attackProbeOffset;
            offset.x *= facing;
            Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireCube((Vector2)transform.position + offset, attackProbeSize);
        }
#endif
    }
}
