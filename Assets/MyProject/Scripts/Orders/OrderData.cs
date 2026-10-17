using System.Collections.Generic;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    [CreateAssetMenu(
        menuName = "Só Mais Um Pedido/Pedido",
        fileName = "Order_")]
    public sealed class OrderData : ScriptableObject
    {
        [Header("Identificação")] [SerializeField]
        private string id;

        [SerializeField] private string displayName;

        [Header("Configuração da marmita")] [SerializeField]
        private LunchboxType lunchboxType;

        [SerializeField] private List<IngredientData> requiredIngredients =
            new List<IngredientData>();

        public string Id => id;
        public string DisplayName => displayName;
        public LunchboxType LunchboxType => lunchboxType;

        public IReadOnlyList<IngredientData> RequiredIngredients =>
            requiredIngredients;

        private void OnValidate()
        {
            id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? string.Empty
                : displayName.Trim();

            requiredIngredients ??= new List<IngredientData>();
        }

        public float CalculateBaseValue()
        {
            float total = 0f;

            if (requiredIngredients == null)
                return total;

            foreach (IngredientData ingredient in requiredIngredients)
            {
                if (ingredient != null)
                    total += ingredient.Value;
            }

            return total;
        }
    }
}