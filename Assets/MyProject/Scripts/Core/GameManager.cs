using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
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

        [Header("Player spawn points")] [SerializeField]
        private Transform cookSpawn;

        [SerializeField] private Transform deliverySpawn;

        [Header("Assignments")] [SerializeField]
        private Transform playerOneSpawn;

        [SerializeField] private Transform playerTwoSpawn;

        private readonly HashSet<string> paidOrderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> purchasedUpgradeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private float elapsedSeconds;
        private float nextClockNotification;
        private float balance = 500f;
        private int reputation = 5;
        private int startHour = 11;
        private int endHour = 19;
        private float secondsPerHour = 40f;
        private bool isDayComplete;
        private int dailyDeliveryGoal = 7;
        private int completedDailyDeliveries;

        public event Action MatchStateChanged;
        public event Action ClockChanged;

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
        public bool IsDayComplete => isDayComplete;
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
                startHour = matchConfig.StartHour;
                endHour = matchConfig.EndHour;
                secondsPerHour = matchConfig.RealSecondsPerGameHour;
                balance = matchConfig.StartingBalance;
                reputation = matchConfig.StartingReputation;
                dailyDeliveryGoal = matchConfig.DailyDeliveryGoal;
            }

            ConfigurePlayers();
            MatchStateChanged?.Invoke();
            ClockChanged?.Invoke();
        }

        private void Update()
        {
            if (isDayComplete || secondsPerHour <= 0f)
            {
                return;
            }

            elapsedSeconds = Mathf.Min(DayDurationSeconds, elapsedSeconds + Time.deltaTime);
            if (elapsedSeconds >= DayDurationSeconds)
            {
                isDayComplete = true;
            }

            if (elapsedSeconds >= nextClockNotification)
            {
                nextClockNotification = elapsedSeconds + 1f;
                ClockChanged?.Invoke();
            }
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

            ConfigureRoleInterfaces(playerOneRole, playerTwoRole);

        }

        private void ConfigureRoleInterfaces(PlayerRole playerOneRole, PlayerRole playerTwoRole)
        {
            PlayerController cookPlayer = playerOneRole == PlayerRole.Cook ? playerOne : playerTwo;
            PlayerController deliveryPlayer = playerOneRole == PlayerRole.Delivery ? playerOne : playerTwo;
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
            return orderManager != null && orderManager.TryStartPreparation(orderId);
        }

        public bool TryCompletePreparation(
            string orderId,
            IReadOnlyList<string> ingredientIds,
            LunchboxType lunchboxType,
            out string result)
        {
            if (orderManager == null)
            {
                result = "OrderManager não está configurado.";
                return false;
            }

            return orderManager.TryCompletePreparation(orderId, ingredientIds, lunchboxType, out result);
        }

        public bool CancelPreparation(string orderId)
        {
            return orderManager != null && orderManager.TryCancelPreparation(orderId);
        }

        public bool TryPickupOrder(string orderId, PlayerController player)
        {
            if (player == null || player.AssignedRole != PlayerRole.Delivery ||
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
                sharedPickupCounter.TryRegisterReadyOrder(orderId);
                if (mealbox != null)
                {
                    Destroy(mealbox.gameObject);
                }

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
            if (order == null || order.State != OrderState.InDelivery ||
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
            if (!isOverdue)
            {
                completedDailyDeliveries++;
            }

            MatchStateChanged?.Invoke();
            return true;
        }

        public bool IsUpgradePurchased(UpgradeData upgrade)
        {
            return upgrade != null &&
                   !string.IsNullOrWhiteSpace(upgrade.Id) &&
                   purchasedUpgradeIds.Contains(GetUpgradePurchaseKey(upgrade));
        }

        public bool CanPurchaseUpgrade(PlayerController buyer, UpgradeData upgrade)
        {
            return buyer != null &&
                   upgrade != null &&
                   buyer.AssignedRole == upgrade.Role &&
                   !string.IsNullOrWhiteSpace(upgrade.Id) &&
                   buyer.CanApplyUpgrade(upgrade) &&
                   !IsUpgradePurchased(upgrade) &&
                   balance >= upgrade.Price &&
                   !float.IsNaN(upgrade.Price) &&
                   !float.IsInfinity(upgrade.Price) &&
                   upgrade.Price >= 0f;
        }

        public bool TryPurchaseUpgrade(PlayerController buyer, UpgradeData upgrade)
        {
            if (!CanPurchaseUpgrade(buyer, upgrade) || !buyer.CanApplyUpgrade(upgrade))
            {
                return false;
            }

            float updatedBalance = balance - upgrade.Price;
            if (updatedBalance < 0f || float.IsNaN(updatedBalance) || float.IsInfinity(updatedBalance) ||
                !buyer.TryApplyUpgrade(upgrade))
            {
                return false;
            }

            purchasedUpgradeIds.Add(GetUpgradePurchaseKey(upgrade));
            balance = updatedBalance;
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

        private Transform GetSpawnPointForRole(PlayerRole role)
        {
            if (role == PlayerRole.Cook)
            {
                return cookSpawn;
            }

            return deliverySpawn;
        }

        private static string GetUpgradePurchaseKey(UpgradeData upgrade)
        {
            return $"{upgrade.Role}:{upgrade.Id.Trim()}";
        }
    }
}
