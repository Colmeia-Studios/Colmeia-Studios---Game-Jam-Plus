using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class PickupCounter : Interactable
    {
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private MealboxPhysical mealboxPrefab;
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
                orderManager == null ||
                mealboxPrefab == null)
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

            if (mealboxPrefab == null)
            {
                Debug.LogError(
                    "PickupCounter requires a closed MealboxPhysical prefab to display completed orders.",
                    this);
            }

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
            return base.CanInteract(player) && player != null && player.AssignedRole == PlayerRole.Delivery &&
                   readyOrderIds.Count > 0;
        }

        public override string GetInteractionText()
        {
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
            if (string.IsNullOrWhiteSpace(orderId) || orderManager == null ||
                !CanRegisterReadyOrder(orderId) ||
                !orderManager.TryGetOrder(orderId, out OrderRecord order) ||
                order.State != OrderState.ReadyForPickup)
            {
                return false;
            }

            string normalizedOrderId = orderId.Trim();
            Transform spawnTransform = displayPoint != null ? displayPoint : transform;
            Vector3 spawnPosition = displayPoint != null
                ? displayPoint.position
                : transform.position + Vector3.up * 1.1f;
            MealboxPhysical mealbox = Instantiate(
                mealboxPrefab, spawnPosition, spawnTransform.rotation, spawnTransform);
            if (mealbox == null)
            {
                Debug.LogError($"Could not create the mealbox for order '{normalizedOrderId}'.", this);
                return false;
            }

            mealbox.Configure(normalizedOrderId, order.DisplayName);
            readyOrderIds.Add(normalizedOrderId);
            queuedOrderIds.Add(normalizedOrderId);
            readyMealboxes[normalizedOrderId] = mealbox;
            RefreshDisplayPositions();
            return true;
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

            int index = readyOrderIds.IndexOf(orderId.Trim());
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
    }
}