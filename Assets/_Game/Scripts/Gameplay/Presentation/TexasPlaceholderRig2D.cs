using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Lightweight visual fallback used until the optional PRTS Spine source is wired to a Spine runtime.
    /// It owns visuals only and can be replaced without touching physics or combat.
    /// </summary>
    public sealed class TexasPlaceholderRig2D : MonoBehaviour
    {
        private void Awake()
        {
            CreatePart("Coat", new Vector2(0f, -0.05f), new Vector2(0.56f, 0.95f), new Color(0.10f, 0.15f, 0.19f), 20);
            CreatePart("Shirt", new Vector2(0f, 0.12f), new Vector2(0.30f, 0.46f), new Color(0.18f, 0.55f, 0.73f), 21);
            CreatePart("Head", new Vector2(0f, 0.62f), new Vector2(0.43f, 0.38f), new Color(0.88f, 0.78f, 0.69f), 22);
            CreatePart("Hair", new Vector2(0f, 0.72f), new Vector2(0.48f, 0.26f), new Color(0.12f, 0.14f, 0.16f), 23);
            CreatePart("EarL", new Vector2(-0.16f, 0.91f), new Vector2(0.13f, 0.26f), new Color(0.12f, 0.14f, 0.16f), 22, 18f);
            CreatePart("EarR", new Vector2(0.16f, 0.91f), new Vector2(0.13f, 0.26f), new Color(0.12f, 0.14f, 0.16f), 22, -18f);
            CreatePart("SwordL", new Vector2(-0.36f, -0.02f), new Vector2(0.07f, 0.90f), new Color(0.60f, 0.90f, 1f), 19, 14f);
            CreatePart("SwordR", new Vector2(0.36f, -0.02f), new Vector2(0.07f, 0.90f), new Color(0.60f, 0.90f, 1f), 19, -14f);
        }

        private void CreatePart(string partName, Vector2 localPosition, Vector2 size, Color color, int sortingOrder, float angle = 0f)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = new Vector3(localPosition.x, localPosition.y, -0.02f);
            part.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            part.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            part.AddComponent<PlaceholderVisual2D>().SetColor(color);
        }
    }
}
