using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    /// <summary>
    /// Runtime-facing identity and local UI resource mapping for the currently composed operator.
    /// Gameplay systems consume this component instead of hard-coding a concrete operator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayableOperatorIdentity : MonoBehaviour
    {
        [SerializeField] private string operatorId;
        [SerializeField] private string displayName;
        [SerializeField] private string skinId;
        [SerializeField] private string skinDisplayName;
        [SerializeField] private string avatarResourceKey;
        [SerializeField] private string skill1IconResourceKey;
        [SerializeField] private string skill2IconResourceKey;

        public string OperatorId => operatorId;
        public string DisplayName => displayName;
        public string SkinId => skinId;
        public string SkinDisplayName => string.IsNullOrWhiteSpace(skinDisplayName) ? skinId : skinDisplayName;
        public string AvatarResourceKey => avatarResourceKey;
        public string Skill1IconResourceKey => skill1IconResourceKey;
        public string Skill2IconResourceKey => skill2IconResourceKey;

        private void OnEnable()
        {
            PlayerRuntimeContext.Instance?.RegisterPlayer(transform);
        }

        public void Configure(
            string id,
            string name,
            string skin,
            string avatarKey,
            string skill1Key,
            string skill2Key,
            string skinName = null)
        {
            operatorId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            skinId = skin ?? string.Empty;
            skinDisplayName = string.IsNullOrWhiteSpace(skinName) ? skinId : skinName;
            avatarResourceKey = avatarKey ?? string.Empty;
            skill1IconResourceKey = skill1Key ?? string.Empty;
            skill2IconResourceKey = skill2Key ?? string.Empty;
        }
    }
}
