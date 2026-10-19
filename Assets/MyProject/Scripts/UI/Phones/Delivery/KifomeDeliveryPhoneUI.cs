using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class KifomeDeliveryPhoneUI : KifomePhoneUI
    {
        [Header("Delivery list")] [SerializeField]
        private GameObject deliveryListView;

        [SerializeField] private Transform deliveryOrdersContent;
        [SerializeField] private KifomeDeliveryCardUI deliveryOrderCardPrefab;
        [SerializeField] private TMP_Text emptyOrdersText;

        [Header("Delivery details")] [SerializeField]
        private GameObject orderDetailsView;

        [SerializeField] private TMP_Text customerNameText;
        [SerializeField] private TMP_Text orderNumberText;
        [SerializeField] private TMP_Text mealNameText;
        [SerializeField] private TMP_Text remainingTimeText;
        [SerializeField] private TMP_Text houseDescriptionText;
        [SerializeField] private TMP_Text orderValueText;
        [SerializeField] private Image housePhoto;

        private readonly List<KifomeDeliveryCardUI> orderCards = new List<KifomeDeliveryCardUI>();
        private readonly List<OrderRecord> visibleOrders = new List<OrderRecord>();
        private string detailsOrderId;

        protected override PlayerRole RequiredRole => PlayerRole.Delivery;

        protected override bool HasRequiredRoleReferences =>
            deliveryListView != null &&
            deliveryOrdersContent != null &&
            deliveryOrderCardPrefab != null &&
            deliveryOrderCardPrefab.HasRequiredReferences &&
            orderDetailsView != null &&
            deliveryListView != orderDetailsView &&
            !deliveryListView.transform.IsChildOf(orderDetailsView.transform) &&
            !orderDetailsView.transform.IsChildOf(deliveryListView.transform) &&
            orderNumberText != null &&
            mealNameText != null &&
            remainingTimeText != null;

        protected override void ShowPrimaryViewContent()
        {
            detailsOrderId = null;
            ClearDetails();
            if (deliveryListView != null) deliveryListView.SetActive(true);
            if (orderDetailsView != null) orderDetailsView.SetActive(false);
        }

        protected override void LogMissingRoleReferences()
        {
            if (deliveryListView == null)
                Debug.LogError("KifomeDeliveryPhoneUI is missing its Deliveries view reference.", this);
            if (deliveryOrdersContent == null)
                Debug.LogError("KifomeDeliveryPhoneUI is missing its delivery card content reference.", this);
            if (deliveryOrderCardPrefab == null)
                Debug.LogError("KifomeDeliveryPhoneUI is missing its delivery card prefab.", this);
            else if (!deliveryOrderCardPrefab.HasRequiredReferences)
                Debug.LogError(
                    "The delivery card prefab requires a Button and text references for all five card fields.",
                    deliveryOrderCardPrefab);
            if (orderDetailsView == null)
                Debug.LogError("KifomeDeliveryPhoneUI is missing its Delivery Expand view reference.", this);
            if (deliveryListView != null && orderDetailsView != null &&
                (deliveryListView == orderDetailsView ||
                 deliveryListView.transform.IsChildOf(orderDetailsView.transform) ||
                 orderDetailsView.transform.IsChildOf(deliveryListView.transform)))
            {
                Debug.LogError("Deliveries and Delivery Expand must be separate, non-nested views.", this);
            }

            if (orderNumberText == null)
                Debug.LogError("Delivery Expand is missing its order number text.", this);
            if (mealNameText == null)
                Debug.LogError("Delivery Expand is missing its meal name text.", this);
            if (remainingTimeText == null)
                Debug.LogError("Delivery Expand is missing its remaining time text.", this);
         }

        protected override void RefreshRoleOrders()
        {
            if (Owner == null || Orders == null ||
                deliveryOrdersContent == null || deliveryOrderCardPrefab == null)
            {
                return;
            }

            visibleOrders.Clear();
            IReadOnlyList<OrderRecord> activeOrders = Orders.ActiveOrders;
            for (int i = 0; i < activeOrders.Count; i++)
            {
                OrderRecord order = activeOrders[i];
                if (IsDisplayable(order))
                {
                    visibleOrders.Add(order);
                }
            }

            ResizeCards(visibleOrders.Count);
            if (emptyOrdersText != null)
            {
                emptyOrdersText.gameObject.SetActive(visibleOrders.Count == 0);
            }

            bool selectedOrderStillAvailable = false;
            for (int i = 0; i < visibleOrders.Count; i++)
            {
                OrderRecord order = visibleOrders[i];
                bool selected = string.Equals(
                    detailsOrderId,
                    order.OrderId,
                    StringComparison.OrdinalIgnoreCase);
                selectedOrderStillAvailable |= selected;
                orderCards[i].Bind(order, Orders);
            }

            if (!string.IsNullOrWhiteSpace(detailsOrderId) && !selectedOrderStillAvailable)
            {
                detailsOrderId = null;
                ClearDetails();
            }

            UpdateViewMode();
            RefreshRoleDetails();
            RefreshNavigationIfOpen();
        }

        protected override void RefreshRoleDetails()
        {
            if (!TryGetDetailsOrder(out OrderRecord order))
            {
                detailsOrderId = null;
                ClearDetails();
                UpdateViewMode();
                return;
            }

            HouseData destination = order.Destination;
            SetOptionalText(customerNameText, destination != null ? destination.CustomerName : null);
            orderNumberText.text = $"#{OrderNumberFormatter.Format(order.OrderId)}";
            mealNameText.text = order.DisplayName;
            remainingTimeText.text = $"{Orders.GetRemainingSeconds(order.OrderId):0}s";
            SetOptionalText(
                houseDescriptionText,
                destination != null ? destination.Description : null);
            SetOptionalText(orderValueText, $"{order.BaseValue:0.00}");

            Sprite image = destination != null ? destination.HouseImage : null;
            if (housePhoto != null)
            {
                housePhoto.sprite = image;
                housePhoto.enabled = image != null;
                housePhoto.gameObject.SetActive(image != null);
            }
        }

        protected override void AddRoleButtonsToNavigation()
        {

            if (deliveryListView == null || !deliveryListView.activeInHierarchy)
            {
                return;
            }

            for (int i = 0; i < orderCards.Count; i++)
            {
                if (orderCards[i] != null)
                {
                    AddNavigableButton(orderCards[i].Button);
                }
            }
        }

        private void ShowOrderDetails(string orderId)
        {
            if (Orders == null ||
                !Orders.TryGetOrder(orderId, out OrderRecord order) ||
                !IsDisplayable(order))
            {
                return;
            }

            detailsOrderId = order.OrderId;
            UpdateViewMode();
            RefreshRoleDetails();
            RefreshNavigationIfOpen();
        }

        private void ShowDeliveryList()
        {
            detailsOrderId = null;
            ClearDetails();
            UpdateViewMode();
            RefreshRoleOrders();
            RefreshNavigationIfOpen();
        }

        private void UpdateViewMode()
        {
            bool showingDetails = TryGetDetailsOrder(out _);
            if (deliveryListView != null)
            {
                deliveryListView.SetActive(!showingDetails);
            }

            if (orderDetailsView != null)
            {
                orderDetailsView.SetActive(showingDetails);
            }

            if (!showingDetails)
            {
                detailsOrderId = null;
                ClearDetails();
            }
        }

        private bool TryGetDetailsOrder(out OrderRecord order)
        {
            order = null;
            return !string.IsNullOrWhiteSpace(detailsOrderId) &&
                   Orders != null &&
                   Orders.TryGetOrder(detailsOrderId, out order) &&
                   IsDisplayable(order);
        }

        private void ClearDetails()
        {
            if (customerNameText != null) customerNameText.text = string.Empty;
            if (orderNumberText != null) orderNumberText.text = string.Empty;
            if (mealNameText != null) mealNameText.text = string.Empty;
            if (remainingTimeText != null) remainingTimeText.text = string.Empty;
            SetOptionalText(houseDescriptionText, string.Empty);
            SetOptionalText(orderValueText, string.Empty);

            if (housePhoto != null)
            {
                housePhoto.sprite = null;
                housePhoto.enabled = false;
                housePhoto.gameObject.SetActive(false);
            }
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
                KifomeDeliveryCardUI card = Instantiate(deliveryOrderCardPrefab, deliveryOrdersContent);
                card.Button.onClick.AddListener(() => ShowOrderDetails(card.OrderId));
                orderCards.Add(card);
            }
        }

        private static void SetOptionalText(TMP_Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            text.text = value ?? string.Empty;
            text.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        }

        private static bool IsDisplayable(OrderRecord order)
        {
            return order != null &&
                   (order.State == OrderState.ReadyForPickup || order.State == OrderState.InDelivery);
        }
    }
}