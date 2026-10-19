using UnityEngine;
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
        [Header("Scene")]
        [SerializeField] private string characterSelectionSceneName = "CharacterSelection";

        private bool loadingSelection;

        private void Awake()
        {
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (recordsButton != null) recordsButton.onClick.AddListener(OpenRecords);
            if (continueButton != null)
            {
                continueButton.interactable = CampaignSaveSystem.HasValidSave;
                continueButton.onClick.AddListener(ContinueGame);
            }
            if (newGameConfirmationPanel != null) newGameConfirmationPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (continueButton != null) continueButton.onClick.RemoveListener(ContinueGame);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
            if (recordsButton != null) recordsButton.onClick.RemoveListener(OpenRecords);
        }

        // Existing Play button callback. It starts a new campaign or asks before replacing one.
        public void StartGame()
        {
            if (loadingSelection) return;
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
            if (loadingSelection) return;
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
            if (loadingSelection || !CampaignSaveSystem.HasValidSave) return;
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
            if (loadingSelection) return;
            loadingSelection = true;
            CampaignSaveSystem.CancelContinue();
            CampaignSaveSystem.BeginNewCampaign();
            MatchSettings.ResetToDefault();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(characterSelectionSceneName);
        }
    }
}
