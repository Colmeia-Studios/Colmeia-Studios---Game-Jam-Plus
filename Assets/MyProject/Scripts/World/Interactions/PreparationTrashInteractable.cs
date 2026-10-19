using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class PreparationTrashInteractable : Interactable
    {
        public override bool CanInteract(PlayerController player)
        {
            return base.CanInteract(player) &&
                   player.AssignedRole == PlayerRole.Cook &&
                   player.HasActivePreparation;
        }

        public override string GetInteractionText()
        {
            return "Interact to clear the mealbox in preparation.";
        }

        public override void Interact(PlayerController player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            player.ClearPreparationContents();
        }
    }
}