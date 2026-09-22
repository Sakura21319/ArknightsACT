using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>Material-backed megastructures outside the playable bounds, rebuilt with each stage.</summary>
    [DefaultExecutionOrder(26)]
    [DisallowMultipleComponent]
    public sealed class ChernobogInfrastructureController : MonoBehaviour
    {
        private RogueliteStageRuntimeContext _context;
        private Transform _built;
        private void Awake() => _context = GetComponent<RogueliteStageRuntimeContext>();
        private void OnEnable()
        {
            if (_context != null) _context.StageRootChanged += Build;
            Build(_context != null ? _context.StageRoot : null);
        }
        private void OnDisable()
        {
            if (_context != null) _context.StageRootChanged -= Build;
        }
        private void Build(Transform stage)
        {
            if (stage == null || _built == stage || _context.EnvironmentKit == null || _context.StageMap == null) return;
            var root = new GameObject("[Chernobog_Infrastructure]").transform;
            root.SetParent(stage, false);
            var halfX = _context.StageMap.Width * RogueliteStageWorldMetrics.ChunkWidth * 0.5f;
            var halfZ = _context.StageMap.Height * RogueliteStageWorldMetrics.ChunkDepth * 0.5f;
            var kit = _context.EnvironmentKit;
            for (var i = 0; i < 3; i++)
            {
                var x = -halfX + 10f + i * halfX * 0.7f;
                Part(root, "IndustrialTower", new Vector3(x, 8f, halfZ + 12f), new Vector3(3.6f, 16f, 3.6f), kit.wallMaterial);
                Part(root, "TowerCrown", new Vector3(x, 16.3f, halfZ + 12f), new Vector3(4f, 0.6f, 4f), kit.steelMaterial);
                for (var band = 0; band < 4; band++)
                    Part(root, "TowerServiceBand", new Vector3(x, 3f + band * 3f, halfZ + 10.1f), new Vector3(3.4f, 0.14f, 0.2f), kit.insetMaterial);
            }
            Part(root, "IndustrialPipeBridge", new Vector3(0f, 7.5f, halfZ + 8f), new Vector3(halfX * 2f, 0.4f, 1.2f), kit.steelMaterial);
            for (var i = -1; i <= 1; i += 2)
                Part(root, "PipeBridgeSupport", new Vector3(i * halfX * 0.7f, 3.7f, halfZ + 8f), new Vector3(0.5f, 7.4f, 0.7f), kit.insetMaterial);
            _built = stage;
        }
        private static void Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = ChernobogBeveledMeshFactory.GetBox(size, 0.06f);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}