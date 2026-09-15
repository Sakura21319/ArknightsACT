using System.Collections;
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
        private PlayerAttackController _attack;
        private PlayerMotor2D _motor;
        private CombatEntity _entity;
        private AttackSlashPresentation2D _presentation;
        private int _attackActions;
        private int _level;

        private void Awake()
        {
            _attack = GetComponent<PlayerAttackController>();
            _motor = GetComponent<PlayerMotor2D>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentInChildren<AttackSlashPresentation2D>();
        }

        public void SetLevel(int level)
        {
            _level = Mathf.Clamp(level, 0, TexasBuildLab.MaxUpgradeLevel);
            _attackActions = 0;
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
            StopAllCoroutines();
            _attackActions = 0;
        }

        private void OnAttackStarted(int _)
        {
            if (_level <= 0)
                return;

            _attackActions++;
            var attacksPerWave = _level switch
            {
                1 => 4,
                2 => 3,
                _ => 3
            };

            if (_attackActions < attacksPerWave)
                return;

            _attackActions = 0;
            FireWave(0);
            if (_level >= 3)
                StartCoroutine(FireSecondWave());
        }

        private IEnumerator FireSecondWave()
        {
            yield return new WaitForSeconds(0.07f);
            FireWave(1);
        }

        private void FireWave(int waveIndex)
        {
            var facing = _motor != null ? _motor.FacingSign : 1;
            _presentation?.PlaySwordWave(facing);

            var waveDamage = _level switch
            {
                1 => 7f,
                2 => 10f,
                _ => 12f
            };

            var center = (Vector2)transform.position + new Vector2((1.75f + waveIndex * 0.35f) * facing, 0.05f);
            var hits = Physics2D.OverlapBoxAll(center, new Vector2(3.4f, 1.30f), 0f);
            var count = AreaDamageResolver.ApplyUnique(
                hits,
                _entity,
                _entity,
                waveDamage,
                DamageType.Arts,
                new Vector2(1.2f * facing, 0.15f),
                "texas_swift_blade",
                (target, _) => target.GetComponentInChildren<HitFlash2D>()?.Flash());

            if (count > 0)
            {
                HitStopService.Instance?.Request(_level >= 3 ? 0.025f : 0.018f);
                CameraShake2D.Instance?.Shake(_level >= 3 ? 0.065f : 0.045f, 0.06f);
            }
        }
    }
}
