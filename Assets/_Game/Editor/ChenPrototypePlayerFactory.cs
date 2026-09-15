#if UNITY_EDITOR
using ArknightsACT.Combat;
using ArknightsACT.Editor.PRTS;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Feedback;
using ArknightsACT.Gameplay.Input;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Editor
{
    /// <summary>
    /// Ch'en-specific prototype composition. It intentionally does not inherit Texas abilities or
    /// Texas build effects. We first discover and validate Ch'en's authored PRTS action library,
    /// then add character-specific gameplay on top of the generic ACT controllers.
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
                    Debug.Log(
                        "[ArknightsACT/Spine] Ch'en combat model + base motion source attached. " +
                        "Runtime retarget will validate shared bones in Play Mode.",
                        go);
                }
                else
                {
                    Debug.LogWarning(
                        "[ArknightsACT/Spine] Ch'en base motion source prefab is missing. " +
                        "Movement will fall back to the combat presentation until it is downloaded/built.",
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
            go.AddComponent<AttackSlashPresentation2D>();
            go.AddComponent<PlayerAttackController>().Configure(attacks, 10f);
            go.AddComponent<PlayerDamageGate>();
            go.AddComponent<PlayerPresentationDriver2D>();
            go.AddComponent<SpineAttackPlaybackSpeed2D>();
            go.AddComponent<DamageTintFlash2D>();

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
