using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Texas
{
    /// <summary>
    /// Owns which Texas prototype build effects are active.
    /// Input/UI policy deliberately lives elsewhere: room rewards call these setters.
    /// </summary>
    public sealed class TexasBuildLab : MonoBehaviour
    {
        private TexasSwiftBladeEffect _swiftBlade;
        private TexasResidualThunderEffect _residualThunder;
        private TexasConductiveEffect _conductive;

        public bool SwiftBlade => _swiftBlade != null && _swiftBlade.enabled;
        public bool ResidualThunder => _residualThunder != null && _residualThunder.enabled;
        public bool Conductive => _conductive != null && _conductive.enabled;
        public bool HasAllUpgrades => SwiftBlade && ResidualThunder && Conductive;

        private void Awake()
        {
            _swiftBlade = GetComponent<TexasSwiftBladeEffect>();
            _residualThunder = GetComponent<TexasResidualThunderEffect>();
            _conductive = GetComponent<TexasConductiveEffect>();
        }

        public bool HasUpgrade(int index)
        {
            return index switch
            {
                0 => SwiftBlade,
                1 => ResidualThunder,
                2 => Conductive,
                _ => false
            };
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
