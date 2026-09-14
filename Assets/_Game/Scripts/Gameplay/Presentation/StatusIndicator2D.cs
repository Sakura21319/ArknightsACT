using ArknightsACT.Combat;
using ArknightsACT.Combat.Status;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>Prototype world-space status marker. Kept presentation-only so status logic stays in Game.Combat.</summary>
    [RequireComponent(typeof(CombatEntity))]
    public sealed class StatusIndicator2D : MonoBehaviour
    {
        private CombatEntity _entity;
        private GameObject _shock;
        private Texture2D _texture;
        private Sprite _sprite;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            _texture.SetPixel(0, 0, Color.white);
            _texture.Apply();
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            _shock = new GameObject("ShockIndicator");
            _shock.transform.SetParent(transform, false);
            _shock.transform.localPosition = new Vector3(0f, 0.95f, -0.05f);
            _shock.transform.localScale = new Vector3(0.13f, 0.52f, 1f);
            var renderer = _shock.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.color = new Color(0.35f, 0.90f, 1f, 0.95f);
            renderer.sortingOrder = 70;
            _shock.SetActive(false);
        }

        private void Update()
        {
            if (_shock == null || _entity == null)
                return;

            var active = _entity.Status.Has(CombatStatusType.Shock);
            if (_shock.activeSelf != active)
                _shock.SetActive(active);

            if (active)
            {
                var angle = Mathf.Sin(Time.time * 22f) * 22f;
                _shock.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void OnDestroy()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }
    }
}
