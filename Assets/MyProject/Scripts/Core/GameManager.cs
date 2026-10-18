using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class DailyMatchResult
    {
        public DailyMatchResult(int day, int deliveries, int deliveryGoal, int expiredOrders, float closingBalance, int reputation)
            : this(day, deliveries, deliveryGoal, expiredOrders, closingBalance, closingBalance, 0f, reputation)
        {
        }

        public DailyMatchResult(int day, int deliveries, int deliveryGoal, int expiredOrders,
            float openingBalance, float closingBalance, float moneyEarned, int reputation)
        {
            Day = day;
            Deliveries = deliveries;
            DeliveryGoal = deliveryGoal;
            ExpiredOrders = expiredOrders;
            OpeningBalance = openingBalance;
            ClosingBalance = closingBalance;
            MoneyEarned = moneyEarned;
            Reputation = reputation;
        }

        public int Day { get; }
        public int Deliveries { get; }
        public int DeliveryGoal { get; }
        public int ExpiredOrders { get; }
        public float OpeningBalance { get; }
        public float ClosingBalance { get; }
        public float MoneyEarned { get; }
        public float BalanceChange => ClosingBalance - OpeningBalance;
        public bool GoalReached => Deliveries >= DeliveryGoal;
        public int Reputation { get; }
    }

    public enum MatchCycleState
    {
        NotStarted,
        Working,
        DayEnded,
        ResultsWaiting,
        RoleSwapVoting,
        RoleSwapCountdown,
        RoleSwapResult,
        Transitioning,
        GameOver
    }

    public enum RoleSwapVote
    {
        Pending,
        Yes,
        No
    }

    public class GameManager : MonoBehaviour
    {
        private static readonly float[] ReputationMultipliers = { 0.40f, 0.52f, 0.64f, 0.76f, 0.88f, 1f };

        public static GameManager Instance { get; private set; }

        [Header("Match data")] [SerializeField]
        private MatchConfig matchConfig;

        [SerializeField] private OrderManager orderManager;
        [SerializeField] private PickupCounter sharedPickupCounter;

        [Header("Players")] [SerializeField] private PlayerController playerOne;
        [SerializeField] private PlayerController playerTwo;
        [SerializeField] private DeliveryUI deliveryUI;
        [SerializeField] private CookPreparationUI cookPrepationUI;

        [Header("Player spawn points")] [SerializeField]
        private Transform cookSpawn;

        [SerializeField] private Transform deliverySpawn;

        [Header("Assignments")] [SerializeField]
        private Transform playerOneSpawn;

        [SerializeField] private Transform playerTwoSpawn;

        private readonly HashSet<string> paidOrderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> purchasedUpgradeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<DailyMatchResult> dailyResults = new List<DailyMatchResult>();
        private float elapsedSeconds;
        private float nextClockNotification;
        private float balance = 500f;
        private int reputation = 5;
        private int startHour = 11;
        private int endHour = 19;
        private float secondsPerHour = 40f;
        private bool isDayComplete;
        private MatchCycleState cycleState = MatchCycleState.NotStarted;
        private int currentDay;
        private float transitionElapsed;
        private int dailyDeliveryGoal = 7;
        private int completedDailyDeliveries;
        private float openingBalance;
        private float dailyMoneyEarned;
        private float voteCountdownRemaining;
        private int lastRoleSwapVoteDay;
        private bool lastRoleSwapSucceeded;
        private PlayerRole lastBoundPlayerOneRole;
        private PlayerRole lastBoundPlayerTwoRole;
        private bool roleBindingsInitialized;

        public event Action MatchStateChanged;
        public event Action ClockChanged;
        public event Action MatchCycleChanged;
        public event Action GameOver;

        public MatchConfig MatchConfiguration => matchConfig;
        public OrderManager Orders => orderManager;
        public PickupCounter SharedPickupCounter => sharedPickupCounter;
        public PlayerController PlayerOne => playerOne;
        public PlayerController PlayerTwo => playerTwo;
        public float ElapsedSeconds => elapsedSeconds;
        public float Balance => balance;
        public int Reputation => reputation;
        public int DailyDeliveryGoal => dailyDeliveryGoal;
        public int CompletedDailyDeliveries => completedDailyDeliveries;
        public float DailyMoneyEarned => dailyMoneyEarned;
        public int CurrentDay => currentDay;
        public IReadOnlyList<DailyMatchResult> DailyResults => dailyResults;
        public MatchCycleState CycleState => cycleState;
        public bool IsDayComplete => currentDay > 0 && cycleState != MatchCycleState.Working &&
                                     cycleState != MatchCycleState.NotStarted;
        public bool IsWorkDayActive => cycleState == MatchCycleState.Working;
        public bool IsGameOver => cycleState == MatchCycleState.GameOver;
        public RoleSwapVote PlayerOneRoleSwapVote { get; private set; } = RoleSwapVote.Pending;
        public RoleSwapVote PlayerTwoRoleSwapVote { get; private set; } = RoleSwapVote.Pending;
        public float RoleSwapCountdownRemaining => voteCountdownRemaining;
        public bool LastRoleSwapSucceeded => lastRoleSwapSucceeded;
        public bool IsRoleSwapVoteActive => cycleState == MatchCycleState.RoleSwapVoting ||
                                            cycleState == MatchCycleState.RoleSwapCountdown;
        public DailyMatchResult LatestDailyResult => dailyResults.Count > 0 ? dailyResults[dailyResults.Count - 1] : null;
        public float DayDurationSeconds => Mathf.Max(0f, (endHour - startHour) * secondsPerHour);

        public string ClockText
        {
            get
            {
                float gameMinutes = elapsedSeconds / secondsPerHour * 60f;
                int minutesFromStart = Mathf.FloorToInt(gameMinutes);
                int hour = startHour + minutesFromStart / 60;
                int minute = minutesFromStart % 60;
                if (isDayComplete)
                {
                    hour = endHour;
                    minute = 0;
                }

                return $"{hour:00}:{minute:00}";
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (orderManager == null)
            {
                orderManager = GetComponent<OrderManager>();
            }

            if (sharedPickupCounter == null)
            {
                sharedPickupCounter = FindAnyObjectByType<PickupCounter>();
            }
        }

        private void Start()
        {
            if (matchConfig != null)
            {
                if (!IsValidDayConfiguration(matchConfig))
                {
                    Debug.LogError(
                        "MatchConfig possui horários, intervalo de geração, prazo, duração de hora ou meta diária inválidos.",
                        matchConfig);
                    enabled = false;
                    return;
                }

                startHour = matchConfig.StartHour;
                endHour = matchConfig.EndHour;
                secondsPerHour = matchConfig.RealSecondsPerGameHour;
                balance = matchConfig.StartingBalance;
                reputation = matchConfig.StartingReputation;
                dailyDeliveryGoal = matchConfig.DailyDeliveryGoal;
            }

            ConfigurePlayers();
            BeginDay();
        }

        private static bool IsValidDayConfiguration(MatchConfig config)
        {
            return config.EndHour > config.StartHour &&
                   IsFinitePositive(config.RealSecondsPerGameHour) &&
                   IsFinitePositive(config.OrderGenerationIntervalSeconds) &&
                   IsFinitePositive(config.OrderDeadlineSeconds) &&
                   config.NextDayTransitionSeconds >= 0f &&
                   !float.IsNaN(config.NextDayTransitionSeconds) &&
                   !float.IsInfinity(config.NextDayTransitionSeconds) &&
                   config.DailyDeliveryGoal > 0;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void Update()
        {
            RefreshRoleBindingsIfChanged();

            if (cycleState == MatchCycleState.Transitioning)
            {
                float transitionDuration = matchConfig != null ? matchConfig.NextDayTransitionSeconds : 5f;
                transitionElapsed += GetGameplayClockDeltaTime();
                if (transitionElapsed >= transitionDuration)
                {
                    BeginNextDay();
                }

                return;
            }

            if (cycleState == MatchCycleState.RoleSwapCountdown)
            {
                if (Time.timeScale > 0f)
                {
                    voteCountdownRemaining = Mathf.Max(0f, voteCountdownRemaining - Time.unscaledDeltaTime);
                }

                if (voteCountdownRemaining <= 0f)
                {
                    ResolveRoleSwapVote();
                }

                return;
            }

            if (cycleState != MatchCycleState.Working || secondsPerHour <= 0f)
            {
                return;
            }

            elapsedSeconds = Mathf.Min(DayDurationSeconds, elapsedSeconds + GetGameplayClockDeltaTime());
            if (elapsedSeconds >= DayDurationSeconds)
            {
                elapsedSeconds = DayDurationSeconds;
                EndWorkDay();
            }

            if (elapsedSeconds >= nextClockNotification)
            {
                nextClockNotification = elapsedSeconds + 1f;
                ClockChanged?.Invoke();
            }
        }

        // MatchConfig times are specified in real seconds. Ignore accelerated or
        // slowed Unity time scales, while still pausing the match at timeScale 0.
        private static float GetGameplayClockDeltaTime()
        {
            return Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;
        }

        private void BeginDay()
        {
            currentDay++;
            elapsedSeconds = 0f;
            completedDailyDeliveries = 0;
            dailyMoneyEarned = 0f;
            openingBalance = balance;
            isDayComplete = false;
            cycleState = MatchCycleState.Working;
            nextClockNotification = 1f;
            transitionElapsed = 0f;
            orderManager?.BeginDay();
            foreach (PlayerController player in GetPlayers())
            {
                player?.ResetDailyState();
            }

            MatchCycleChanged?.Invoke();
            MatchStateChanged?.Invoke();
            ClockChanged?.Invoke();
        }

        private void BeginNextDay()
        {
            if (cycleState != MatchCycleState.Transitioning)
                return;

            BeginDay();
        }

        public void ContinueAfterDailyResults()
        {
            if (cycleState != MatchCycleState.ResultsWaiting || reputation <= 0)
                return;

            bool isRoleSwapMilestone = currentDay <= 25 && currentDay % 5 == 0;
            if (isRoleSwapMilestone && lastRoleSwapVoteDay != currentDay)
            {
                PlayerOneRoleSwapVote = RoleSwapVote.Pending;
                PlayerTwoRoleSwapVote = RoleSwapVote.Pending;
                voteCountdownRemaining = 0f;
                cycleState = MatchCycleState.RoleSwapVoting;
                MatchCycleChanged?.Invoke();
                return;
            }

            BeginTransitionToNextDay();
        }

        public bool SetRoleSwapVote(int playerSlot, bool wantsSwap)
        {
            if (!IsRoleSwapVoteActive || (playerSlot != 1 && playerSlot != 2))
                return false;

            RoleSwapVote choice = wantsSwap ? RoleSwapVote.Yes : RoleSwapVote.No;
            if (playerSlot == 1)
                PlayerOneRoleSwapVote = choice;
            else
                PlayerTwoRoleSwapVote = choice;

            if (cycleState == MatchCycleState.RoleSwapVoting &&
                PlayerOneRoleSwapVote != RoleSwapVote.Pending &&
                PlayerTwoRoleSwapVote != RoleSwapVote.Pending)
            {
                voteCountdownRemaining = 3f;
                cycleState = MatchCycleState.RoleSwapCountdown;
            }

            MatchCycleChanged?.Invoke();
            return true;
        }

        public bool ToggleRoleSwapVote(PlayerController player)
        {
            if (!IsRoleSwapVoteActive || player == null)
                return false;

            if (player == playerOne && PlayerOneRoleSwapVote != RoleSwapVote.Pending)
                return SetRoleSwapVote(1, PlayerOneRoleSwapVote != RoleSwapVote.Yes);
            if (player == playerTwo && PlayerTwoRoleSwapVote != RoleSwapVote.Pending)
                return SetRoleSwapVote(2, PlayerTwoRoleSwapVote != RoleSwapVote.Yes);

            return false;
        }

        public void ContinueAfterRoleSwapResult()
        {
            if (cycleState != MatchCycleState.RoleSwapResult || reputation <= 0)
                return;

            BeginTransitionToNextDay();
        }

        private void ResolveRoleSwapVote()
        {
            if (cycleState != MatchCycleState.RoleSwapCountdown)
                return;

            lastRoleSwapSucceeded = PlayerOneRoleSwapVote == RoleSwapVote.Yes &&
                                    PlayerTwoRoleSwapVote == RoleSwapVote.Yes;
            if (lastRoleSwapSucceeded && !SwapPlayerRoles())
                lastRoleSwapSucceeded = false;

            lastRoleSwapVoteDay = currentDay;
            cycleState = MatchCycleState.RoleSwapResult;
            MatchCycleChanged?.Invoke();
            MatchStateChanged?.Invoke();
        }

        private bool SwapPlayerRoles()
        {
            if (playerOne == null || playerTwo == null ||
                playerOne.AssignedRole == playerTwo.AssignedRole ||
                GetSpawnPointForRole(playerOne.AssignedRole == PlayerRole.Cook ? PlayerRole.Delivery : PlayerRole.Cook) == null ||
                GetSpawnPointForRole(playerTwo.AssignedRole == PlayerRole.Cook ? PlayerRole.Delivery : PlayerRole.Cook) == null)
            {
                Debug.LogError("Não foi possível trocar as funções: verifique os jogadores e os pontos Cook Spawn/Delivery Spawn.", this);
                return false;
            }

            PlayerRole newPlayerOneRole = playerOne.AssignedRole == PlayerRole.Cook
                ? PlayerRole.Delivery
                : PlayerRole.Cook;
            PlayerRole newPlayerTwoRole = playerTwo.AssignedRole == PlayerRole.Cook
                ? PlayerRole.Delivery
                : PlayerRole.Cook;
            Transform playerOneRoleSpawn = GetSpawnPointForRole(newPlayerOneRole);
            Transform playerTwoRoleSpawn = GetSpawnPointForRole(newPlayerTwoRole);

            // Update role data only. The existing PlayerInput pairings remain with their player slots.
            playerOne.SetRoleForRotation(newPlayerOneRole);
            playerTwo.SetRoleForRotation(newPlayerTwoRole);
            MatchSettings.SetRoles(newPlayerOneRole, newPlayerTwoRole);
            playerOne.transform.SetPositionAndRotation(playerOneRoleSpawn.position, playerOneRoleSpawn.rotation);
            playerTwo.transform.SetPositionAndRotation(playerTwoRoleSpawn.position, playerTwoRoleSpawn.rotation);
            RefreshRoleAssignments();
            MatchStateChanged?.Invoke();
            return true;
        }

        private void BeginTransitionToNextDay()
        {
            if (cycleState != MatchCycleState.ResultsWaiting && cycleState != MatchCycleState.RoleSwapResult)
                return;

            cycleState = MatchCycleState.Transitioning;
            transitionElapsed = 0f;
            MatchCycleChanged?.Invoke();
        }

        private void EndWorkDay()
        {
            if (cycleState != MatchCycleState.Working)
                return;

            // Move out of Working before notifications/penalties so repeated callbacks cannot close twice.
            cycleState = MatchCycleState.ResultsWaiting;
            isDayComplete = true;
            orderManager?.EndDay();
            int delivered = completedDailyDeliveries;
            int expired = 0;
            orderManager?.GetDayOrderCounts(currentDay, out delivered, out expired);
            if (delivered < dailyDeliveryGoal)
            {
                SetReputation(reputation - 1);
            }

            dailyResults.Add(new DailyMatchResult(
                currentDay,
                delivered,
                dailyDeliveryGoal,
                expired,
                openingBalance,
                balance,
                dailyMoneyEarned,
                reputation));
            foreach (PlayerController player in GetPlayers())
            {
                player?.ResetDailyState();
            }

            MatchCycleChanged?.Invoke();
            MatchStateChanged?.Invoke();
            ClockChanged?.Invoke();
            if (reputation <= 0)
            {
                cycleState = MatchCycleState.GameOver;
                MatchCycleChanged?.Invoke();
                GameOver?.Invoke();
            }
        }

        private IEnumerable<PlayerController> GetPlayers()
        {
            yield return playerOne;
            yield return playerTwo;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        [ContextMenu("Configure Players")]
        public void ConfigurePlayers()
        {
            if (playerOne == null || playerTwo == null)
            {
                Debug.LogWarning("GameManager requires both player references assigned.", this);
                return;
            }

            PlayerRole playerOneRole = MatchSettings.PlayerOneRole;
            PlayerRole playerTwoRole = MatchSettings.PlayerTwoRole;

            PlayerConfig playerOneConfig = matchConfig != null ? matchConfig.PlayerOne : null;
            PlayerConfig playerTwoConfig = matchConfig != null ? matchConfig.PlayerTwo : null;

            if (MatchSettings.PlayerOneDevice != null && MatchSettings.PlayerTwoDevice != null &&
                MatchSettings.PlayerOneDevice.added && MatchSettings.PlayerTwoDevice.added)
            {
                bool playerOneConfigured = playerOne.ConfigureMatchInput(playerOneRole, playerOneConfig,
                    MatchSettings.PlayerOneDevice, MatchSettings.PlayerOneMouse, MatchSettings.PlayerOneScheme);
                bool playerTwoConfigured = playerTwo.ConfigureMatchInput(playerTwoRole, playerTwoConfig,
                    MatchSettings.PlayerTwoDevice, MatchSettings.PlayerTwoMouse, MatchSettings.PlayerTwoScheme);
                if (!playerOneConfigured || !playerTwoConfigured)
                {
                    Debug.LogError("Não foi possível vincular os dispositivos aos jogadores da partida.", this);
                    return;
                }
            }
            else
            {
                Debug.LogError("A configuração de dispositivos da partida está inválida ou um dispositivo foi desconectado.", this);
                return;
            }

            Transform playerOneSpawnPoint = GetSpawnPointForRole(playerOneRole);
            Transform playerTwoSpawnPoint = GetSpawnPointForRole(playerTwoRole);

            if (playerOneSpawnPoint == null || playerTwoSpawnPoint == null)
            {
                Debug.LogError("Configure os pontos Cook Spawn e Delivery Spawn no GameManager.", this);
                return;
            }

            if (playerOneSpawnPoint != null)
            {
                playerOne.transform.SetPositionAndRotation(playerOneSpawnPoint.position, playerOneSpawnPoint.rotation);
            }

            if (playerTwoSpawnPoint != null)
            {
                playerTwo.transform.SetPositionAndRotation(playerTwoSpawnPoint.position, playerTwoSpawnPoint.rotation);
            }

            ConfigureRoleInterfaces();

        }

        public void RefreshRoleAssignments()
        {
            ConfigureRoleInterfaces();
        }

        private void RefreshRoleBindingsIfChanged()
        {
            if (playerOne == null || playerTwo == null)
                return;

            if (!roleBindingsInitialized ||
                playerOne.AssignedRole != lastBoundPlayerOneRole ||
                playerTwo.AssignedRole != lastBoundPlayerTwoRole)
            {
                ConfigureRoleInterfaces();
            }
        }

        private void ConfigureRoleInterfaces()
        {
            if (playerOne != null && playerTwo != null)
            {
                lastBoundPlayerOneRole = playerOne.AssignedRole;
                lastBoundPlayerTwoRole = playerTwo.AssignedRole;
                roleBindingsInitialized = true;
            }

            if (playerOne == null || playerTwo == null || playerOne.AssignedRole == playerTwo.AssignedRole)
            {
                Debug.LogError("GameManager requires exactly one Cook and one Delivery player before binding cameras and interfaces.", this);
                return;
            }

            PlayerRole playerOneRole = playerOne.AssignedRole;
            PlayerRole playerTwoRole = playerTwo.AssignedRole;
            PlayerController cookPlayer = playerOneRole == PlayerRole.Cook ? playerOne : playerTwo;
            PlayerController deliveryPlayer = playerOneRole == PlayerRole.Delivery ? playerOne : playerTwo;
            AssignCameraSide(cookPlayer, ScreenSide.Left);
            AssignCameraSide(deliveryPlayer, ScreenSide.Right);
            KifomePhoneUI firstPhone = playerOne.AssignedPhone;
            KifomePhoneUI secondPhone = playerTwo.AssignedPhone;
            KifomePhoneUI cookPhone = GetPhoneForRole(firstPhone, secondPhone, PlayerRole.Cook);
            KifomePhoneUI deliveryPhone = GetPhoneForRole(firstPhone, secondPhone, PlayerRole.Delivery);

            if (cookPhone == null || deliveryPhone == null)
            {
                Debug.LogError("GameManager requires one Cook phone and one Delivery phone assigned to the player slots.", this);
                return;
            }

            cookPhone.BindGameplay(cookPlayer, this);
            deliveryPhone.BindGameplay(deliveryPlayer, this);
            playerOne.AssignKifomePhone(playerOneRole == PlayerRole.Cook ? cookPhone : deliveryPhone);
            playerTwo.AssignKifomePhone(playerTwoRole == PlayerRole.Cook ? cookPhone : deliveryPhone);

            if (deliveryUI == null)
            {
                Debug.LogError("GameManager is missing its Delivery UI reference.", this);
                return;
            }

            deliveryUI.BindGameplay(deliveryPlayer, this);
            if (cookPrepationUI != null)
            {
                cookPrepationUI.BindGameplay(cookPlayer, this);
            }
            else
            {
                Debug.LogError("GameManager is missing its CookPreparationUI reference.", this);
            }
        }

        private void AssignCameraSide(PlayerController player, ScreenSide side)
        {
            Camera roleCamera = player != null ? player.GetCamera() : null;
            if (roleCamera == null)
            {
                Debug.LogError($"Cannot assign {side} split-screen to a player without a Camera.", player);
                return;
            }

            SplitScreenSetup split = roleCamera.GetComponent<SplitScreenSetup>();
            if (split == null)
            {
                Debug.LogError($"Camera '{roleCamera.name}' requires a SplitScreenSetup component.", roleCamera);
                return;
            }

            split.SetScreenSide(side);
        }

        private static KifomePhoneUI GetPhoneForRole(
            KifomePhoneUI first,
            KifomePhoneUI second,
            PlayerRole role)
        {
            if (first != null && first.PhoneRole == role) return first;
            if (second != null && second.PhoneRole == role) return second;
            return null;
        }

        public bool TryGetOrder(string orderId, out OrderRecord order)
        {
            if (orderManager == null)
            {
                order = null;
                return false;
            }

            return orderManager.TryGetOrder(orderId, out order);
        }

        public bool TryStartNextPreparation(out OrderRecord order)
        {
            order = null;
            return orderManager != null && orderManager.TryStartNextPreparation(out order);
        }

        public bool TryStartPreparation(string orderId)
        {
            return IsWorkDayActive && orderManager != null && orderManager.TryStartPreparation(orderId);
        }

        public bool TryCompletePreparation(
            string orderId,
            IReadOnlyList<string> ingredientIds,
            LunchboxType lunchboxType,
            PlayerController cook,
            out string result)
        {
            if (!IsWorkDayActive || orderManager == null)
            {
                result = !IsWorkDayActive ? "O expediente foi encerrado." : "OrderManager não está configurado.";
                return false;
            }

            return orderManager.TryCompletePreparation(orderId, ingredientIds, lunchboxType, cook, out result);
        }

        public bool TryDepositPreparedMealbox(PlayerController cook, out string result)
        {
            if (!IsWorkDayActive || cook == null || cook.AssignedRole != PlayerRole.Cook ||
                sharedPickupCounter == null)
            {
                result = "O Cook ou o balcão não está disponível para depositar a marmita.";
                return false;
            }

            return sharedPickupCounter.TryDepositCookMealbox(cook, out result);
        }

        public void RemovePreparedMealbox(string orderId)
        {
            if (playerOne != null) playerOne.RemovePreparedMealbox(orderId);
            if (playerTwo != null) playerTwo.RemovePreparedMealbox(orderId);
        }

        public bool CancelPreparation(string orderId)
        {
            return IsWorkDayActive && orderManager != null && orderManager.TryCancelPreparation(orderId);
        }

        public bool TryPickupOrder(string orderId, PlayerController player)
        {
            if (!IsWorkDayActive || player == null || player.AssignedRole != PlayerRole.Delivery ||
                orderManager == null ||
                sharedPickupCounter == null ||
                !player.CanAddOrderToInventory(orderId) ||
                !sharedPickupCounter.TryGetReadyMealbox(orderId, out MealboxPhysical mealbox) ||
                !sharedPickupCounter.TryTakeReadyOrder(orderId, out mealbox))
            {
                return false;
            }

            if (!player.TryAddOrderToInventory(orderId) || !orderManager.TryMarkPickedUp(orderId))
            {
                player.RemoveOrderFromInventory(orderId);
                if (!sharedPickupCounter.TryRestoreReadyOrder(orderId, mealbox) && mealbox != null)
                    Destroy(mealbox.gameObject);

                return false;
            }

            if (mealbox != null)
            {
                Destroy(mealbox.gameObject);
            }

            MatchStateChanged?.Invoke();
            return true;
        }

        public bool TryProcessOrderPayment(OrderRecord order)
        {
            if (!IsWorkDayActive || order == null || order.State != OrderState.InDelivery ||
                paidOrderIds.Contains(order.OrderId))
            {
                return false;
            }

            float adjustedValue = order.BaseValue * ReputationMultipliers[Mathf.Clamp(reputation, 0, 5)];
            float tipRate = 0f;
            float remainingRatio = order.DeadlineSeconds > 0f
                ? Mathf.Clamp01(order.GetRemainingSeconds(elapsedSeconds) / order.DeadlineSeconds)
                : 0f;

            bool isOverdue = order.IsOverdue(elapsedSeconds);
            if (!isOverdue)
            {
                if (remainingRatio > 0.75f)
                {
                    tipRate = 0.15f;
                }
                else if (remainingRatio > 0.5f)
                {
                    tipRate = 0.10f;
                }
            }

            float total = adjustedValue + adjustedValue * tipRate;
            if (isOverdue)
            {
                total *= 0.25f;
            }

            if (float.IsNaN(total) || float.IsInfinity(total) || total < 0f)
            {
                Debug.LogError($"Could not calculate a valid payment for order '{order.OrderId}'.", this);
                return false;
            }

            float updatedBalance = balance + total;
            if (float.IsNaN(updatedBalance) || float.IsInfinity(updatedBalance))
            {
                Debug.LogError($"Payment for order '{order.OrderId}' would produce an invalid balance.", this);
                return false;
            }

            paidOrderIds.Add(order.OrderId);
            balance = updatedBalance;
            dailyMoneyEarned += total;
            completedDailyDeliveries++;

            MatchStateChanged?.Invoke();
            return true;
        }

        public void SetReputation(int stars)
        {
            int clamped = Mathf.Clamp(stars, 0, 5);
            if (reputation == clamped)
            {
                return;
            }

            reputation = clamped;
            MatchStateChanged?.Invoke();
        }

        internal void NotifyMatchStateChanged()
        {
            MatchStateChanged?.Invoke();
        }

        public bool IsIngredientUnlocked(IngredientData ingredient)
        {
            return matchConfig == null || matchConfig.IsIngredientUnlocked(ingredient, Mathf.Max(1, currentDay));
        }

        public bool IsIngredientIdUnlocked(string ingredientId)
        {
            return matchConfig == null || matchConfig.IsIngredientIdUnlocked(ingredientId, Mathf.Max(1, currentDay));
        }

        public bool IsLunchboxUnlocked(LunchboxType type)
        {
            return matchConfig == null || matchConfig.IsLunchboxUnlocked(type, Mathf.Max(1, currentDay));
        }

        private Transform GetSpawnPointForRole(PlayerRole role)
        {
            if (role == PlayerRole.Cook)
            {
                return cookSpawn;
            }

            return deliverySpawn;
        }

    }
}
