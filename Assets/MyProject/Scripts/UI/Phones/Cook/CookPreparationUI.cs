using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class CookPreparationUI : MonoBehaviour
    {
        [SerializeField] private PlayerController cook;
        [SerializeField] private Transform ingredientContent;
        [SerializeField] private KifomeOrderListUI ingredientIndicatorPrefab;
        [SerializeField] private TMP_Text recipeNameText;
        [SerializeField] private TMP_Text preparationStatusText;
        [SerializeField] private Renderer preparationMealboxRenderer;
        [SerializeField] private Color incompleteColor = new Color(0.75f, 0.35f, 0.18f);
        [SerializeField] private Color completeColor = new Color(0.22f, 0.62f, 0.3f);

        private readonly List<KifomeOrderListUI> indicators = new List<KifomeOrderListUI>();
        private string renderedOrderId;
        private bool initialized;

        private void OnEnable()
        {
            if (initialized)
            {
                Subscribe();
                Refresh();
            }
        }

        private void Start()
        {
            if (cook == null)
            {
                Debug.LogError("CookPreparationUI requires its cook PlayerController to be assigned.", this);
                enabled = false;
                return;
            }

            if (ingredientContent == null || ingredientIndicatorPrefab == null || recipeNameText == null ||
                preparationMealboxRenderer == null)
            {
                Debug.LogError(
                    "CookPreparationUI requires Ingredient Content, an indicator prefab, the recipe name text, and the preparation mealbox Renderer.",
                    this);
                enabled = false;
                return;
            }

            Subscribe();
            initialized = true;
            Refresh();
        }

        private void OnDisable()
        {
            if (!initialized)
            {
                return;
            }

            Unsubscribe();
        }

        private void Subscribe()
        {
            if (cook != null)
            {
                cook.PreparationChanged -= Refresh;
                cook.PreparationChanged += Refresh;
            }

            if (GameManager.Instance != null && GameManager.Instance.Orders != null)
            {
                GameManager.Instance.Orders.OrdersChanged -= Refresh;
                GameManager.Instance.Orders.OrdersChanged += Refresh;
            }
        }

        private void Unsubscribe()
        {
            if (cook != null)
            {
                cook.PreparationChanged -= Refresh;
            }

            if (GameManager.Instance != null && GameManager.Instance.Orders != null)
            {
                GameManager.Instance.Orders.OrdersChanged -= Refresh;
            }
        }

        private void Refresh()
        {
            if (cook == null || ingredientContent == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(cook.SelectedOrderId) ||
                GameManager.Instance == null ||
                !GameManager.Instance.TryGetOrder(cook.SelectedOrderId, out OrderRecord order) ||
                order.State != OrderState.InPreparation)
            {
                recipeNameText.text = string.Empty;
                SetIngredientsVisible(true);
                ClearIndicators();
                bool preparationCompleted = !string.IsNullOrWhiteSpace(cook.PreparationMessage) &&
                                            cook.PreparationMessage.IndexOf(
                                                "pronto", StringComparison.OrdinalIgnoreCase) >= 0;
                SetStatus(string.IsNullOrWhiteSpace(cook.PreparationMessage)
                    ? "Selecione um pedido disponível no telefone."
                    : cook.PreparationMessage);
                SetMealboxColor(preparationCompleted ? completeColor : incompleteColor);
                return;
            }

            recipeNameText.text = order.DisplayName;

            if (!cook.IsPreparationPresentationVisible)
            {
                ClearIndicators();
                SetIngredientsVisible(false);
                SetStatus($"A preparação do pedido #{order.OrderId} continua. O conteúdo foi preservado.");
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
            foreach (string actualId in cook.CurrentIngredientIds)
            {
                if (!string.IsNullOrWhiteSpace(actualId))
                {
                    actualCounts.TryGetValue(actualId.Trim(), out int count);
                    actualCounts[actualId.Trim()] = count + 1;
                }
            }

            var requiredCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool exactContents = cook.CurrentIngredientIds.Count == order.RequiredIngredientIds.Count;
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

            bool correctLunchbox = cook.SelectedLunchboxType == order.LunchboxType;
            bool preparationComplete = exactContents && correctLunchbox;
            SetMealboxColor(preparationComplete ? completeColor : incompleteColor);
            SetStatus(preparationComplete
                ? "Receita correta — conclusão automática em andamento."
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
                indicators.Add(Instantiate(ingredientIndicatorPrefab, ingredientContent));
            }
        }

        private void ClearIndicators()
        {
            for (int i = indicators.Count - 1; i >= 0; i--)
            {
                if (indicators[i] != null)
                {
                    Destroy(indicators[i].gameObject);
                }
            }

            indicators.Clear();
            renderedOrderId = null;
        }

        private void SetMealboxColor(Color color)
        {
            if (preparationMealboxRenderer != null)
            {
                preparationMealboxRenderer.material.color = color;
            }
        }

        private void SetStatus(string message)
        {
            if (preparationStatusText != null)
            {
                preparationStatusText.text = message;
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