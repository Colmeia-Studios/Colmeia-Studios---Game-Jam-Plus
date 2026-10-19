using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class KifomeChecklistItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text ingredientText;
        [SerializeField] private Image stateIcon;
        private bool hasState;
        private bool previousComplete;

        public void Configure(TMP_Text label, Image icon)
        {
            ingredientText = label;
            stateIcon = icon;
        }

        public void SetState(string label, bool complete, bool invalid = false)
        {
            if (hasState && !previousComplete && complete && !invalid)
                AudioManager.Instance?.PlaySfx(AudioCue.ChecklistComplete);
            hasState = true;
            previousComplete = complete;
            if (ingredientText != null)
            {
                ingredientText.text = label;
                ingredientText.color = invalid
                    ? new Color(0.65f, 0.08f, 0.08f)
                    : complete ? new Color(0.12f, 0.42f, 0.2f) : new Color(0.29f, 0.22f, 0.17f);
            }

            if (stateIcon != null)
            {
                stateIcon.color = invalid
                    ? new Color(0.75f, 0.16f, 0.12f)
                    : complete ? new Color(0.22f, 0.62f, 0.3f) : new Color(0.72f, 0.62f, 0.48f);
            }
        }
    }
}
