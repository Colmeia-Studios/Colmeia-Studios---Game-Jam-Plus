using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class KifomeOrderCardUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image cardBackground;
        [SerializeField] private TMP_Text orderNumberText;
        [SerializeField] private TMP_Text orderNameText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text remainingTimeText;

        [Header("State colors")] [SerializeField]
        private Color availableColor = new Color(0.78f, 0.91f, 0.74f);

        [SerializeField] private Color preparationColor = new Color(1f, 0.88f, 0.58f);
        [SerializeField] private Color expiredColor = new Color(0.94f, 0.62f, 0.58f);
        [SerializeField] private Color selectedTint = new Color(1f, 0.78f, 0.35f);

        private OrderManager orderManager;
        private GameManager gameManager;
        private Action<string> selectionHandler;
        private string orderId;
        private bool selectable;
        private bool selected;

        public Button Button => button;

        public bool HasRequiredReferences =>
            button != null && cardBackground != null && orderNumberText != null &&
            orderNameText != null && summaryText != null && stateText != null &&
            remainingTimeText != null;

        public void BindForCook(
            OrderRecord order,
            OrderManager orders,
            GameManager match,
            bool isSelectable,
            bool isSelected,
            Action<string> onSelected)
        {
            BindInternal(order, orders, match, isSelectable, isSelected, onSelected);
        }

        private void BindInternal(
            OrderRecord order,
            OrderManager orders,
            GameManager match,
            bool isSelectable,
            bool isSelected,
            Action<string> onSelected)
        {
            if (order == null)
            {
                return;
            }

            orderId = order.OrderId;
            orderManager = orders;
            gameManager = match;
            selectable = isSelectable;
            selected = isSelected;
            selectionHandler = onSelected;
            if (button != null)
            {
                button.onClick.RemoveListener(Select);
                button.onClick.AddListener(Select);
            }

            Refresh();
        }

        private void Refresh()
        {
            if (orderManager == null || !orderManager.TryGetOrder(orderId, out OrderRecord order))
            {
                return;
            }

            if (button != null)
            {
                button.interactable = selectable && IsBusinessSelectable(order.State);
            }

            if (orderNumberText != null)
            {
                orderNumberText.text = $"#{order.OrderId}";
            }

            if (orderNameText != null)
            {
                orderNameText.text = order.DisplayName;
            }

            if (summaryText != null)
            {
                var ingredients = new StringBuilder();
                for (int i = 0; i < order.RequiredIngredientIds.Count; i++)
                {
                    if (i > 0)
                    {
                        ingredients.Append(", ");
                    }

                    IngredientData ingredient = i < order.RequiredIngredients.Count
                        ? order.RequiredIngredients[i]
                        : null;
                    ingredients.Append(ingredient != null
                        ? ingredient.DisplayName
                        : order.RequiredIngredientIds[i]);
                }

                summaryText.text = ingredients.ToString();
            }

            if (stateText != null)
            {
                stateText.text = GetStateLabel(order.State);
            }

            if (remainingTimeText != null)
            {
                remainingTimeText.text = order.State == OrderState.Delivered || order.State == OrderState.Expired
                    ? "--"
                    : $"{orderManager.GetRemainingSeconds(order.OrderId):0}s";
            }

            if (cardBackground != null)
            {
                Color stateColor = GetStateColor(order.State);
                cardBackground.color = selected ? Color.Lerp(stateColor, selectedTint, 0.48f) : stateColor;
            }
        }

        private void Select()
        {
            if (selectable && orderManager != null &&
                orderManager.TryGetOrder(orderId, out OrderRecord order) &&
                IsBusinessSelectable(order.State))
            {
                selectionHandler?.Invoke(orderId);
            }
        }

        private static bool IsBusinessSelectable(OrderState state)
        {
            return state == OrderState.Available || state == OrderState.InPreparation;
        }

        private Color GetStateColor(OrderState state)
        {
            if (orderManager != null && orderManager.TryGetOrder(orderId, out OrderRecord order) &&
                order.IsOverdue(gameManager != null ? gameManager.ElapsedSeconds : 0f) &&
                state != OrderState.Expired)
            {
                return expiredColor;
            }

            switch (state)
            {
                case OrderState.Available: return availableColor;
                case OrderState.InPreparation: return preparationColor;
                case OrderState.Expired: return expiredColor;
                default: return Color.white;
            }
        }

        private static string GetStateLabel(OrderState state)
        {
            switch (state)
            {
                case OrderState.Available: return "Disponível";
                case OrderState.InPreparation: return "Em preparação";
                case OrderState.Expired: return "Expirado";
                default: return state.ToString();
            }
        }
    }
}