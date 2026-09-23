using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    /// <summary>
    /// Public gameplay-facing entry point for operator switching.
    /// UI, menus and stage events call this controller instead of enabling/disabling player objects.
    /// No input binding is owned here, so control schemes can evolve independently.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayableOperatorSwitchController : MonoBehaviour
    {
        [SerializeField] private PlayerRuntimeContext context;
        [SerializeField] private bool blockDuringPlayerActions = true;
        [SerializeField, Min(0f)] private float switchCooldown = 0.20f;

        private float _nextSwitchAt;

        public PlayerRuntimeContext Context => context != null ? context : PlayerRuntimeContext.Instance;
        public Transform ActivePlayer => Context != null ? Context.ActivePlayer : null;

        public void Configure(PlayerRuntimeContext runtimeContext)
        {
            context = runtimeContext;
        }

        private void Awake()
        {
            context ??= GetComponent<PlayerRuntimeContext>();
        }

        public void Register(Transform player)
        {
            Context?.RegisterPlayer(player);
        }

        public void RegisterReserve(Transform player)
        {
            var runtime = Context;
            if (runtime == null || player == null)
                return;

            runtime.RegisterPlayer(player);
            if (player != runtime.ActivePlayer && player.gameObject.activeSelf)
                player.gameObject.SetActive(false);
        }

        public bool TrySwitchTo(
            string operatorId,
            string skinId = null,
            bool preserveRunState = true)
        {
            var runtime = Context;
            if (runtime == null || !CanSwitchNow())
                return false;

            if (!runtime.TrySwitchTo(operatorId, skinId, preserveRunState))
                return false;

            _nextSwitchAt = Time.unscaledTime + switchCooldown;
            return true;
        }

        public bool TrySwitchTo(Transform player, bool preserveRunState = true)
        {
            var runtime = Context;
            if (runtime == null || player == null || !CanSwitchNow())
                return false;

            if (!runtime.SwitchTo(player, preserveRunState))
                return false;

            _nextSwitchAt = Time.unscaledTime + switchCooldown;
            return true;
        }

        public bool TryCycleNext(bool preserveRunState = true)
        {
            var runtime = Context;
            if (runtime == null || !CanSwitchNow())
                return false;

            var players = runtime.RegisteredPlayers;
            if (players == null || players.Count <= 1)
                return false;

            var current = runtime.ActivePlayer;
            var currentIndex = -1;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i] == current)
                {
                    currentIndex = i;
                    break;
                }
            }

            for (var offset = 1; offset <= players.Count; offset++)
            {
                var index = (Mathf.Max(-1, currentIndex) + offset) % players.Count;
                var candidate = players[index];
                if (candidate == null || candidate == current)
                    continue;
                if (runtime.SwitchTo(candidate, preserveRunState))
                {
                    _nextSwitchAt = Time.unscaledTime + switchCooldown;
                    return true;
                }
            }

            return false;
        }

        public bool CanSwitchNow()
        {
            if (Time.unscaledTime < _nextSwitchAt)
                return false;

            var current = ActivePlayer;
            if (current == null)
                return false;

            var entity = current.GetComponent<ArknightsACT.Combat.CombatEntity>();
            if (entity?.Health == null || entity.Health.IsDead)
                return false;
            if (CombatActionUtility.IsBlocked(entity, CombatActionMask.Interaction))
                return false;

            if (!blockDuringPlayerActions)
                return true;

            var attack = current.GetComponent<PlayerAttackController>();
            if (attack != null && attack.IsAttacking)
                return false;

            var skills = current.GetComponent<PlayerSkillController>();
            if (skills != null)
            {
                if (skills.IsCasting)
                    return false;
                if (skills.Skill1 is IPlayerSkillActiveState activeSkill1 && activeSkill1.IsActive)
                    return false;
                if (skills.Skill2 is IPlayerSkillActiveState activeSkill2 && activeSkill2.IsActive)
                    return false;
            }

            var dash = current.GetComponent<PlayerDashController>();
            if (dash != null && dash.IsDashing)
                return false;

            return true;
        }
    }
}
