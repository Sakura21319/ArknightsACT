using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Texas
{
    [RequireComponent(typeof(PlayerAttackController), typeof(PlayerMotor2D), typeof(CombatEntity))]
    public sealed class TexasSwiftBladeEffect : MonoBehaviour
    {
        [SerializeField, Min(1)] private int attacksPerWave = 8;
        [SerializeField, Min(0f)] private float waveDamage = 6f;

        private PlayerAttackController _attack;
        private PlayerMotor2D _motor;
        private CombatEntity _entity;
        private AttackSlashPresentation2D _presentation;
        private int _attackActions;

        private void Awake()
        {
            _attack = GetComponent<PlayerAttackController>();
            _motor = GetComponent<PlayerMotor2D>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentInChildren<AttackSlashPresentation2D>();
        }

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackStarted += OnAttackStarted;
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackStarted -= OnAttackStarted;
            _attackActions = 0;
        }

        private void OnAttackStarted(int _)
        {
            _attackActions++;
            if (_attackActions < attacksPerWave)
                return;

            _attackActions = 0;
            var facing = _motor.FacingSign;
            _presentation?.PlaySwordWave(facing);

            var center = (Vector2)transform.position + new Vector2(1.8f * facing, 0.05f);
            var hits = Physics2D.OverlapBoxAll(center, new Vector2(3.2f, 1.15f), 0f);
            var count = AreaDamageResolver.ApplyUnique(
                hits,
                _entity,
                _entity,
                waveDamage,
                DamageType.Arts,
                Vector2.zero,
                "texas_swift_blade",
                (target, _) => target.GetComponentInChildren<HitFlash2D>()?.Flash());

            if (count > 0)
                CameraShake2D.Instance?.Shake(0.045f, 0.06f);
        }
    }
}
