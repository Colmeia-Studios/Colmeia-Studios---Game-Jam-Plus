using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    [CreateAssetMenu(menuName = "Só Mais Um Pedido/Melhoria", fileName = "Upgrade_")]
    public sealed class UpgradeData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0f)] private float price;
        [SerializeField] private PlayerRole role;
        [SerializeField, Min(1f)] private float movementSpeedMultiplier = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public float Price => price;
        public PlayerRole Role => role;
        public float MovementSpeedMultiplier => movementSpeedMultiplier;

        private void OnValidate()
        {
            id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            price = float.IsNaN(price) || float.IsInfinity(price) ? 0f : Mathf.Max(0f, price);
            movementSpeedMultiplier = float.IsNaN(movementSpeedMultiplier) ||
                                      float.IsInfinity(movementSpeedMultiplier)
                ? 1f
                : Mathf.Max(1f, movementSpeedMultiplier);
        }
    }
}
