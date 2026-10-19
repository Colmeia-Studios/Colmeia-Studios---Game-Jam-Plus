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
        public static readonly string CharacterOne = "Cook";
        public static readonly string CharacterTwo = "Delivery";
        public static PlayerRole PlayerOneRole { get; private set; } = PlayerRole.Cook;
        public static PlayerRole PlayerTwoRole { get; private set; } = PlayerRole.Delivery;
        public static InputDevice PlayerOneDevice { get; private set; }
        public static InputDevice PlayerTwoDevice { get; private set; }
        public static Mouse PlayerOneMouse { get; private set; }
        public static Keyboard PlayerOneKeyboard { get; private set; }
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
            PlayerOneKeyboard = null;
            PlayerTwoMouse = null;
            PlayerOneScheme = null;
            PlayerTwoScheme = null;
        }

        public static bool SetMatch(PlayerRole playerOneRole, PlayerRole playerTwoRole,
            InputDevice playerOneDevice, string playerOneScheme, Mouse playerOneMouse,
            InputDevice playerTwoDevice, string playerTwoScheme, Mouse playerTwoMouse)
        {
            Keyboard keyboard = playerOneDevice as Keyboard;
            return SetMatch(playerOneRole, playerTwoRole, playerOneDevice, playerOneScheme,
                playerOneMouse, keyboard, playerTwoDevice, playerTwoScheme, playerTwoMouse);
        }

        public static bool SetMatch(PlayerRole playerOneRole, PlayerRole playerTwoRole,
            InputDevice playerOneDevice, string playerOneScheme, Mouse playerOneMouse, Keyboard playerOneKeyboard,
            InputDevice playerTwoDevice, string playerTwoScheme, Mouse playerTwoMouse)
        {
            return TrySetMatch(playerOneRole, playerTwoRole, playerOneDevice, playerOneScheme,
                playerOneMouse, playerOneKeyboard, playerTwoDevice, playerTwoScheme, playerTwoMouse, out _);
        }

        public static bool TrySetMatch(PlayerRole playerOneRole, PlayerRole playerTwoRole,
            InputDevice playerOneDevice, string playerOneScheme, Mouse playerOneMouse, Keyboard playerOneKeyboard,
            InputDevice playerTwoDevice, string playerTwoScheme, Mouse playerTwoMouse, out string reason)
        {
            if (!IsValidRole(playerOneRole) || !IsValidRole(playerTwoRole) || playerOneRole == playerTwoRole)
            { reason = "Players must have valid, different roles."; return false; }
            if (!IsValidAssignment(playerOneDevice, playerOneScheme, playerOneMouse, playerOneKeyboard, out reason))
            { reason = $"Player 1: {reason}"; return false; }
            if (!IsValidAssignment(playerTwoDevice, playerTwoScheme, playerTwoMouse, null, out reason))
            { reason = $"Player 2: {reason}"; return false; }

            var firstDevices = new System.Collections.Generic.HashSet<InputDevice>();
            AddIfPresent(firstDevices, playerOneDevice); AddIfPresent(firstDevices, playerOneKeyboard); AddIfPresent(firstDevices, playerOneMouse);
            var secondDevices = new System.Collections.Generic.HashSet<InputDevice>();
            AddIfPresent(secondDevices, playerTwoDevice); AddIfPresent(secondDevices, playerTwoMouse);
            if (firstDevices.Count != CountDevices(playerOneDevice, playerOneKeyboard, playerOneMouse) ||
                secondDevices.Count != CountDevices(playerTwoDevice, null, playerTwoMouse))
            { reason = "A player has the same device listed more than once."; return false; }
            foreach (InputDevice device in firstDevices)
                if (secondDevices.Contains(device))
                { reason = $"Device '{device.displayName}' would be shared between players."; return false; }

            SetRoles(playerOneRole, playerTwoRole);
            PlayerOneDevice = playerOneDevice;
            PlayerTwoDevice = playerTwoDevice;
            PlayerOneMouse = playerOneMouse;
            PlayerOneKeyboard = playerOneKeyboard;
            PlayerTwoMouse = playerTwoMouse;
            PlayerOneScheme = playerOneScheme;
            PlayerTwoScheme = playerTwoScheme;
            reason = "Device pairing is valid.";
            return true;
        }

        private static bool IsValidAssignment(InputDevice device, string scheme, Mouse mouse, Keyboard keyboard, out string reason)
        {
            if (device == null || !device.added)
            { reason = "The primary device is missing or disconnected."; return false; }

            if (scheme == "KeyboardMouse")
            {
                bool valid = device is Keyboard && mouse != null && mouse.added && (keyboard == null || keyboard == device);
                reason = valid ? "" : "KeyboardMouse requires a connected keyboard and mouse.";
                return valid;
            }

            if (scheme == "Gamepad")
            {
                bool valid = device is Gamepad && mouse == null && keyboard == null;
                reason = valid ? "" : "Gamepad requires an independent gamepad.";
                return valid;
            }

            if (scheme == "GamepadKeyboardMouse")
            {
                bool valid = device is Gamepad && mouse != null && mouse.added && keyboard != null && keyboard.added;
                reason = valid ? "" : "The hybrid scheme requires a connected gamepad, keyboard, and mouse.";
                return valid;
            }

            reason = $"control scheme '{scheme}' is not recognized.";
            return false;
        }

        private static void AddIfPresent(System.Collections.Generic.HashSet<InputDevice> set, InputDevice device)
        { if (device != null) set.Add(device); }
        private static int CountDevices(InputDevice first, InputDevice second, InputDevice third)
        { return (first != null ? 1 : 0) + (second != null ? 1 : 0) + (third != null ? 1 : 0); }

        private static bool IsValidRole(PlayerRole role)
        {
            return role == PlayerRole.Cook || role == PlayerRole.Delivery;
        }
    }
}
