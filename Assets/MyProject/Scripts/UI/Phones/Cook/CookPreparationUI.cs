using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class CookPreparationUI : MonoBehaviour
    {
        [SerializeField] private PlayerController cookPlayer;
        [SerializeField] private Transform ingredientContent;
        [SerializeField] private KifomeOrderListUI ingredientIndicatorPrefab;
        [SerializeField] private TMP_Text recipeNameText;
        
        private readonly List<KifomeOrderListUI> indicators = new List<KifomeOrderListUI>();
        private string renderedOrderId;
        private GameManager gameManager;
        private OrderManager orderManager;
        private Canvas preparationCanvas;
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
            cookPlayer = player;
            gameManager = match;
            orderManager = gameManager != null ? gameManager.Orders : null;
            preparationCanvas ??= GetComponentInParent<Canvas>();

            List<string> missing = new List<string>();
            if (cookPlayer == null)
                missing.Add("Cook PlayerController (GameManager role binding)");
            else if (cookPlayer.AssignedRole != PlayerRole.Cook)
                missing.Add("PlayerController assigned to CookPreparationUI must have role Cook");
            if (gameManager == null) missing.Add("GameManager");
            if (orderManager == null) missing.Add("OrderManager (GameManager.Orders)");
            if (recipeNameText == null) missing.Add("recipeNameText (Inspector)");
            if (ingredientContent == null) missing.Add("ingredientContent (Inspector)");
            if (ingredientIndicatorPrefab == null) missing.Add("ingredientIndicatorPrefab (Inspector)");
            if (preparationCanvas == null) missing.Add("parent Canvas (CookPanel)");

            if (missing.Count > 0)
            {
                Debug.LogError($"CookPreparationUI cannot initialize. Missing or invalid: {string.Join(", ", missing)}.", this);
                initialized = false;
                ClearIndicators();
                return;
            }

            Camera cookCamera = cookPlayer.GetCamera();
            if (cookCamera == null)
            {
                Debug.LogError("CookPreparationUI cannot bind because the Cook PlayerController has no Camera.", this);
                initialized = false;
                return;
            }

            preparationCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            preparationCanvas.worldCamera = cookCamera;
            preparationCanvas.planeDistance = 0.02f;
            preparationCanvas.overrideSorting = true;
            preparationCanvas.sortingOrder = 100;

            initialized = true;
            Subscribe();
            Refresh();
        }

        private void Subscribe()
        {
            if (cookPlayer != null)
            {
                cookPlayer.PreparationChanged -= Refresh;
                cookPlayer.PreparationChanged += Refresh;
            }

            if (orderManager != null)
            {
                orderManager.OrdersChanged -= Refresh;
                orderManager.OrdersChanged += Refresh;
            }
        }

        private void Unsubscribe()
        {
            if (cookPlayer != null)
                cookPlayer.PreparationChanged -= Refresh;
            if (orderManager != null)
                orderManager.OrdersChanged -= Refresh;
        }

        private void Refresh()
        {
            if (!initialized || cookPlayer == null || orderManager == null)
                return;

            if (string.IsNullOrWhiteSpace(cookPlayer.SelectedOrderId) ||
                !orderManager.TryGetOrder(cookPlayer.SelectedOrderId, out OrderRecord order) ||
                order.State != OrderState.InPreparation)
            {
                ClearIndicators();
                SetIngredientsVisible(true);
                bool carryingMealbox = cookPlayer.CarriedPreparedMealbox != null;
                string status = carryingMealbox
                    ? "Mealbox ready — take it to the counter."
                    : string.IsNullOrWhiteSpace(cookPlayer.PreparationMessage)
                        ? "Select an order on the phone to begin."
                        : cookPlayer.PreparationMessage;
                SetRecipeAndStatus(string.Empty, status);
                return;
            }

            if (!cookPlayer.IsPreparationPresentationVisible)
            {
                ClearIndicators();
                SetIngredientsVisible(false);
                SetRecipeAndStatus(order.DisplayName,
                    $"Order {OrderNumberFormatter.Format(order.OrderId)} is still in preparation. Its contents were preserved.");
               return;
            }

            SetIngredientsVisible(true);
            if (!string.Equals(renderedOrderId, order.OrderId, StringComparison.OrdinalIgnoreCase) ||
                indicators.Count != order.RequiredIngredientIds.Count)
            {
                RebuildIndicators(order);
            }

            var actualCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string actualId in cookPlayer.CurrentIngredientIds)
            {
                if (!string.IsNullOrWhiteSpace(actualId))
                {
                    string normalizedActualId = IngredientStation.NormalizeLegacyId(actualId);
                    actualCounts.TryGetValue(normalizedActualId, out int count);
                    actualCounts[normalizedActualId] = count + 1;
                }
            }

            var requiredCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool exactContents = cookPlayer.CurrentIngredientIds.Count == order.RequiredIngredientIds.Count;
            for (int i = 0; i < order.RequiredIngredientIds.Count; i++)
            {
                string requiredId = order.RequiredIngredientIds[i];
                string normalizedRequiredId = IngredientStation.NormalizeLegacyId(requiredId);
                requiredCounts.TryGetValue(normalizedRequiredId, out int occurrence);
                requiredCounts[normalizedRequiredId] = occurrence + 1;
                actualCounts.TryGetValue(normalizedRequiredId, out int actualCount);
                bool placed = actualCount > occurrence;
                IngredientData ingredient = i < order.RequiredIngredients.Count
                    ? order.RequiredIngredients[i]
                    : null;
                indicators[i].SetState(ingredient, requiredId, placed);
                exactContents &= placed;
            }

            foreach (KeyValuePair<string, int> actual in actualCounts)
            {
                requiredCounts.TryGetValue(actual.Key, out int requiredCount);
                exactContents &= actual.Value == requiredCount;
            }

            MealboxPhysical physicalMealbox = cookPlayer.PreparationMealbox;
            bool correctLunchbox = physicalMealbox != null && !physicalMealbox.IsClosed &&
                                   physicalMealbox.Type == order.LunchboxType &&
                                   cookPlayer.SelectedLunchboxType == order.LunchboxType;
            bool carryingOtherMealbox = cookPlayer.CarriedPreparedMealbox != null;
            bool preparationComplete = exactContents && correctLunchbox && !carryingOtherMealbox;
            string completionError = cookPlayer.PreparationMessage;
            if (string.IsNullOrWhiteSpace(completionError) ||
                !completionError.StartsWith("Could not", StringComparison.OrdinalIgnoreCase))
                completionError = null;
            SetRecipeAndStatus(order.DisplayName, preparationComplete
                ? completionError ?? "Recipe complete — closing the mealbox."
                : correctLunchbox && exactContents && carryingOtherMealbox
                    ? "Recipe complete. Deposit the mealbox you are carrying first."
                : !correctLunchbox
                    ? $"Collect the {GetCompartmentCount(order.LunchboxType)}-compartment mealbox for order {OrderNumberFormatter.Format(order.OrderId)} from its station."
                    : $"Order {OrderNumberFormatter.Format(order.OrderId)}: ingredients are missing or extra.");
        }

        private static int GetCompartmentCount(LunchboxType type)
        {
            switch (type)
            {
                case LunchboxType.OneCompartment: return 1;
                case LunchboxType.TwoCompartments: return 2;
                case LunchboxType.ThreeCompartments: return 3;
                case LunchboxType.FourCompartments: return 4;
                case LunchboxType.FiveCompartments: return 5;
                default: return 0;
            }
        }

        private void RebuildIndicators(OrderRecord order)
        {
            ClearIndicators();
            renderedOrderId = order.OrderId;
            for (int i = 0; i < order.RequiredIngredientIds.Count; i++)
            {
                KifomeOrderListUI indicator = Instantiate(ingredientIndicatorPrefab, ingredientContent);
                if (indicator != null)
                    indicators.Add(indicator);
            }
        }

        private void ClearIndicators()
        {
            for (int i = indicators.Count - 1; i >= 0; i--)
            {
                if (indicators[i] != null)
                    Destroy(indicators[i].gameObject);
            }

            indicators.Clear();
            renderedOrderId = null;
        }

        private void SetRecipeAndStatus(string recipeName, string status)
        {
            if (recipeNameText != null)
            {
                recipeNameText.text = recipeName;
            }
            else
            {
                recipeNameText.text = string.IsNullOrWhiteSpace(recipeName)
                    ? status
                    : $"{recipeName}\n{status}";
            }
        }
        

        private void SetIngredientsVisible(bool visible)
        {
            if (ingredientContent != transform && !transform.IsChildOf(ingredientContent) &&
                ingredientContent.gameObject.activeSelf != visible)
            {
                ingredientContent.gameObject.SetActive(visible);
            }
        }
    }
}
