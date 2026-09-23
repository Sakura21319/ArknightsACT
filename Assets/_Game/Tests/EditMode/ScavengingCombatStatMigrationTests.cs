using System.Collections.Generic;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using ArknightsACT.Gameplay.Roguelite.Treasure;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsACT.Tests
{
    public sealed class ScavengingCombatStatMigrationTests
    {
        [Test]
        public void EnemyDefenseRelics_UseFormalDefenseAuraEffect()
        {
            var definitions = ScavengingCatalog.Load();
            try
            {
                AssertDefenseAura(definitions, "relic_59", -0.12f);
                AssertDefenseAura(definitions, "relic_60", -0.15f);
                AssertDefenseAura(definitions, "relic_61", -0.21f);
                AssertDefenseAura(definitions, "rogue_6_relic_legacy_86", -0.30f);
            }
            finally
            {
                for (var i = 0; i < definitions.Count; i++)
                    if (definitions[i] != null)
                        Object.DestroyImmediate(definitions[i]);
            }
        }

        private static void AssertDefenseAura(
            List<CollectibleDefinition> definitions,
            string id,
            float expected)
        {
            CollectibleDefinition found = null;
            for (var i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && definitions[i].Id == id)
                {
                    found = definitions[i];
                    break;
                }
            }

            Assert.That(found, Is.Not.Null, "Missing collectible " + id);

            var hasAura = false;
            var hasLegacyPhysicalDamage = false;
            var effects = found.Effects;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect.Type == CollectibleEffectType.EnemyPhysicalDefensePercent)
                {
                    hasAura = true;
                    Assert.That(effect.Value, Is.EqualTo(expected).Within(0.0001f));
                }

                if (effect.Type == CollectibleEffectType.PhysicalDamagePercent)
                    hasLegacyPhysicalDamage = true;
            }

            Assert.That(hasAura, Is.True, id + " should use EnemyPhysicalDefensePercent");
            Assert.That(hasLegacyPhysicalDamage, Is.False, id + " should not use legacy PhysicalDamagePercent");
        }
    }
}
