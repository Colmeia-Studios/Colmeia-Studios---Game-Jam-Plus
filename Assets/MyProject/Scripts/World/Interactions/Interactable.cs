using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] protected string interactionText = "Press E to interact.";
        [SerializeField] protected bool canInteract = true;

        public virtual bool CanInteract(PlayerController player)
        {
            return canInteract && player != null;
        }

        public virtual bool CanInteractFromCollider(PlayerController player, Collider hitCollider)
        {
            return CanInteract(player);
        }

        public virtual string GetInteractionText()
        {
            return interactionText;
        }

        public abstract void Interact(PlayerController player);
    }
}
