using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public class IngredientStation : Interactable
    {
        [SerializeField] private IngredientData ingredientData;

        // Retained until the Editor migration has copied the current scene values to assets.
        [SerializeField] private string ingredientId = "rice";
        [SerializeField] private string displayName = "Arroz";
        [SerializeField] private Color ingredientColor = Color.white;

        public IngredientData Data => ingredientData;
        public string IngredientId => ingredientData != null ? ingredientData.Id : NormalizeLegacyId(ingredientId);
        public string DisplayName => ingredientData != null ? ingredientData.DisplayName : displayName;
        public Color IngredientColor => ingredientData != null ? ingredientData.Color : ingredientColor;

        public override string GetInteractionText()
        {
            return $"Interagir para adicionar {DisplayName}.";
        }

        public override void Interact(PlayerController player)
        {
            if (player == null || player.AssignedRole != PlayerRole.Cook)
            {
                return;
            }

            if (string.IsNullOrEmpty(player.SelectedOrderId))
            {
                player.ShowGameplayMessage("Selecione um pedido no telefone antes de adicionar ingredientes.");
                return;
            }

            bool added = ingredientData != null
                ? player.TryAddIngredient(ingredientData)
                : player.TryAddIngredient(ingredientId, displayName);
            if (added)
            {
                Debug.Log($"{player.name} adicionou {DisplayName} na preparação.");
            }
            else
            {
                Debug.LogWarning($"{player.name} não conseguiu adicionar {DisplayName}.");
            }
        }

        private void OnValidate()
        {
            if (ingredientData != null)
            {
                ingredientId = ingredientData.Id;
                displayName = ingredientData.DisplayName;
                ingredientColor = ingredientData.Color;
            }
        }

        public static string NormalizeLegacyId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return string.Empty;
            }

            switch (id.Trim().ToLowerInvariant())
            {
                case "arroz":
                case "arooz":
                case "rice":
                    return "rice";
                case "feijão":
                case "feijao":
                case "bean":
                    return "bean";
                case "carne":
                case "meat":
                    return "meat";
                case "frango":
                case "chicken":
                    return "chicken";
                default:
                    return id.Trim();
            }
        }
    }
}
