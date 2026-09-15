using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Input
{
    /// <summary>
    /// Desktop/Gamepad input adapter for the horizontal ACT prototype.
    /// Mobile can provide another adapter against the same contract later.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInputSource
    {
        private InputActionMap _gameplay;
        private InputAction _move;
        private InputAction _jump;
        private InputAction _attack;
        private InputAction _dash;
        private InputAction _skill1;
        private InputAction _skill2;

        public Vector2 Move => _move?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool JumpPressedThisFrame => _jump?.WasPressedThisFrame() ?? false;
        public bool AttackPressedThisFrame => _attack?.WasPressedThisFrame() ?? false;
        public bool DashPressedThisFrame => _dash?.WasPressedThisFrame() ?? false;
        public bool Skill1PressedThisFrame => _skill1?.WasPressedThisFrame() ?? false;
        public bool Skill2PressedThisFrame => _skill2?.WasPressedThisFrame() ?? false;

        private void Awake() => BuildActions();
        private void OnEnable() => _gameplay?.Enable();
        private void OnDisable() => _gameplay?.Disable();
        private void OnDestroy() => _gameplay?.Dispose();

        private void BuildActions()
        {
            if (_gameplay != null)
                return;

            _gameplay = new InputActionMap("Gameplay");

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

            _attack = _gameplay.AddAction("Attack", InputActionType.Button);
            _attack.AddBinding("<Keyboard>/j");
            _attack.AddBinding("<Mouse>/leftButton");
            _attack.AddBinding("<Gamepad>/buttonWest");

            _dash = _gameplay.AddAction("Dash", InputActionType.Button);
            _dash.AddBinding("<Keyboard>/k");
            _dash.AddBinding("<Keyboard>/leftShift");
            _dash.AddBinding("<Gamepad>/buttonEast");

            _skill1 = _gameplay.AddAction("Skill1", InputActionType.Button);
            _skill1.AddBinding("<Keyboard>/l");
            _skill1.AddBinding("<Gamepad>/buttonNorth");

            _skill2 = _gameplay.AddAction("Skill2", InputActionType.Button);
            _skill2.AddBinding("<Keyboard>/i");
            _skill2.AddBinding("<Mouse>/rightButton");
            _skill2.AddBinding("<Gamepad>/rightShoulder");
        }
    }
}
