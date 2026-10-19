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
                return "Interact to deposit the prepared mealbox.";

            PlayerController deliveryPlayer = GameManager.Instance != null
                ? GameManager.Instance.PlayerOne != null &&
                  GameManager.Instance.PlayerOne.AssignedRole == PlayerRole.Delivery
                    ? GameManager.Instance.PlayerOne
                    : GameManager.Instance.PlayerTwo
                : null;
            if (deliveryPlayer != null && deliveryPlayer.CarriedOrderIds.Count >= deliveryPlayer.InventoryCapacity)
            {
                return "Backpack full: maximum of three mealboxes.";
            }

            return ReadyOrderCount == 1
                ? "Interact to collect the mealbox."
                : $"Interact to collect a mealbox ({ReadyOrderCount} available).";
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
                    player.ShowGameplayMessage("Could not deposit the mealbox at the counter.");
                }

                return;
            }

            if (player.CarriedOrderIds.Count >= player.InventoryCapacity)
            {
                player.ShowGameplayMessage("Backpack full: maximum of three mealboxes.");
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
                    player.ShowGameplayMessage($"Order #{orderId} stored in the backpack.");
                }
                else
                {
                    player.ShowGameplayMessage("Could not collect this mealbox.");
                }

                return;
            }
        }

        public MealboxPhysical CreateCampaignMealbox(OrderRecord order, bool closed, int closedVariant = -1)
        {
            if (order == null || prefabCatalog == null) return null;
            GameObject prefab = closed
                ? (closedVariant >= 0 ? prefabCatalog.GetClosedPrefab(closedVariant) : prefabCatalog.ChooseClosedPrefab(out closedVariant))
                : prefabCatalog.GetEmptyPrefab(order.LunchboxType);
            if (prefab == null) return null;
            GameObject instance = Instantiate(prefab);
            if (instance == null) return null;
            MealboxPhysical physical = instance.GetComponent<MealboxPhysical>();
            if (physical == null && closed) physical = instance.AddComponent<MealboxPhysical>();
            if (physical == null) { Destroy(instance); return null; }
            if (closed) physical.ConfigureClosedForOrder(order.OrderId, order.DisplayName, order.LunchboxType, closedVariant);
            else physical.ConfigureEmptyForOrder(order.OrderId, order.DisplayName, order.LunchboxType);
            return physical;
        }

        public bool RestoreReadyMealbox(OrderRecord order)
        {
            if (order == null || order.State != OrderState.ReadyForPickup ||
                !orderManager.TryGetOrder(order.OrderId, out OrderRecord registered) || registered != order ||
                !CanRegisterReadyOrder(order.OrderId)) return false;
            MealboxPhysical physical = CreateCampaignMealbox(order, true, order.ClosedVisualVariant);
            if (physical == null) return false;
            if (TryRegisterReadyOrder(order.OrderId, physical))
            {
                order.ClosedVisualVariant = physical.ClosedVisualVariant;
                return true;
            }
            Destroy(physical.gameObject);
            return false;
        }

        public bool RestoreCampaignPhysicals(System.Collections.Generic.IReadOnlyList<OrderRecord> orders)
        {
            if (orders == null) return false;
            foreach (OrderRecord order in orders)
                if (order != null && order.State == OrderState.ReadyForPickup && !readyMealboxes.ContainsKey(order.OrderId) &&
                    !RestoreReadyMealbox(order))
                {
                    Debug.LogError($"Could not reconstruct the counter mealbox for order '{order.OrderId}'. Check the closed prefab catalog.", this);
                    return false;
                }
            return true;
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

            GameObject closedPrefab = prefabCatalog.ChooseClosedPrefab(out int visualVariant);
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
            mealbox.ConfigureClosedForOrder(order.OrderId, order.DisplayName, order.LunchboxType, visualVariant);
            order.ClosedVisualVariant = visualVariant;
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
                result = "The Cook mealbox or counter slot is unavailable.";
                return false;
            }

            if (!orderManager.TryMarkDepositedForPickup(order.OrderId))
            {
                result = "The order expired or cannot be deposited.";
                return false;
            }

            if (!TryRegisterReadyOrder(order.OrderId, mealbox))
            {
                orderManager.TryRollbackDepositForPickup(order.OrderId);
                result = "The counter could not register the mealbox; the Cook is still carrying it.";
                return false;
            }

            if (cook.ReleasePreparedMealbox(false) != mealbox)
            {
                UnregisterReadyOrderKeepMealbox(order.OrderId);
                orderManager.TryRollbackDepositForPickup(order.OrderId);
                mealbox.transform.SetParent(cook.GetMealboxCarryPoint(), false);
                mealbox.SetAttachedToPlayer(true);
                result = "Mealbox transfer failed. Please try again.";
                return false;
            }

            orderManager.NotifyOrdersChanged();
            cook.TryCompletePendingPreparation();
            result = $"Order #{order.OrderId} deposited. The Delivery player can now collect it.";
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
