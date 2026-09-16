using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.Input
{
    /// <summary>
    /// Desktop/Gamepad input adapter. Runtime InputActions remain the primary abstraction,
    /// while direct keyboard/mouse polling provides a deterministic fallback for generated
    /// prototype scenes if an action map misses its enable lifecycle.
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

        public Vector2 Move
        {
            get
            {
                var actionValue = _move?.ReadValue<Vector2>() ?? Vector2.zero;
                var keyboardValue = ReadKeyboardMove();
                return keyboardValue.sqrMagnitude > 0.001f ? keyboardValue : actionValue;
            }
        }

        public bool JumpPressedThisFrame =>
            (_jump?.WasPressedThisFrame() ?? false) ||
            (Keyboard.current?.spaceKey.wasPressedThisFrame ?? false);

        public bool AttackPressedThisFrame =>
            (_attack?.WasPressedThisFrame() ?? false) ||
            (Keyboard.current?.jKey.wasPressedThisFrame ?? false) ||
            (Mouse.current?.leftButton.wasPressedThisFrame ?? false);

        public bool DashPressedThisFrame =>
            (_dash?.WasPressedThisFrame() ?? false) ||
            (Keyboard.current?.kKey.wasPressedThisFrame ?? false) ||
            (Keyboard.current?.leftShiftKey.wasPressedThisFrame ?? false);

        public bool Skill1PressedThisFrame =>
            (_skill1?.WasPressedThisFrame() ?? false) ||
            (Keyboard.current?.lKey.wasPressedThisFrame ?? false);

        public bool Skill2PressedThisFrame =>
            (_skill2?.WasPressedThisFrame() ?? false) ||
            (Keyboard.current?.iKey.wasPressedThisFrame ?? false) ||
            (Mouse.current?.rightButton.wasPressedThisFrame ?? false);

        private void Awake()
        {
            BuildActions();
        }

        private void OnEnable()
        {
            // Generated prototype actors may be composed while inactive. Build again here so
            // enabling the actor always leaves a valid, enabled map even after editor rebuilds.
            BuildActions();
            _gameplay?.Enable();
        }

        private void OnDisable()
        {
            _gameplay?.Disable();
        }

        private void OnDestroy()
        {
            _gameplay?.Dispose();
            _gameplay = null;
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
            _move.AddCompositeBinding("2DVector")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow");
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

        private static Vector2 ReadKeyboardMove()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            var value = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                value.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                value.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                value.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                value.y += 1f;

            return value.sqrMagnitude > 1f ? value.normalized : value;
        }
    }
}
