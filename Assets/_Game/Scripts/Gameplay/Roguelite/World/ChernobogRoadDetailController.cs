using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Visual road dressing only. Keeps roads readable as real city roads without touching navigation.
    /// </summary>
    [DefaultExecutionOrder(26)]
    public sealed class ChernobogRoadDetailController : MonoBehaviour
    {
        private RogueliteStageRuntimeContext _context;
        private GameObject _preparedStage;

        private void Awake()
        {
            _context = GetComponent<RogueliteStageRuntimeContext>();
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
            if (_context == null || _context.StageRoot == null)
                return;

            var stage = _context.StageRoot.gameObject;
            if (stage == _preparedStage && stage.transform.Find("[Chernobog_Road_Detail]") != null)
                return;

            var root = new GameObject("[Chernobog_Road_Detail]").transform;
            root.SetParent(stage.transform, false);
            CreateDetail(root, "DrainCover_A", new Vector3(-7f, 0.12f, 3f));
            CreateDetail(root, "DrainCover_B", new Vector3(7f, 0.12f, -3f));
            CreateDetail(root, "OilStain_A", new Vector3(2f, 0.115f, 1f));
            CreateDetail(root, "RoadPatch_A", new Vector3(-3f, 0.115f, -2f));
            _preparedStage = stage;
        }

        private static void CreateDetail(Transform parent, string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(1.2f, 0.02f, 0.6f);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
        }
    }
}
