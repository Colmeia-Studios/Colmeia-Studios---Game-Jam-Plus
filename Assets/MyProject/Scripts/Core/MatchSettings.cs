using UnityEngine;
using UnityEngine.InputSystem;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public enum PlayerRole
    {
        Cook,
        Delivery
    }

    public static class MatchSettings
    {
        public static readonly string CharacterOne = "Cozinheiro";
        public static readonly string CharacterTwo = "Entregador";
        public static PlayerRole PlayerOneRole { get; private set; } = PlayerRole.Cook;
        public static PlayerRole PlayerTwoRole { get; private set; } = PlayerRole.Delivery;
        public static InputDevice PlayerOneDevice { get; private set; }
        public static InputDevice PlayerTwoDevice { get; private set; }
        public static Mouse PlayerOneMouse { get; private set; }
        public static Mouse PlayerTwoMouse { get; private set; }
        public static string PlayerOneScheme { get; private set; }
        public static string PlayerTwoScheme { get; private set; }

        public static void SetRoles(PlayerRole playerOneRole, PlayerRole playerTwoRole)
        {
            PlayerOneRole = playerOneRole;
            PlayerTwoRole = playerTwoRole;
        }

        public static void ResetToDefault()
        {
            SetRoles(PlayerRole.Cook, PlayerRole.Delivery);
            PlayerOneDevice = null;
            PlayerTwoDevice = null;
            PlayerOneMouse = null;
            PlayerTwoMouse = null;
            PlayerOneScheme = null;
            PlayerTwoScheme = null;
        }

        public static bool SetMatch(PlayerRole playerOneRole, PlayerRole playerTwoRole,
            InputDevice playerOneDevice, string playerOneScheme, Mouse playerOneMouse,
            InputDevice playerTwoDevice, string playerTwoScheme, Mouse playerTwoMouse)
        {
            if (!IsValidRole(playerOneRole) || !IsValidRole(playerTwoRole) ||
                playerOneRole == playerTwoRole ||
                !IsValidAssignment(playerOneDevice, playerOneScheme, playerOneMouse) ||
                !IsValidAssignment(playerTwoDevice, playerTwoScheme, playerTwoMouse) ||
                playerOneDevice == playerTwoDevice ||
                (playerOneMouse != null && playerOneMouse == playerTwoMouse))
                return false;

            SetRoles(playerOneRole, playerTwoRole);
            PlayerOneDevice = playerOneDevice;
            PlayerTwoDevice = playerTwoDevice;
            PlayerOneMouse = playerOneMouse;
            PlayerTwoMouse = playerTwoMouse;
            PlayerOneScheme = playerOneScheme;
            PlayerTwoScheme = playerTwoScheme;
            return true;
        }

        private static bool IsValidAssignment(InputDevice device, string scheme, Mouse mouse)
        {
            if (device == null || !device.added)
                return false;

            if (scheme == "KeyboardMouse")
                return device is Keyboard && mouse != null && mouse.added;

            if (scheme == "Gamepad")
                return device is Gamepad && mouse == null;

            return false;
        }

        private static bool IsValidRole(PlayerRole role)
        {
            return role == PlayerRole.Cook || role == PlayerRole.Delivery;
        }
    }
}
