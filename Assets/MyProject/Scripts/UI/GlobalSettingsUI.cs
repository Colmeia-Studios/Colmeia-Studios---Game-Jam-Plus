using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    /// <summary>Inspector-wired persistent settings/pause panel. Add this to a dedicated Canvas root.</summary>
    public sealed class GlobalSettingsUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject soundPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject exitConfirmationPanel;
        [Header("Settings")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text musicValueText;
        [SerializeField] private TMP_Text sfxValueText;
        [Header("Gameplay only")]
        [SerializeField] private GameObject backToMenuButton;
        [SerializeField] private GameObject closeSettingsButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button saveAndExitButton;
        [SerializeField] private Button exitWithoutSavingButton;
        [SerializeField] private Button cancelExitButton;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        private bool openedDuringGameplay;
        private bool wasPausedBeforeOpen;
        private bool isAllowedScene;
        private bool leavingToMenu;
        private static GlobalSettingsUI instance;

        public static GlobalSettingsUI Instance => instance;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            SceneManager.activeSceneChanged += OnSceneChanged;
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            if (continueButton != null) continueButton.onClick.AddListener(ContinueGame);
            if (closeSettingsButton != null) closeSettingsButton.GetComponent<Button>()?.onClick.AddListener(Close);
            if (saveAndExitButton != null) saveAndExitButton.onClick.AddListener(SaveAndExit);
            if (exitWithoutSavingButton != null) exitWithoutSavingButton.onClick.AddListener(ExitWithoutSaving);
            if (cancelExitButton != null) cancelExitButton.onClick.AddListener(CancelExit);
            LoadSliderValues();
            if (AudioManager.Instance == null)
                Debug.LogWarning("GlobalSettingsUI: assign an AudioManager to the persistent Canvas to apply slider changes.", this);
            SetVisible(false);
            OnSceneChanged(default, SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            if (instance == this) instance = null;
        }

        private void Update()
        {
            ApplyCursorForCurrentState();
            if (!isAllowedScene || settingsPanel == null || settingsPanel.activeSelf ||
                SceneManager.GetActiveScene().name != "Gameplay")
                return;

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null) return;

            // The action is evaluated by each PlayerInput, so only the keyboard/gamepad
            // actually paired to that player can open the global settings panel.
            if ((gameManager.PlayerOne != null && gameManager.PlayerOne.WasOpenSettingsPressedThisFrame()) ||
                (gameManager.PlayerTwo != null && gameManager.PlayerTwo.WasOpenSettingsPressedThisFrame()))
                Open();
        }

        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            isAllowedScene = newScene.name == mainMenuSceneName || newScene.name == "Gameplay";
            bool gameplay = newScene.name == "Gameplay";
            if (newScene.name == mainMenuSceneName) { Time.timeScale = 1f; leavingToMenu = false; }
            if (!isAllowedScene)
            {
                if (openedDuringGameplay) Time.timeScale = 1f;
                openedDuringGameplay = false;
                wasPausedBeforeOpen = false;
                RestoreCursor();
            }
            if (backToMenuButton != null) backToMenuButton.SetActive(gameplay);
            if (continueButton != null) continueButton.gameObject.SetActive(gameplay);
            if (!isAllowedScene) SetVisible(false);
            ApplyCursorForCurrentState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) ApplyCursorForCurrentState();
        }

        public void Open()
        {
            if (!isAllowedScene || settingsPanel == null || settingsPanel.activeSelf) return;
            openedDuringGameplay = SceneManager.GetActiveScene().name == "Gameplay";
            wasPausedBeforeOpen = Time.timeScale == 0f;
            if (openedDuringGameplay) Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            LoadSliderValues();
            SetVisible(true);
            ShowSoundPanel();
        }

        public void Close()
        {
            SetVisible(false);
            if (openedDuringGameplay && !wasPausedBeforeOpen) Time.timeScale = 1f;
            openedDuringGameplay = false;
            wasPausedBeforeOpen = false;
            ApplyCursorForCurrentState();
        }

        public void ContinueGame()
        {
            if (openedDuringGameplay && !wasPausedBeforeOpen &&
                SceneManager.GetActiveScene().name == "Gameplay")
                Time.timeScale = 1f;
            wasPausedBeforeOpen = false;
            SetVisible(false);
            openedDuringGameplay = false;
            ApplyCursorForCurrentState();
        }

        public void ShowSoundPanel()
        {
            if (soundPanel != null) soundPanel.SetActive(true);
            if (controlsPanel != null) controlsPanel.SetActive(false);
        }

        public void ShowControlsPanel()
        {
            if (soundPanel != null) soundPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(true);
        }

        public void RequestReturnToMenu()
        {
            if (!isAllowedScene || SceneManager.GetActiveScene().name != "Gameplay") return;
            if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(true);
        }

        public void SaveAndExit()
        {
            if (leavingToMenu || GameManager.Instance == null || !GameManager.Instance.SaveCampaign()) return;
            leavingToMenu = true;
            LoadMainMenu();
        }

        public void ExitWithoutSaving()
        {
            if (leavingToMenu) return;
            leavingToMenu = true;
            CampaignSaveSystem.CancelContinue();
            CampaignSaveSystem.EndActiveCampaign();
            LoadMainMenu();
        }

        public void CancelExit()
        {
            if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(false);
        }

        private void LoadMainMenu()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            MatchSettings.ResetToDefault();
            SetVisible(false);
            if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(false);
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void LoadSliderValues()
        {
            AudioManager audio = AudioManager.Instance;
            if (musicSlider != null)
            {
                musicSlider.minValue = 0f; musicSlider.maxValue = 1f;
                musicSlider.SetValueWithoutNotify(audio != null ? audio.MusicVolume : 0.8f);
                UpdateValue(musicValueText, musicSlider.value);
            }
            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f;
                sfxSlider.SetValueWithoutNotify(audio != null ? audio.SfxVolume : 0.8f);
                UpdateValue(sfxValueText, sfxSlider.value);
            }
        }

        public void OnMusicChanged(float value)
        {
            AudioManager.Instance?.SetMusicVolume(value);
            UpdateValue(musicValueText, value);
        }
        public void OnSfxChanged(float value)
        {
            AudioManager.Instance?.SetSfxVolume(value);
            UpdateValue(sfxValueText, value);
        }
        private void RestoreCursor()
        {
            ApplyCursorForCurrentState();
        }

        private void ApplyCursorForCurrentState()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            bool needsMouse = sceneName != "Gameplay" ||
                              (settingsPanel != null && settingsPanel.activeSelf) ||
                              KifomePhoneUI.IsCursorRequested;
            GameManager manager = GameManager.Instance;
            if (sceneName == "Gameplay" && manager != null)
                needsMouse |= manager.CycleState != MatchCycleState.Working;

            Cursor.lockState = needsMouse ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = needsMouse;
        }

        private static void UpdateValue(TMP_Text label, float value) { if (label != null) label.text = $"{Mathf.RoundToInt(value * 100f)}%"; }
        private void SetVisible(bool visible)
        {
            if (settingsPanel != null) settingsPanel.SetActive(visible && isAllowedScene);
            if (!visible && exitConfirmationPanel != null) exitConfirmationPanel.SetActive(false);
        }
    }
}
