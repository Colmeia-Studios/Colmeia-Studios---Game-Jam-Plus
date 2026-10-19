using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Menu options")]
        [SerializeField] private Button howToPlayButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button recordsButton;
        [SerializeField] private CampaignRecordsUI campaignRecordsUI;
        [SerializeField] private GameObject newGameConfirmationPanel;
        [SerializeField] private GlobalSettingsUI globalSettingsUI;
        [SerializeField] private TutorialVideoUI tutorialVideoUI;
        [Header("Scene")]
        [SerializeField] private string characterSelectionSceneName = "CharacterSelection";

        private bool loadingSelection;
        private bool awaitingTutorialBeforeNewGame;

        private void Awake()
        {
            if (howToPlayButton != null) howToPlayButton.onClick.AddListener(OpenHowToPlay);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (recordsButton != null) recordsButton.onClick.AddListener(OpenRecords);
            if (continueButton != null)
            {
                continueButton.interactable = CampaignSaveSystem.HasValidSave;
                continueButton.onClick.AddListener(ContinueGame);
            }
            if (newGameConfirmationPanel != null) newGameConfirmationPanel.SetActive(false);
        }

        private void Start() => EnsureMenuPointerReady();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) EnsureMenuPointerReady();
        }

        private void EnsureMenuPointerReady()
        {
            SetMenuCursor();
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Debug.LogWarning("MainMenuManager: no EventSystem in the scene; mouse UI clicks will not work.", this);
                return;
            }
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null &&
                eventSystem.GetComponent<BaseInputModule>() == null)
                Debug.LogWarning("MainMenuManager: EventSystem has no UI input module for pointer clicks.", this);
        }

        private static void SetMenuCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (howToPlayButton != null) howToPlayButton.onClick.RemoveListener(OpenHowToPlay);
            if (continueButton != null) continueButton.onClick.RemoveListener(ContinueGame);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
            if (recordsButton != null) recordsButton.onClick.RemoveListener(OpenRecords);
        }

        public void OpenHowToPlay()
        {
            if (loadingSelection || awaitingTutorialBeforeNewGame) return;
            if (tutorialVideoUI == null)
            {
                Debug.LogError("MainMenuManager: assign TutorialVideoUI for HOW TO PLAY.", this);
                return;
            }
            tutorialVideoUI.OpenForManualReview();
        }

        // Existing Play button callback. It starts a new campaign or asks before replacing one.
        public void StartGame()
        {
            if (loadingSelection || awaitingTutorialBeforeNewGame) return;
            if (CampaignSaveSystem.HasValidSave)
            {
                if (newGameConfirmationPanel != null) newGameConfirmationPanel.SetActive(true);
                else Debug.LogError("Assign the New Game confirmation panel to MainMenuManager.", this);
                return;
            }
            BeginNewGame();
        }

        public void ConfirmNewGame()
        {
            if (loadingSelection || awaitingTutorialBeforeNewGame) return;
            CampaignSaveSystem.Delete();
            if (newGameConfirmationPanel != null) newGameConfirmationPanel.SetActive(false);
            BeginNewGame();
        }

        public void CancelNewGame()
        {
            if (newGameConfirmationPanel != null) newGameConfirmationPanel.SetActive(false);
        }

        public void ContinueGame()
        {
            if (loadingSelection || awaitingTutorialBeforeNewGame || !CampaignSaveSystem.HasValidSave) return;
            loadingSelection = true;
            CampaignSaveSystem.RequestContinue();
            SceneManager.LoadScene(characterSelectionSceneName);
        }

        public void OpenSettings() => globalSettingsUI?.Open();
        public void OpenRecords()
        {
            if (campaignRecordsUI == null)
            {
                Debug.LogError("MainMenuManager: assign the CampaignRecordsUI from the MainMenu scene.", this);
                return;
            }
            campaignRecordsUI.OpenRecords();
        }

        private void BeginNewGame()
        {
            if (loadingSelection || awaitingTutorialBeforeNewGame) return;

            if (tutorialVideoUI != null && !TutorialVideoUI.HasSeenTutorial)
            {
                awaitingTutorialBeforeNewGame = true;
                tutorialVideoUI.OpenBeforeNewGame(OnTutorialFinishedBeforeNewGame);
                return;
            }

            ProceedToCharacterSelection();
        }

        private void OnTutorialFinishedBeforeNewGame()
        {
            awaitingTutorialBeforeNewGame = false;
            ProceedToCharacterSelection();
        }

        private void ProceedToCharacterSelection()
        {
            if (loadingSelection) return;
            loadingSelection = true;
            CampaignSaveSystem.CancelContinue();
            CampaignSaveSystem.BeginNewCampaign();
            MatchSettings.ResetToDefault();
            Time.timeScale = 1f;
            SetMenuCursor();
            SceneManager.LoadScene(characterSelectionSceneName);
        }
    }
}
