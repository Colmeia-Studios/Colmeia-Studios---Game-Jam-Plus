using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Role selection")]
        [SerializeField] private Button cookButton;
        [SerializeField] private Button deliveryButton;
        [SerializeField] private Image cookSelectionImage;
        [SerializeField] private Image deliverySelectionImage;

        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = "Gameplay";

        private bool playerOneIsCook = true;

        private void Awake()
        {
            if (cookButton != null)
            {
                cookButton.onClick.AddListener(SelectCook);
            }

            if (deliveryButton != null)
            {
                deliveryButton.onClick.AddListener(SelectDelivery);
            }

            UpdateSelectionVisuals();
        }

        public void SelectCook()
        {
            playerOneIsCook = true;
            UpdateSelectionVisuals();
        }

        public void SelectDelivery()
        {
            playerOneIsCook = false;
            UpdateSelectionVisuals();
        }

        public void StartGame()
        {
            if (playerOneIsCook)
            {
                MatchSettings.SetRoles(PlayerRole.Cook, PlayerRole.Delivery);
            }
            else
            {
                MatchSettings.SetRoles(PlayerRole.Delivery, PlayerRole.Cook);
            }

            SceneManager.LoadScene(gameplaySceneName);
        }

        private void UpdateSelectionVisuals()
        {
            if (cookSelectionImage != null)
            {
                cookSelectionImage.enabled = playerOneIsCook;
            }

            if (deliverySelectionImage != null)
            {
                deliverySelectionImage.enabled = !playerOneIsCook;
            }
        }
    }
}
