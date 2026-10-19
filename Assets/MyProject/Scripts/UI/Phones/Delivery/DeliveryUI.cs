using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class DeliveryUI : MonoBehaviour
    {
        [SerializeField] private PlayerController deliveryPlayer;
        [SerializeField] private Transform backpackContent;
        [SerializeField] private TMP_Text mealNamePrefab;
        [SerializeField] private TMP_Text backpackCountText;
        [SerializeField] private Canvas gameplayCanvas;

        private readonly List<TMP_Text> mealNames = new List<TMP_Text>(3);
        private GameManager gameManager;
        private OrderManager orderManager;
        private bool initialized;

        private void OnEnable()
        {
            if (initialized)
            {
                Subscribe();
                Refresh();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void BindGameplay(PlayerController player, GameManager match)
        {
            Unsubscribe();
            deliveryPlayer = player;
            gameManager = match;
            orderManager = gameManager != null ? gameManager.Orders : null;
            gameplayCanvas ??= GetComponentInParent<Canvas>();

            List<string> missing = new List<string>();
            if (deliveryPlayer == null)
                missing.Add("Delivery PlayerController (GameManager role binding)");
            else if (deliveryPlayer.AssignedRole != PlayerRole.Delivery)
                missing.Add("Delivery PlayerController has the wrong assigned role");
            if (gameManager == null) missing.Add("GameManager");
            if (orderManager == null) missing.Add("OrderManager (GameManager.Orders)");
            if (backpackContent == null) missing.Add("Backpack Content (Inspector)");
            if (mealNamePrefab == null) missing.Add("Meal Name Prefab (Inspector)");
            if (backpackCountText == null) missing.Add("Backpack Count Text (Inspector)");
            if (gameplayCanvas == null) missing.Add("parent Canvas (DeliveryPanel)");
            if (deliveryPlayer != null && deliveryPlayer.GetCamera() == null) missing.Add("Delivery player camera");

            if (missing.Count > 0)
            {
                Debug.LogError($"DeliveryUI cannot initialize. Missing or invalid: {string.Join(", ", missing)}.", this);
                initialized = false;
                return;
            }

            gameplayCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            gameplayCanvas.worldCamera = deliveryPlayer.GetCamera();
            gameplayCanvas.planeDistance = 0.02f;
            gameplayCanvas.overrideSorting = true;
            gameplayCanvas.sortingOrder = 100;
            initialized = true;
            Subscribe();
            Refresh();
        }

        public void Refresh()
        {
            if (!initialized || deliveryPlayer == null || orderManager == null)
            {
                return;
            }

            IReadOnlyList<string> carriedIds = deliveryPlayer.CarriedOrderIds;
            int validCount = 0;
            for (int i = 0; i < carriedIds.Count; i++)
            {
                if (!orderManager.TryGetOrder(carriedIds[i], out OrderRecord order) ||
                    order.State != OrderState.InDelivery)
                {
                    continue;
                }

                validCount++;
                EnsureEntryCount(validCount);
                mealNames[validCount - 1].text = BuildMealDescription(order);
            }

            EnsureEntryCount(validCount);
            backpackCountText.text = $"{validCount}/{deliveryPlayer.InventoryCapacity}";
        }

        private static string BuildMealDescription(OrderRecord order)
        {
            return order.DisplayName;
        }
        

        private void EnsureEntryCount(int count)
        {
            while (mealNames.Count > count)
            {
                int last = mealNames.Count - 1;
                if (mealNames[last] != null)
                {
                    Destroy(mealNames[last].gameObject);
                }

                mealNames.RemoveAt(last);
            }

            while (mealNames.Count < count)
            {
                mealNames.Add(Instantiate(mealNamePrefab, backpackContent));
            }
        }

        private void Subscribe()
        {
            if (!initialized)
            {
                return;
            }

            if (deliveryPlayer != null)
            {
                deliveryPlayer.InventoryChanged -= Refresh;
                deliveryPlayer.InventoryChanged += Refresh;
            }

            if (orderManager != null)
            {
                orderManager.OrdersChanged -= Refresh;
                orderManager.OrdersChanged += Refresh;
            }
        }

        private void Unsubscribe()
        {
            if (deliveryPlayer != null)
            {
                deliveryPlayer.InventoryChanged -= Refresh;
            }

            if (orderManager != null)
            {
                orderManager.OrdersChanged -= Refresh;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
