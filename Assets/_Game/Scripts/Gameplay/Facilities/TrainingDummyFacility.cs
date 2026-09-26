using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArknightsACT.Gameplay.Facilities
{
    /// <summary>
    /// Scene-level training facility. It is deliberately independent from every playable operator:
    /// switching/rebuilding a character must never create or destroy the target dummy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingDummyFacility : MonoBehaviour
    {
        // Spawn-area test target. Keep a short forward offset instead of exact overlap with the
        // player's birth coordinate so melee/ranged target selection and projectile travel remain testable.
        public const float SpawnTestDistance = 3.2f;
        // Keep this comfortably within float precision. The dummy is made invincible by refilling
        // after each accepted hit, not by using an enormous HP value. At 1e9f, small attacks such
        // as 15 damage can round away entirely, causing Health.TakeDamage to report dealt=0.
        public const float DummyMaxHealth = 100000f;

        [SerializeField] private Vector3 dummyWorldPosition = new(3.2f, 0.03f, 0f);
        [SerializeField] private bool showHealthBar = true;
        [SerializeField] private GameObject presentationPrefab;

        private GameObject _dummy;

        public void Configure(
            Vector3 worldPosition,
            GameObject presentation,
            bool showBar = true)
        {
            dummyWorldPosition = worldPosition;
            presentationPrefab = presentation;
            showHealthBar = showBar;
        }

        private void Start()
        {
            ResolveActivePlayerSpawnPosition();
            EnsureDummy();
        }

        private void ResolveActivePlayerSpawnPosition()
        {
            var player = Object.FindFirstObjectByType<PlayableOperatorIdentity>();
            if (player == null)
            {
                return;
            }

            var motor = player.GetComponent<PlayerMotor25D>();
            var forward = motor != null ? motor.PlanarForward : Vector3.right;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.right;

            dummyWorldPosition =
                player.transform.position +
                forward.normalized * SpawnTestDistance +
                Vector3.up * 0.03f;
        }

        public GameObject EnsureDummy()
        {
            if (_dummy != null)
            {
                return _dummy;
            }

            var existing = GameObject.Find("TrainingDummy_Infinite");
            if (existing != null)
            {
                _dummy = existing;
                EnsureCombatShell(_dummy);
                return _dummy;
            }

            ResolvePresentationPrefab();

            _dummy = new GameObject("TrainingDummy_Infinite");
            _dummy.transform.position = dummyWorldPosition;

            EnsureCombatShell(_dummy);

            if (!BuildPresentation(_dummy.transform))
                BuildFallbackVisual(_dummy.transform);

            Physics.SyncTransforms();
            return _dummy;
        }

        private void EnsureCombatShell(GameObject dummy)
        {
            if (dummy == null)
                return;

            // A training target does not move, so a plain capsule is a more reliable target/hit
            // collider than CharacterController. Disable any legacy controller left by older scenes.
            var legacyController = dummy.GetComponent<CharacterController>();
            if (legacyController != null)
                legacyController.enabled = false;

            var hitCollider = dummy.GetComponent<CapsuleCollider>();
            if (hitCollider == null)
                hitCollider = dummy.AddComponent<CapsuleCollider>();
            hitCollider.isTrigger = false;
            hitCollider.direction = 1;
            hitCollider.radius = 0.42f;
            hitCollider.height = 1.72f;
            hitCollider.center = new Vector3(0f, 0.86f, 0f);
            hitCollider.enabled = true;

            var health = dummy.GetComponent<Health>() ?? dummy.AddComponent<Health>();
            health.SetMaxHealth(DummyMaxHealth, true);

            if (dummy.GetComponent<StatusController>() == null)
                dummy.AddComponent<StatusController>();

            var stats = dummy.GetComponent<CombatStats>() ?? dummy.AddComponent<CombatStats>();
            stats.SetBasePhysicalDefense(0f);
            stats.SetBaseArtsResistance(0f);

            var entity = dummy.GetComponent<CombatEntity>() ?? dummy.AddComponent<CombatEntity>();
            entity.enabled = true;
            entity.SetTeam(Team.Enemy);

            if (dummy.GetComponent<TrainingDummyInvincible>() == null)
                dummy.AddComponent<TrainingDummyInvincible>();
            if (dummy.GetComponent<EnemyHitReaction2D>() == null)
                dummy.AddComponent<EnemyHitReaction2D>();
            if (dummy.GetComponent<DamageTintFlash2D>() == null)
                dummy.AddComponent<DamageTintFlash2D>();
            if (dummy.GetComponent<DamageNumberEmitter2D>() == null)
                dummy.AddComponent<DamageNumberEmitter2D>();
            if (showHealthBar && dummy.GetComponent<WorldHealthBar2D>() == null)
                dummy.AddComponent<WorldHealthBar2D>();

            Physics.SyncTransforms();

        }

        private static bool IsRegistered(CombatEntity entity)
        {
            if (entity == null)
                return false;

            foreach (var candidate in CombatEntity.ActiveEntities)
                if (candidate == entity)
                    return true;

            return false;
        }

        private void ResolvePresentationPrefab()
        {
            if (presentationPrefab != null)
                return;

            var loadedObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (var i = 0; i < loadedObjects.Length; i++)
            {
                var candidate = loadedObjects[i];
                if (candidate == null ||
                    candidate.scene.IsValid() ||
                    candidate.name != "enemy_1006_shield")
                    continue;

                presentationPrefab = candidate;
                return;
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
            visual.name = "Presentation_TrainingDummy";
            visual.transform.localPosition = new Vector3(0f, 0.71f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            return true;
        }

        private static void BuildFallbackVisual(Transform parent)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "TrainingDummyBody";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            body.transform.localScale = new Vector3(0.62f, 0.72f, 0.62f);
            var bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null)
                Object.Destroy(bodyCollider);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "TrainingDummyCore";
            head.transform.SetParent(parent, false);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.transform.localScale = new Vector3(0.48f, 0.48f, 0.48f);
            var headCollider = head.GetComponent<Collider>();
            if (headCollider != null)
                Object.Destroy(headCollider);
        }
    }

    internal static class TrainingDummyFacilityBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePrototypeFacility()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "PrototypeRun")
                return;

            var existingFacility = Object.FindFirstObjectByType<TrainingDummyFacility>();
            if (existingFacility != null)
            {
                return;
            }

            var player = Object.FindFirstObjectByType<PlayableOperatorIdentity>();
            var root = new GameObject("[TrainingDummyFacility]");
            var facility = root.AddComponent<TrainingDummyFacility>();
            if (player == null)
            {
                return;
            }

            var motor = player.GetComponent<PlayerMotor25D>();
            var forward = motor != null ? motor.PlanarForward : Vector3.right;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.right;

            facility.Configure(
                player.transform.position +
                forward.normalized * TrainingDummyFacility.SpawnTestDistance +
                Vector3.up * 0.03f,
                null,
                showBar: true);
        }
    }

    /// <summary>
    /// Receives the normal damage pipeline so hit FX, damage events and damage numbers still fire,
    /// but never allows one hit to consume the final point of HP. Health is refilled immediately
    /// after every accepted hit, making the public training target effectively invincible.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity), typeof(Health))]
    public sealed class TrainingDummyInvincible : MonoBehaviour, IDamageModifier
    {
        private const float DummyHealth = TrainingDummyFacility.DummyMaxHealth;
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

        public float ModifyOutgoingDamage(in DamageContext context, float currentDamage) =>
            currentDamage;

        public float ModifyIncomingDamage(in DamageContext context, float currentDamage)
        {
            var health = _entity != null ? _entity.Health : null;
            if (health == null)
                return currentDamage;

            // Keep the normal non-zero Applied result while guaranteeing the target cannot die.
            return Mathf.Min(
                Mathf.Max(0f, currentDamage),
                Mathf.Max(0.001f, health.CurrentHealth - 1f));
        }

        private void OnDamaged(DamageContext context, DamageResult result)
        {

            if (!result.Applied || _entity?.Health == null)
                return;

            _entity.Health.SetMaxHealth(
                Mathf.Max(DummyHealth, _entity.Health.MaxHealth),
                true);
        }
    }
}
