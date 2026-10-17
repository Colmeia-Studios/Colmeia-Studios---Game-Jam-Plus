using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class MealboxPhysical : MonoBehaviour
    {
        [SerializeField] private string orderId;
        [SerializeField] private string orderDisplayName;

        public string OrderId => orderId;
        public string OrderDisplayName => orderDisplayName;

        public void Configure(string id, string displayName)
        {
            orderId = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            orderDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            gameObject.name = string.IsNullOrEmpty(orderId) ? "Mealbox" : $"Mealbox_{orderId}";
        }
    }
}
