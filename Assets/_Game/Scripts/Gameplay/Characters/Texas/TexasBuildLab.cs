using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Characters.Texas
{
    /// <summary>
    /// Phase-2 only: keyboard switches for validating build feel before the roguelite reward system exists.
    /// Production upgrades will call the Set* methods instead of reading keyboard state.
    /// </summary>
    public sealed class TexasBuildLab : MonoBehaviour
    {
        private TexasSwiftBladeEffect _swiftBlade;
        private TexasResidualThunderEffect _residualThunder;
        private TexasConductiveEffect _conductive;

        public bool SwiftBlade => _swiftBlade != null && _swiftBlade.enabled;
        public bool ResidualThunder => _residualThunder != null && _residualThunder.enabled;
        public bool Conductive => _conductive != null && _conductive.enabled;

        private void Awake()
        {
            _swiftBlade = GetComponent<TexasSwiftBladeEffect>();
            _residualThunder = GetComponent<TexasResidualThunderEffect>();
            _conductive = GetComponent<TexasConductiveEffect>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
                SetSwiftBlade(!SwiftBlade);
            if (keyboard.digit2Key.wasPressedThisFrame)
                SetResidualThunder(!ResidualThunder);
            if (keyboard.digit3Key.wasPressedThisFrame)
                SetConductive(!Conductive);
        }

        public void SetSwiftBlade(bool active)
        {
            if (_swiftBlade != null)
                _swiftBlade.enabled = active;
        }

        public void SetResidualThunder(bool active)
        {
            if (_residualThunder != null)
                _residualThunder.enabled = active;
        }

        public void SetConductive(bool active)
        {
            if (_conductive != null)
                _conductive.enabled = active;
        }
    }
}
