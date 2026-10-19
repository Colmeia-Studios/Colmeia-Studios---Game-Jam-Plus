using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class KifomeDeliveryCardUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text orderNameText;
        [SerializeField] private TMP_Text orderNumberText;
        [SerializeField] private TMP_Text remainingTimeText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text houseDescriptionText;

        private string orderId;

        public Button Button => button;
        public string OrderId => orderId;

        public bool HasRequiredReferences =>
            button != null && orderNameText != null && orderNumberText != null &&
            remainingTimeText != null && priceText != null && houseDescriptionText != null;

        public void Bind(OrderRecord order, OrderManager orders)
        {
            if (order == null || orders == null)
            {
                Clear();
                return;
            }

            orderId = order.OrderId;
            orderNameText.text = order.DisplayName;
            orderNumberText.text = $"#{OrderNumberFormatter.Format(order.OrderId)}";
            remainingTimeText.text = $"{orders.GetRemainingSeconds(order.OrderId):0}s";
            priceText.text = $"{order.BaseValue:0.00}";
            houseDescriptionText.text = order.Destination != null
                ? order.Destination.Description
                : string.Empty;
        }

        private void Awake()
        {
            if (!HasRequiredReferences)
            {
                Debug.LogError(
                    "KifomeDeliveryCardUI requires a Button and text references for the order name, number, time, price, and house description.",
                    this);
            }
        }

        private void Clear()
        {
            orderId = null;
            if (orderNameText != null) orderNameText.text = string.Empty;
            if (orderNumberText != null) orderNumberText.text = string.Empty;
            if (remainingTimeText != null) remainingTimeText.text = string.Empty;
            if (priceText != null) priceText.text = string.Empty;
            if (houseDescriptionText != null) houseDescriptionText.text = string.Empty;
        }
    }
}