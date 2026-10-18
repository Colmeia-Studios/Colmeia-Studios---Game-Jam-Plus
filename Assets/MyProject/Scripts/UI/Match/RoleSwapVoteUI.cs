using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    /// <summary>Collects each slot's role-swap vote and displays its three-second decision window.</summary>
    public sealed class RoleSwapVoteUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject votePanel;

        [Header("Player choices")]
        [SerializeField] private TMP_Text playerOneChoiceText;
        [SerializeField] private TMP_Text playerTwoChoiceText;
        [SerializeField] private Button playerOneYesButton;
        [SerializeField] private Button playerOneNoButton;
        [SerializeField] private Button playerTwoYesButton;
        [SerializeField] private Button playerTwoNoButton;

        [Header("Vote status")]
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button continueButton;

        private GameManager gameManager;
        private bool reportedMissingReferences;

        private void OnEnable()
        {
            Bind();
            AddButtonListeners();
            Refresh();
        }

        private void Start()
        {
            Bind();
            Refresh();
        }

        private void Update()
        {
            if (gameManager == null || !gameManager.IsRoleSwapVoteActive)
                return;

            PlayerController playerOne = gameManager.PlayerOne;
            PlayerController playerTwo = gameManager.PlayerTwo;
            if (gameManager.CycleState == MatchCycleState.RoleSwapVoting)
            {
                PollInitialVote(playerOne, 1);
                PollInitialVote(playerTwo, 2);
            }
            else if (gameManager.CycleState == MatchCycleState.RoleSwapCountdown)
            {
                if (playerOne != null && playerOne.WasRoleVoteTogglePressedThisFrame())
                    gameManager.ToggleRoleSwapVote(playerOne);
                if (playerTwo != null && playerTwo.WasRoleVoteTogglePressedThisFrame())
                    gameManager.ToggleRoleSwapVote(playerTwo);
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (gameManager != null)
                gameManager.MatchCycleChanged -= Refresh;
            RemoveButtonListeners();
        }

        private void Bind()
        {
            GameManager current = GameManager.Instance;
            if (current == null || current == gameManager)
                return;

            if (gameManager != null)
                gameManager.MatchCycleChanged -= Refresh;
            gameManager = current;
            gameManager.MatchCycleChanged += Refresh;
        }

        private void AddButtonListeners()
        {
            playerOneYesButton?.onClick.AddListener(OnPlayerOneYes);
            playerOneNoButton?.onClick.AddListener(OnPlayerOneNo);
            playerTwoYesButton?.onClick.AddListener(OnPlayerTwoYes);
            playerTwoNoButton?.onClick.AddListener(OnPlayerTwoNo);
            continueButton?.onClick.AddListener(ContinueMatch);
        }

        private void RemoveButtonListeners()
        {
            playerOneYesButton?.onClick.RemoveListener(OnPlayerOneYes);
            playerOneNoButton?.onClick.RemoveListener(OnPlayerOneNo);
            playerTwoYesButton?.onClick.RemoveListener(OnPlayerTwoYes);
            playerTwoNoButton?.onClick.RemoveListener(OnPlayerTwoNo);
            continueButton?.onClick.RemoveListener(ContinueMatch);
        }

        private void Refresh()
        {
            Bind();
            if (gameManager == null)
                return;

            MatchCycleState state = gameManager.CycleState;
            bool voting = state == MatchCycleState.RoleSwapVoting || state == MatchCycleState.RoleSwapCountdown;
            bool show = voting || state == MatchCycleState.RoleSwapResult;
            if (votePanel != null && votePanel.activeSelf != show)
                votePanel.SetActive(show);
            if (!show)
                return;

            ValidateReferences();
            SetText(playerOneChoiceText, $"Player 1: {FormatVote(gameManager.PlayerOneRoleSwapVote)}");
            SetText(playerTwoChoiceText, $"Player 2: {FormatVote(gameManager.PlayerTwoRoleSwapVote)}");

            bool canVote = voting;
            SetInteractable(playerOneYesButton, canVote);
            SetInteractable(playerOneNoButton, canVote);
            SetInteractable(playerTwoYesButton, canVote);
            SetInteractable(playerTwoNoButton, canVote);

            if (state == MatchCycleState.RoleSwapVoting)
            {
                SetText(countdownText, "...");
                SetText(resultText, "Choose Yes To Swap Roles Or No To Keep");
            }
            else if (state == MatchCycleState.RoleSwapCountdown)
            {
                SetText(countdownText, $"{Mathf.CeilToInt(gameManager.RoleSwapCountdownRemaining)}");
                SetText(resultText, "Durante a contagem, cada jogador pode alternar sua escolha com B / botão direito.");
            }
            else
            {
                SetText(countdownText, "!");
                SetText(resultText, gameManager.LastRoleSwapSucceeded
                    ? "Swaping Roles!"
                    : "Keeping Roles!");
            }

            if (continueButton != null)
                continueButton.interactable = state == MatchCycleState.RoleSwapResult && gameManager.Reputation > 0;
        }

        private void ValidateReferences()
        {
            if (reportedMissingReferences)
                return;

            if (votePanel == null || playerOneChoiceText == null || playerTwoChoiceText == null ||
                playerOneYesButton == null || playerOneNoButton == null || playerTwoYesButton == null ||
                playerTwoNoButton == null || countdownText == null || resultText == null || continueButton == null)
            {
                Debug.LogError(
                    "RoleSwapVoteUI: atribua Vote Panel, os textos Player 1/2, os quatro botões de voto, " +
                    "Countdown Text, Result Text e Continue Button no Inspector.", this);
                reportedMissingReferences = true;
            }
        }

        private void SetVote(int slot, bool wantsSwap)
        {
            if (gameManager != null)
                gameManager.SetRoleSwapVote(slot, wantsSwap);
        }

        private void PollInitialVote(PlayerController player, int slot)
        {
            if (player == null)
                return;

            if (player.WasRoleVoteYesPressedThisFrame())
                gameManager.SetRoleSwapVote(slot, true);
            else if (player.WasRoleVoteNoPressedThisFrame())
                gameManager.SetRoleSwapVote(slot, false);
        }

        private void OnPlayerOneYes() => SetVote(1, true);
        private void OnPlayerOneNo() => SetVote(1, false);
        private void OnPlayerTwoYes() => SetVote(2, true);
        private void OnPlayerTwoNo() => SetVote(2, false);

        private void ContinueMatch()
        {
            if (gameManager != null)
                gameManager.ContinueAfterRoleSwapResult();
        }

        private static string FormatVote(RoleSwapVote vote)
        {
            switch (vote)
            {
                case RoleSwapVote.Yes: return "Swap";
                case RoleSwapVote.No: return "Keep";
                default: return "Waiting Choose";
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
                label.text = value;
        }

        private static void SetInteractable(Selectable selectable, bool value)
        {
            if (selectable != null)
                selectable.interactable = value;
        }
    }
}
