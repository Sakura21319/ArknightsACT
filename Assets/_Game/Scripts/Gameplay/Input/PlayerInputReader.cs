using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Input
{
    /// <summary>
    /// Desktop/Gamepad input adapter.
    /// Mobile later supplies another adapter against the same player-facing contract.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInputSource
    {
        private InputActionMap _gameplay;
        private InputAction _move;
        private InputAction _jump;
        private InputAction _attack;
        private InputAction _dash;

        public Vector2 Move => _move?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool JumpPressedThisFrame => _jump?.WasPressedThisFrame() ?? false;
        public bool AttackPressedThisFrame => _attack?.WasPressedThisFrame() ?? false;
        public bool DashPressedThisFrame => _dash?.WasPressedThisFrame() ?? false;

        private void Awake()
        {
            BuildActions();
        }

        private void OnEnable()
        {
            _gameplay?.Enable();
        }

        private void OnDisable()
        {
            _gameplay?.Disable();
        }

        private void OnDestroy()
        {
            _gameplay?.Dispose();
        }

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
        }
    }
}
