using UnityEngine;
using ArknightsACT.Gameplay.Roguelite.Routing;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Chernobog facade dressing pass. Keeps visual modules separate from tactical buildings.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChernobogFacadeModuleController : MonoBehaviour
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
                "[Chernobog_Facade_Modules]");
            if (root == null)
                return;

            if (_preparedStage == stage)
                return;

            CreateFacadeModule(root, "ExteriorWallLayers", new Vector3(0f, 0.25f, 0f));
            CreateFacadeModule(root, "WindowGrid", new Vector3(0f, 0.45f, 0f));
            CreateFacadeModule(root, "WallPipelines", new Vector3(0f, 0.8f, 0f));
            CreateFacadeModule(root, "RoofEquipment", new Vector3(0f, 1.2f, 0f));
            CreateFacadeModule(root, "IndustrialMaintenance", new Vector3(0f, 0.6f, 0f));

            _preparedStage = stage;
        }

        private static void CreateFacadeModule(Transform root, string name, Vector3 offset)
        {
            var node = new GameObject(name);
            node.transform.SetParent(root, false);
            node.transform.localPosition = offset;

            var anchor = new GameObject("ModuleAnchor");
            anchor.transform.SetParent(node.transform, false);
        }
    }
}
