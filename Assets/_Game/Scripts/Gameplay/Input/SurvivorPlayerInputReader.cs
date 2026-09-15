using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Input
{
    /// <summary>
    /// Experimental input adapter for the survivor prototype.
    /// Movement, jump, dash and skill remain manual, while basic attack is owned by
    /// TexasSurvivorAutoMelee2D. This keeps the experiment isolated from the normal ACT controls.
    /// </summary>
    public sealed class SurvivorPlayerInputReader : MonoBehaviour, IPlayerInputSource
    {
        private InputActionMap _gameplay;
        private InputAction _move;
        private InputAction _jump;
        private InputAction _dash;
        private InputAction _skill;

        public Vector2 Move => _move?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool JumpPressedThisFrame => _jump?.WasPressedThisFrame() ?? false;
        public bool AttackPressedThisFrame => false;
        public bool DashPressedThisFrame => _dash?.WasPressedThisFrame() ?? false;
        public bool SkillPressedThisFrame => _skill?.WasPressedThisFrame() ?? false;

        private void Awake() => BuildActions();
        private void OnEnable() => _gameplay?.Enable();
        private void OnDisable() => _gameplay?.Disable();
        private void OnDestroy() => _gameplay?.Dispose();

        private void BuildActions()
        {
            if (_gameplay != null)
                return;

            _gameplay = new InputActionMap("SurvivorGameplay");

            _move = _gameplay.AddAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s");
            _move.AddBinding("<Gamepad>/leftStick");

            _jump = _gameplay.AddAction("Jump", InputActionType.Button);
            _jump.AddBinding("<Keyboard>/space");
            _jump.AddBinding("<Gamepad>/buttonSouth");

            _dash = _gameplay.AddAction("Dash", InputActionType.Button);
            _dash.AddBinding("<Keyboard>/k");
            _dash.AddBinding("<Keyboard>/leftShift");
            _dash.AddBinding("<Gamepad>/buttonEast");

            _skill = _gameplay.AddAction("Skill", InputActionType.Button);
            _skill.AddBinding("<Keyboard>/l");
            _skill.AddBinding("<Mouse>/rightButton");
            _skill.AddBinding("<Gamepad>/buttonNorth");
        }
    }
}
