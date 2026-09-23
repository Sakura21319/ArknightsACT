using System;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Abilities;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    /// <summary>
    /// Optional contract for run state that must follow the player when the active operator changes.
    /// Character-local presentation/skill state should not implement this interface unless it is
    /// intentionally shared between operators.
    /// </summary>
    public interface IPlayerSwitchStateTransfer
    {
        void CopySwitchStateTo(Transform destination);
    }

    /// <summary>
    /// Stable runtime indirection for the current controllable operator.
    /// World/run systems depend on this service instead of retaining a scene-baked Player reference.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class PlayerRuntimeContext : MonoBehaviour
    {
        [SerializeField] private Transform activePlayer;

        private readonly List<Transform> _registeredPlayers = new();

        public static PlayerRuntimeContext Instance { get; private set; }
        public Transform ActivePlayer => activePlayer;
        public IReadOnlyList<Transform> RegisteredPlayers => _registeredPlayers;

        public event Action<Transform, Transform> ActivePlayerChanged;

        public static Transform Resolve(Transform fallback = null)
        {
            var current = Instance != null ? Instance.activePlayer : null;
            return current != null ? current : fallback;
        }

        public void Configure(Transform initialPlayer)
        {
            RegisterPlayer(initialPlayer);
            activePlayer = initialPlayer;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ArknightsACT/Player] Duplicate PlayerRuntimeContext detected; keeping the first instance.", this);
                return;
            }

            Instance = this;

            // Reserve operators are intentionally inactive in PrototypeRun. Their OnEnable does not
            // run when the scene is loaded, and the runtime registration list is not serialized.
            // Rebuild the registry from all scene identities so saved/generated scenes can switch
            // operators immediately without requiring every reserve GameObject to be activated once.
            DiscoverScenePlayers();

            if (activePlayer != null)
                RegisterPlayer(activePlayer);

            Debug.Log(
                $"[ArknightsACT/Player] Runtime operator registry initialized: {_registeredPlayers.Count} player(s).",
                this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void DiscoverScenePlayers()
        {
            var identities = Resources.FindObjectsOfTypeAll<PlayableOperatorIdentity>();
            for (var i = 0; i < identities.Length; i++)
            {
                var identity = identities[i];
                if (identity == null)
                    continue;

                var candidate = identity.gameObject;
                if (!candidate.scene.IsValid() || candidate.scene != gameObject.scene)
                    continue;

                var entity = candidate.GetComponent<CombatEntity>();
                if (entity == null || entity.Team != Team.Player)
                    continue;

                RegisterPlayer(identity.transform);
            }
        }

        public void RegisterPlayer(Transform player)
        {
            if (player == null || _registeredPlayers.Contains(player))
                return;
            _registeredPlayers.Add(player);
        }

        public void UnregisterPlayer(Transform player)
        {
            if (player == null)
                return;
            _registeredPlayers.Remove(player);
        }

        public bool TrySwitchTo(string operatorId, string skinId = null, bool preserveRunState = true)
        {
            if (string.IsNullOrWhiteSpace(operatorId))
                return false;

            for (var i = 0; i < _registeredPlayers.Count; i++)
            {
                var candidate = _registeredPlayers[i];
                if (candidate == null)
                    continue;
                var identity = candidate.GetComponent<PlayableOperatorIdentity>();
                if (identity == null ||
                    !string.Equals(identity.OperatorId, operatorId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrWhiteSpace(skinId) &&
                    !string.Equals(identity.SkinId, skinId, StringComparison.OrdinalIgnoreCase))
                    continue;
                return SwitchTo(candidate, preserveRunState);
            }

            return false;
        }

        public bool SwitchTo(Transform nextPlayer, bool preserveRunState = true)
        {
            if (nextPlayer == null)
                return false;
            if (nextPlayer == activePlayer)
                return true;

            var nextEntity = nextPlayer.GetComponent<CombatEntity>();
            if (nextEntity == null || nextEntity.Team != Team.Player)
            {
                Debug.LogError("[ArknightsACT/Player] Runtime switch target must be a Team.Player CombatEntity.", nextPlayer);
                return false;
            }

            var previous = activePlayer;
            if (previous != null)
            {
                var previousHealth = previous.GetComponent<Health>();
                if (previousHealth != null && previousHealth.IsDead)
                    return false;

                nextPlayer.position = previous.position;
                nextPlayer.rotation = previous.rotation;
            }

            RegisterPlayer(nextPlayer);

            // Awake/OnEnable must run before state is copied into a previously inactive operator.
            if (!nextPlayer.gameObject.activeSelf)
                nextPlayer.gameObject.SetActive(true);

            if (preserveRunState && previous != null)
            {
                var transferables = previous.GetComponents<MonoBehaviour>();
                for (var i = 0; i < transferables.Length; i++)
                {
                    if (transferables[i] is IPlayerSwitchStateTransfer transferable)
                        transferable.CopySwitchStateTo(nextPlayer);
                }

                CopyHealthRatio(previous, nextPlayer);
                CopyCombatStatuses(previous, nextPlayer);
                CopySameOperatorSkillState(previous, nextPlayer);
            }

            activePlayer = nextPlayer;

            if (previous != null && previous != nextPlayer && previous.gameObject.activeSelf)
                previous.gameObject.SetActive(false);
            if (!nextPlayer.gameObject.activeSelf)
                nextPlayer.gameObject.SetActive(true);

            ActivePlayerChanged?.Invoke(previous, nextPlayer);

            var identity = nextPlayer.GetComponent<PlayableOperatorIdentity>();
            Debug.Log(
                identity != null
                    ? $"[ArknightsACT/Player] Active operator -> {identity.DisplayName} ({identity.OperatorId}/{identity.SkinId})."
                    : $"[ArknightsACT/Player] Active operator -> {nextPlayer.name}.",
                nextPlayer);
            return true;
        }

        private static void CopyCombatStatuses(Transform previous, Transform next)
        {
            if (previous == null || next == null)
                return;

            var source = previous.GetComponent<StatusController>();
            var destination = next.GetComponent<StatusController>();
            source?.CopyActiveTo(destination);
        }

        private static void CopySameOperatorSkillState(Transform previous, Transform next)
        {
            var sourceIdentity = previous != null
                ? previous.GetComponent<PlayableOperatorIdentity>()
                : null;
            var destinationIdentity = next != null
                ? next.GetComponent<PlayableOperatorIdentity>()
                : null;
            if (sourceIdentity == null ||
                destinationIdentity == null ||
                !string.Equals(
                    sourceIdentity.OperatorId,
                    destinationIdentity.OperatorId,
                    StringComparison.OrdinalIgnoreCase))
                return;

            var sourceSkills = previous.GetComponent<PlayerSkillController>();
            var destinationSkills = next.GetComponent<PlayerSkillController>();
            if (sourceSkills == null || destinationSkills == null)
                return;

            CopySkillPointRatio(sourceSkills.Skill1, destinationSkills.Skill1);
            CopySkillPointRatio(sourceSkills.Skill2, destinationSkills.Skill2);
        }

        private static void CopySkillPointRatio(IPlayerSkill source, IPlayerSkill destination)
        {
            if (source == null || destination == null)
                return;

            destination.SetSkillPoints(
                Mathf.Clamp01(source.SkillPointRatio) * destination.SkillPointCost);
        }

        private static void CopyHealthRatio(Transform previous, Transform next)
        {
            var source = previous != null ? previous.GetComponent<Health>() : null;
            var destination = next != null ? next.GetComponent<Health>() : null;
            if (source == null || destination == null)
                return;

            var ratio = source.MaxHealth > 0f
                ? Mathf.Clamp01(source.CurrentHealth / source.MaxHealth)
                : 1f;
            destination.SetCurrentHealth(destination.MaxHealth * ratio);
        }
    }
}
