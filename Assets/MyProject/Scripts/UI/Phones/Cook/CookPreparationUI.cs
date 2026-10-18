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
        [SerializeField] private TMP_Text preparationStatusText;
        [SerializeField] private Renderer preparationMealboxRenderer;
        [SerializeField] private Color incompleteColor = new Color(0.75f, 0.35f, 0.18f);
        [SerializeField] private Color completeColor = new Color(0.22f, 0.62f, 0.3f);

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

            if (preparationStatusText == null)
            {
                Debug.LogWarning(
                    "CookPreparationUI has no preparationStatusText. Recipe name and status will share recipeNameText; assign a separate TMP_Text to show them independently.",
                    this);
            }

            if (preparationMealboxRenderer == null)
            {
                Debug.LogWarning("CookPreparationUI has no preparationMealboxRenderer; mealbox progress color will not be shown.", this);
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
                    ? "Marmita pronta — leve ao balcão."
                    : string.IsNullOrWhiteSpace(cookPlayer.PreparationMessage)
                        ? "Select an Order."
                        : cookPlayer.PreparationMessage;
                SetRecipeAndStatus(string.Empty, status);
                SetMealboxColor(carryingMealbox ? completeColor : incompleteColor);
                return;
            }

            if (!cookPlayer.IsPreparationPresentationVisible)
            {
                ClearIndicators();
                SetIngredientsVisible(false);
                SetRecipeAndStatus(order.DisplayName,
                    $"A preparação do pedido #{order.OrderId} continua. O conteúdo foi preservado.");
                SetMealboxColor(incompleteColor);
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
                    actualCounts.TryGetValue(actualId.Trim(), out int count);
                    actualCounts[actualId.Trim()] = count + 1;
                }
            }

            var requiredCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool exactContents = cookPlayer.CurrentIngredientIds.Count == order.RequiredIngredientIds.Count;
            for (int i = 0; i < order.RequiredIngredientIds.Count; i++)
            {
                string requiredId = order.RequiredIngredientIds[i];
                requiredCounts.TryGetValue(requiredId, out int occurrence);
                requiredCounts[requiredId] = occurrence + 1;
                actualCounts.TryGetValue(requiredId, out int actualCount);
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

            bool correctLunchbox = cookPlayer.SelectedLunchboxType == order.LunchboxType;
            bool preparationComplete = exactContents && correctLunchbox;
            SetMealboxColor(preparationComplete ? completeColor : incompleteColor);
            SetRecipeAndStatus(order.DisplayName, preparationComplete
                ? "Receita correta — concluindo a marmita."
                : !correctLunchbox
                    ? $"Selecione a marmita correta para o pedido #{order.OrderId}."
                    : $"Preparação do pedido #{order.OrderId}: ingredientes pendentes ou extras.");
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
            if (preparationStatusText != null)
            {
                recipeNameText.text = recipeName;
                preparationStatusText.text = status;
            }
            else
            {
                recipeNameText.text = string.IsNullOrWhiteSpace(recipeName)
                    ? status
                    : $"{recipeName}\n{status}";
            }
        }

        private void SetMealboxColor(Color color)
        {
            if (preparationMealboxRenderer != null)
                preparationMealboxRenderer.material.color = color;
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
