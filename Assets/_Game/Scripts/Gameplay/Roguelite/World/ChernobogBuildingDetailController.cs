using UnityEngine;
using ArknightsACT.Gameplay.Roguelite.Routing;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Adds city-life details without coupling them to gameplay buildings.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChernobogBuildingDetailController : MonoBehaviour
    {
        [SerializeField] private RogueliteStageMapController stageMap;
        private RogueliteStageRuntimeContext _context;
        private GameObject _preparedStage;

        public void Configure(RogueliteStageMapController map)
        {
            _context ??= GetComponent<RogueliteStageRuntimeContext>();
            stageMap = map;
        }

        private void Awake()
        {
            _context = GetComponent<RogueliteStageRuntimeContext>();
            if (_context != null)
                stageMap ??= _context.StageMap;
        }

        private void Update()
        {
            if (stageMap == null && _context != null)
                stageMap = _context.StageMap;
            if (stageMap == null)
                return;

            var stage = _context != null && _context.StageRoot != null
                ? _context.StageRoot.gameObject
                : null;
            if (stage == null || stage == _preparedStage)
                return;

            var root = RogueliteStageVisualRootUtility.GetOrCreate(
                stage.transform,
                "[Chernobog_Building_Details]");
            if (root == null)
                return;

            if (_preparedStage == stage)
                return;

            CreateDetailLayer(root, "Facade_Pipes", 0.18f, 4f, 0.08f);
            CreateDetailLayer(root, "Window_Lights", 0.05f, 6f, 0.02f);
            CreateDetailLayer(root, "Roof_Machinery", 0.35f, 8f, 0.35f);
            CreateDetailLayer(root, "Industrial_Signs", 0.12f, 3f, 0.4f);
            CreateDetailLayer(root, "Maintenance_Platforms", 0.22f, 5f, 0.16f);

            _preparedStage = stage;
        }

        private static void CreateDetailLayer(Transform parent, string name, float height, float width, float depth)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(parent, false);

            // Visual dressing only. Gameplay buildings keep their own collision/navigation.
            // These placeholders provide stable attachment points for future prefabs/material passes.
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"{name}_Anchor";
            marker.transform.SetParent(layer.transform, false);
            marker.transform.localScale = new Vector3(width, height, depth);
            marker.transform.localPosition = Vector3.zero;
            var collider = marker.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }
    }
}
