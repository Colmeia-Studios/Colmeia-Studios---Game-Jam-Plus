
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
        [System.Serializable]
        private class CharacterCard
        {
            [Header("Personagem")]
            public PlayerRole role;
            public Button selectButton;
            public GameObject ready;

            [Header("Jogador")]
            public TMP_Text playerLabel;

            [Header("Dispositivo")]
            public Transform deviceIconContainer;
        }

        [System.Serializable]
        private class DeviceVisual
        {
            public string scheme;
            public GameObject prefab;
        }

        private class PlayerDevice
        {
            public InputDevice device;
            public Mouse mouse;
            public string scheme;
        }

        [Header("Cenas")]
        [SerializeField] private string gameplaySceneName = "Gameplay";
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Cards")]
        [SerializeField] private CharacterCard[] cards = new CharacterCard[2];

        [Header("Interface")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Prefabs visuais de dispositivos")]
        [SerializeField] private DeviceVisual[] deviceVisuals;

        private readonly PlayerDevice[] players =
        {
            new PlayerDevice(),
            new PlayerDevice()
        };

        private PlayerRole? firstCharacter;
        private Gamepad[] assignedPads = new Gamepad[2];

        private void Awake()
        {
            ConfigureButtons();
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private void Start()
        {
            DetectInitialDevices();
            RefreshUI();
        }

        private void Update()
        {
            DetectGamepadInput();
            RefreshDeviceValidity();
        }

        private void ConfigureButtons()
        {
            if (cards != null)
            {
                foreach (CharacterCard card in cards)
                {
                    if (card == null || card.selectButton == null)
                        continue;

                    PlayerRole role = card.role;
                    card.selectButton.onClick.AddListener(
                        () => ChooseCharacter(role));
                }
            }

            if (startButton != null)
                startButton.onClick.AddListener(StartMatch);

            if (backButton != null)
                backButton.onClick.AddListener(
                    () => SceneManager.LoadScene(mainMenuSceneName));
        }

        private void ChooseCharacter(PlayerRole role)
        {
            if (cards == null ||
                cards.Count(card => card != null) != 2 ||
                cards.All(card => card.role != role))
                return;

            firstCharacter = role;
            RefreshUI();
        }

        private void DetectInitialDevices()
        {
            // Padrão inicial: teclado e mouse no Player 1.
            // Um gamepad pode ser atribuído ao Player 2.
            if (Keyboard.current != null && Mouse.current != null)
            {
                AssignKeyboardMouse(0, Keyboard.current, Mouse.current);
            }

            Gamepad[] pads = Gamepad.all
                .Where(pad => pad.added)
                .ToArray();

            // Um único gamepad disponível pode ser atribuído ao
            // segundo jogador quando o primeiro usa teclado e mouse.
            if (pads.Length == 1 && players[0].device != null)
                AssignGamepad(1, pads[0]);

            // Dois gamepads: atribuição inicial aos dois jogadores.
            else if (pads.Length >= 2)
            {
                AssignGamepad(0, pads[0]);
                AssignGamepad(1, pads[1]);
            }
        }

        private void DetectGamepadInput()
        {
            foreach (Gamepad pad in Gamepad.all)
            {
                if (!pad.added || !PressedThisFrame(pad))
                    continue;

                if (assignedPads.Contains(pad))
                    continue;

                int slot = FindAvailablePlayerSlot(pad);

                if (slot < 0)
                    continue;

                AssignGamepad(slot, pad);
                RefreshUI();
            }
        }

        private int FindAvailablePlayerSlot(Gamepad pad)
        {
            // Prefere substituir um slot vazio.
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].device == null)
                    return i;
            }

            // Se ambos já possuem dispositivos, não substitui
            // silenciosamente uma atribuição existente.
            return -1;
        }

        private static bool PressedThisFrame(Gamepad pad)
        {
            foreach (InputControl control in pad.allControls)
            {
                if (control is ButtonControl button &&
                    button.wasPressedThisFrame)
                    return true;
            }

            return false;
        }

        private void AssignKeyboardMouse(
            int playerIndex,
            Keyboard keyboard,
            Mouse mouse)
        {
            if (playerIndex < 0 || playerIndex >= players.Length ||
                keyboard == null || mouse == null)
                return;

            players[playerIndex].device = keyboard;
            players[playerIndex].mouse = mouse;
            players[playerIndex].scheme = "KeyboardMouse";

            assignedPads[playerIndex] = null;
        }

        private void AssignGamepad(int playerIndex, Gamepad pad)
        {
            if (playerIndex < 0 || playerIndex >= players.Length ||
                pad == null || !pad.added)
                return;

            // Evita atribuir o mesmo gamepad aos dois jogadores.
            for (int i = 0; i < players.Length; i++)
            {
                if (i != playerIndex && players[i].device == pad)
                    return;
            }

            players[playerIndex].device = pad;
            players[playerIndex].mouse = null;
            players[playerIndex].scheme = "Gamepad";
            assignedPads[playerIndex] = pad;
        }

        private void OnDeviceChange(
            InputDevice device,
            InputDeviceChange change)
        {
            if (change != InputDeviceChange.Disconnected &&
                change != InputDeviceChange.Removed)
                return;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].device == device ||
                    players[i].mouse == device)
                {
                    players[i].device = null;
                    players[i].mouse = null;
                    players[i].scheme = null;
                    assignedPads[i] = null;
                }
            }

            RefreshUI();
        }

        private void RefreshDeviceValidity()
        {
            // Mantém a interface atualizada caso algum dispositivo
            // seja desconectado ou deixe de estar disponível.
            if (!DevicesValid() && startButton != null)
                startButton.interactable = false;
        }

        private bool DevicesValid()
        {
            if (players[0].device == null || players[1].device == null ||
                players[0].device == players[1].device ||
                (players[0].mouse != null && players[0].mouse == players[1].mouse))
                return false;

            for (int i = 0; i < players.Length; i++)
            {
                PlayerDevice player = players[i];

                if (player.device == null || !player.device.added)
                    return false;

                if (i == 1 && player.device == players[0].device)
                    return false;

                if (player.scheme == "KeyboardMouse")
                {
                    if (!(player.device is Keyboard) ||
                        player.mouse == null ||
                        !player.mouse.added)
                        return false;

                    // O Input System normalmente expõe um único teclado
                    // e mouse. Não os apresenta como dois conjuntos
                    // independentes sem uma implementação específica.
                }
                else if (player.scheme == "Gamepad")
                {
                    if (!(player.device is Gamepad))
                        return false;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private void RefreshUI()
        {
            if (cards == null || cards.Length != 2 ||
                cards.Any(card => card == null))
                return;

            for (int i = 0; i < cards.Length; i++)
            {
                CharacterCard card = cards[i];
                bool selected = firstCharacter.HasValue &&
                                firstCharacter.Value == card.role;
                
                if (card.playerLabel != null)
                {
                    if (!firstCharacter.HasValue)
                    {
                        card.playerLabel.text = "Escolha";
                    }
                    else
                    {
                        card.playerLabel.text = selected
                            ? "PLAYER 1"
                            : "PLAYER 2";
                    }
                }

                if (card.ready != null)
                    card.ready.SetActive(true);

                RefreshDeviceVisual(i, card);
            }

            bool devicesValid = DevicesValid();
            bool characterSelected = firstCharacter.HasValue;

            if (startButton != null)
                startButton.interactable =
                    characterSelected && devicesValid;

            if (statusText != null)
            {
                if (!characterSelected)
                {
                    statusText.text =
                        "Player 1: escolha um personagem.";
                }
                else if (!devicesValid)
                {
                    statusText.text =
                        "Atribua dois dispositivos independentes para iniciar.";
                }
                else
                {
                    statusText.text = "Dois jogadores prontos.";
                }
            }
        }

        private void RefreshDeviceVisual(
            int cardIndex,
            CharacterCard card)
        {
            if (card.deviceIconContainer == null)
                return;

            // Remove apenas os filhos visuais que este método controla.
            for (int i = card.deviceIconContainer.childCount - 1; i >= 0; i--)
                Destroy(card.deviceIconContainer.GetChild(i).gameObject);

            PlayerRole role = card.role;
            if (!firstCharacter.HasValue)
                return;

            int playerIndex = 0;

            if (firstCharacter.HasValue &&
                firstCharacter.Value != role)
                playerIndex = 1;

            PlayerDevice player = players[playerIndex];

            if (player.device == null || string.IsNullOrEmpty(player.scheme))
                return;

            DeviceVisual visual = deviceVisuals.FirstOrDefault(
                item => item != null && item.scheme == player.scheme);

            if (visual == null || visual.prefab == null)
                return;

            Instantiate(visual.prefab, card.deviceIconContainer);
        }

        private void StartMatch()
        {
            if (!firstCharacter.HasValue || !DevicesValid())
                return;

            PlayerRole first = firstCharacter.Value;
            PlayerRole second = cards
                .First(card => card.role != first)
                .role;

            bool saved = MatchSettings.SetMatch(
                first,
                second,
                players[0].device,
                players[0].scheme,
                players[0].mouse,
                players[1].device,
                players[1].scheme,
                players[1].mouse);

            if (!saved)
            {
                if (statusText != null)
                    statusText.text =
                        "Não foi possível validar os dispositivos.";
                return;
            }

            SceneManager.LoadScene(gameplaySceneName);
        }
    }
}
