using System.Collections;
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

        private readonly List<TMP_Text> mealNames = new List<TMP_Text>(3);
        private GameManager gameManager;
        private OrderManager orderManager;
        private bool initialized;

        private IEnumerator Start()
        {
            yield return null;
            BindGameplay(deliveryPlayer, GameManager.Instance);
        }

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

            if (deliveryPlayer == null || deliveryPlayer.AssignedRole != PlayerRole.Delivery ||
                gameManager == null || orderManager == null || backpackContent == null ||
                mealNamePrefab == null || backpackCountText == null)
            {
                Debug.LogError(
                    "DeliveryUI requires a delivery PlayerController, backpack content, a meal-name text prefab, and a count text.",
                    this);
                initialized = false;
                return;
            }

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
            var ingredientNames = new List<string>(order.RequiredIngredientIds.Count);
            bool needsStationLookup = order.RequiredIngredients.Count < order.RequiredIngredientIds.Count;
            for (int i = 0; i < order.RequiredIngredients.Count && !needsStationLookup; i++)
            {
                IngredientData ingredient = order.RequiredIngredients[i];
                needsStationLookup = ingredient == null || string.IsNullOrWhiteSpace(ingredient.DisplayName);
            }

            IngredientStation[] stations = needsStationLookup
                ? FindObjectsByType<IngredientStation>()
                : System.Array.Empty<IngredientStation>();

            for (int i = 0; i < order.RequiredIngredientIds.Count; i++)
            {
                IngredientData ingredient = i < order.RequiredIngredients.Count
                    ? order.RequiredIngredients[i]
                    : null;
                string ingredientName = ingredient != null &&
                                        !string.IsNullOrWhiteSpace(ingredient.DisplayName)
                    ? ingredient.DisplayName
                    : FindIngredientName(order.RequiredIngredientIds[i], stations);
                ingredientNames.Add(ingredientName);
            }

            string mealDescription = string.Join(", ", ingredientNames);
            return string.IsNullOrWhiteSpace(mealDescription)
                ? order.DisplayName
                : $"{order.DisplayName} — {mealDescription}";
        }

        private static string FindIngredientName(string ingredientId, IReadOnlyList<IngredientStation> stations)
        {
            string normalizedId = IngredientStation.NormalizeLegacyId(ingredientId);
            for (int i = 0; i < stations.Count; i++)
            {
                IngredientStation station = stations[i];
                if (station == null)
                {
                    continue;
                }

                if (string.Equals(station.IngredientId, ingredientId, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        IngredientStation.NormalizeLegacyId(station.DisplayName),
                        normalizedId,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(station.DisplayName) ? ingredientId : station.DisplayName;
                }
            }

            return ingredientId;
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