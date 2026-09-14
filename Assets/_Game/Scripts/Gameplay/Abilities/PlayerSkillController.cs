using ArknightsACT.Gameplay.Input;
using UnityEngine;

namespace ArknightsACT.Gameplay.Abilities
{
    public sealed class PlayerSkillController : MonoBehaviour
    {
        private IPlayerInputSource _input;
        private IPlayerSkill _skill;

        public IPlayerSkill Skill => _skill;

        private void Awake()
        {
            _input = GetComponent<IPlayerInputSource>();
            var behaviours = GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerSkill skill)
                {
                    _skill = skill;
                    break;
                }
            }
        }

        private void Update()
        {
            if (_input != null && _input.SkillPressedThisFrame)
                _skill?.TryCast();
        }
    }
}
