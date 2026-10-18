using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class PickupCounter : Interactable
    {
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private LunchboxPrefabCatalog prefabCatalog;
        [SerializeField] private Transform displayPoint;

        private readonly List<string> readyOrderIds = new List<string>();
        private readonly HashSet<string> queuedOrderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, MealboxPhysical> readyMealboxes =
            new Dictionary<string, MealboxPhysical>(StringComparer.OrdinalIgnoreCase);

        public int ReadyOrderCount
        {
            get
            {
                PruneInvalidOrders();
                return readyOrderIds.Count;
            }
        }

        public bool CanRegisterReadyOrder(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId) ||
                !isActiveAndEnabled ||
                !gameObject.activeInHierarchy ||
                orderManager == null)
            {
                return false;
            }

            string normalizedOrderId = orderId.Trim();
            return !queuedOrderIds.Contains(normalizedOrderId) && !readyMealboxes.ContainsKey(normalizedOrderId);
        }

        private void Awake()
        {
            if (orderManager == null && GameManager.Instance != null)
            {
                orderManager = GameManager.Instance.Orders;
            }

            if (orderManager == null)
            {
                orderManager = FindAnyObjectByType<OrderManager>();
            }

            if (orderManager == null)
            {
                Debug.LogError("PickupCounter requires an OrderManager reference.", this);
            }

            if (prefabCatalog == null)
                Debug.LogError("PickupCounter requires a LunchboxPrefabCatalog with both closed mealbox prefabs.", this);

            if (displayPoint == null)
            {
                Debug.LogWarning(
                    "PickupCounter has no display point; completed mealboxes will be laid out above this object's transform. Assign a display point to customize their placement.",
                    this);
            }
        }

        public override bool CanInteract(PlayerController player)
        {
            PruneInvalidOrders();
            if (!base.CanInteract(player) || player == null)
                return false;

            if (player.AssignedRole == PlayerRole.Cook)
                return player.CarriedPreparedMealbox != null;

            return player.AssignedRole == PlayerRole.Delivery && readyOrderIds.Count > 0;
        }

        public override string GetInteractionText()
        {
            PlayerController cookPlayer = GetPlayerForRole(PlayerRole.Cook);
            if (cookPlayer != null && cookPlayer.CarriedPreparedMealbox != null)
                return "Interagir para depositar a marmita pronta.";

            PlayerController deliveryPlayer = GameManager.Instance != null
                ? GameManager.Instance.PlayerOne != null &&
                  GameManager.Instance.PlayerOne.AssignedRole == PlayerRole.Delivery
                    ? GameManager.Instance.PlayerOne
                    : GameManager.Instance.PlayerTwo
                : null;
            if (deliveryPlayer != null && deliveryPlayer.CarriedOrderIds.Count >= deliveryPlayer.InventoryCapacity)
            {
                return "Mochila cheia: limite de três marmitas.";
            }

            return ReadyOrderCount == 1
                ? "Interagir para retirar a marmita."
                : $"Interagir para retirar uma marmita ({ReadyOrderCount} disponíveis).";
        }

        public override void Interact(PlayerController player)
        {
            if (player == null || !CanInteract(player) || orderManager == null)
            {
                return;
            }

            if (player.AssignedRole == PlayerRole.Cook)
            {
                if (GameManager.Instance != null &&
                    GameManager.Instance.TryDepositPreparedMealbox(player, out string depositResult))
                {
                    player.ShowGameplayMessage(depositResult);
                }
                else
                {
                    player.ShowGameplayMessage("Não foi possível depositar a marmita no balcão.");
                }

                return;
            }

            if (player.CarriedOrderIds.Count >= player.InventoryCapacity)
            {
                player.ShowGameplayMessage("Mochila cheia: limite de três marmitas.");
                return;
            }

            while (readyOrderIds.Count > 0)
            {
                string orderId = !string.IsNullOrWhiteSpace(player.SelectedPickupOrderId) &&
                                 queuedOrderIds.Contains(player.SelectedPickupOrderId.Trim())
                    ? player.SelectedPickupOrderId.Trim()
                    : readyOrderIds[0];
                if (!orderManager.TryGetOrder(orderId, out OrderRecord order) ||
                    order.State != OrderState.ReadyForPickup)
                {
                    RemoveQueuedOrder(orderId);
                    continue;
                }

                if (GameManager.Instance == null)
                {
                    return;
                }

                if (GameManager.Instance.TryPickupOrder(orderId, player))
                {
                    player.ShowGameplayMessage($"Pedido #{orderId} guardado na mochila.");
                }
                else
                {
                    player.ShowGameplayMessage("Não foi possível retirar essa marmita.");
                }

                return;
            }
        }

        public bool TryRegisterReadyOrder(string orderId)
        {
            Debug.LogError($"PickupCounter cannot register order '{orderId}' without the Cook's physical mealbox. Use TryRegisterReadyOrder(orderId, mealbox).", this);
            return false;
        }

        public bool TryRegisterReadyOrder(string orderId, MealboxPhysical mealbox)
        {
            if (mealbox == null || string.IsNullOrWhiteSpace(orderId) || orderManager == null ||
                !CanRegisterReadyOrder(orderId) ||
                !orderManager.TryGetOrder(orderId, out OrderRecord order) ||
                order.State != OrderState.ReadyForPickup || !mealbox.IsClosed ||
                !string.Equals(mealbox.OrderId, order.OrderId, StringComparison.OrdinalIgnoreCase) ||
                mealbox.Type != order.LunchboxType || readyMealboxes.ContainsValue(mealbox))
                return false;

            string normalizedOrderId = orderId.Trim();
            Transform spawnTransform = displayPoint != null ? displayPoint : transform;
            mealbox.transform.SetParent(spawnTransform, true);
            mealbox.SetAttachedToPlayer(false);
            readyOrderIds.Add(normalizedOrderId);
            queuedOrderIds.Add(normalizedOrderId);
            readyMealboxes.Add(normalizedOrderId, mealbox);
            RefreshDisplayPositions();
            return true;
        }

        public bool TryCreateClosedMealboxForCompletion(
            string orderId,
            PlayerController cook,
            out MealboxPhysical mealbox)
        {
            mealbox = null;
            if (cook == null || cook.AssignedRole != PlayerRole.Cook || !cook.CanCarryPreparedMealbox ||
                cook.PreparationMealbox == null || cook.PreparationMealbox.IsClosed ||
                !string.Equals(cook.PreparationMealbox.OrderId, orderId, StringComparison.OrdinalIgnoreCase) ||
                !isActiveAndEnabled || !gameObject.activeInHierarchy ||
                prefabCatalog == null || orderManager == null ||
                !orderManager.TryGetOrder(orderId, out OrderRecord order) ||
                order.State != OrderState.InPreparation || cook.PreparationMealbox.Type != order.LunchboxType)
                return false;

            if (!prefabCatalog.HasCompleteConfiguration)
            {
                Debug.LogError($"LunchboxPrefabCatalog '{prefabCatalog.name}' must have all five empty and both closed prefabs assigned.", prefabCatalog);
                return false;
            }

            GameObject closedPrefab = prefabCatalog.ChooseClosedPrefab();
            if (closedPrefab == null)
            {
                Debug.LogError($"LunchboxPrefabCatalog '{prefabCatalog.name}' has no closed mealbox prefab assigned.", prefabCatalog);
                return false;
            }

            GameObject instance = Instantiate(closedPrefab);
            if (instance == null) return false;
            mealbox = instance.GetComponent<MealboxPhysical>();
            if (mealbox == null)
            {
                mealbox = instance.AddComponent<MealboxPhysical>();
                Debug.LogWarning($"Closed prefab '{closedPrefab.name}' had no MealboxPhysical; added the component to its instance.", closedPrefab);
            }
            mealbox.ConfigureClosedForOrder(order.OrderId, order.DisplayName, order.LunchboxType);
            return true;
        }

        public bool TryDepositCookMealbox(PlayerController cook, out string result)
        {
            result = "";
            MealboxPhysical mealbox = cook != null ? cook.CarriedPreparedMealbox : null;
            if (cook == null || cook.AssignedRole != PlayerRole.Cook || mealbox == null ||
                orderManager == null || !orderManager.TryGetOrder(mealbox.OrderId, out OrderRecord order) ||
                order.State != OrderState.ReadyForCookPickup || !mealbox.IsClosed ||
                mealbox.Type != order.LunchboxType || !CanRegisterReadyOrder(mealbox.OrderId))
            {
                result = "A marmita do Cook ou a vaga do balcão está indisponível.";
                return false;
            }

            if (!orderManager.TryMarkDepositedForPickup(order.OrderId))
            {
                result = "O pedido expirou ou não pode ser depositado.";
                return false;
            }

            if (!TryRegisterReadyOrder(order.OrderId, mealbox))
            {
                orderManager.TryRollbackDepositForPickup(order.OrderId);
                result = "O balcão não conseguiu registrar a marmita; ela continua com o Cook.";
                return false;
            }

            if (cook.ReleasePreparedMealbox(false) != mealbox)
            {
                UnregisterReadyOrderKeepMealbox(order.OrderId);
                orderManager.TryRollbackDepositForPickup(order.OrderId);
                mealbox.transform.SetParent(cook.GetMealboxCarryPoint(), false);
                mealbox.SetAttachedToPlayer(true);
                result = "A transferência da marmita falhou; tente novamente.";
                return false;
            }

            orderManager.NotifyOrdersChanged();
            cook.TryCompletePendingPreparation();
            result = $"Pedido #{order.OrderId} depositado. O Delivery pode retirá-lo.";
            return true;
        }

        public bool TryRestoreReadyOrder(string orderId, MealboxPhysical mealbox)
        {
            return orderManager != null && orderManager.TryGetOrder(orderId, out OrderRecord order) &&
                   order.State == OrderState.ReadyForPickup && TryRegisterReadyOrder(orderId, mealbox);
        }

        private void UnregisterReadyOrderKeepMealbox(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId)) return;
            string id = orderId.Trim();
            readyMealboxes.Remove(id);
            queuedOrderIds.Remove(id);
            readyOrderIds.Remove(id);
            RefreshDisplayPositions();
        }

        public bool TryGetReadyMealbox(string orderId, out MealboxPhysical mealbox)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                mealbox = null;
                return false;
            }

            string normalizedOrderId = orderId.Trim();
            if (!readyMealboxes.TryGetValue(normalizedOrderId, out mealbox) || mealbox == null ||
                !queuedOrderIds.Contains(normalizedOrderId))
            {
                mealbox = null;
                return false;
            }

            if (orderManager == null || !orderManager.TryGetOrder(normalizedOrderId, out OrderRecord order) ||
                order.State != OrderState.ReadyForPickup || !mealbox.IsClosed ||
                !string.Equals(mealbox.OrderId, normalizedOrderId, StringComparison.OrdinalIgnoreCase) ||
                mealbox.Type != order.LunchboxType)
            {
                RemoveReadyOrder(normalizedOrderId);
                mealbox = null;
                return false;
            }

            return true;
        }

        public bool TryTakeReadyOrder(string orderId, out MealboxPhysical mealbox)
        {
            if (!TryGetReadyMealbox(orderId, out mealbox))
            {
                return false;
            }

            string normalizedOrderId = orderId.Trim();
            readyMealboxes.Remove(normalizedOrderId);
            RemoveQueuedOrder(normalizedOrderId, false);
            RefreshDisplayPositions();
            return true;
        }

        public void RemoveReadyOrder(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return;
            }

            string normalizedOrderId = orderId.Trim();
            readyMealboxes.TryGetValue(normalizedOrderId, out MealboxPhysical mealbox);
            readyMealboxes.Remove(normalizedOrderId);
            RemoveQueuedOrder(normalizedOrderId, false);
            if (mealbox != null)
            {
                Destroy(mealbox.gameObject);
            }

            RefreshDisplayPositions();
        }

        private void PruneInvalidOrders()
        {
            if (orderManager == null)
            {
                return;
            }

            for (int i = readyOrderIds.Count - 1; i >= 0; i--)
            {
                string orderId = readyOrderIds[i];
                if (!orderManager.TryGetOrder(orderId, out OrderRecord order) ||
                    order.State != OrderState.ReadyForPickup)
                {
                    RemoveQueuedOrderAt(i, true);
                }
            }
        }

        private void RemoveQueuedOrder(string orderId, bool destroyMealbox = true)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return;
            }

            int index = -1;
            for (int i = 0; i < readyOrderIds.Count; i++)
            {
                if (string.Equals(readyOrderIds[i], orderId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
            if (index >= 0)
            {
                RemoveQueuedOrderAt(index, destroyMealbox);
            }
        }

        private void RemoveQueuedOrderAt(int index, bool destroyMealbox)
        {
            string orderId = readyOrderIds[index];
            queuedOrderIds.Remove(orderId);
            readyOrderIds.RemoveAt(index);
            if (readyMealboxes.TryGetValue(orderId, out MealboxPhysical mealbox))
            {
                readyMealboxes.Remove(orderId);
                if (destroyMealbox && mealbox != null)
                {
                    Destroy(mealbox.gameObject);
                }
            }

            RefreshDisplayPositions();
        }

        private void RefreshDisplayPositions()
        {
            Transform spawnTransform = displayPoint != null ? displayPoint : transform;
            Vector3 basePosition = displayPoint != null
                ? displayPoint.position
                : transform.position + Vector3.up * 1.1f;

            for (int i = 0; i < readyOrderIds.Count; i++)
            {
                string orderId = readyOrderIds[i];
                if (!readyMealboxes.TryGetValue(orderId, out MealboxPhysical mealbox) || mealbox == null)
                {
                    continue;
                }

                int column = i % 3;
                int row = i / 3;
                Vector3 offset = spawnTransform.right * ((column - 1) * 0.5f) +
                                 spawnTransform.forward * (row * 0.4f);
                mealbox.transform.SetPositionAndRotation(
                    basePosition + offset,
                    spawnTransform.rotation);
            }
        }

        private static PlayerController GetPlayerForRole(PlayerRole role)
        {
            GameManager manager = GameManager.Instance;
            if (manager == null) return null;
            if (manager.PlayerOne != null && manager.PlayerOne.AssignedRole == role) return manager.PlayerOne;
            if (manager.PlayerTwo != null && manager.PlayerTwo.AssignedRole == role) return manager.PlayerTwo;
            return null;
        }
    }
}
