using UnityEngine;
using UnityEngine.Rendering;

namespace ArknightsACT.Gameplay.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypeEnemyCombatBrain25D))]
    public sealed class EnemyVisionCone25D : MonoBehaviour
    {
        [SerializeField, Range(6, 40)] private int segments = 20;
        [SerializeField] private float groundOffset = 0.06f;

        private PrototypeEnemyCombatBrain25D _brain;
        private LineRenderer _line;

        private void Awake()
        {
            _brain = GetComponent<PrototypeEnemyCombatBrain25D>();
            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = false;
            _line.widthMultiplier = 0.035f;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.numCapVertices = 2;
            _line.material = new Material(FindCompatibleUnlitShader());
        }

        private void LateUpdate()
        {
            if (_brain == null || _line == null)
                return;

            var count = Mathf.Max(6, segments);
            _line.positionCount = count + 3;
            var origin = transform.position + Vector3.up * groundOffset;
            _line.SetPosition(0, origin);

            var forward = _brain.LogicForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            var half = _brain.ViewAngle * 0.5f;
            for (var i = 0; i <= count; i++)
            {
                var t = i / (float)count;
                var angle = Mathf.Lerp(-half, half, t);
                var dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                _line.SetPosition(i + 1, origin + dir * _brain.ViewDistance);
            }
            _line.SetPosition(count + 2, origin);

            var color = _brain.IsAlerted
                ? new Color(1f, 0.22f, 0.16f, 0.90f)
                : new Color(1f, 0.78f, 0.18f, 0.62f);
            _line.startColor = color;
            _line.endColor = color;
            if (_line.material.HasProperty("_BaseColor"))
                _line.material.SetColor("_BaseColor", color);
            if (_line.material.HasProperty("_Color"))
                _line.material.SetColor("_Color", color);
        }

        private static Shader FindCompatibleUnlitShader()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var urp = Shader.Find("Universal Render Pipeline/Unlit");
                if (urp != null)
                    return urp;
            }
            return Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        }
    }
}
