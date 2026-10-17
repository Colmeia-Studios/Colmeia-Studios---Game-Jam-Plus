using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    [CreateAssetMenu(menuName = "Só Mais Um Pedido/Residência", fileName = "House_")]
    public sealed class HouseData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string customerName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Sprite houseImage;

        public string Id => id;
        public string CustomerName => customerName;
        public string Description => description;
        public Sprite HouseImage => houseImage;

        private void OnValidate()
        {
            id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            customerName = string.IsNullOrWhiteSpace(customerName) ? string.Empty : customerName.Trim();
            description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        }
    }
}
