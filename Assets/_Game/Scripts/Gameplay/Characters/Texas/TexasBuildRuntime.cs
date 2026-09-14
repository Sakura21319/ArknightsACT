using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Characters.Texas
{
    /// <summary>
    /// Temporary Phase-2 build laboratory. These toggles will later be driven by the roguelite upgrade runtime.
    /// </summary>
    public sealed class TexasBuildRuntime : MonoBehaviour
    {
        private PlayerAttackController _attack;
        private TexasSwordRainSkill _skill;
        private CombatEntity _entity;
        private PlayerMotor2D _motor;
        private AttackSlashPresentation2D _attackVfx;
        private SwordRainPresentation2D _skillVfx;
        private int _attackActions;

        public bool SwiftBlade { get; private set; }
        public bool ResidualThunder { get; private set; }
        public bool Conductive { get; private set; }

        private void Awake()
        {
            _attack = GetComponent<PlayerAttackController>();
            _skill = GetComponent<TexasSwordRainSkill>();
            _entity = GetComponent<CombatEntity>();
            _motor = GetComponent<PlayerMotor2D>();
            _attackVfx = GetComponentInChildren<AttackSlashPresentation2D>();
            _skillVfx = GetComponentInChildren<SwordRainPresentation2D>();
        }

        private void OnEnable()
        {
            if (_attack != null)
            {
                _attack.AttackStarted += OnAttackStarted;
                _attack.AttackHit += OnAttackHit;
            }
            if (_skill != null)
                _skill.CastResolved += OnSwordRainResolved;
        }

        private void OnDisable()
        {
            if (_attack != null)
            {
                _attack.AttackStarted -= OnAttackStarted;
                _attack.AttackHit -= OnAttackHit;
            }
            if (_skill != null)
                _skill.CastResolved -= OnSwordRainResolved;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) SwiftBlade = !SwiftBlade;
            if (keyboard.digit2Key.wasPressedThisFrame) ResidualThunder = !ResidualThunder;
            if (keyboard.digit3Key.wasPressedThisFrame) Conductive = !Conductive;
        }

        private void OnAttackStarted(int _)
        {
            if (!SwiftBlade) return;
            _attackActions++;
            if (_attackActions < 8) return;
            _attackActions = 0;
            TriggerSwordWave();
        }

        private void OnAttackHit(CombatEntity target)
        {
            if (Conductive && target != null && target.Status.Has(CombatStatusType.Shock))
                _skill.ReduceCooldown(0.15f);
        }

        private void OnSwordRainResolved(Vector2 center, float radius)
        {
            if (ResidualThunder)
                StartCoroutine(ThunderField(center, radius));
        }

        private void TriggerSwordWave()
        {
            var facing = _motor.FacingSign;
            _attackVfx?.PlaySwordWave(facing);
            var center = (Vector2)transform.position + new Vector2(1.8f * facing, 0.05f);
            var colliders = Physics2D.OverlapBoxAll(center, new Vector2(3.2f, 1.15f), 0f);
            DamageUnique(colliders, 6f, false, "texas_swift_blade");
        }

        private IEnumerator ThunderField(Vector2 center, float radius)
        {
            const float duration = 3f;
            const float interval = 0.5f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                _skillVfx?.PlayFieldPulse(center, radius);
                var colliders = Physics2D.OverlapCircleAll(center, radius);
                DamageUnique(colliders, 3f, true, "texas_residual_thunder");
                elapsed += interval;
                yield return new WaitForSeconds(interval);
            }
        }

        private void DamageUnique(Collider2D[] colliders, float damage, bool applyShock, string sourceId)
        {
            var targets = new HashSet<CombatEntity>();
            var hitAny = false;
            foreach (var collider in colliders)
            {
                var target = collider.GetComponentInParent<CombatEntity>();
                if (target == null || target == _entity || target.Team == _entity.Team || !targets.Add(target))
                    continue;

                var result = DamageSystem.Apply(new DamageContext(_entity, _entity, target, damage, DamageType.Arts, Vector2.zero, sourceId: sourceId));
                if (!result.Applied) continue;
                if (applyShock) target.Status.Apply(CombatStatusType.Shock, 1.2f);
                target.GetComponentInChildren<HitFlash2D>()?.Flash();
                hitAny = true;
            }

            if (hitAny)
                CameraShake2D.Instance?.Shake(0.045f, 0.06f);
        }
    }
}
