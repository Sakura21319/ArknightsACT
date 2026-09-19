using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// Non-playable megastructure layer. Adds the industrial identity of Chernobog:
    /// elevated transport, pipe corridors and factory silhouettes.
    /// No navigation or gameplay collision is created here.
    /// </summary>
    [DefaultExecutionOrder(26)]
    [DisallowMultipleComponent]
    public sealed class ChernobogInfrastructureController : MonoBehaviour
    {
        private GameObject _built;

        private void Start()
        {
            if (_built != null)
                return;

            var root = new GameObject("[Chernobog_Infrastructure]");
            root.transform.SetParent(transform, false);

            BuildPipeBridge(root.transform, new Vector3(0, 12, 18));
            BuildPipeBridge(root.transform, new Vector3(-28, 18, -8));
            BuildTower(root.transform, new Vector3(36, 0, 28), 24);
            BuildTower(root.transform, new Vector3(-40, 0, 35), 30);
            BuildFactoryStack(root.transform, new Vector3(20, 0, -35));

            _built = root;
        }

        private static void BuildPipeBridge(Transform parent, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Industrial_Pipe_Bridge";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(34, .35f, 1.2f);
            Object.Destroy(go.GetComponent<Collider>());
        }

        private static void BuildTower(Transform parent, Vector3 pos, float height)
        {
            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Chernobog_Industrial_Tower";
            tower.transform.SetParent(parent, false);
            tower.transform.localPosition = pos + Vector3.up * height * .5f;
            tower.transform.localScale = new Vector3(5, height, 5);
            Object.Destroy(tower.GetComponent<Collider>());
        }

        private static void BuildFactoryStack(Transform parent, Vector3 pos)
        {
            var stack = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stack.name = "Factory_Exhaust_Stack";
            stack.transform.SetParent(parent, false);
            stack.transform.localPosition = pos + Vector3.up * 10;
            stack.transform.localScale = new Vector3(2, 10, 2);
            Object.Destroy(stack.GetComponent<Collider>());
        }
    }
}
