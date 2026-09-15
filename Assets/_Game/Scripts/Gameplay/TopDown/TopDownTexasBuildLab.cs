using ArknightsACT.Gameplay.Characters.Texas;
using UnityEngine;

namespace ArknightsACT.Gameplay.TopDown
{
    /// <summary>
    /// Build state for the isolated top-down prototype. It deliberately does not depend on the
    /// side-view PlayerAttackController so both experiments can evolve independently.
    /// </summary>
    public sealed class TopDownTexasBuildLab : MonoBehaviour
    {
        public const int MaxUpgradeLevel = 3;

        private TopDownTexasSwiftBladeEffect _swiftBlade;
        private TexasResidualThunderEffect _residualThunder;
        private TopDownTexasConductiveEffect _conductive;

        public int SwiftBladeLevel { get; private set; }
        public int ResidualThunderLevel { get; private set; }
        public int ConductiveLevel { get; private set; }
        public bool HasAllUpgrades => SwiftBladeLevel >= MaxUpgradeLevel &&
                                      ResidualThunderLevel >= MaxUpgradeLevel &&
                                      ConductiveLevel >= MaxUpgradeLevel;

        private void Awake()
        {
            _swiftBlade = GetComponent<TopDownTexasSwiftBladeEffect>();
            _residualThunder = GetComponent<TexasResidualThunderEffect>();
            _conductive = GetComponent<TopDownTexasConductiveEffect>();
            ApplyAll();
        }

        public int GetLevel(int index)
        {
            return index switch
            {
                0 => SwiftBladeLevel,
                1 => ResidualThunderLevel,
                2 => ConductiveLevel,
                _ => 0
            };
        }

        public bool CanUpgrade(int index) => GetLevel(index) < MaxUpgradeLevel;

        public bool Upgrade(int index)
        {
            if (!CanUpgrade(index))
                return false;

            switch (index)
            {
                case 0: SwiftBladeLevel++; break;
                case 1: ResidualThunderLevel++; break;
                case 2: ConductiveLevel++; break;
                default: return false;
            }

            ApplyAll();
            return true;
        }

        private void ApplyAll()
        {
            if (_swiftBlade != null)
            {
                _swiftBlade.SetLevel(SwiftBladeLevel);
                _swiftBlade.enabled = SwiftBladeLevel > 0;
            }
            if (_residualThunder != null)
            {
                _residualThunder.SetLevel(ResidualThunderLevel);
                _residualThunder.enabled = ResidualThunderLevel > 0;
            }
            if (_conductive != null)
            {
                _conductive.SetLevel(ConductiveLevel);
                _conductive.enabled = ConductiveLevel > 0;
            }
        }
    }
}
