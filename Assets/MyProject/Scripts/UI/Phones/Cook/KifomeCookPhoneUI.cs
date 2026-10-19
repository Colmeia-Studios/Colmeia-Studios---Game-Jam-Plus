using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class KifomeCookPhoneUI : KifomePhoneUI
    {
        [Header("Cook orders")] [SerializeField]
        private GameObject ordersView;

        [SerializeField] private Transform ordersContent;
        [SerializeField] private KifomeOrderCardUI orderCardPrefab;
        [SerializeField] private TMP_Text emptyOrdersText;
        [SerializeField] private bool logOrderListDiagnostics;

        private readonly Dictionary<string, KifomeOrderCardUI> orderCards =
            new Dictionary<string, KifomeOrderCardUI>(StringComparer.OrdinalIgnoreCase);
        private readonly List<OrderRecord> visibleOrders = new List<OrderRecord>();
        private string lastLoggedListState;

        protected override PlayerRole RequiredRole => PlayerRole.Cook;

        protected override bool HasRequiredRoleReferences =>
            ordersView != null &&
            ordersContent != null &&
            orderCardPrefab != null &&
            orderCardPrefab.HasRequiredReferences;

        protected override void ShowPrimaryViewContent()
        {
            if (ordersView != null)
            {
                ordersView.SetActive(true);
            }
        }

        protected override void LogMissingRoleReferences()
        {
            if (ordersView == null)
            {
                Debug.LogError("KifomeCookPhoneUI is missing its Orders view reference.", this);
            }

            if (ordersContent == null)
            {
                Debug.LogError("KifomeCookPhoneUI is missing its Orders card content reference.", this);
            }

            if (orderCardPrefab == null)
            {
                Debug.LogError("KifomeCookPhoneUI is missing its cook order card prefab.", this);
            }
            else if (!orderCardPrefab.HasRequiredReferences)
            {
                Debug.LogError(
                    "The cook order card prefab requires a Button, background, and text references for number, name, ingredients, status, and time.",
                    orderCardPrefab);
            }
        }

        protected override void RefreshRoleOrders()
        {
            if (Owner == null || Orders == null || ordersContent == null || orderCardPrefab == null)
            {
                return;
            }

            visibleOrders.Clear();
            IReadOnlyList<OrderRecord> activeOrders = Orders.ActiveOrders;
            for (int i = 0; i < activeOrders.Count; i++)
            {
                OrderRecord order = activeOrders[i];
                if (order.State == OrderState.Available || order.State == OrderState.InPreparation)
                {
                    visibleOrders.Add(order);
                }
            }

            ReconcileCards();
            LogListStateIfChanged(activeOrders);
            if (emptyOrdersText != null)
            {
                emptyOrdersText.gameObject.SetActive(visibleOrders.Count == 0);
            }

            for (int i = 0; i < visibleOrders.Count; i++)
            {
                OrderRecord order = visibleOrders[i];
                bool selected = string.Equals(
                    Owner.SelectedOrderId,
                    order.OrderId,
                    StringComparison.OrdinalIgnoreCase);
                bool selectable = order.State == OrderState.Available &&
                                  !Owner.HasActivePreparation ||
                                  order.State == OrderState.InPreparation && selected;

                orderCards[order.OrderId].BindForCook(
                    order,
                    Orders,
                    Match,
                    selectable,
                    selected,
                    SelectOrder);
            }

            RefreshNavigationIfOpen();
        }

        protected override void AddRoleButtonsToNavigation()
        {
            for (int i = 0; i < visibleOrders.Count; i++)
            {
                if (orderCards.TryGetValue(visibleOrders[i].OrderId, out KifomeOrderCardUI card) && card != null)
                {
                    AddNavigableButton(card.Button);
                }
            }
        }

        private void SelectOrder(string orderId)
        {
            if (Owner == null || Orders == null || !Orders.TryGetOrder(orderId, out OrderRecord order))
            {
                return;
            }

            if (Owner.HasActivePreparation)
            {
                if (!string.Equals(
                        Owner.SelectedOrderId,
                        order.OrderId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    Owner.ShowGameplayMessage("Finish or discard the current preparation before starting another order.");
                    return;
                }

                if (Owner.CurrentIngredientIds.Count == 0)
                {
                    Owner.CancelPreparation();
                }

                RefreshRoleOrders();
                return;
            }

            if (order.State == OrderState.Available && !Owner.StartPreparation(order.OrderId))
            {
                Owner.ShowGameplayMessage("Could not start preparing this order.");
            }

            RefreshRoleOrders();
        }

        private void ReconcileCards()
        {
            var desiredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (OrderRecord order in visibleOrders)
                desiredIds.Add(order.OrderId);

            var removedIds = new List<string>();
            foreach (KeyValuePair<string, KifomeOrderCardUI> entry in orderCards)
            {
                if (!desiredIds.Contains(entry.Key))
                    removedIds.Add(entry.Key);
            }

            foreach (string removedId in removedIds)
            {
                KifomeOrderCardUI card = orderCards[removedId];
                if (card != null)
                    Destroy(card.gameObject);
                orderCards.Remove(removedId);
            }

            for (int i = 0; i < visibleOrders.Count; i++)
            {
                string orderId = visibleOrders[i].OrderId;
                if (!orderCards.TryGetValue(orderId, out KifomeOrderCardUI card) || card == null)
                {
                    card = Instantiate(orderCardPrefab, ordersContent);
                    orderCards[orderId] = card;
                }

                // Keep existing cards and their children; only reorder them when
                // the active order sequence itself has changed.
                if (card.transform.GetSiblingIndex() != i)
                    card.transform.SetSiblingIndex(i);
            }
        }

        private void LogListStateIfChanged(IReadOnlyList<OrderRecord> activeOrders)
        {
            if (!logOrderListDiagnostics)
                return;

            var signature = new StringBuilder();
            signature.Append("visible[");
            foreach (OrderRecord order in visibleOrders)
                signature.Append(order.OrderId).Append(':').Append(order.State).Append(';');
            signature.Append("] filtered[");
            foreach (OrderRecord order in activeOrders)
            {
                if (order.State != OrderState.Available && order.State != OrderState.InPreparation)
                    signature.Append(order.OrderId).Append(':').Append(order.State).Append(';');
            }
            signature.Append(']');

            string currentState = signature.ToString();
            if (string.Equals(lastLoggedListState, currentState, StringComparison.Ordinal))
                return;

            lastLoggedListState = currentState;
            Debug.Log($"KifomeCookPhoneUI list reconciled: {currentState}", this);
        }
    }
}
