using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    /// <summary>Shows the separate Victory/Defeat gate, records the campaign once, then opens its summary.</summary>
    public sealed class CampaignEndingUI : MonoBehaviour
    {
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;
        [SerializeField] private Button victoryContinueButton;
        [SerializeField] private Button defeatContinueButton;
        [SerializeField] private TMP_Text persistenceErrorText;
        [SerializeField] private CampaignSummaryUI summaryUI;

        private GameManager manager;
        private CampaignHistoryRecord completedRecord;
        private bool audioPlayed;
        private bool missingReferencesReported;

        private void Awake()
        {
            victoryContinueButton?.onClick.AddListener(ContinueToSummary);
            defeatContinueButton?.onClick.AddListener(ContinueToSummary);
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
            if (persistenceErrorText != null) persistenceErrorText.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            Bind();
            Refresh();
        }

        private void Start() { ValidateReferences(); Bind(); Refresh(); }

        private void OnDisable()
        {
            if (manager != null)
            {
                manager.GameOver -= OnDefeat;
                manager.GameWon -= OnVictory;
                manager.MatchCycleChanged -= Refresh;
            }
        }

        private void OnDestroy()
        {
            victoryContinueButton?.onClick.RemoveListener(ContinueToSummary);
            defeatContinueButton?.onClick.RemoveListener(ContinueToSummary);
        }

        private void Bind()
        {
            GameManager current = GameManager.Instance;
            if (current == null || current == manager) return;
            if (manager != null)
            {
                manager.GameOver -= OnDefeat;
                manager.GameWon -= OnVictory;
                manager.MatchCycleChanged -= Refresh;
            }
            manager = current;
            manager.GameOver += OnDefeat;
            manager.GameWon += OnVictory;
            manager.MatchCycleChanged += Refresh;
        }

        private void OnDefeat() => ShowEnding(false);
        private void OnVictory() => ShowEnding(true);

        private void Refresh()
        {
            Bind();
            if (manager == null || !manager.IsMatchEnded) return;
            ShowEnding(manager.IsVictory);
        }

        private void ShowEnding(bool victory)
        {
            if (victoryPanel != null) victoryPanel.SetActive(victory);
            if (defeatPanel != null) defeatPanel.SetActive(!victory);
            if (!audioPlayed)
            {
                if (victory) AudioManager.Instance?.PlayVictory();
                else AudioManager.Instance?.PlayDefeat();
                audioPlayed = true;
            }
            TryRegister();
        }

        private bool TryRegister()
        {
            if (completedRecord != null) return true;
            if (manager != null && manager.TryRegisterCampaignCompletion(out completedRecord))
            {
                if (persistenceErrorText != null) persistenceErrorText.gameObject.SetActive(false);
                return true;
            }
            if (persistenceErrorText != null)
            {
                persistenceErrorText.text = "Could not save the campaign record. Please try again before continuing.";
                persistenceErrorText.gameObject.SetActive(true);
            }
            return false;
        }

        private void ValidateReferences()
        {
            if (missingReferencesReported) return;
            if (victoryPanel == null || defeatPanel == null || victoryContinueButton == null ||
                defeatContinueButton == null || summaryUI == null)
            {
                Debug.LogError("CampaignEndingUI: assign Victory Panel, Defeat Panel, both continue buttons and the final CampaignSummaryUI in Gameplay.", this);
                missingReferencesReported = true;
            }
        }

        private void ContinueToSummary()
        {
            if (!TryRegister()) return;
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
            if (summaryUI == null)
            {
                Debug.LogError("CampaignEndingUI: assign the final CampaignSummaryUI in the Inspector.", this);
                return;
            }
            summaryUI.Show(completedRecord, null, true);
        }
    }
}
