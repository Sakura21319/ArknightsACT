using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Small in-game IMGUI panel for tuning extracted FX without stopping Play Mode.
    /// Values are applied immediately and can be persisted with the Save button.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChenFxTuningOverlay : MonoBehaviour
    {
        private ChenExtractedFxController _controller;
        private Rect _window = new(16f, 16f, 720f, 470f);
        private bool _visible = true;

        private void Awake()
        {
            _controller = GetComponent<ChenExtractedFxController>();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F8))
                _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible || _controller == null)
                return;

            _window = GUI.Window(GetInstanceID(), _window, DrawWindow, "Chen FX Tuning (F8)");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Runtime values apply immediately. Save persists them.");

            GUILayout.Label("每个特效：出现位置 X/Y + 整体缩放（等比）", GUI.skin.box);

            GUILayout.Label("S2 / 拔刀", GUI.skin.box);
            DrawEffectRow("Start / 横向刀光", _controller.Skill02StartOffset, _controller.Skill02StartScale,
                _controller.SetSkill02StartOffsetX, _controller.SetSkill02StartOffsetY, _controller.SetSkill02StartScale);
            DrawEffectRow("Buff / 自身 buff", _controller.Skill02BuffOffset, _controller.Skill02BuffScale,
                _controller.SetSkill02BuffOffsetX, _controller.SetSkill02BuffOffsetY, _controller.SetSkill02BuffScale);
            DrawEffectRow("Hit / 命中", _controller.Skill02HitOffset, _controller.Skill02HitScale,
                _controller.SetSkill02HitOffsetX, _controller.SetSkill02HitOffsetY, _controller.SetSkill02HitScale);

            GUILayout.Label("S3 / 绝影", GUI.skin.box);
            DrawEffectRow("Start / 起手", _controller.Skill03StartOffset, _controller.Skill03StartScale,
                _controller.SetSkill03StartOffsetX, _controller.SetSkill03StartOffsetY, _controller.SetSkill03StartScale);
            DrawEffectRow("Start02 / 起手二段", _controller.Skill03Start02Offset, _controller.Skill03Start02Scale,
                _controller.SetSkill03Start02OffsetX, _controller.SetSkill03Start02OffsetY, _controller.SetSkill03Start02Scale);
            DrawEffectRow("Dragon / 龙", _controller.DragonOffset, _controller.DragonScale,
                _controller.SetDragonOffsetX, _controller.SetDragonOffsetY, _controller.SetDragonScale);
            DrawEffectRow("Hit / 连斩命中", _controller.Skill03HitOffset, _controller.Skill03HitScale,
                _controller.SetSkill03HitOffsetX, _controller.SetSkill03HitOffsetY, _controller.SetSkill03HitScale);

            GUILayout.Label("普攻", GUI.skin.box);
            DrawEffectRow("Attack / 出刀", _controller.BasicAttackOffset, _controller.BasicAttackScale,
                _controller.SetBasicAttackOffsetX, _controller.SetBasicAttackOffsetY, _controller.SetBasicAttackScale);
            DrawEffectRow("Hit / 命中", _controller.BasicHitOffset, _controller.BasicHitScale,
                _controller.SetBasicHitOffsetX, _controller.SetBasicHitOffsetY, _controller.SetBasicHitScale);

            DrawStepper("Dragon back depth", _controller.DragonBackDepth, 0.05f, -2f, 2f, _controller.SetDragonBackDepth);

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
                _controller.SaveTuning();
            if (GUILayout.Button("Reset"))
                _controller.ResetTuning();
            if (GUILayout.Button("Hide"))
                _visible = false;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Keep the controls clickable while making the four outer edges draggable. This is
            // less intrusive than making the entire content area a drag handle.
            const float edge = 18f;
            GUI.DragWindow(new Rect(0f, 0f, _window.width, edge));
            GUI.DragWindow(new Rect(0f, _window.height - edge, _window.width, edge));
            GUI.DragWindow(new Rect(0f, 0f, edge, _window.height));
            GUI.DragWindow(new Rect(_window.width - edge, 0f, edge, _window.height));
        }

        private static void DrawEffectRow(
            string label,
            Vector2 offset,
            float scale,
            System.Action<float> setOffsetX,
            System.Action<float> setOffsetY,
            System.Action<float> setScale)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(155f));
            GUILayout.Label($"位置 X {offset.x:0.00}", GUILayout.Width(92f));
            if (GUILayout.Button("-", GUILayout.Width(26f)))
                setOffsetX(Mathf.Clamp(offset.x - 0.05f, -3f, 3f));
            if (GUILayout.Button("+", GUILayout.Width(26f)))
                setOffsetX(Mathf.Clamp(offset.x + 0.05f, -3f, 3f));
            GUILayout.Label($"位置 Y {offset.y:0.00}", GUILayout.Width(92f));
            if (GUILayout.Button("-", GUILayout.Width(26f)))
                setOffsetY(Mathf.Clamp(offset.y - 0.05f, -3f, 3f));
            if (GUILayout.Button("+", GUILayout.Width(26f)))
                setOffsetY(Mathf.Clamp(offset.y + 0.05f, -3f, 3f));
            GUILayout.Label($"整体 {scale:0.00}", GUILayout.Width(86f));
            if (GUILayout.Button("-", GUILayout.Width(26f)))
                setScale(Mathf.Clamp(scale - 0.25f, 0.1f, 64f));
            if (GUILayout.Button("+", GUILayout.Width(26f)))
                setScale(Mathf.Clamp(scale + 0.25f, 0.1f, 64f));
            GUILayout.EndHorizontal();
        }

        private static void DrawStepper(
            string label,
            float value,
            float step,
            float min,
            float max,
            System.Action<float> setter)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {value:0.00}", GUILayout.Width(160f));
            if (GUILayout.Button("-", GUILayout.Width(32f)))
                setter(Mathf.Clamp(value - step, min, max));
            if (GUILayout.Button("+", GUILayout.Width(32f)))
                setter(Mathf.Clamp(value + step, min, max));
            GUILayout.EndHorizontal();
        }
    }
}
