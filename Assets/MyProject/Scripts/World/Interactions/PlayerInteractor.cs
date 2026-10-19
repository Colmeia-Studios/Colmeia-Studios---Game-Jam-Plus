using UnityEngine;
using TMPro;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private string promptText = "";
        [SerializeField] private TMP_Text promptDisplay;
        [SerializeField] private TMP_Text gameplayMessageDisplay;

        private Interactable currentInteractable;

        private void Reset()
        {
            playerController = GetComponentInParent<PlayerController>();
            interactionCamera = GetComponentInChildren<Camera>();
        }

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponentInParent<PlayerController>();
            }

            if (interactionCamera == null)
            {
                interactionCamera = playerController != null ? playerController.GetCamera() : null;
            }
        }

        private void OnEnable()
        {
            if (playerController != null)
            {
                playerController.GameplayMessageChanged += RefreshGameplayMessage;
                RefreshGameplayMessage();
            }
        }

        private void OnDisable()
        {
            if (playerController != null)
            {
                playerController.GameplayMessageChanged -= RefreshGameplayMessage;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            UpdateCurrentInteractable();
        }

        private void UpdateCurrentInteractable()
        {
            if (playerController == null || interactionCamera == null || playerController.IsPhoneOpen)
            {
                currentInteractable = null;
                promptText = string.Empty;
                if (promptDisplay != null)
                {
                    promptDisplay.text = string.Empty;
                    promptDisplay.gameObject.SetActive(false);
                }

                if (gameplayMessageDisplay != null)
                {
                    gameplayMessageDisplay.gameObject.SetActive(false);
                }

                return;
            }

            RefreshGameplayMessage();
            Ray ray = new Ray(interactionCamera.transform.position, interactionCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionRange))
            {
                Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
                if (interactable != null && interactable.CanInteractFromCollider(playerController, hit.collider))
                {
                    currentInteractable = interactable;
                    promptText = interactable.GetInteractionText();
                    SetPrompt(promptText);
                    return;
                }
            }

            currentInteractable = null;
            promptText = string.Empty;
            SetPrompt(string.Empty);
            RefreshGameplayMessage();
        }

        public void TryInteract()
        {
            if (playerController == null || playerController.IsPhoneOpen || currentInteractable == null)
            {
                return;
            }

            currentInteractable.Interact(playerController);
        }

        public string GetPromptText()
        {
            return playerController != null && playerController.IsPhoneOpen ? string.Empty : promptText;
        }

        private void SetPrompt(string value)
        {
            if (promptDisplay == null)
            {
                return;
            }

            promptDisplay.text = value;
            promptDisplay.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }

        private void RefreshGameplayMessage()
        {
            if (gameplayMessageDisplay == null)
            {
                return;
            }

            bool visible = playerController != null && !playerController.IsPhoneOpen &&
                           !string.IsNullOrEmpty(playerController.GameplayMessage);
            gameplayMessageDisplay.text = visible ? playerController.GameplayMessage : string.Empty;
            gameplayMessageDisplay.gameObject.SetActive(visible);
        }
    }
}
