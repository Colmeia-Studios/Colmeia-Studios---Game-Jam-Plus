using System;
using System.Collections.Generic;
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

        private readonly List<KifomeOrderCardUI> orderCards = new List<KifomeOrderCardUI>();
        private readonly List<OrderRecord> visibleOrders = new List<OrderRecord>();

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

            ResizeCards(visibleOrders.Count);
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

                orderCards[i].BindForCook(
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
            for (int i = 0; i < orderCards.Count; i++)
            {
                if (orderCards[i] != null)
                {
                    AddNavigableButton(orderCards[i].Button);
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
                    Owner.ShowGameplayMessage("Conclua ou descarte a preparação atual antes de iniciar outro pedido.");
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
                Owner.ShowGameplayMessage("Não foi possível iniciar a preparação deste pedido.");
            }

            RefreshRoleOrders();
        }

        private void ResizeCards(int count)
        {
            while (orderCards.Count > count)
            {
                int last = orderCards.Count - 1;
                if (orderCards[last] != null)
                {
                    Destroy(orderCards[last].gameObject);
                }

                orderCards.RemoveAt(last);
            }

            while (orderCards.Count < count)
            {
                orderCards.Add(Instantiate(orderCardPrefab, ordersContent));
            }
        }
    }
}