using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OCoveiro.Player
{
    /// <summary>
    /// Todo o input do jogador num lugar só. Só existe no DONO do personagem;
    /// na cópia do outro jogador tudo retorna "nada apertado".
    /// </summary>
    public class PlayerInputReader : NetworkBehaviour
    {
        private InputAction move;
        private InputAction aim;
        private InputAction lightAttack;
        private InputAction heavyAttack;
        private InputAction dodge;
        private InputAction lantern;
        private InputAction heal;
        private InputAction interact;
        private InputAction debugDamage;
        private InputAction debugRefill;
        private InputAction[] all;

        public Vector2 Move => move != null ? move.ReadValue<Vector2>() : Vector2.zero;
        /// <summary>Analógico direito (mira no controle).</summary>
        public Vector2 AimStick => aim != null ? aim.ReadValue<Vector2>() : Vector2.zero;
        public bool HeavyHeld => heavyAttack != null && heavyAttack.IsPressed();

        public bool LightPressed => Pressed(lightAttack);
        public bool HeavyPressed => Pressed(heavyAttack);
        public bool DodgePressed => Pressed(dodge);
        public bool LanternPressed => Pressed(lantern);
        public bool HealPressed => Pressed(heal);
        public bool InteractPressed => Pressed(interact);
        public bool DebugDamagePressed => Pressed(debugDamage);
        public bool DebugRefillPressed => Pressed(debugRefill);

        /// <summary>True se o último comando veio do controle (aí a mira não usa o mouse).</summary>
        public bool LastDeviceWasGamepad { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (IsOwner) CreateActions();
        }

        public override void OnNetworkDespawn()
        {
            if (all == null) return;
            foreach (var action in all)
            {
                action.Disable();
                action.Dispose();
            }
            all = null;
            move = aim = lightAttack = heavyAttack = dodge = lantern = heal = interact = debugDamage = debugRefill = null;
        }

        private void CreateActions()
        {
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            aim = new InputAction("Aim", InputActionType.Value, "<Gamepad>/rightStick", expectedControlType: "Vector2");

            lightAttack = Button("LightAttack", "<Mouse>/leftButton", "<Gamepad>/buttonWest");
            heavyAttack = Button("HeavyAttack", "<Mouse>/rightButton", "<Gamepad>/buttonNorth");
            dodge = Button("Dodge", "<Keyboard>/space", "<Gamepad>/buttonEast");
            lantern = Button("Lantern", "<Keyboard>/f", "<Gamepad>/rightShoulder");
            heal = Button("Heal", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            debugDamage = Button("DebugDamage", "<Keyboard>/k", null);
            debugRefill = Button("DebugRefill", "<Keyboard>/o", null);

            all = new[] { move, aim, lightAttack, heavyAttack, dodge, lantern, heal, interact, debugDamage, debugRefill };
            foreach (var action in all)
            {
                action.performed += TrackDevice;
                action.Enable();
            }
        }

        private static InputAction Button(string name, string keyboardOrMouse, string gamepad)
        {
            var action = new InputAction(name, InputActionType.Button, keyboardOrMouse);
            if (!string.IsNullOrEmpty(gamepad)) action.AddBinding(gamepad);
            return action;
        }

        private void TrackDevice(InputAction.CallbackContext context)
        {
            LastDeviceWasGamepad = context.control?.device is Gamepad;
        }

        private static bool Pressed(InputAction action) => action != null && action.WasPressedThisFrame();
    }
}
