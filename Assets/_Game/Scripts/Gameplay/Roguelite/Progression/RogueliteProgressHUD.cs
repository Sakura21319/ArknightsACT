using ArknightsACT.Gameplay.Roguelite.Routing;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Progression
{
    [DisallowMultipleComponent]
    public sealed class RogueliteProgressHUD : MonoBehaviour
    {
        [SerializeField] private RogueliteRunState runState;

        public void Configure(RogueliteRunState state) => runState = state;

        private void OnGUI()
        {
            if (runState == null)
                return;

            const float x = 18f;
            const float y = 18f;
            const float width = 290f;
            const float barHeight = 18f;

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };

            var required = runState.ExperienceToNextLevel;
            var progress = runState.Level >= runState.MaxLevel || required <= 0
                ? 1f
                : Mathf.Clamp01(runState.CurrentExperience / (float)required);

            GUI.Label(new Rect(x, y, width, 24f), $"Lv.{runState.Level}    源石锭 {runState.Ingots}", labelStyle);
            GUI.Box(new Rect(x, y + 27f, width, barHeight), GUIContent.none);
            var fill = Mathf.Max(0f, (width - 4f) * progress);
            if (fill > 0f)
                GUI.Box(new Rect(x + 2f, y + 29f, fill, barHeight - 4f), GUIContent.none);

            var xpText = runState.Level >= runState.MaxLevel
                ? "EXP MAX"
                : $"EXP {runState.CurrentExperience}/{required}";
            var xpStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            GUI.Label(new Rect(x, y + 25f, width, barHeight + 4f), xpText, xpStyle);
        }
    }
}
