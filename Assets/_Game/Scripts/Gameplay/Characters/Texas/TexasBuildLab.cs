using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Texas
{
    /// <summary>
    /// Prototype holder for Texas build flags. Upgrade/reward UI owns selection now; this
    /// component only exposes explicit setters so gameplay effects remain decoupled from UI.
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
