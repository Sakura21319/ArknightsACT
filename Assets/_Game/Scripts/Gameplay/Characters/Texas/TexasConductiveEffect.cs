using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Texas
{
    [RequireComponent(typeof(PlayerAttackController), typeof(TexasSwordRainSkill), typeof(CombatEntity))]
    public sealed class TexasConductiveEffect : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float chainRadius = 3.6f;

        private PlayerAttackController _attack;
        private TexasSwordRainSkill _skill;
        private CombatEntity _entity;
        private AttackSlashPresentation2D _presentation;
        private int _level;

        private void Awake()
        {
            _attack = GetComponent<PlayerAttackController>();
            _skill = GetComponent<TexasSwordRainSkill>();
            _entity = GetComponent<CombatEntity>();
            _presentation = GetComponentInChildren<AttackSlashPresentation2D>();
        }

        public void SetLevel(int level)
        {
            _level = Mathf.Clamp(level, 0, TexasBuildLab.MaxUpgradeLevel);
        }

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackHit += OnAttackHit;
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackHit -= OnAttackHit;
        }

        private void OnAttackHit(CombatEntity target)
        {
            if (_level <= 0 || target == null || target.Status == null || !target.Status.Has(CombatStatusType.Shock))
                return;

            var cooldownReduction = _level switch
            {
                1 => 0.25f,
                2 => 0.40f,
                _ => 0.60f
            };
            _skill?.ReduceCooldown(cooldownReduction);

            var chainCount = _level switch
            {
                1 => 1,
                2 => 2,
                _ => 4
            };
            var chainDamage = _level switch
            {
                1 => 5f,
                2 => 7f,
                _ => 10f
            };

            ChainLightning(target, chainCount, chainDamage);
        }

        private void ChainLightning(CombatEntity primaryTarget, int maxChains, float damage)
        {
            if (_entity == null || maxChains <= 0 || damage <= 0f)
                return;

            var origin = (Vector2)primaryTarget.transform.position;
            var colliders = Physics2D.OverlapCircleAll(origin, chainRadius);
            var seen = new HashSet<CombatEntity>();
            var candidates = new List<CombatEntity>();

            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null)
                    continue;

                var candidate = collider.GetComponentInParent<CombatEntity>();
                if (candidate == null ||
                    candidate == _entity ||
                    candidate == primaryTarget ||
                    candidate.Team == _entity.Team ||
                    candidate.Health == null ||
                    candidate.Health.IsDead ||
                    !seen.Add(candidate))
                    continue;

                candidates.Add(candidate);
            }

            candidates.Sort((a, b) =>
                ((Vector2)a.transform.position - origin).sqrMagnitude.CompareTo(
                    ((Vector2)b.transform.position - origin).sqrMagnitude));

            var from = origin;
            var applied = 0;
            for (var i = 0; i < candidates.Count && applied < maxChains; i++)
            {
                var candidate = candidates[i];
                var to = (Vector2)candidate.transform.position;
                var context = new DamageContext(
                    _entity,
                    _entity,
                    candidate,
                    damage,
                    DamageType.Arts,
                    Vector2.zero,
                    sourceId: "texas_conductive_chain");
                var result = DamageSystem.Apply(context);
                if (!result.Applied)
                    continue;

                candidate.Status.Apply(CombatStatusType.Shock, _level >= 3 ? 1.6f : 1.2f);
                candidate.GetComponentInChildren<HitFlash2D>()?.Flash();
                _presentation?.PlayChainLightning(from, to, 0.9f + _level * 0.12f);
                from = to;
                applied++;
            }

            if (applied > 0)
            {
                HitStopService.Instance?.Request(0.018f);
                CameraShake2D.Instance?.Shake(0.04f + applied * 0.008f, 0.06f);
            }
        }
    }
}
