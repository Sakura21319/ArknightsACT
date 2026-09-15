using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Texas
{
    /// <summary>
    /// Owns Texas prototype build progression. Reward/UI code decides when the player upgrades;
    /// this component only stores levels and applies them to the gameplay effect components.
    /// </summary>
    public sealed class TexasBuildLab : MonoBehaviour
    {
        public const int MaxUpgradeLevel = 3;

        private TexasSwiftBladeEffect _swiftBlade;
        private TexasResidualThunderEffect _residualThunder;
        private TexasConductiveEffect _conductive;

        public int SwiftBladeLevel { get; private set; }
        public int ResidualThunderLevel { get; private set; }
        public int ConductiveLevel { get; private set; }

        public bool SwiftBlade => SwiftBladeLevel > 0;
        public bool ResidualThunder => ResidualThunderLevel > 0;
        public bool Conductive => ConductiveLevel > 0;
        public bool HasAllUpgrades =>
            SwiftBladeLevel >= MaxUpgradeLevel &&
            ResidualThunderLevel >= MaxUpgradeLevel &&
            ConductiveLevel >= MaxUpgradeLevel;

        private void Awake()
        {
            _swiftBlade = GetComponent<TexasSwiftBladeEffect>();
            _residualThunder = GetComponent<TexasResidualThunderEffect>();
            _conductive = GetComponent<TexasConductiveEffect>();
            ApplyAllLevels();
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
                case 0:
                    SwiftBladeLevel++;
                    ApplySwiftBladeLevel();
                    break;
                case 1:
                    ResidualThunderLevel++;
                    ApplyResidualThunderLevel();
                    break;
                case 2:
                    ConductiveLevel++;
                    ApplyConductiveLevel();
                    break;
                default:
                    return false;
            }

            return true;
        }

        // Compatibility helpers for existing prototype callers.
        public void SetSwiftBlade(bool active)
        {
            SwiftBladeLevel = active ? Mathf.Max(1, SwiftBladeLevel) : 0;
            ApplySwiftBladeLevel();
        }

        public void SetResidualThunder(bool active)
        {
            ResidualThunderLevel = active ? Mathf.Max(1, ResidualThunderLevel) : 0;
            ApplyResidualThunderLevel();
        }

        public void SetConductive(bool active)
        {
            ConductiveLevel = active ? Mathf.Max(1, ConductiveLevel) : 0;
            ApplyConductiveLevel();
        }

        private void ApplyAllLevels()
        {
            ApplySwiftBladeLevel();
            ApplyResidualThunderLevel();
            ApplyConductiveLevel();
        }

        private void ApplySwiftBladeLevel()
        {
            if (_swiftBlade == null)
                return;
            _swiftBlade.SetLevel(SwiftBladeLevel);
            _swiftBlade.enabled = SwiftBladeLevel > 0;
        }

        private void ApplyResidualThunderLevel()
        {
            if (_residualThunder == null)
                return;
            _residualThunder.SetLevel(ResidualThunderLevel);
            _residualThunder.enabled = ResidualThunderLevel > 0;
        }

        private void ApplyConductiveLevel()
        {
            if (_conductive == null)
                return;
            _conductive.SetLevel(ConductiveLevel);
            _conductive.enabled = ConductiveLevel > 0;
        }
    }
}
