using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Characters.Texas;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    [RequireComponent(typeof(TopDownTexasMeleeController), typeof(TexasSwordRainSkill), typeof(CombatEntity))]
    public sealed class TopDownTexasConductiveEffect : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float chainRadius = 3.6f;

        private TopDownTexasMeleeController _melee;
        private TexasSwordRainSkill _skill;
        private CombatEntity _entity;
        private TopDownCombatFx2D _fx;
        private int _level;

        private void Awake()
        {
            _melee = GetComponent<TopDownTexasMeleeController>();
            _skill = GetComponent<TexasSwordRainSkill>();
            _entity = GetComponent<CombatEntity>();
            _fx = GetComponent<TopDownCombatFx2D>();
        }

        public void SetLevel(int level)
        {
            _level = Mathf.Clamp(level, 0, TopDownTexasBuildLab.MaxUpgradeLevel);
        }

        private void OnEnable()
        {
            if (_melee != null)
                _melee.AttackHit += OnAttackHit;
        }

        private void OnDisable()
        {
            if (_melee != null)
                _melee.AttackHit -= OnAttackHit;
        }

        private void OnAttackHit(CombatEntity target)
        {
            if (_level <= 0 || target == null || target.Status == null || !target.Status.Has(CombatStatusType.Shock))
                return;

            _skill?.ReduceCooldown(_level switch { 1 => 0.25f, 2 => 0.40f, _ => 0.60f });
            var chainCount = _level switch { 1 => 1, 2 => 2, _ => 4 };
            var damage = _level switch { 1 => 5f, 2 => 7f, _ => 10f };
            Chain(target, chainCount, damage);
        }

        private void Chain(CombatEntity primary, int maxChains, float damage)
        {
            if (_entity == null || primary == null || maxChains <= 0)
                return;

            var seen = new HashSet<CombatEntity> { primary };
            var from = (Vector2)primary.transform.position;
            var current = primary;
            var applied = 0;

            while (applied < maxChains)
            {
                var colliders = Physics2D.OverlapCircleAll(current.transform.position, chainRadius);
                CombatEntity best = null;
                var bestSqr = float.PositiveInfinity;

                for (var i = 0; i < colliders.Length; i++)
                {
                    var candidate = colliders[i]?.GetComponentInParent<CombatEntity>();
                    if (candidate == null || candidate == _entity || candidate.Team == _entity.Team ||
                        candidate.Health == null || candidate.Health.IsDead || seen.Contains(candidate))
                        continue;

                    var sqr = ((Vector2)candidate.transform.position - from).sqrMagnitude;
                    if (sqr >= bestSqr)
                        continue;
                    bestSqr = sqr;
                    best = candidate;
                }

                if (best == null)
                    break;

                var to = (Vector2)best.transform.position;
                var context = new DamageContext(
                    _entity,
                    _entity,
                    best,
                    damage,
                    DamageType.Arts,
                    Vector2.zero,
                    sourceId: "TopDown_Texas_Conductive");
                if (!DamageSystem.Apply(context).Applied)
                    break;

                seen.Add(best);
                best.Status.Apply(CombatStatusType.Shock, _level >= 3 ? 1.6f : 1.2f);
                best.GetComponentInChildren<HitFlash2D>()?.Flash();
                _fx?.PlayChain(from, to);
                current = best;
                from = to;
                applied++;
            }

            if (applied > 0)
            {
                HitStopService.Instance?.Request(0.015f);
                CameraShake2D.Instance?.Shake(0.035f + applied * 0.007f, 0.055f);
            }
        }
    }
}
