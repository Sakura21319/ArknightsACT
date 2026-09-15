using ArknightsACT.Gameplay.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsACT.Gameplay.TopDown
{
    /// <summary>
    /// Desktop/gamepad adapter for the Soul-Knight-style prototype.
    /// Also implements the existing player input contract so reusable skill code can stay unchanged.
    /// </summary>
    public sealed class TopDownPlayerInputReader : MonoBehaviour, ITopDownInputSource, IPlayerInputSource
    {
        private InputActionMap _gameplay;
        private InputAction _move;
        private InputAction _aim;
        private InputAction _attack;
        private InputAction _dash;
        private InputAction _skill;

        public Vector2 Move => _move?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 AimStick => _aim?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 PointerScreenPosition => Mouse.current?.position.ReadValue() ?? Vector2.zero;
        public bool HasPointer => Mouse.current != null;
        public bool AttackPressedThisFrame => _attack?.WasPressedThisFrame() ?? false;
        public bool AttackHeld => _attack?.IsPressed() ?? false;
        public bool DashPressedThisFrame => _dash?.WasPressedThisFrame() ?? false;
        public bool SkillPressedThisFrame => _skill?.WasPressedThisFrame() ?? false;

        // Top-down mode has no jump. This keeps TexasSwordRainSkill/PlayerSkillController reusable.
        bool IPlayerInputSource.JumpPressedThisFrame => false;

        private void Awake() => BuildActions();
        private void OnEnable() => _gameplay?.Enable();
        private void OnDisable() => _gameplay?.Disable();
        private void OnDestroy() => _gameplay?.Dispose();

        private void BuildActions()
        {
            if (_gameplay != null)
                return;

            _gameplay = new InputActionMap("TopDownGameplay");

            _move = _gameplay.AddAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s");
            _move.AddBinding("<Gamepad>/leftStick");

            _aim = _gameplay.AddAction("Aim", InputActionType.Value);
            _aim.AddBinding("<Gamepad>/rightStick");

            _attack = _gameplay.AddAction("Attack", InputActionType.Button);
            _attack.AddBinding("<Mouse>/leftButton");
            _attack.AddBinding("<Keyboard>/j");
            _attack.AddBinding("<Gamepad>/buttonWest");

            _dash = _gameplay.AddAction("Dash", InputActionType.Button);
            _dash.AddBinding("<Keyboard>/space");
            _dash.AddBinding("<Keyboard>/leftShift");
            _dash.AddBinding("<Keyboard>/k");
            _dash.AddBinding("<Gamepad>/buttonEast");

            _skill = _gameplay.AddAction("Skill", InputActionType.Button);
            _skill.AddBinding("<Mouse>/rightButton");
            _skill.AddBinding("<Keyboard>/l");
            _skill.AddBinding("<Gamepad>/buttonNorth");
        }
    }
}
