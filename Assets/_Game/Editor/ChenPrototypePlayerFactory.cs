#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.Chen;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Presentation;
using ArknightsACT.Gameplay.Roguelite;
using ArknightsACT.Gameplay.Roguelite.Collectibles;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Ch'en-specific horizontal ACT composition. Character-specific skills and presentation
    /// mapping live under Gameplay/Characters/Chen; generic movement/combat stays reusable.
    /// </summary>
    internal static class ChenPrototypePlayerFactory
    {
        public static GameObject Create(AttackDefinition[] attacks, float spawnY)
        {
            var go = new GameObject("Player_Chen");
            go.transform.position = new Vector3(0f, spawnY, 0f);
            go.transform.localScale = Vector3.one;

            GameObject combatPresentation = null;
            var hasCombatPresentation = PrtsGeneratedPresentation.TryAttach(
                PrtsPrototypeAssetCatalog.Chen.BaseName,
                go.transform,
                out combatPresentation);

            if (!hasCombatPresentation)
            {
                CreatePlaceholder(go.transform);
                Debug.LogWarning(
                    "[ArknightsACT/Spine] Ch'en combat presentation prefab is missing; using placeholder. " +
                    "Download Ch'en and run '3. Build Presentation Prefabs' before rebuilding Prototype Scene.",
                    go);
            }
            else
            {
                var hasMotionSource = PrtsGeneratedPresentation.TryAttach(
                    PrtsPrototypeAssetCatalog.ChenBaseMotion.BaseName,
                    go.transform,
                    out var motionSource);

                if (hasMotionSource)
                {
                    motionSource.name = "MotionSource_Chen_Base";
                    var retarget = go.AddComponent<SpineBoneMotionRetarget2D>();
                    retarget.Configure(combatPresentation.transform, motionSource.transform, "Move");
                }
                else
                {
                    Debug.LogWarning(
                        "[ArknightsACT/Spine] Ch'en base motion source prefab is missing. " +
                        "Movement will use the combat presentation fallback until it is downloaded/built.",
                        go);
                }
            }

            var body = go.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.gravityScale = 1f;

            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.72f, 1.45f);

            go.AddComponent<Health>().SetMaxHealth(100f);
            var entity = go.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Player);

            go.AddComponent<PlayerInputReader>();
            go.AddComponent<PlayerMotor2D>();
            go.AddComponent<PlayerDashController>();
            go.AddComponent<PlayerAttackController>().Configure(attacks, 10f);
            go.AddComponent<PlayerDamageGate>();

            go.AddComponent<ChenSkill1>();
            go.AddComponent<ChenSkill2>();
            go.AddComponent<PlayerSkillController>();

            var profile = go.AddComponent<PlayerCombatProfile>();
            profile.Configure(
                CombatFeature.BasicAttack |
                CombatFeature.ActiveSkills |
                CombatFeature.Dash |
                CombatFeature.PhysicalDamage |
                CombatFeature.ArtsDamage);
            go.AddComponent<CollectibleInventory>();

            go.AddComponent<ChenPresentationDriver2D>();
            go.AddComponent<DamageTintFlash2D>();
            go.AddComponent<WorldHealthBar2D>();
            go.AddComponent<DamageNumberEmitter2D>();
            return go;
        }

        private static void CreatePlaceholder(Transform parent)
        {
            var visual = new GameObject("ChenPlaceholder");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.78f, 1.52f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 30;
            visual.AddComponent<PlaceholderVisual2D>().SetColor(new Color(0.60f, 0.70f, 0.82f));
        }
    }
}
#endif
