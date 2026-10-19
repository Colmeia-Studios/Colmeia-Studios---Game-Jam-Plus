using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    /// <summary>Displays a completed campaign snapshot without depending on a live GameManager.</summary>
    public sealed class CampaignSummaryUI : MonoBehaviour
    {
        [SerializeField] private GameObject summaryPanel;
        [SerializeField] private TMP_Text outcomeText;
        [SerializeField] private TMP_Text revenueText;
        [SerializeField] private TMP_Text deliveriesText;
        [SerializeField] private TMP_Text reputationText;
        [SerializeField] private TMP_Text playTimeText;
        [SerializeField] private TMP_Text completedDateText;
        [SerializeField] private Button backButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private Action backAction;
        private bool missingReferencesReported;

        private void Awake()
        {
            if (backButton != null && backButton != mainMenuButton)
                backButton.onClick.AddListener(GoBack);
            if (mainMenuButton != null && mainMenuButton != backButton)
                mainMenuButton.onClick.AddListener(GoToMainMenu);
            if (summaryPanel == null) summaryPanel = gameObject;
            summaryPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            backButton?.onClick.RemoveListener(GoBack);
            mainMenuButton?.onClick.RemoveListener(GoToMainMenu);
        }

        public void Show(CampaignHistoryRecord record, Action onBack = null, bool allowMainMenu = true)
        {
            if (record == null)
            {
                Debug.LogError("CampaignSummaryUI cannot show an empty campaign record.", this);
                return;
            }
            ValidateReferences();
            backAction = onBack;
            ConfigureNavigationButtons(onBack != null, allowMainMenu);
            SetText(outcomeText, record.victory ? "VICTORY" : "DEFEAT");
            SetText(revenueText, $"{record.revenue.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"))}");
            SetText(deliveriesText, record.deliveries.ToString("N0", CultureInfo.GetCultureInfo("pt-BR")));
            SetText(reputationText, $"{record.reputation}");
            SetText(playTimeText, FormatDuration(record.playSeconds));
            if (DateTime.TryParse(record.completedAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime completed))
                SetText(completedDateText, completed.ToLocalTime().ToString("MM/dd/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR")));
            else SetText(completedDateText, "—");
            summaryPanel.SetActive(true);
        }

        private void ConfigureNavigationButtons(bool hasBackAction, bool allowMainMenu)
        {
            if (backButton != null && backButton == mainMenuButton)
            {
                // The current MainMenu scene intentionally reuses one button. Make
                // its action contextual instead of attaching conflicting callbacks.
                backButton.onClick.RemoveListener(GoBack);
                backButton.onClick.RemoveListener(GoToMainMenu);
                if (hasBackAction)
                    backButton.onClick.AddListener(GoBack);
                else if (allowMainMenu)
                    backButton.onClick.AddListener(GoToMainMenu);
                backButton.gameObject.SetActive(hasBackAction || allowMainMenu);
                return;
            }

            if (backButton != null) backButton.gameObject.SetActive(hasBackAction);
            if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(allowMainMenu);
        }

        public void Hide() => summaryPanel?.SetActive(false);

        public static string FormatDuration(float seconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0d, seconds));
            return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }

        private void GoBack()
        {
            if (backAction != null) backAction.Invoke();
            else Hide();
        }

        private void GoToMainMenu()
        {
            Time.timeScale = 1f;
            MatchSettings.ResetToDefault();
            CampaignSaveSystem.CancelContinue();
            CampaignSaveSystem.EndActiveCampaign();
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) label.text = value;
        }

        private void ValidateReferences()
        {
            if (missingReferencesReported) return;
            if (outcomeText == null || revenueText == null || deliveriesText == null ||
                reputationText == null || playTimeText == null)
            {
                Debug.LogError("CampaignSummaryUI: assign Outcome, Revenue, Deliveries, Reputation and Play Time text fields.", this);
                missingReferencesReported = true;
            }
        }
    }
}
