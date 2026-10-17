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
        Expired
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

        internal OrderRecord(OrderData definition, HouseData destination, float deadlineSeconds)
        {
            OrderId = definition.Id.Trim();
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
            Destination = destination;
            DestinationHouseId = destination.Id.Trim();
            State = OrderState.Pending;
        }

        internal OrderRecord(OrderDefinition definition, HouseData destination)
        {
            OrderId = definition.OrderId.Trim();
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
        public string DestinationHouseId { get; }
        public HouseData Destination { get; }
        public float ActivatedAt { get; private set; }
        public float PickedUpAt { get; private set; }
        public OrderState State { get; private set; }

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
            ActivatedAt = currentTime;
            State = OrderState.Available;
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
        [Header("Data-driven catalog")] [SerializeField]
        private MatchConfig matchConfig;

        [Header("Legacy scene catalog (fallback only without MatchConfig)")] [SerializeField, Range(4, 6)]
        private int maximumActiveOrders = 5;

        [SerializeField] private List<OrderDefinition> orderCatalog = new List<OrderDefinition>();

        private readonly List<OrderRecord> activeOrders = new List<OrderRecord>();
        private readonly List<OrderRecord> allOrders = new List<OrderRecord>();
        private ReadOnlyCollection<OrderRecord> activeOrdersView;

        private readonly Dictionary<string, OrderRecord> ordersById =
            new Dictionary<string, OrderRecord>(StringComparer.OrdinalIgnoreCase);

        private int activeOrderLimit = 5;
        private bool initialized;

        public event Action OrdersChanged;
        public IReadOnlyList<OrderRecord> ActiveOrders => activeOrdersView;
        public IReadOnlyList<OrderRecord> AllOrders => allOrders;
        public MatchConfig Config => matchConfig;

        private float CurrentTime => GameManager.Instance != null
            ? GameManager.Instance.ElapsedSeconds
            : Time.time;

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
            if (initialized)
            {
                ExpireDueOrders();
            }
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
            return TryChangeState(orderId, OrderState.Available, OrderState.InPreparation, true);
        }

        public bool TryMarkPickedUp(string orderId)
        {
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

            order.SetState(OrderState.Available);
            OrdersChanged?.Invoke();
            return true;
        }

        public bool TryCompletePreparation(
            string orderId,
            IReadOnlyList<string> addedIngredientIds,
            LunchboxType lunchboxType,
            out string result)
        {
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
            if (counter == null || !counter.CanRegisterReadyOrder(orderId))
            {
                result = "Balcão compartilhado ausente ou indisponível.";
                return false;
            }

            order.SetState(OrderState.ReadyForPickup);
            if (!counter.TryRegisterReadyOrder(orderId))
            {
                order.SetState(OrderState.InPreparation);
                result = "Não foi possível registrar o pedido no balcão.";
                return false;
            }

            result = $"Pedido #{orderId} pronto e colocado no balcão.";
            OrdersChanged?.Invoke();
            return true;
        }

        public bool TryDeliverToHouse(string orderId, HouseData house, PlayerController player)
        {
            if (house == null || string.IsNullOrWhiteSpace(house.Id) ||
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
            ordersById.Clear();
            activeOrderLimit = matchConfig != null
                ? Mathf.Clamp(matchConfig.MaximumActiveOrders, 4, 6)
                : maximumActiveOrders;

            if (matchConfig != null)
            {
                InitializeConfiguredOrders();
            }
            else
            {
                InitializeLegacyOrders();
            }

            ActivatePendingOrders();
            initialized = true;
            if (activeOrders.Count == 0)
            {
                Debug.LogWarning("OrderManager has no valid orders to activate. Check MatchConfig and its catalogs.",
                    this);
            }

            OrdersChanged?.Invoke();
        }

        private void InitializeConfiguredOrders()
        {
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

                HouseData destination = houses[UnityEngine.Random.Range(0, houses.Count)];
                AddOrder(new OrderRecord(data, destination, orderDeadlineSeconds));
            }
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
            HouseDeliveryInteractable[] sceneHouses = FindObjectsByType<HouseDeliveryInteractable>();
            foreach (HouseData house in matchConfig.HouseCatalog)
            {
                if (house == null || string.IsNullOrWhiteSpace(house.Id))
                {
                    Debug.LogError("MatchConfig contains a null HouseData or a house with an empty ID.", matchConfig);
                    continue;
                }

                string id = house.Id.Trim();
                if (!seenHouseIds.Add(id))
                {
                    Debug.LogError($"MatchConfig contains duplicate house ID '{id}'. Duplicate entry ignored.", house);
                    continue;
                }

                bool isDeliverableInScene = false;
                foreach (HouseDeliveryInteractable sceneHouse in sceneHouses)
                {
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

                AddOrder(new OrderRecord(definition, destination));
            }
        }

        private void AddOrder(OrderRecord order)
        {
            if (ordersById.ContainsKey(order.OrderId))
            {
                Debug.LogError($"Order catalog contains duplicate order ID '{order.OrderId}'. Entry ignored.", this);
                return;
            }

            allOrders.Add(order);
            ordersById.Add(order.OrderId, order);
        }

        private void ActivatePendingOrders()
        {
            for (int i = 0; i < allOrders.Count && activeOrders.Count < activeOrderLimit; i++)
            {
                OrderRecord order = allOrders[i];
                if (order.State == OrderState.Pending)
                {
                    order.Activate(CurrentTime);
                    activeOrders.Add(order);
                }
            }
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

            foreach (IngredientData ingredient in data.RequiredIngredients)
            {
                if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.Id))
                {
                    Debug.LogError(
                        $"OrderData '{data.Id}' contains a null ingredient or an ingredient with an empty ID.", data);
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
            HouseDeliveryInteractable[] houses = FindObjectsByType<HouseDeliveryInteractable>();
            foreach (HouseDeliveryInteractable house in houses)
            {
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
            order.SetState(OrderState.Expired);
            if (wasReadyForPickup && GameManager.Instance != null)
            {
                GameManager.Instance.SharedPickupCounter?.RemoveReadyOrder(order.OrderId);
            }

            activeOrders.Remove(order);
            ActivatePendingOrders();
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