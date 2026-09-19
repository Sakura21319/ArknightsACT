using UnityEngine;
using ArknightsACT.Gameplay.Roguelite.Routing;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Visual road language layer. Keeps markings separate from tactical collision.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChernobogRoadMarkingController : MonoBehaviour
    {
        private const float ChunkWidth = RogueliteStageWorldMetrics.ChunkWidth;
        private const float ChunkDepth = RogueliteStageWorldMetrics.ChunkDepth;

        [SerializeField] private RogueliteStageMapController stageMap;
        private RogueliteStageRuntimeContext _context;
        private GameObject _preparedStage;

        public void Configure(RogueliteStageMapController map)
        {
            _context ??= GetComponent<RogueliteStageRuntimeContext>();
            stageMap = map;
            TryBuild();
        }

        private void Awake()
        {
            _context = GetComponent<RogueliteStageRuntimeContext>();
            if (_context != null)
                stageMap ??= _context.StageMap;
        }

        private void OnEnable()
        {
            if (_context != null)
                _context.StageRootChanged += OnStageRootChanged;

            TryBuild();
        }

        private void OnDisable()
        {
            if (_context != null)
                _context.StageRootChanged -= OnStageRootChanged;
        }

        private void OnStageRootChanged(Transform stageRoot)
        {
            TryBuild();
        }

        private void TryBuild()
        {
            if (_context == null)
                return;
            stageMap ??= _context.StageMap;
            if (stageMap == null)
                return;

            var stage = _context.StageRoot != null ? _context.StageRoot.gameObject : null;
            if (stage == null || stage == _preparedStage)
                return;

            var root = new GameObject("[Chernobog_RoadVisualLanguage]").transform;
            root.SetParent(stage.transform, false);

            for (var i = 0; i < stageMap.Blocks.Count; i++)
            {
                var data = stageMap.Blocks[i];
                if (data == null)
                    continue;

                var block = new GameObject($"RoadMarking_Block_{i:00}");
                block.transform.SetParent(root, false);
                block.transform.localPosition = GetChunkCenter(data.Coordinate) + Vector3.up * 0.12f;

                CreateMark(block.transform, "LaneCenter", new Vector3(0.08f, 0.01f, 8f));
                CreateMark(block.transform, "Crosswalk", new Vector3(4.5f, 0.01f, 0.18f));
                CreateMark(block.transform, "Maintenance", new Vector3(2.2f, 0.008f, 0.06f));
            }

            _preparedStage = stage;
        }

        private Vector3 GetChunkCenter(Vector2Int coordinate)
        {
            var origin = new Vector3(
                -(stageMap.Width - 1) * ChunkWidth * 0.5f,
                0f,
                -(stageMap.Height - 1) * ChunkDepth * 0.5f);
            return origin + new Vector3(coordinate.x * ChunkWidth, 0f, coordinate.y * ChunkDepth);
        }

        private static void CreateMark(Transform parent, string name, Vector3 scale)
        {
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = name;
            mark.transform.SetParent(parent, false);
            mark.transform.localScale = scale;
            var collider = mark.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }
    }
}
