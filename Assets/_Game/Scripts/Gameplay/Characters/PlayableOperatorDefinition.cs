using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    [Serializable]
    public sealed class PlayableOperatorSkinDefinition
    {
        [SerializeField] private string skinId = "default";
        [SerializeField] private string displayName = "默认";
        [SerializeField] private string avatarResourceKey;
        [SerializeField] private string skill1IconResourceKey;
        [SerializeField] private string skill2IconResourceKey;
        [SerializeField] private bool isDefault;
        [SerializeField] private bool isReserved;

        public string SkinId => skinId;
        public string DisplayName => displayName;
        public string AvatarResourceKey => avatarResourceKey;
        public string Skill1IconResourceKey => skill1IconResourceKey;
        public string Skill2IconResourceKey => skill2IconResourceKey;
        public bool IsDefault => isDefault;
        public bool IsReserved => isReserved;

        public PlayableOperatorSkinDefinition() { }

        public PlayableOperatorSkinDefinition(
            string id,
            string name,
            string avatarKey,
            string skill1Key,
            string skill2Key,
            bool defaultSkin = false,
            bool reserved = false)
        {
            skinId = string.IsNullOrWhiteSpace(id) ? "default" : id;
            displayName = string.IsNullOrWhiteSpace(name) ? skinId : name;
            avatarResourceKey = avatarKey ?? string.Empty;
            skill1IconResourceKey = skill1Key ?? string.Empty;
            skill2IconResourceKey = skill2Key ?? string.Empty;
            isDefault = defaultSkin;
            isReserved = reserved;
        }
    }

    /// <summary>
    /// Data-only description of a playable operator and its available skins.
    /// Runtime/editor systems consume this asset instead of maintaining central character switch statements.
    /// Character-specific construction remains in an auto-discovered editor builder.
    /// </summary>
    [CreateAssetMenu(
        menuName = "ArknightsACT/Characters/Operator Definition",
        fileName = "Operator_")]
    public sealed class PlayableOperatorDefinition : ScriptableObject
    {
        [SerializeField] private string operatorId;
        [SerializeField] private string displayName;
        [SerializeField] private string englishName;
        [SerializeField] private int sortOrder;
        [SerializeField] private PlayableOperatorSkinDefinition[] skins = Array.Empty<PlayableOperatorSkinDefinition>();

        public string OperatorId => operatorId;
        public string DisplayName => displayName;
        public string EnglishName => englishName;
        public int SortOrder => sortOrder;
        public IReadOnlyList<PlayableOperatorSkinDefinition> Skins => skins;

        public PlayableOperatorSkinDefinition DefaultSkin
        {
            get
            {
                if (skins == null || skins.Length == 0)
                    return null;

                for (var i = 0; i < skins.Length; i++)
                {
                    if (skins[i] != null && skins[i].IsDefault)
                        return skins[i];
                }

                return skins[0];
            }
        }

        public PlayableOperatorSkinDefinition FindSkin(string skinId)
        {
            if (skins == null || skins.Length == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(skinId))
            {
                for (var i = 0; i < skins.Length; i++)
                {
                    var skin = skins[i];
                    if (skin != null &&
                        string.Equals(skin.SkinId, skinId, StringComparison.OrdinalIgnoreCase))
                        return skin;
                }
            }

            return DefaultSkin;
        }

#if UNITY_EDITOR
        public void Configure(
            string id,
            string name,
            string english,
            int order,
            params PlayableOperatorSkinDefinition[] skinDefinitions)
        {
            operatorId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            englishName = english ?? string.Empty;
            sortOrder = order;
            skins = skinDefinitions ?? Array.Empty<PlayableOperatorSkinDefinition>();
        }
#endif
    }
}
