#if UNITY_EDITOR
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Editor
{
    internal static class PrototypeBackdropFactory
    {
        public static void Create()
        {
            var root = new GameObject("[Backdrop]");

            CreatePanel(root.transform, "BackWall", new Vector2(6f, 2.25f), new Vector2(30f, 8.5f),
                new Color(0.075f, 0.09f, 0.125f), -100);

            CreatePanel(root.transform, "FarFloorBand", new Vector2(6f, -0.78f), new Vector2(30f, 0.10f),
                new Color(0.18f, 0.20f, 0.25f), -80);

            CreatePanel(root.transform, "Column_A", new Vector2(-3.5f, 1.3f), new Vector2(0.45f, 4.2f),
                new Color(0.11f, 0.13f, 0.17f), -90);
            CreatePanel(root.transform, "Column_B", new Vector2(5.8f, 1.5f), new Vector2(0.55f, 4.8f),
                new Color(0.11f, 0.13f, 0.17f), -90);
            CreatePanel(root.transform, "Column_C", new Vector2(15.2f, 1.2f), new Vector2(0.50f, 4.0f),
                new Color(0.11f, 0.13f, 0.17f), -90);

            CreatePanel(root.transform, "FarPlatform_A", new Vector2(-0.8f, 2.15f), new Vector2(4.2f, 0.14f),
                new Color(0.22f, 0.24f, 0.30f), -70);
            CreatePanel(root.transform, "FarPlatform_B", new Vector2(8.2f, 3.25f), new Vector2(5.2f, 0.16f),
                new Color(0.22f, 0.24f, 0.30f), -70);
            CreatePanel(root.transform, "FarPlatform_C", new Vector2(16.2f, 1.85f), new Vector2(4.5f, 0.14f),
                new Color(0.22f, 0.24f, 0.30f), -70);
        }

        private static void CreatePanel(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            go.AddComponent<PlaceholderVisual2D>().SetColor(color);
        }
    }
}
#endif
