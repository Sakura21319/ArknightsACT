using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Texas
{
    [RequireComponent(typeof(PlayerAttackController), typeof(TexasSwordRainSkill))]
    public sealed class TexasConductiveEffect : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float cooldownReductionPerHit = 0.15f;

        private PlayerAttackController _attack;
        private TexasSwordRainSkill _skill;

        private void Awake()
        {
            _attack = GetComponent<PlayerAttackController>();
            _skill = GetComponent<TexasSwordRainSkill>();
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
            if (target != null && target.Status.Has(CombatStatusType.Shock))
                _skill.ReduceCooldown(cooldownReductionPerHit);
        }
    }
}
