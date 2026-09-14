using UnityEngine;

namespace ArknightsACT.Gameplay.Debugging
{
    public sealed class WorldLabel2D : MonoBehaviour
    {
        [SerializeField] private string text = "Entity";
        [SerializeField] private Vector2 worldOffset = new(0f, 1.1f);

        private GUIStyle _style;

        public void Configure(string value, Vector2 offset)
        {
            text = value;
            worldOffset = offset;
        }

        private void OnGUI()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            var screen = camera.WorldToScreenPoint((Vector2)transform.position + worldOffset);
            if (screen.z <= 0f)
                return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            var rect = new Rect(screen.x - 70f, Screen.height - screen.y - 18f, 140f, 28f);
            GUI.Label(rect, text, _style);
        }
    }
}
