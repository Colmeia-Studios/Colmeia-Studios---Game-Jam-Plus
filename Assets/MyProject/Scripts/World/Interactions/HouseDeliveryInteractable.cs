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
                ? $"Interact with {houseData.CustomerName}’s door."
                : "Interact with the door.";
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
                    "The correct mealbox is not selected in the backpack. No order was removed.");
                return;
            }

            if (!GameManager.Instance.TryGetOrder(orderId, out OrderRecord order))
            {
                player.ShowGameplayMessage("The selected order is no longer valid.");
                return;
            }

            if (!string.Equals(order.DestinationHouseId, houseData.Id, System.StringComparison.OrdinalIgnoreCase))
            {
                player.ShowGameplayMessage("This order is not for this house. No mealbox was removed.");
                return;
            }

            if (GameManager.Instance.Orders.TryDeliverToHouse(orderId, houseData, player))
            {
                player.ShowGameplayMessage($"Order #{orderId} delivered successfully.");
            }
            else
            {
                player.ShowGameplayMessage("Delivery could not be completed. The mealbox remains in the backpack.");
            }
        }
    }
}