using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    /// <summary>Displays the latest completed day and explicitly gates continuing the match.</summary>
    public sealed class DailyResultsUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject resultsPanel;

        [Header("Result labels")]
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private TMP_Text deliveriesText;
        [SerializeField] private TMP_Text goalText;
        [SerializeField] private TMP_Text goalStatusText;
        [SerializeField] private TMP_Text moneyEarnedText;
        [SerializeField] private TMP_Text closingBalanceText;
        [SerializeField] private TMP_Text reputationText;
        [SerializeField] private TMP_Text expiredOrdersText;
        [SerializeField] private Button continueButton;

        private GameManager gameManager;
        private bool reportedMissingReferences;

        private void OnEnable()
        {
            Bind();
            if (continueButton != null)
                continueButton.onClick.AddListener(ContinueMatch);
            Refresh();
        }

        private void Start()
        {
            Bind();
            Refresh();
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.MatchCycleChanged -= Refresh;
                gameManager.MatchStateChanged -= Refresh;
            }

            if (continueButton != null)
                continueButton.onClick.RemoveListener(ContinueMatch);
        }

        private void Bind()
        {
            GameManager current = GameManager.Instance;
            if (current == null || current == gameManager)
                return;

            if (gameManager != null)
            {
                gameManager.MatchCycleChanged -= Refresh;
                gameManager.MatchStateChanged -= Refresh;
            }

            gameManager = current;
            gameManager.MatchCycleChanged += Refresh;
            gameManager.MatchStateChanged += Refresh;
        }

        private void Refresh()
        {
            Bind();
            if (gameManager == null)
                return;

            DailyMatchResult result = gameManager.LatestDailyResult;
            bool isVisible = result != null &&
                             gameManager.CycleState == MatchCycleState.ResultsWaiting;
            bool wasVisible = resultsPanel != null && resultsPanel.activeSelf;
            if (resultsPanel != null && resultsPanel.activeSelf != isVisible)
                resultsPanel.SetActive(isVisible);
            if (isVisible && !wasVisible)
                AudioManager.Instance?.PlaySfx(AudioCue.DayEnd);

            if (!isVisible)
                return;

            ValidateReferences();
            SetText(dayText, $"Day {result.Day} Completed");
            SetText(deliveriesText,$"{result.Deliveries} Deliveries Done");
            SetText(goalText, $"Delivery Goal: {result.Deliveries}/{result.DeliveryGoal}");
            SetText(goalStatusText, result.GoalReached ? "Goal Reached" : "Goal Not Reached");
            SetText(moneyEarnedText, $"R$ {result.MoneyEarned:0.00}");
            SetText(closingBalanceText, $"R$ {result.ClosingBalance:0.00}");
            SetText(reputationText, $"{result.Reputation}");
            SetText(expiredOrdersText, result.ExpiredOrders.ToString());

            if (continueButton != null)
                continueButton.interactable = gameManager.CycleState == MatchCycleState.ResultsWaiting;
        }

        private void ValidateReferences()
        {
            if (reportedMissingReferences)
                return;

            bool missing = resultsPanel == null || dayText == null || deliveriesText == null ||
                           goalText == null || goalStatusText == null || moneyEarnedText == null ||
                           closingBalanceText == null || reputationText == null || continueButton == null;
            if (missing)
            {
                Debug.LogError(
                    "DailyResultsUI: atribua Results Panel, Day Text, Deliveries Text, Goal Text, Goal Status Text, " +
                    "Money Earned Text, Closing Balance Text, Reputation Text e Continue Button no Inspector. " +
                    "Expired Orders Text is optional.", this);
                reportedMissingReferences = true;
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
                label.text = value;
        }

        private void ContinueMatch()
        {
            if (gameManager != null)
                gameManager.ContinueAfterDailyResults();
        }
    }
}
