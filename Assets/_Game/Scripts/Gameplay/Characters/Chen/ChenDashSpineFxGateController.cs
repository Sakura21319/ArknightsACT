using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Chen
{
    /// <summary>
    /// Keeps the character-level Spine effect separate from skill slash FX.
    /// It is only enabled during dash; skill VFX come from independent client battle-effect assets.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerDashController))]
    public sealed class ChenDashSpineFxGateController : MonoBehaviour
    {
        [SerializeField] private ChenOriginalSpineFxVisibilityController characterFx;

        private PlayerDashController _dash;

        public void Configure(ChenOriginalSpineFxVisibilityController fx)
        {
            characterFx = fx;
            if (characterFx != null)
                characterFx.enabled = false;
        }

        private void Awake()
        {
            _dash = GetComponent<PlayerDashController>();
            if (characterFx == null)
                characterFx = GetComponent<ChenOriginalSpineFxVisibilityController>();
            if (characterFx != null)
                characterFx.enabled = false;
        }

        private void OnEnable()
        {
            if (_dash == null)
                _dash = GetComponent<PlayerDashController>();
            if (_dash != null)
            {
                _dash.DashStarted += OnDashStarted;
                _dash.DashEnded += OnDashEnded;
            }
        }

        private void OnDisable()
        {
            if (_dash != null)
            {
                _dash.DashStarted -= OnDashStarted;
                _dash.DashEnded -= OnDashEnded;
            }
            if (characterFx != null)
                characterFx.enabled = false;
        }

        private void OnDashStarted()
        {
            if (characterFx != null)
                characterFx.enabled = true;
        }

        private void OnDashEnded()
        {
            if (characterFx != null)
                characterFx.enabled = false;
        }
    }
}
