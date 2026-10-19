using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class CharacterSelectionManager : MonoBehaviour
    {
        [Serializable]
        private class CharacterCard
        {
            [Header("Character")]
            public PlayerRole role;
            public Button selectButton;
            public GameObject ready;
            [Header("Player")]
            public TMP_Text playerLabel;
            [Header("Assigned and alternate devices")]
            public Transform deviceIconContainer;
            public TMP_Text deviceLabel;
        }

        [Serializable]
        private class DeviceVisual
        {
            public string scheme;
            public GameObject prefab;
        }

        private sealed class PlayerDevice
        {
            public InputDevice primary;
            public Keyboard keyboard;
            public Mouse mouse;
            public string scheme;
            public bool IsEmpty => primary == null;
            public void Clear() { primary = null; keyboard = null; mouse = null; scheme = null; }
        }

        [Header("Scenes")]
        [SerializeField] private string gameplaySceneName = "Gameplay";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [Header("Cards")]
        [SerializeField] private CharacterCard[] cards = new CharacterCard[2];
        [Header("Interface")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text statusText;
        [Header("Device visuals")]
        [SerializeField] private DeviceVisual[] deviceVisuals;
        [Header("Temporary diagnostics")]
        [SerializeField] private bool logDeviceDiagnostics;

        private readonly PlayerDevice[] players = { new PlayerDevice(), new PlayerDevice() };
        private readonly List<Gamepad> connectedPadsInOrder = new List<Gamepad>();
        private PlayerRole? firstCharacter;
        private bool initialized;
        private bool lastValidity;

        private void Awake() => ConfigureButtons();

        private void OnEnable() => InputSystem.onDeviceChange += OnDeviceChange;

        private void OnDisable() => InputSystem.onDeviceChange -= OnDeviceChange;

        private void Start()
        {
            foreach (Gamepad gamepad in Gamepad.all)
                TrackConnectedGamepad(gamepad);
            LogDeviceSnapshot("inicialização");
            DetectInitialDevices();
            initialized = true;
            lastValidity = DevicesValid(out _);
            RefreshUI();
            LogAssignment("atribuição inicial");
        }

        private void Update()
        {
            DetectGamepadInput();
            bool valid = DevicesValid(out string reason);
            if (valid != lastValidity)
            {
                lastValidity = valid;
                RefreshUI();
                if (logDeviceDiagnostics)
                    Debug.Log($"Device pairing validation: {(valid ? "valid" : "invalid")}. {reason}", this);
            }
            if (startButton != null)
                startButton.interactable = firstCharacter.HasValue && valid;
        }

        private void ConfigureButtons()
        {
            if (cards != null)
                foreach (CharacterCard card in cards)
                {
                    if (card?.selectButton == null) continue;
                    PlayerRole role = card.role;
                    card.selectButton.onClick.AddListener(() => ChooseCharacter(role));
                }
            startButton?.onClick.AddListener(StartMatch);
            backButton?.onClick.AddListener(() => SceneManager.LoadScene(mainMenuSceneName));
        }

        private void ChooseCharacter(PlayerRole role)
        {
            if (cards == null || cards.Count(card => card != null) != 2 || cards.All(card => card.role != role)) return;
            firstCharacter = role;
            RefreshUI();
        }

        private void DetectInitialDevices()
        {
            Keyboard keyboard = Keyboard.current != null && Keyboard.current.added ? Keyboard.current : null;
            Mouse mouse = Mouse.current != null && Mouse.current.added ? Mouse.current : null;
            bool keyboardMouseAvailable = keyboard != null && mouse != null;
            if (keyboardMouseAvailable)
                AssignKeyboardMouse(0, keyboard, mouse);

            List<Gamepad> pads = connectedPadsInOrder.Where(pad => pad != null && pad.added).ToList();
            if (pads.Count >= 2)
            {
                // Initial choice only: retain keyboard/mouse as P1's alternate controls.
                AssignGamepad(0, pads[0], keyboardMouseAvailable ? keyboard : null,
                    keyboardMouseAvailable ? mouse : null);
                AssignGamepad(1, pads[1]);
            }
            else if (pads.Count == 1)
            {
                if (keyboardMouseAvailable)
                    AssignGamepad(1, pads[0]);
                else
                    AssignGamepad(0, pads[0]);
            }
        }

        private void DetectGamepadInput()
        {
            foreach (Gamepad pad in connectedPadsInOrder.ToArray())
            {
                if (pad == null || !pad.added || !PressedThisFrame(pad) || IsAssigned(pad)) continue;
                int slot = FindEmptyPlayerSlot();
                if (slot < 0) continue;
                AssignGamepad(slot, pad);
                RefreshUI();
                LogAssignment($"gamepad conectado e atribuído explicitamente ao Player {slot + 1}");
            }
        }

        private int FindEmptyPlayerSlot()
        {
            for (int i = 0; i < players.Length; i++)
                if (players[i].IsEmpty) return i;
            return -1;
        }

        private static bool PressedThisFrame(Gamepad pad)
        {
            foreach (InputControl control in pad.allControls)
                if (control is ButtonControl button && button.wasPressedThisFrame) return true;
            return false;
        }

        private void AssignKeyboardMouse(int index, Keyboard keyboard, Mouse mouse)
        {
            if (index < 0 || index >= players.Length || keyboard == null || !keyboard.added || mouse == null || !mouse.added) return;
            if (IsAssignedToOtherSlot(index, keyboard) || IsAssignedToOtherSlot(index, mouse)) return;
            players[index].primary = keyboard;
            players[index].keyboard = keyboard;
            players[index].mouse = mouse;
            players[index].scheme = "KeyboardMouse";
        }

        private void AssignGamepad(int index, Gamepad pad, Keyboard alternateKeyboard = null, Mouse alternateMouse = null)
        {
            if (index < 0 || index >= players.Length || pad == null || !pad.added || IsAssignedToOtherSlot(index, pad)) return;
            players[index].primary = pad;
            players[index].keyboard = alternateKeyboard != null && alternateKeyboard.added ? alternateKeyboard : null;
            players[index].mouse = alternateMouse != null && alternateMouse.added ? alternateMouse : null;
            players[index].scheme = players[index].keyboard != null && players[index].mouse != null
                ? "GamepadKeyboardMouse" : "Gamepad";
        }

        private bool IsAssigned(InputDevice device) => players.Any(player => player.primary == device);
        private bool IsAssignedToOtherSlot(int index, InputDevice device) =>
            players.Where((_, playerIndex) => playerIndex != index).Any(player =>
                player.primary == device || player.keyboard == device || player.mouse == device);

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
            {
                if (device is Gamepad gamepad)
                {
                    TrackConnectedGamepad(gamepad);
                    // Fill an empty slot, but never replace a valid existing assignment.
                    int slot = FindEmptyPlayerSlot();
                    if (initialized && slot >= 0) AssignGamepad(slot, gamepad);
                }
                else if (initialized && (device is Keyboard || device is Mouse))
                {
                    Keyboard keyboard = Keyboard.current != null && Keyboard.current.added ? Keyboard.current : null;
                    Mouse mouse = Mouse.current != null && Mouse.current.added ? Mouse.current : null;
                    int slot = FindEmptyPlayerSlot();
                    if (slot >= 0 && keyboard != null && mouse != null &&
                        !IsAssignedToOtherSlot(slot, keyboard) && !IsAssignedToOtherSlot(slot, mouse))
                        AssignKeyboardMouse(slot, keyboard, mouse);
                }
                LogDeviceSnapshot($"dispositivo conectado: {device.displayName}");
                if (initialized) { RefreshUI(); LogAssignment("após conexão"); }
                return;
            }

            if (change != InputDeviceChange.Disconnected && change != InputDeviceChange.Removed) return;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerDevice player = players[i];
                if (player.primary == device && player.scheme == "GamepadKeyboardMouse" &&
                    player.keyboard != null && player.keyboard.added && player.mouse != null && player.mouse.added)
                {
                    // This is an already assigned alternate pair, not a newly selected replacement.
                    player.primary = player.keyboard;
                    player.scheme = "KeyboardMouse";
                }
                else if (player.primary is Gamepad && player.scheme == "GamepadKeyboardMouse" &&
                         (player.keyboard == device || player.mouse == device))
                {
                    // Keep the already assigned primary gamepad if an alternate keyboard/mouse goes away.
                    player.keyboard = null;
                    player.mouse = null;
                    player.scheme = "Gamepad";
                }
                else if (player.primary == device || player.keyboard == device || player.mouse == device)
                    player.Clear();
            }
            connectedPadsInOrder.Remove(device as Gamepad);
            LogDeviceSnapshot($"dispositivo removido: {device.displayName}");
            if (initialized) { RefreshUI(); LogAssignment("após desconexão; nenhuma substituição automática foi feita"); }
        }

        private void TrackConnectedGamepad(Gamepad pad)
        {
            if (pad != null && pad.added && !connectedPadsInOrder.Contains(pad)) connectedPadsInOrder.Add(pad);
        }

        private bool DevicesValid(out string reason)
        {
            reason = "";
            if (players[0].IsEmpty || players[1].IsEmpty) { reason = "Two valid input assignments are required."; return false; }
            if (players[0].primary == players[1].primary) { reason = "The same primary device is assigned to both players."; return false; }
            if (players[0].primary == players[1].mouse || players[1].primary == players[0].mouse ||
                players[0].keyboard == players[1].primary || players[1].keyboard == players[0].primary ||
                (players[0].mouse != null && players[0].mouse == players[1].mouse) ||
                (players[0].keyboard != null && players[0].keyboard == players[1].keyboard))
            { reason = "Players share an assigned device."; return false; }

            foreach (PlayerDevice player in players)
            {
                if (player.primary == null || !player.primary.added) { reason = "An assigned device was disconnected."; return false; }
                if (player.scheme == "KeyboardMouse")
                {
                    if (!(player.primary is Keyboard) || player.keyboard != player.primary ||
                        player.mouse == null || !player.mouse.added)
                    { reason = "KeyboardMouse requires a connected keyboard and mouse."; return false; }
                }
                else if (player.scheme == "Gamepad")
                {
                    if (!(player.primary is Gamepad) || player.keyboard != null || player.mouse != null)
                    { reason = "Gamepad requires an independent gamepad."; return false; }
                }
                else if (player.scheme == "GamepadKeyboardMouse")
                {
                    if (!(player.primary is Gamepad) || player.keyboard == null || !player.keyboard.added ||
                        player.mouse == null || !player.mouse.added)
                    { reason = "The hybrid scheme requires a connected gamepad, keyboard, and mouse."; return false; }
                }
                else { reason = "Unknown control scheme."; return false; }
            }

            if (players[0].scheme == "KeyboardMouse" && players[1].scheme == "KeyboardMouse")
            { reason = "The Input System provides one keyboard and mouse; two independent keyboard-and-mouse players are not supported."; return false; }
            return true;
        }

        private void RefreshUI()
        {
            if (cards == null || cards.Length != 2 || cards.Any(card => card == null)) return;
            for (int i = 0; i < cards.Length; i++)
            {
                CharacterCard card = cards[i];
                bool selected = firstCharacter.HasValue && firstCharacter.Value == card.role;
                if (card.playerLabel != null) card.playerLabel.text = !firstCharacter.HasValue ? "Choose a character" : selected ? "PLAYER 1" : "PLAYER 2";
                if (card.ready != null) card.ready.SetActive(selected);
                int playerIndex = firstCharacter.HasValue && !selected ? 1 : 0;
                RefreshDeviceVisual(playerIndex, card);
            }

            bool valid = DevicesValid(out string reason);
            if (startButton != null) startButton.interactable = firstCharacter.HasValue && valid;
            if (statusText != null)
            {
                if (!firstCharacter.HasValue) statusText.text = "Player 1: choose a character.";
                else if (valid)
                {
                    int assignedPads = players.Count(player => player.primary is Gamepad);
                    int unassignedPads = Mathf.Max(0, connectedPadsInOrder.Count(pad => pad != null && pad.added) - assignedPads);
                    statusText.text = unassignedPads > 0
                        ? $"Both players are ready. {unassignedPads} additional gamepad(s) detected but unassigned."
                        : "Both players are ready.";
                }
                else statusText.text = $"Incomplete controls: {reason} Detected: {Gamepad.all.Count(p => p.added)} gamepad(s).";
            }
            lastValidity = valid;
        }

        private void RefreshDeviceVisual(int playerIndex, CharacterCard card)
        {
            if (card.deviceIconContainer == null) return;
            for (int i = card.deviceIconContainer.childCount - 1; i >= 0; i--)
                Destroy(card.deviceIconContainer.GetChild(i).gameObject);
            if (!firstCharacter.HasValue)
            {
                if (card.deviceLabel != null) card.deviceLabel.text = "";
                return;
            }

            PlayerDevice player = players[playerIndex];
            var labels = new List<string>();
            if (player.primary is Gamepad)
            {
                AddVisual(card.deviceIconContainer, "Gamepad");
                labels.Add($"Primary: {player.primary.displayName}");
                if (player.keyboard != null && player.mouse != null)
                {
                    AddVisual(card.deviceIconContainer, "KeyboardMouse");
                    labels.Add("Alternate: Keyboard + Mouse");
                }
            }
            else if (player.scheme == "KeyboardMouse")
            {
                AddVisual(card.deviceIconContainer, "KeyboardMouse");
                labels.Add("Primary: Keyboard + Mouse");
            }
            string deviceText = string.Join("\n", labels);
            if (card.deviceLabel != null)
            {
                card.deviceLabel.text = deviceText;
            }
            else if (card.playerLabel != null && firstCharacter.HasValue)
            {
                // The current CharacterSelection scene has no separate device-label
                // object assigned. Keep the existing player label useful instead of
                // silently omitting the actual assigned device from the card.
                string slotLabel = playerIndex == 0 ? "PLAYER 1" : "PLAYER 2";
                card.playerLabel.text = string.IsNullOrEmpty(deviceText)
                    ? slotLabel
                    : $"{slotLabel}\n{deviceText}";
            }
        }

        private void AddVisual(Transform container, string scheme)
        {
            DeviceVisual visual = deviceVisuals?.FirstOrDefault(item => item != null && item.scheme == scheme);
            if (visual?.prefab != null) Instantiate(visual.prefab, container);
        }

        private void StartMatch()
        {
            string reason = "Choose a character before starting.";
            if (!firstCharacter.HasValue || !DevicesValid(out reason))
            {
                if (statusText != null) statusText.text = $"Cannot start: {reason}";
                return;
            }

            PlayerRole first = firstCharacter.Value;
            PlayerRole second = cards.First(card => card.role != first).role;
            bool saved = MatchSettings.TrySetMatch(first, second,
                players[0].primary, players[0].scheme, players[0].mouse, players[0].keyboard,
                players[1].primary, players[1].scheme, players[1].mouse, out string pairingReason);
            if (!saved)
            {
                if (statusText != null) statusText.text = $"Device pairing rejected: {pairingReason}";
                return;
            }
            LogAssignment("validado e transferido para MatchSettings");
            SceneManager.LoadScene(gameplaySceneName);
        }

        private void LogDeviceSnapshot(string reason)
        {
            if (!logDeviceDiagnostics) return;
            string allDevices = string.Join(" | ", InputSystem.devices.Select(DescribeDevice));
            string pads = string.Join(" | ", Gamepad.all.Select(DescribeDevice));
            Debug.Log($"[Input diagnostic: {reason}] InputSystem.devices={InputSystem.devices.Count} [{allDevices}] Gamepad.all={Gamepad.all.Count} [{pads}]", this);
        }

        private void LogAssignment(string reason)
        {
            if (!logDeviceDiagnostics) return;
            Debug.Log($"[Input assignment: {reason}] P1={DescribeAssignment(players[0])}; P2={DescribeAssignment(players[1])}; validation={DevicesValid(out string validation)} ({validation})", this);
        }

        private static string DescribeDevice(InputDevice device)
        {
            if (device == null) return "<null>";
            var d = device.description;
            return $"name='{device.displayName}', layout='{device.layout}', id={device.deviceId}, added={device.added}, interface='{d.interfaceName}', manufacturer='{d.manufacturer}', product='{d.product}', serial='{d.serial}', capabilities='{d.capabilities}'";
        }

        private static string DescribeAssignment(PlayerDevice player)
        {
            if (player == null || player.IsEmpty) return "unassigned";
            return $"scheme={player.scheme}, primary=({DescribeDevice(player.primary)}), keyboard=({DescribeDevice(player.keyboard)}), mouse=({DescribeDevice(player.mouse)})";
        }
    }
}
