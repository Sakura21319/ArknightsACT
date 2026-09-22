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
        [SerializeField] private string avatarResourceKey;
        [SerializeField] private string skill1IconResourceKey;
        [SerializeField] private string skill2IconResourceKey;

        public string OperatorId => operatorId;
        public string DisplayName => displayName;
        public string SkinId => skinId;
        public string AvatarResourceKey => avatarResourceKey;
        public string Skill1IconResourceKey => skill1IconResourceKey;
        public string Skill2IconResourceKey => skill2IconResourceKey;

        public void Configure(
            string id,
            string name,
            string skin,
            string avatarKey,
            string skill1Key,
            string skill2Key)
        {
            operatorId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            skinId = skin ?? string.Empty;
            avatarResourceKey = avatarKey ?? string.Empty;
            skill1IconResourceKey = skill1Key ?? string.Empty;
            skill2IconResourceKey = skill2Key ?? string.Empty;
        }
    }
}
