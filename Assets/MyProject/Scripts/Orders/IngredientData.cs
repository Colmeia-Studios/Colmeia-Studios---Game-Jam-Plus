using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    [CreateAssetMenu(menuName = "Só Mais Um Pedido/Ingrediente", fileName = "Ingredient_")]
    public sealed class IngredientData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, Min(0f)] private float value;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private Sprite icon;

        public string Id => id;
        public string DisplayName => displayName;
        public float Value => value;
        public Color Color => color;
        public Sprite Icon => icon;

        private void OnValidate()
        {
            id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            value = Mathf.Max(0f, value);
        }
    }
}
