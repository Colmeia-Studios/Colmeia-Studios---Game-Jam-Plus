using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public enum LunchboxType
    {
        TwoCompartments,
        ThreeCompartments,
        FourCompartments,
        FiveCompartments
    }

    public enum OrderState
    {
        Pending,
        Available,
        InPreparation,
        ReadyForPickup,
        InDelivery,
        Delivered,
        Expired,
        ReadyForCookPickup
    }

    [Serializable]
    public sealed class OrderDefinition
    {
        [SerializeField] private string orderId;
        [SerializeField] private List<string> requiredIngredients = new List<string>();
        [SerializeField] private LunchboxType lunchboxType;
        [SerializeField, Min(0f)] private float baseValue;
        [SerializeField, Min(1f)] private float deadlineSeconds = 120f;
        [SerializeField] private string destinationHouseId;

        public string OrderId => orderId;
        public IReadOnlyList<string> RequiredIngredients => requiredIngredients;
        public LunchboxType LunchboxType => lunchboxType;
        public float BaseValue => baseValue;
        public float DeadlineSeconds => deadlineSeconds;
        public string DestinationHouseId => destinationHouseId;
    }

    public sealed class OrderRecord
    {
        private readonly ReadOnlyCollection<string> requiredIngredientIds;
        private readonly ReadOnlyCollection<IngredientData> requiredIngredients;

        internal OrderRecord(
            OrderData definition,
            HouseData destination,
            float deadlineSeconds,
            string runtimeId = null,
            int dayNumber = 1)
        {
            OrderId = string.IsNullOrWhiteSpace(runtimeId) ? definition.Id.Trim() : runtimeId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? OrderId
                : definition.DisplayName.Trim();

            var ingredientAssets = new List<IngredientData>(definition.RequiredIngredients);
            requiredIngredients = ingredientAssets.AsReadOnly();
            var ids = new List<string>(ingredientAssets.Count);
            foreach (IngredientData ingredient in ingredientAssets)
            {
                ids.Add(ingredient.Id.Trim());
            }

            requiredIngredientIds = ids.AsReadOnly();
            LunchboxType = definition.LunchboxType;
            BaseValue = definition.CalculateBaseValue();
            DeadlineSeconds = deadlineSeconds;
            DayNumber = Mathf.Max(1, dayNumber);
            Destination = destination;
            DestinationHouseId = destination.Id.Trim();
            State = OrderState.Pending;
        }

        internal OrderRecord(OrderDefinition definition, HouseData destination, string runtimeId = null, int dayNumber = 1)
        {
            OrderId = string.IsNullOrWhiteSpace(runtimeId) ? definition.OrderId.Trim() : runtimeId.Trim();
            DisplayName = OrderId;
            var ids = new List<string>(definition.RequiredIngredients.Count);
            foreach (string id in definition.RequiredIngredients)
            {
                ids.Add(IngredientStation.NormalizeLegacyId(id));
            }

            requiredIngredientIds = ids.AsReadOnly();
            requiredIngredients = new List<IngredientData>().AsReadOnly();
            LunchboxType = definition.LunchboxType;
            BaseValue = definition.BaseValue;
            DeadlineSeconds = definition.DeadlineSeconds;
            DayNumber = Mathf.Max(1, dayNumber);
            Destination = destination;
            DestinationHouseId = destination.Id.Trim();
            State = OrderState.Pending;
        }

        public string OrderId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<string> RequiredIngredientIds => requiredIngredientIds;
        public IReadOnlyList<IngredientData> RequiredIngredients => requiredIngredients;
        public LunchboxType LunchboxType { get; }
        public float BaseValue { get; }
        public float DeadlineSeconds { get; }
        public int DayNumber { get; }
        public string DestinationHouseId { get; }
        public HouseData Destination { get; }
        public float ActivatedAt { get; private set; }
        public float PickedUpAt { get; private set; }
        public OrderState State { get; private set; }
        private bool hasSuspendedDeadline;
        private float suspendedRemainingSeconds;

        public float GetRemainingSeconds(float currentTime)
        {
            if (State == OrderState.Pending || State == OrderState.Delivered || State == OrderState.Expired)
            {
                return 0f;
            }

            return Mathf.Max(0f, DeadlineSeconds - (currentTime - ActivatedAt));
        }

        public bool IsOverdue(float currentTime)
        {
            return State != OrderState.Pending &&
                   currentTime - ActivatedAt >= DeadlineSeconds;
        }

        internal void Activate(float currentTime)
        {
            if (hasSuspendedDeadline)
            {
                ActivatedAt = currentTime - (DeadlineSeconds - suspendedRemainingSeconds);
                hasSuspendedDeadline = false;
            }
            else
            {
                ActivatedAt = currentTime;
            }

            State = OrderState.Available;
        }

        internal void SuspendAsPending(float currentTime)
        {
            suspendedRemainingSeconds = GetRemainingSeconds(currentTime);
            hasSuspendedDeadline = true;
            State = OrderState.Pending;
        }

        internal void MarkPickedUp(float currentTime)
        {
            PickedUpAt = currentTime;
            State = OrderState.InDelivery;
        }

        internal void SetState(OrderState state)
        {
            State = state;
        }
    }

    public sealed class OrderManager : MonoBehaviour
    {
        private const int MaximumWaitingOrders = 4;
        private const int MaximumOrdersInPreparation = 1;

        private sealed class LegacyTemplate
        {
            public OrderDefinition Definition;
            public HouseData Destination;
        }

        [Header("Data-driven catalog")] [SerializeField]
        private MatchConfig matchConfig;

        [Header("Legacy scene catalog (fallback only without MatchConfig)")]
        [Tooltip("Retido para compatibilidade. A capacidade da fila é fixa: quatro aguardando e um em preparação.")]
        [SerializeField, Range(4, 6)]
        private int maximumActiveOrders = 5;

        [SerializeField] private List<OrderDefinition> orderCatalog = new List<OrderDefinition>();

        private readonly List<OrderRecord> activeOrders = new List<OrderRecord>();
        private readonly List<OrderRecord> allOrders = new List<OrderRecord>();
        private readonly Queue<OrderRecord> pendingOrders = new Queue<OrderRecord>();
        private ReadOnlyCollection<OrderRecord> activeOrdersView;

        private readonly Dictionary<string, OrderRecord> ordersById =
            new Dictionary<string, OrderRecord>(StringComparer.OrdinalIgnoreCase);

        private readonly List<OrderData> validOrderCatalog = new List<OrderData>();
        private readonly List<HouseData> validHouses = new List<HouseData>();
        private readonly List<LegacyTemplate> validLegacyCatalog = new List<LegacyTemplate>();
        private IngredientStation[] sceneIngredientStations = Array.Empty<IngredientStation>();
        private HouseDeliveryInteractable[] sceneHouseInteractables = Array.Empty<HouseDeliveryInteractable>();
        private float nextGenerationAt;
        private int generatedOrderSequence;
        private int lastDayWithoutRecipesWarning = -1;
        private bool noEligibleOrdersForDay;
        private bool progressionRulesValid;
        private bool initialized;

        public event Action OrdersChanged;
        public IReadOnlyList<OrderRecord> ActiveOrders => activeOrdersView;
        public IReadOnlyList<OrderRecord> AllOrders => allOrders;
        public MatchConfig Config => matchConfig;

        private float CurrentTime => GameManager.Instance != null
            ? GameManager.Instance.ElapsedSeconds
            : Time.time;

        public int WaitingOrderCount => CountState(OrderState.Available);
        public bool HasOrderInPreparation => CountState(OrderState.InPreparation) > 0;
        public IReadOnlyList<OrderRecord> OrderHistory => allOrders;

        public void GetDayOrderCounts(int day, out int delivered, out int expired)
        {
            delivered = 0;
            expired = 0;
            foreach (OrderRecord order in allOrders)
            {
                if (order.DayNumber != day)
                    continue;

                if (order.State == OrderState.Delivered) delivered++;
                else if (order.State == OrderState.Expired) expired++;
            }
        }

        private void Awake()
        {
            activeOrdersView = activeOrders.AsReadOnly();
            if (matchConfig == null)
            {
                GameManager manager = GetComponent<GameManager>();
                if (manager != null)
                {
                    matchConfig = manager.MatchConfiguration;
                }
            }
        }

        private void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.MatchConfiguration != null)
            {
                if (matchConfig != null && matchConfig != GameManager.Instance.MatchConfiguration)
                {
                    Debug.LogError(
                        "GameManager and OrderManager reference different MatchConfig assets. Orders were not initialized.",
                        this);
                    enabled = false;
                    return;
                }

                matchConfig = GameManager.Instance.MatchConfiguration;
            }

            InitializeOrders();
        }

        private void Update()
        {
            if (!initialized || (GameManager.Instance != null && !GameManager.Instance.IsWorkDayActive))
                return;

            ExpireDueOrders();
            if (ActivatePendingOrders())
                OrdersChanged?.Invoke();
            if (noEligibleOrdersForDay || CurrentTime < nextGenerationAt ||
                WaitingOrderCount >= MaximumWaitingOrders)
            {
                return;
            }

            GenerateOneOrder();
            float interval = matchConfig != null ? matchConfig.OrderGenerationIntervalSeconds : 8f;
            nextGenerationAt = CurrentTime + Mathf.Max(0.1f, interval);
        }

        private void OnValidate()
        {
            maximumActiveOrders = Mathf.Clamp(maximumActiveOrders, 4, 6);
        }

        public bool TryGetOrder(string orderId, out OrderRecord order)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                order = null;
                return false;
            }

            return ordersById.TryGetValue(orderId.Trim(), out order);
        }

        public float GetRemainingSeconds(string orderId)
        {
            return TryGetOrder(orderId, out OrderRecord order)
                ? order.GetRemainingSeconds(CurrentTime)
                : 0f;
        }

        public bool TryStartNextPreparation(out OrderRecord order)
        {
            order = null;
            for (int i = 0; i < activeOrders.Count;)
            {
                OrderRecord candidate = activeOrders[i];
                if (candidate.State == OrderState.Available && TryStartPreparation(candidate.OrderId))
                {
                    order = candidate;
                    return true;
                }

                if (candidate.State != OrderState.Expired)
                {
                    i++;
                }
            }

            return false;
        }

        public bool TryStartPreparation(string orderId)
        {
            if (CountState(OrderState.InPreparation) >= MaximumOrdersInPreparation ||
                (GameManager.Instance != null && !GameManager.Instance.IsWorkDayActive))
            {
                return false;
            }

            return TryChangeState(orderId, OrderState.Available, OrderState.InPreparation, true);
        }

        public bool TryMarkPickedUp(string orderId)
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsWorkDayActive)
                return false;

            if (!TryGetOrder(orderId, out OrderRecord order) ||
                order.State != OrderState.ReadyForPickup)
            {
                return false;
            }

            if (order.GetRemainingSeconds(CurrentTime) <= 0f)
            {
                Expire(order);
                return false;
            }

            order.MarkPickedUp(CurrentTime);
            OrdersChanged?.Invoke();
            return true;
        }

        private bool TryMarkDelivered(string orderId)
        {
            if (!TryGetOrder(orderId, out OrderRecord order) ||
                order.State != OrderState.InDelivery ||
                GameManager.Instance == null ||
                !GameManager.Instance.TryProcessOrderPayment(order))
            {
                return false;
            }

            order.SetState(OrderState.Delivered);
            activeOrders.Remove(order);
            ActivatePendingOrders();
            OrdersChanged?.Invoke();
            return true;
        }

        public bool TryCancelPreparation(string orderId)
        {
            if (!TryGetOrder(orderId, out OrderRecord order) ||
                order.State != OrderState.InPreparation)
            {
                return false;
            }

            if (order.GetRemainingSeconds(CurrentTime) <= 0f)
            {
                Expire(order);
                return false;
            }

            // A cancelled preparation returns to the waiting queue. If the
            // four visible waiting places are full, keep it pending until a
            // place opens instead of exceeding the queue limit.
            order.SuspendAsPending(CurrentTime);
            activeOrders.Remove(order);
            pendingOrders.Enqueue(order);
            ActivatePendingOrders();
            OrdersChanged?.Invoke();
            return true;
        }

        public bool TryCompletePreparation(
            string orderId,
            IReadOnlyList<string> addedIngredientIds,
            LunchboxType lunchboxType,
            PlayerController cook,
            out string result)
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsWorkDayActive)
            {
                result = "O expediente foi encerrado.";
                return false;
            }

            if (!TryGetOrder(orderId, out OrderRecord order) ||
                order.State != OrderState.InPreparation)
            {
                result = "O pedido não está em preparação.";
                return false;
            }

            if (order.GetRemainingSeconds(CurrentTime) <= 0f)
            {
                Expire(order);
                result = "O prazo do pedido expirou antes de ficar pronto.";
                return false;
            }

            if (lunchboxType != order.LunchboxType)
            {
                result = "Tipo de marmita incorreto.";
                return false;
            }

            if (addedIngredientIds == null ||
                !IngredientsMatch(order.RequiredIngredientIds, addedIngredientIds))
            {
                result = "Ingredientes incorretos: confira ausentes, extras e duplicados.";
                return false;
            }

            PickupCounter counter = GameManager.Instance != null
                ? GameManager.Instance.SharedPickupCounter
                : null;
            if (cook == null || cook.AssignedRole != PlayerRole.Cook || counter == null ||
                !counter.CanCreateCookCarriedMealbox(orderId, cook))
            {
                result = "Não foi possível disponibilizar a marmita para o Cook. Verifique o balcão e o ponto de transporte.";
                return false;
            }

            if (!counter.TryCreateCookCarriedMealbox(orderId, cook, out MealboxPhysical mealbox))
            {
                result = "Não foi possível criar a marmita física para transporte pelo Cook.";
                return false;
            }

            order.SetState(OrderState.ReadyForCookPickup);
            if (!cook.TryCarryPreparedMealbox(mealbox))
            {
                order.SetState(OrderState.InPreparation);
                Destroy(mealbox.gameObject);
                result = "Não foi possível vincular a marmita concluída ao Cook.";
                return false;
            }

            result = $"Pedido #{orderId} pronto. Leve a marmita até o balcão.";
            ActivatePendingOrders();
            OrdersChanged?.Invoke();
            return true;
        }

        public bool TryMarkDepositedForPickup(string orderId)
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsWorkDayActive)
                return false;

            if (!TryGetOrder(orderId, out OrderRecord order) ||
                order.State != OrderState.ReadyForCookPickup)
                return false;

            if (order.GetRemainingSeconds(CurrentTime) <= 0f)
            {
                Expire(order);
                return false;
            }

            order.SetState(OrderState.ReadyForPickup);
            return true;
        }

        internal void NotifyOrdersChanged()
        {
            OrdersChanged?.Invoke();
        }

        public bool TryDeliverToHouse(string orderId, HouseData house, PlayerController player)
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsWorkDayActive ||
                house == null || string.IsNullOrWhiteSpace(house.Id) ||
                player == null || player.AssignedRole != PlayerRole.Delivery ||
                !player.HasOrderInInventory(orderId) ||
                !AreIdsEqual(player.SelectedDeliveryOrderId, orderId) ||
                !TryGetOrder(orderId, out OrderRecord order) ||
                order.State != OrderState.InDelivery ||
                !AreIdsEqual(order.DestinationHouseId, house.Id))
            {
                return false;
            }

            if (!TryMarkDelivered(orderId))
            {
                return false;
            }

            player.RemoveOrderFromInventory(orderId);
            GameManager.Instance.NotifyMatchStateChanged();
            return true;
        }

        public bool TryExpireOrder(string orderId)
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsWorkDayActive)
                return false;

            if (!TryGetOrder(orderId, out OrderRecord order) ||
                order.State == OrderState.Pending ||
                order.State == OrderState.Delivered ||
                order.State == OrderState.Expired ||
                order.State == OrderState.InDelivery ||
                order.GetRemainingSeconds(CurrentTime) > 0f)
            {
                return false;
            }

            Expire(order);
            return true;
        }

        private void InitializeOrders()
        {
            activeOrders.Clear();
            allOrders.Clear();
            pendingOrders.Clear();
            ordersById.Clear();
            validOrderCatalog.Clear();
            validHouses.Clear();
            validLegacyCatalog.Clear();
            generatedOrderSequence = 0;
            sceneIngredientStations = FindObjectsByType<IngredientStation>();
            sceneHouseInteractables = FindObjectsByType<HouseDeliveryInteractable>();

            if (matchConfig != null)
            {
                InitializeConfiguredOrders();
            }
            else
            {
                InitializeLegacyOrders();
            }

            initialized = true;
            nextGenerationAt = CurrentTime;
            if (validOrderCatalog.Count == 0 && validLegacyCatalog.Count == 0)
            {
                Debug.LogWarning("OrderManager has no valid order templates. Check the active catalog and its houses/ingredients.", this);
            }

            OrdersChanged?.Invoke();
        }

        private void InitializeConfiguredOrders()
        {
            ValidateProgressionRules();
            if (!progressionRulesValid)
                return;

            List<HouseData> houses = GetValidConfiguredHouses();
            if (houses.Count == 0)
            {
                Debug.LogError(
                    "MatchConfig has no valid HouseData in its house catalog; no orders can be activated.",
                    matchConfig);
                return;
            }

            if (matchConfig.OrderCatalog == null)
            {
                Debug.LogError("MatchConfig order catalog is null.", matchConfig);
                return;
            }

            float orderDeadlineSeconds = matchConfig.OrderDeadlineSeconds;
            if (float.IsNaN(orderDeadlineSeconds) ||
                float.IsInfinity(orderDeadlineSeconds) ||
                orderDeadlineSeconds <= 0f)
            {
                Debug.LogError("MatchConfig order deadline must be a finite positive number.", matchConfig);
                return;
            }

            var seenOrderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (OrderData data in matchConfig.OrderCatalog)
            {
                if (!IsOrderDataValid(data))
                {
                    continue;
                }

                string id = data.Id.Trim();
                if (!seenOrderIds.Add(id))
                {
                    Debug.LogError($"MatchConfig contains duplicate order ID '{id}'. Duplicate entry ignored.", data);
                    continue;
                }

                validOrderCatalog.Add(data);
            }

            foreach (OrderData data in validOrderCatalog)
            {
                if (!HasLunchboxUnlockRule(data.LunchboxType))
                {
                    Debug.LogError($"OrderData '{data.Id}' uses lunchbox '{data.LunchboxType}' without an unlock rule in MatchConfig.", data);
                }
            }

            validHouses.AddRange(houses);
        }

        private List<HouseData> GetValidConfiguredHouses()
        {
            var validHouses = new List<HouseData>();
            if (matchConfig.HouseCatalog == null)
            {
                Debug.LogError("MatchConfig house catalog is null.", matchConfig);
                return validHouses;
            }

            var seenHouseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HouseData house in matchConfig.HouseCatalog)
            {
                if (house == null || string.IsNullOrWhiteSpace(house.Id) ||
                    string.IsNullOrWhiteSpace(house.CustomerName))
                {
                    Debug.LogError("MatchConfig contains a null HouseData or a house without an ID/customer name.", matchConfig);
                    continue;
                }

                string id = house.Id.Trim();
                if (!seenHouseIds.Add(id))
                {
                    Debug.LogError($"MatchConfig contains duplicate house ID '{id}'. Duplicate entry ignored.", house);
                    continue;
                }

                bool isDeliverableInScene = false;
                foreach (HouseDeliveryInteractable sceneHouse in sceneHouseInteractables)
                {
                    if (sceneHouse == null)
                        continue;

                    Collider doorCollider = sceneHouse.InteractionCollider;
                    if (sceneHouse.Data != null &&
                        AreIdsEqual(sceneHouse.Data.Id, id) &&
                        doorCollider != null &&
                        doorCollider.enabled &&
                        doorCollider.gameObject.activeInHierarchy)
                    {
                        isDeliverableInScene = true;
                        break;
                    }
                }

                if (!isDeliverableInScene)
                {
                    Debug.LogError(
                        $"HouseData '{id}' has no active HouseDeliveryInteractable with an enabled door collider in the scene.",
                        house);
                    continue;
                }

                validHouses.Add(house);
            }

            return validHouses;
        }

        private void InitializeLegacyOrders()
        {
            foreach (OrderDefinition definition in orderCatalog)
            {
                if (!IsLegacyDefinitionValid(definition))
                {
                    continue;
                }

                HouseData destination = FindHouseData(definition.DestinationHouseId);
                if (destination == null)
                {
                    Debug.LogError(
                        $"Legacy order '{definition.OrderId}' refers to house '{definition.DestinationHouseId}', " +
                        "but no matching HouseDeliveryInteractable with HouseData exists.",
                        this);
                    continue;
                }

                if (definition.RequiredIngredients.Count > GetCompartmentCount(definition.LunchboxType) ||
                    !HasLegacyIngredientStations(definition.RequiredIngredients))
                {
                    Debug.LogError(
                        $"Legacy order '{definition.OrderId}' exceeds its lunchbox compartments or requires an ingredient without an active station.",
                        this);
                    continue;
                }

                validLegacyCatalog.Add(new LegacyTemplate { Definition = definition, Destination = destination });
            }
        }

        private bool AddOrder(OrderRecord order)
        {
            if (ordersById.ContainsKey(order.OrderId))
            {
                Debug.LogError($"Order catalog contains duplicate order ID '{order.OrderId}'. Entry ignored.", this);
                return false;
            }

            allOrders.Add(order);
            ordersById.Add(order.OrderId, order);
            return true;
        }

        public void BeginDay()
        {
            nextGenerationAt = CurrentTime;
            lastDayWithoutRecipesWarning = -1;
            noEligibleOrdersForDay = false;
        }

        public void EndDay()
        {
            var expiredReadyIds = new List<string>();
            var expiredCookCarriedIds = new List<string>();
            foreach (OrderRecord order in allOrders)
            {
                if (order.State == OrderState.Pending ||
                    order.State == OrderState.Available ||
                    order.State == OrderState.InPreparation ||
                    order.State == OrderState.ReadyForCookPickup ||
                    order.State == OrderState.ReadyForPickup ||
                    order.State == OrderState.InDelivery)
                {
                    if (order.State == OrderState.ReadyForPickup)
                        expiredReadyIds.Add(order.OrderId);
                    else if (order.State == OrderState.ReadyForCookPickup)
                        expiredCookCarriedIds.Add(order.OrderId);
                    order.SetState(OrderState.Expired);
                }
            }

            foreach (string orderId in expiredReadyIds)
                GameManager.Instance?.SharedPickupCounter?.RemoveReadyOrder(orderId);
            foreach (string orderId in expiredCookCarriedIds)
                GameManager.Instance?.RemovePreparedMealbox(orderId);

            activeOrders.Clear();
            pendingOrders.Clear();
            OrdersChanged?.Invoke();
        }

        private int CountState(OrderState state)
        {
            int count = 0;
            foreach (OrderRecord order in activeOrders)
            {
                if (order.State == state)
                    count++;
            }

            return count;
        }

        private void GenerateOneOrder()
        {
            if (WaitingOrderCount >= MaximumWaitingOrders)
                return;

            MatchConfig config = matchConfig;
            int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            OrderRecord generatedOrder = null;
            if (config != null)
            {
                var eligible = new List<OrderData>();
                foreach (OrderData data in validOrderCatalog)
                {
                    if (IsOrderAvailableForDay(data, day))
                        eligible.Add(data);
                }

                if (eligible.Count > 0 && validHouses.Count > 0)
                {
                    OrderData data = eligible[UnityEngine.Random.Range(0, eligible.Count)];
                    HouseData destination = validHouses[UnityEngine.Random.Range(0, validHouses.Count)];
                    float deadline = config.OrderDeadlineSeconds;
                    string generatedId = $"D{day:00}-{++generatedOrderSequence:000}-{data.Id.Trim()}";
                    generatedOrder = new OrderRecord(data, destination, deadline, generatedId, day);
                }
            }
            else if (validLegacyCatalog.Count > 0)
            {
                LegacyTemplate template = validLegacyCatalog[UnityEngine.Random.Range(0, validLegacyCatalog.Count)];
                string generatedId = $"D{day:00}-{++generatedOrderSequence:000}-{template.Definition.OrderId.Trim()}";
                generatedOrder = new OrderRecord(template.Definition, template.Destination, generatedId, day);
            }

            if (generatedOrder == null)
            {
                noEligibleOrdersForDay = true;
                if (lastDayWithoutRecipesWarning != day)
                {
                    Debug.LogWarning(
                        $"Nenhum pedido válido está disponível no catálogo para o dia {day}. Revise OrderCatalog, desbloqueios, ingredientes/estações e casas no MatchConfig.",
                        config != null ? config : this);
                    lastDayWithoutRecipesWarning = day;
                }

                return;
            }

            generatedOrder.Activate(CurrentTime);
            if (!AddOrder(generatedOrder))
                return;

            activeOrders.Add(generatedOrder);
            OrdersChanged?.Invoke();
        }

        private bool IsOrderAvailableForDay(OrderData data, int day)
        {
            if (data == null || matchConfig == null || !matchConfig.IsLunchboxUnlocked(data.LunchboxType, day))
                return false;

            if (data.RequiredIngredients.Count > GetCompartmentCount(data.LunchboxType))
                return false;

            foreach (IngredientData ingredient in data.RequiredIngredients)
            {
                if (!matchConfig.IsIngredientUnlocked(ingredient, day) || !HasUsableIngredientStation(ingredient))
                    return false;
            }

            return true;
        }

        private static int GetCompartmentCount(LunchboxType type)
        {
            return Enum.IsDefined(typeof(LunchboxType), type) ? (int)type + 2 : 0;
        }

        private bool HasUsableIngredientStation(IngredientData ingredient)
        {
            foreach (IngredientStation station in sceneIngredientStations)
            {
                if (station != null && station.isActiveAndEnabled && station.gameObject.activeInHierarchy &&
                    station.Data == ingredient)
                {
                    return true;
                }
            }

            return false;
        }

        private void ScheduleGenerationIfQueueHasRoom()
        {
            if (WaitingOrderCount < MaximumWaitingOrders)
                nextGenerationAt = Mathf.Min(nextGenerationAt, CurrentTime);
        }

        private bool ActivatePendingOrders()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsWorkDayActive)
                return false;

            bool activated = false;
            while (pendingOrders.Count > 0 && WaitingOrderCount < MaximumWaitingOrders)
            {
                OrderRecord order = pendingOrders.Dequeue();
                if (order == null || order.State != OrderState.Pending)
                    continue;

                order.Activate(CurrentTime);
                activeOrders.Add(order);
                activated = true;
            }

            if (activated)
                ScheduleGenerationIfQueueHasRoom();

            return activated;
        }

        private bool IsOrderDataValid(OrderData data)
        {
            if (data == null)
            {
                Debug.LogError("MatchConfig order catalog contains a null OrderData reference.", matchConfig);
                return false;
            }

            if (string.IsNullOrWhiteSpace(data.Id) ||
                data.RequiredIngredients == null ||
                data.RequiredIngredients.Count == 0 ||
                string.IsNullOrWhiteSpace(data.DisplayName) ||
                !Enum.IsDefined(typeof(LunchboxType), data.LunchboxType))
            {
                Debug.LogError($"OrderData '{data.name}' has a missing ID, name, ingredients, or valid lunchbox type.",
                    data);
                return false;
            }

            if (string.IsNullOrWhiteSpace(data.Id) || !HasLunchboxUnlockRule(data.LunchboxType) ||
                data.RequiredIngredients.Count > GetCompartmentCount(data.LunchboxType))
            {
                Debug.LogError(
                    $"OrderData '{data.name}' has an invalid ID, an unconfigured lunchbox unlock, or more ingredients than compartments.",
                    data);
                return false;
            }

            var ingredientIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IngredientData ingredient in data.RequiredIngredients)
            {
                if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.Id))
                {
                    Debug.LogError(
                        $"OrderData '{data.Id}' contains a null ingredient or an ingredient with an empty ID.", data);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(ingredient.DisplayName))
                {
                    Debug.LogError($"IngredientData '{ingredient.name}' has no display name.", ingredient);
                    return false;
                }

                if (!ingredientIds.Add(ingredient.Id.Trim()))
                {
                    Debug.LogError($"OrderData '{data.Id}' repeats ingredient ID '{ingredient.Id}'.", data);
                    return false;
                }

                if (!HasIngredientUnlockRule(ingredient))
                {
                    Debug.LogError(
                        $"MatchConfig has no unlock rule for ingredient '{ingredient.Id}' required by order '{data.Id}'.",
                        matchConfig);
                    return false;
                }

                if (!HasUsableIngredientStation(ingredient))
                {
                    Debug.LogError(
                        $"OrderData '{data.Id}' requires ingredient '{ingredient.Id}', but the scene has no active IngredientStation using that asset.",
                        data);
                    return false;
                }

                if (float.IsNaN(ingredient.Value) || float.IsInfinity(ingredient.Value) || ingredient.Value < 0f)
                {
                    Debug.LogError(
                        $"Ingredient '{ingredient.Id.Trim()}' has an invalid value and cannot be used in order '{data.Id}'.",
                        ingredient);
                    return false;
                }
            }

            float baseValue = data.CalculateBaseValue();
            if (float.IsNaN(baseValue) || float.IsInfinity(baseValue))
            {
                Debug.LogError($"OrderData '{data.Id}' has an invalid calculated base value.", data);
                return false;
            }

            return true;
        }

        private bool HasIngredientUnlockRule(IngredientData ingredient)
        {
            if (matchConfig.IngredientUnlocks == null)
                return false;

            int matches = 0;
            foreach (IngredientUnlockRule rule in matchConfig.IngredientUnlocks)
            {
                if (rule != null && rule.Ingredient == ingredient && rule.AvailableFromDay >= 1)
                    matches++;
            }

            return matches == 1;
        }

        private bool HasLunchboxUnlockRule(LunchboxType type)
        {
            if (matchConfig.LunchboxUnlocks == null)
                return false;

            int matches = 0;
            foreach (LunchboxUnlockRule rule in matchConfig.LunchboxUnlocks)
            {
                if (rule != null && rule.LunchboxType == type && rule.AvailableFromDay >= 1)
                    matches++;
            }

            return matches == 1;
        }

        private bool HasLegacyIngredientStations(IReadOnlyList<string> ingredientIds)
        {
            foreach (string rawId in ingredientIds)
            {
                string expectedId = IngredientStation.NormalizeLegacyId(rawId);
                bool hasStation = false;
                foreach (IngredientStation station in sceneIngredientStations)
                {
                    if (station != null && station.isActiveAndEnabled && station.gameObject.activeInHierarchy &&
                        AreIdsEqual(IngredientStation.NormalizeLegacyId(station.IngredientId), expectedId))
                    {
                        hasStation = true;
                        break;
                    }
                }

                if (!hasStation)
                    return false;
            }

            return true;
        }

        private void ValidateProgressionRules()
        {
            progressionRulesValid = true;
            if (matchConfig.IngredientUnlocks == null || matchConfig.LunchboxUnlocks == null)
            {
                Debug.LogError("MatchConfig progression lists are missing; configure ingredient and lunchbox unlock rules.", matchConfig);
                progressionRulesValid = false;
                return;
            }

            var ingredientRules = new HashSet<IngredientData>();
            var ingredientIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IngredientUnlockRule rule in matchConfig.IngredientUnlocks)
            {
                if (rule == null || rule.Ingredient == null || string.IsNullOrWhiteSpace(rule.Ingredient.Id) ||
                    rule.AvailableFromDay < 1)
                {
                    Debug.LogError("MatchConfig has an ingredient unlock rule with a missing asset or invalid day.", matchConfig);
                    progressionRulesValid = false;
                    continue;
                }

                if (!ingredientRules.Add(rule.Ingredient))
                {
                    Debug.LogError($"MatchConfig repeats the unlock rule for ingredient '{rule.Ingredient.Id}'.", matchConfig);
                    progressionRulesValid = false;
                }
                if (!string.IsNullOrWhiteSpace(rule.Ingredient.Id) && !ingredientIds.Add(rule.Ingredient.Id.Trim()))
                {
                    Debug.LogError($"MatchConfig ingredient unlocks contain duplicate ID '{rule.Ingredient.Id}'.", matchConfig);
                    progressionRulesValid = false;
                }
            }

            var lunchboxRules = new HashSet<LunchboxType>();
            foreach (LunchboxUnlockRule rule in matchConfig.LunchboxUnlocks)
            {
                if (rule == null || rule.AvailableFromDay < 1 ||
                    !Enum.IsDefined(typeof(LunchboxType), rule.LunchboxType))
                {
                    Debug.LogError("MatchConfig has a lunchbox unlock rule with an invalid type or day.", matchConfig);
                    progressionRulesValid = false;
                    continue;
                }

                if (!lunchboxRules.Add(rule.LunchboxType))
                {
                    Debug.LogError($"MatchConfig repeats the unlock rule for lunchbox '{rule.LunchboxType}'.", matchConfig);
                    progressionRulesValid = false;
                }
            }
        }

        private static bool IsLegacyDefinitionValid(OrderDefinition definition)
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.OrderId) ||
                definition.RequiredIngredients == null ||
                definition.RequiredIngredients.Count == 0 ||
                string.IsNullOrWhiteSpace(definition.DestinationHouseId) ||
                float.IsNaN(definition.BaseValue) ||
                float.IsInfinity(definition.BaseValue) ||
                definition.BaseValue < 0f ||
                float.IsNaN(definition.DeadlineSeconds) ||
                float.IsInfinity(definition.DeadlineSeconds) ||
                definition.DeadlineSeconds <= 0f ||
                !Enum.IsDefined(typeof(LunchboxType), definition.LunchboxType))
            {
                Debug.LogError("Legacy order catalog contains an incomplete or invalid entry; entry ignored.");
                return false;
            }

            return true;
        }

        private HouseData FindHouseData(string houseId)
        {
            foreach (HouseDeliveryInteractable house in sceneHouseInteractables)
            {
                if (house == null)
                    continue;

                Collider doorCollider = house.InteractionCollider;
                if (house.Data != null &&
                    AreIdsEqual(house.Data.Id, houseId) &&
                    doorCollider != null &&
                    doorCollider.enabled &&
                    doorCollider.gameObject.activeInHierarchy)
                {
                    return house.Data;
                }
            }

            return null;
        }

        private static bool IngredientsMatch(
            IReadOnlyList<string> required,
            IReadOnlyList<string> added)
        {
            if (required == null || added == null || required.Count != added.Count)
            {
                return false;
            }

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in required)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return false;
                }

                string normalizedId = id.Trim();
                counts.TryGetValue(normalizedId, out int count);
                counts[normalizedId] = count + 1;
            }

            foreach (string id in added)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return false;
                }

                string normalizedId = id.Trim();
                if (!counts.TryGetValue(normalizedId, out int count) || count == 0)
                {
                    return false;
                }

                counts[normalizedId] = count - 1;
            }

            foreach (int count in counts.Values)
            {
                if (count != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryChangeState(
            string orderId,
            OrderState requiredState,
            OrderState nextState,
            bool expireIfLate)
        {
            if (!TryGetOrder(orderId, out OrderRecord order) || order.State != requiredState)
            {
                return false;
            }

            if (expireIfLate && order.GetRemainingSeconds(CurrentTime) <= 0f)
            {
                Expire(order);
                return false;
            }

            order.SetState(nextState);
            if (nextState == OrderState.InPreparation)
                ScheduleGenerationIfQueueHasRoom();
            OrdersChanged?.Invoke();
            return true;
        }

        private void ExpireDueOrders()
        {
            for (int i = activeOrders.Count - 1; i >= 0; i--)
            {
                OrderRecord order = activeOrders[i];
                if (order.State != OrderState.InDelivery &&
                    order.State != OrderState.Delivered &&
                    order.State != OrderState.Expired &&
                    order.GetRemainingSeconds(CurrentTime) <= 0f)
                {
                    Expire(order);
                }
            }
        }

        private void Expire(OrderRecord order)
        {
            if (order.State == OrderState.Pending ||
                order.State == OrderState.Delivered ||
                order.State == OrderState.Expired ||
                order.State == OrderState.InDelivery)
            {
                return;
            }

            bool wasReadyForPickup = order.State == OrderState.ReadyForPickup;
            bool wasCarriedByCook = order.State == OrderState.ReadyForCookPickup;
            order.SetState(OrderState.Expired);
            if (wasReadyForPickup && GameManager.Instance != null)
            {
                GameManager.Instance.SharedPickupCounter?.RemoveReadyOrder(order.OrderId);
            }

            if (wasCarriedByCook)
            {
                GameManager.Instance?.RemovePreparedMealbox(order.OrderId);
            }

            activeOrders.Remove(order);
            ActivatePendingOrders();
            ScheduleGenerationIfQueueHasRoom();
            OrdersChanged?.Invoke();
        }

        private static bool AreIdsEqual(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) &&
                   !string.IsNullOrWhiteSpace(second) &&
                   string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
