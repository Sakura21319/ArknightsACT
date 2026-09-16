using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Adds tall, renderer-free perimeter collision around the generated stage. The visible low walls
    /// stay visually faithful to the deck, while this gameplay shell prevents jumping/dashing over the
    /// edge and falling onto the decorative mobile-city chassis.
    /// </summary>
    [DefaultExecutionOrder(27)]
    [DisallowMultipleComponent]
    public sealed class RogueliteStageContainmentController : MonoBehaviour
    {
        private const float ChunkWidth = 18f;
        private const float ChunkDepth = 14f;
        private const float WallHeight = 10f;
        private const float WallThickness = 0.80f;

        [SerializeField] private RogueliteStageMapController stageMap;

        private GameObject _preparedStage;
        private float _nextResolveAt;

        public void Configure(RogueliteStageMapController map)
        {
            stageMap = map;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextResolveAt)
                return;
            _nextResolveAt = Time.unscaledTime + 0.15f;

            stageMap ??= FindFirstObjectByType<RogueliteStageMapController>();
            if (stageMap == null)
                return;

            var stage = GameObject.Find($"[Stage_{stageMap.StageIndex:00}_Runtime]");
            if (stage == null || stage == _preparedStage)
                return;

            BuildContainment(stage.transform);
            _preparedStage = stage;
        }

        private void BuildContainment(Transform stage)
        {
            var old = stage.Find("[GameplayContainment]");
            if (old != null)
                Destroy(old.gameObject);

            var width = Mathf.Max(ChunkWidth, stageMap.Width * ChunkWidth);
            var depth = Mathf.Max(ChunkDepth, stageMap.Height * ChunkDepth);
            var root = new GameObject("[GameplayContainment]").transform;
            root.SetParent(stage, false);

            // Start slightly below deck level so CharacterController step offset cannot squeeze beneath
            // the collider. The top reaches far above the player's normal jump apex.
            var centerY = WallHeight * 0.5f - 0.35f;
            CreateWall(root, "Containment_N", new Vector3(0f, centerY, depth * 0.5f + WallThickness * 0.25f),
                new Vector3(width + WallThickness * 2f, WallHeight, WallThickness));
            CreateWall(root, "Containment_S", new Vector3(0f, centerY, -depth * 0.5f - WallThickness * 0.25f),
                new Vector3(width + WallThickness * 2f, WallHeight, WallThickness));
            CreateWall(root, "Containment_E", new Vector3(width * 0.5f + WallThickness * 0.25f, centerY, 0f),
                new Vector3(WallThickness, WallHeight, depth + WallThickness * 2f));
            CreateWall(root, "Containment_W", new Vector3(-width * 0.5f - WallThickness * 0.25f, centerY, 0f),
                new Vector3(WallThickness, WallHeight, depth + WallThickness * 2f));

            Physics.SyncTransforms();
            Debug.Log($"[ArknightsACT/Containment] Stage {stageMap.StageIndex}: invisible jump-proof perimeter installed.", this);
        }

        private static void CreateWall(Transform parent, string name, Vector3 localPosition, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var collider = go.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = size;
            collider.isTrigger = false;
        }
    }
}
