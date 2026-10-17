using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Menu options")]
        [SerializeField] private Button howToPlayButton;
        [SerializeField] private Button creditsButton;
        [Header("Scene")]
        [SerializeField] private string characterSelectionSceneName = "CharacterSelection";

        private void Awake()
        {
            if (howToPlayButton != null) howToPlayButton.interactable = false;
            if (creditsButton != null) creditsButton.interactable = false;
        }

        // Kept as the existing Start button callback in MainMenu.unity.
        public void StartGame()
        {
            SceneManager.LoadScene(characterSelectionSceneName);
        }
    }
}
