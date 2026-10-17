using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class KifomeOrderListUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text ingredientText;
        [SerializeField] private Image stateIcon;
        [SerializeField] private Toggle stateToggle;
        [SerializeField] private Color pendingColor = new Color(0.72f, 0.62f, 0.48f);
        [SerializeField] private Color completedColor = new Color(0.22f, 0.62f, 0.3f);

        private void Awake()
        {
            stateIcon ??= GetComponentInChildren<Image>(true);

            if (stateIcon == null)
            {
                Debug.LogError("KifomeOrderListUI requires an Image to display its ingredient Toggle.", this);
                enabled = false;
                return;
            }

            stateToggle ??= stateIcon.GetComponent<Toggle>();
            stateToggle ??= stateIcon.gameObject.AddComponent<Toggle>();
            stateToggle.targetGraphic = stateIcon;
            stateToggle.graphic = stateIcon;
            stateToggle.interactable = false;
        }

        public void SetState(IngredientData ingredient, string fallbackName, bool placed)
        {
            if (ingredientText != null)
            {
                ingredientText.text = ingredient != null && !string.IsNullOrWhiteSpace(ingredient.DisplayName)
                    ? ingredient.DisplayName
                    : fallbackName;
            }

            if (stateIcon != null)
            {
                stateIcon.color = placed ? completedColor : pendingColor;
            }

            if (stateToggle != null)
            {
                stateToggle.interactable = false;
                stateToggle.SetIsOnWithoutNotify(placed);
            }
        }
    }
}