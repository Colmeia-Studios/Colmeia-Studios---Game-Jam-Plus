using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class HouseDeliveryInteractable : Interactable
    {
        [SerializeField] private HouseData houseData;
        [SerializeField] private Collider interactionCollider;

        public HouseData Data => houseData;
        public Collider InteractionCollider => interactionCollider;

        public override bool CanInteract(PlayerController player)
        {
            return base.CanInteract(player) &&
                   player != null &&
                   player.AssignedRole == PlayerRole.Delivery &&
                   houseData != null &&
                   !string.IsNullOrWhiteSpace(houseData.Id) &&
                   interactionCollider != null &&
                   interactionCollider.enabled &&
                   interactionCollider.gameObject.activeInHierarchy;
        }

        public override bool CanInteractFromCollider(PlayerController player, Collider hitCollider)
        {
            return hitCollider != null && hitCollider == interactionCollider && CanInteract(player);
        }

        public override string GetInteractionText()
        {
            return houseData != null
                ? $"Interagir com a porta de {houseData.CustomerName}."
                : "Interagir com a porta.";
        }

        public override void Interact(PlayerController player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            if (houseData == null)
            {
                Debug.LogError($"HouseData is not assigned on {name}; delivery was not completed.", this);
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.Orders == null)
            {
                Debug.LogError("GameManager/OrderManager is unavailable; delivery was not completed.", this);
                return;
            }

            string orderId = player.SelectedDeliveryOrderId;
            if (string.IsNullOrWhiteSpace(orderId) || !player.HasOrderInInventory(orderId))
            {
                player.ShowGameplayMessage(
                    "A marmita correta não está selecionada na mochila. Nenhum pedido foi removido.");
                return;
            }

            if (!GameManager.Instance.TryGetOrder(orderId, out OrderRecord order))
            {
                player.ShowGameplayMessage("O pedido selecionado não é mais válido.");
                return;
            }

            if (!string.Equals(order.DestinationHouseId, houseData.Id, System.StringComparison.OrdinalIgnoreCase))
            {
                player.ShowGameplayMessage("Este pedido não é para esta residência. Nenhuma marmita foi removida.");
                return;
            }

            if (GameManager.Instance.Orders.TryDeliverToHouse(orderId, houseData, player))
            {
                player.ShowGameplayMessage($"Pedido #{orderId} entregue com sucesso.");
            }
            else
            {
                player.ShowGameplayMessage("A entrega não pôde ser concluída. A marmita permanece na mochila.");
            }
        }
    }
}