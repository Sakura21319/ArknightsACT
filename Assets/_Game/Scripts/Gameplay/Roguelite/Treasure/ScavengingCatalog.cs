using System;
using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Treasure
{
    public static class ScavengingCatalog
    {
        [Serializable] private sealed class Document { public Entry[] items; }
        [Serializable] private sealed class Entry
        {
            public string id, name, description, effect, profession, rarity, kind;
            public string salvageRarity, icon, rawEffect;
            public float value;
            public int stacks, grade, collectionValue, w, h;
            public EffectEntry[] effects;
        }
        [Serializable] private sealed class EffectEntry
        {
            public string type, profession;
            public float value, duration;
        }
        // Caller owns these runtime definitions. New items require only a JSON entry.
        public static List<CollectibleDefinition> Load()
        {
            var result = new List<CollectibleDefinition>();
            var asset = Resources.Load<TextAsset>("ScavengingCatalog");
            if (asset == null) throw new InvalidOperationException("Missing Resources/ScavengingCatalog.json");
            var ids = new HashSet<string>();
            foreach (var item in JsonUtility.FromJson<Document>(asset.text).items)
            {
                if (string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id))
                    throw new InvalidOperationException("Duplicate/empty scavenging collectible ID: " + item.id);
                var commodity = string.Equals(item.kind, "Commodity", StringComparison.OrdinalIgnoreCase);
                var rarity = string.IsNullOrWhiteSpace(item.rarity)
                    ? GradeToCollectibleRarity(item.grade)
                    : Enum.Parse<CollectibleRarity>(item.rarity);
                var effect = string.IsNullOrWhiteSpace(item.effect)
                    ? CollectibleEffectType.MaxHealthPercent
                    : Enum.Parse<CollectibleEffectType>(item.effect);
                var profession = string.IsNullOrWhiteSpace(item.profession)
                    ? OperatorProfession.Unspecified
                    : Enum.Parse<OperatorProfession>(item.profession);
                var stacks = item.stacks > 0 ? item.stacks : (commodity ? 99 : 3);
                var grade = item.grade > 0 ? item.grade : rarity switch
                {
                    CollectibleRarity.Epic => 5,
                    CollectibleRarity.Rare => 4,
                    _ => 3
                };
                var collectionValue = item.collectionValue > 0 ? item.collectionValue : rarity switch
                {
                    CollectibleRarity.Epic => 1800,
                    CollectibleRarity.Rare => 760,
                    _ => 280
                };
                var lootRarity = ResolveSalvageRarity(item.salvageRarity, grade, rarity);
                var gridSize = new Vector2Int(Mathf.Max(1, item.w), Mathf.Max(1, item.h));

                var definition = ScriptableObject.CreateInstance<CollectibleDefinition>();
                definition.Configure(item.id, item.name, item.description, rarity, CombatFeature.None, effect, item.value, stacks);
                definition.SetProfession(profession);
                var modifiers = new List<CollectibleEffectModifier>();
                if (item.effects != null)
                {
                    foreach (var spec in item.effects)
                    {
                        if (spec == null || string.IsNullOrWhiteSpace(spec.type) ||
                            !Enum.TryParse(spec.type, true, out CollectibleEffectType parsedType))
                            continue;
                        var parsedProfession = OperatorProfession.Unspecified;
                        if (!string.IsNullOrWhiteSpace(spec.profession))
                            Enum.TryParse(spec.profession, true, out parsedProfession);
                        modifiers.Add(new CollectibleEffectModifier(parsedType, spec.value, parsedProfession, spec.duration));
                    }
                }
                if (modifiers.Count == 0 && !commodity && !string.IsNullOrWhiteSpace(item.effect))
                    modifiers.Add(new CollectibleEffectModifier(effect, item.value, profession));
                definition.ConfigureEffects(modifiers.ToArray(), string.IsNullOrWhiteSpace(item.rawEffect) ? item.description : item.rawEffect);
                definition.ConfigureScavengingMetadata(commodity, lootRarity, gridSize, item.icon, collectionValue);
                result.Add(definition);
            }
            return result;
        }

        private static CollectibleRarity GradeToCollectibleRarity(int grade) => grade switch
        {
            >= 5 => CollectibleRarity.Epic,
            >= 3 => CollectibleRarity.Rare,
            _ => CollectibleRarity.Common
        };

        private static SalvageRarity ResolveSalvageRarity(string value, int legacyGrade, CollectibleRarity combatRarity)
        {
            if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out SalvageRarity parsed))
                return parsed;
            if (legacyGrade >= 5) return SalvageRarity.Mythic;
            if (legacyGrade >= 4) return SalvageRarity.Precious;
            if (legacyGrade >= 2) return SalvageRarity.Rare;
            return combatRarity switch
            {
                CollectibleRarity.Epic => SalvageRarity.Precious,
                CollectibleRarity.Rare => SalvageRarity.Rare,
                _ => SalvageRarity.Common
            };
        }
    }
}
