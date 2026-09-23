using ArknightsACT.Gameplay.Characters;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsACT.Tests
{
    public sealed class PlayableOperatorDefinitionTests
    {
        [Test]
        public void FindSkin_ReturnsExactSkinAndFallsBackToDefault()
        {
            var definition = ScriptableObject.CreateInstance<PlayableOperatorDefinition>();
            try
            {
                var defaultSkin = new PlayableOperatorSkinDefinition(
                    "default",
                    "原版",
                    "avatar/default",
                    "skill/1",
                    "skill/2",
                    true);
                var alternateSkin = new PlayableOperatorSkinDefinition(
                    "snow#1",
                    "Snow",
                    "avatar/snow",
                    "skill/1",
                    "skill/2");

                definition.Configure(
                    "Schwarz",
                    "黑",
                    "SCHWARZ",
                    20,
                    defaultSkin,
                    alternateSkin);

                Assert.That(definition.FindSkin("snow#1"), Is.SameAs(alternateSkin));
                Assert.That(definition.FindSkin("missing"), Is.SameAs(defaultSkin));
                Assert.That(definition.FindSkin(null), Is.SameAs(defaultSkin));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Identity_StoresSkinDisplayNameSeparatelyFromStableSkinId()
        {
            var go = new GameObject("IdentityTest");
            try
            {
                var identity = go.AddComponent<PlayableOperatorIdentity>();
                identity.Configure(
                    "Schwarz",
                    "黑",
                    "snow#1",
                    "avatar/snow",
                    "skill/1",
                    "skill/2",
                    "Snow");

                Assert.That(identity.OperatorId, Is.EqualTo("Schwarz"));
                Assert.That(identity.SkinId, Is.EqualTo("snow#1"));
                Assert.That(identity.SkinDisplayName, Is.EqualTo("Snow"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
