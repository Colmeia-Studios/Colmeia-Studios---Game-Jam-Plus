using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class KifomeUpgradeCardUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Button purchaseButton;
        [SerializeField] private Image purchaseButtonImage;
        [SerializeField] private Color availableColor = new Color(0.25f, 0.68f, 0.35f);
        [SerializeField] private Color unavailableColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color purchasedColor = new Color(0.32f, 0.48f, 0.35f);

        private UpgradeData upgrade;
        private GameManager gameManager;
        private PlayerController buyer;
        private Action purchaseCompleted;

        public Button PurchaseButton => purchaseButton;

        private void OnEnable()
        {
            if (gameManager != null)
            {
                gameManager.MatchStateChanged += Refresh;
            }
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.MatchStateChanged -= Refresh;
            }
        }

        public void Bind(
            UpgradeData data,
            GameManager match,
            PlayerController player,
            Action onPurchaseCompleted)
        {
            if (gameManager != null)
            {
                gameManager.MatchStateChanged -= Refresh;
            }

            upgrade = data;
            gameManager = match;
            buyer = player;
            purchaseCompleted = onPurchaseCompleted;

            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveListener(Purchase);
                purchaseButton.onClick.AddListener(Purchase);
            }

            if (isActiveAndEnabled && gameManager != null)
            {
                gameManager.MatchStateChanged -= Refresh;
                gameManager.MatchStateChanged += Refresh;
            }

            Refresh();
        }

        private void Purchase()
        {
            if (upgrade == null || gameManager == null || buyer == null)
            {
                return;
            }

            if (gameManager.TryPurchaseUpgrade(buyer, upgrade))
            {
                purchaseCompleted?.Invoke();
            }

            Refresh();
        }

        private void Refresh()
        {
            if (upgrade == null)
            {
                return;
            }

            if (iconImage != null)
            {
                iconImage.sprite = upgrade.Icon;
                iconImage.enabled = upgrade.Icon != null;
            }

            if (titleText != null) titleText.text = upgrade.DisplayName;
            if (descriptionText != null) descriptionText.text = upgrade.Description;
            if (costText != null) costText.text = $"R$ {upgrade.Price:0.00}";

            bool purchased = gameManager != null && gameManager.IsUpgradePurchased(upgrade);
            bool canPurchase = gameManager != null && gameManager.CanPurchaseUpgrade(buyer, upgrade);
            if (purchaseButton != null)
            {
                purchaseButton.interactable = canPurchase;
            }

            Image buttonImage = purchaseButtonImage;
            if (buttonImage == null && purchaseButton != null)
            {
                buttonImage = purchaseButton.targetGraphic as Image;
            }

            if (buttonImage != null)
            {
                buttonImage.color = purchased
                    ? purchasedColor
                    : canPurchase ? availableColor : unavailableColor;
            }
        }
    }
}
