using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Adds one stationary target at Ch'en's prototype spawn point. It deliberately has no AI
    /// component, so the target cannot move or attack while FX timing is being tuned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChenTrainingDummySpawner : MonoBehaviour
    {
        [SerializeField] private Vector3 spawnOffset = new(1.8f, 0f, 0f);
        [SerializeField] private bool showHealthBar = true;
        [SerializeField] private GameObject presentationPrefab;

        private GameObject _dummy;

        public void ConfigurePresentationPrefab(GameObject prefab)
        {
            presentationPrefab = prefab;
        }

        private void Start()
        {
            // Legacy compatibility only. Training dummies are now scene-level public facilities
            // created by Prototype25DProductionFactory, never by a playable operator.
            enabled = false;
        }

        private void SpawnDummy()
        {
            if (_dummy != null)
                return;

            ResolvePresentationPrefab();

            var camera = Camera.main;
            var horizontalRight = camera != null
                ? Vector3.ProjectOnPlane(camera.transform.right, Vector3.up)
                : Vector3.right;
            if (horizontalRight.sqrMagnitude < 0.001f)
                horizontalRight = Vector3.right;
            horizontalRight.Normalize();

            _dummy = new GameObject("Chen_TrainingDummy_Infinite");
            _dummy.transform.position = transform.position +
                                        horizontalRight * spawnOffset.x +
                                        Vector3.up * spawnOffset.y;

            var collider = _dummy.AddComponent<CharacterController>();
            collider.radius = 0.42f;
            collider.height = 1.72f;
            collider.center = new Vector3(0f, 0.86f, 0f);
            collider.stepOffset = 0.2f;
            collider.slopeLimit = 45f;

            if (!BuildPresentation(_dummy.transform))
                BuildFallbackVisual(_dummy.transform);

            var health = _dummy.AddComponent<Health>();
            health.SetMaxHealth(1000000000f);
            _dummy.AddComponent<StatusController>();
            var entity = _dummy.AddComponent<CombatEntity>();
            entity.SetTeam(Team.Enemy);
            _dummy.AddComponent<ChenTrainingDummyInfiniteHealth>();
            _dummy.AddComponent<EnemyHitReaction2D>();
            _dummy.AddComponent<DamageTintFlash2D>();
            _dummy.AddComponent<DamageNumberEmitter2D>();
            if (showHealthBar)
                _dummy.AddComponent<WorldHealthBar2D>();

            Debug.Log(
                $"[ArknightsACT/ChenFX] 已创建测试假人：位置={_dummy.transform.position}，模型={(presentationPrefab != null ? "大盾" : "Fallback")}，可受击、无 AI、无限血量。",
                _dummy);
        }

        private void ResolvePresentationPrefab()
        {
            if (presentationPrefab != null)
                return;

            // Older saved scenes may contain the generated HeavyDefender template but not the
            // new serialized field yet. Find the already-loaded prefab asset so those scenes get
            // the real big-shield model without requiring a destructive scene rebuild.
            var loadedObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (var i = 0; i < loadedObjects.Length; i++)
            {
                var candidate = loadedObjects[i];
                if (candidate != null && candidate.name == "enemy_1006_shield" && !candidate.scene.IsValid())
                {
                    presentationPrefab = candidate;
                    return;
                }
            }
        }

        private bool BuildPresentation(Transform parent)
        {
            if (presentationPrefab == null)
                return false;

            var billboard = new GameObject("PresentationBillboard");
            billboard.transform.SetParent(parent, false);
            billboard.transform.localPosition = Vector3.zero;
            billboard.AddComponent<BillboardPresentation25D>().Configure(Camera.main);

            var visual = Instantiate(presentationPrefab, billboard.transform, false);
            visual.name = "Presentation_enemy_1006_shield";
            visual.transform.localPosition = new Vector3(0f, 0.71f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            return true;
        }

        private static void BuildFallbackVisual(Transform parent)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            body.transform.localScale = new Vector3(0.62f, 0.72f, 0.62f);
            ApplyMaterial(body.GetComponent<Renderer>(), new Color(0.78f, 0.22f, 0.18f));

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "TargetCore";
            head.transform.SetParent(parent, false);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.transform.localScale = new Vector3(0.48f, 0.48f, 0.48f);
            ApplyMaterial(head.GetComponent<Renderer>(), new Color(1.0f, 0.62f, 0.16f));
        }

        private static void ApplyMaterial(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Unlit/Color");
            if (shader == null)
                return;

            var material = new Material(shader) { name = "Chen_TrainingDummy_Material" };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            renderer.sharedMaterial = material;
        }
    }

    /// <summary>
    /// Lets the normal damage pipeline run so hit feedback, damage numbers and target-side FX
    /// are observable, then refills the target after the damage notification. The large health
    /// pool plus refill makes the dummy effectively infinite without making it invulnerable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity), typeof(Health))]
    public sealed class ChenTrainingDummyInfiniteHealth : MonoBehaviour, IDamageModifier
    {
        private CombatEntity _entity;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
        }

        private void OnEnable()
        {
            if (_entity == null)
                _entity = GetComponent<CombatEntity>();
            if (_entity != null)
                _entity.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_entity != null)
                _entity.Damaged -= OnDamaged;
        }

        public float ModifyOutgoingDamage(in DamageContext context, float currentDamage) => currentDamage;
        public float ModifyIncomingDamage(in DamageContext context, float currentDamage) => currentDamage;

        private void OnDamaged(DamageContext context, DamageResult result)
        {
            if (result.Applied && _entity?.Health != null)
                _entity.Health.SetMaxHealth(Mathf.Max(1000000000f, _entity.Health.MaxHealth), true);
        }
    }
}
