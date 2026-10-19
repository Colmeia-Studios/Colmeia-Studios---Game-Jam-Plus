using UnityEngine;
using UnityEngine.UI;

namespace ColmeiaStudios.SoMaisUmPedido
{
    /// <summary>Use on dynamically spawned button prefabs; static scene buttons are bound by AudioManager.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class ButtonClickAudio : MonoBehaviour
    {
        private Button button;
        private void Awake() => button = GetComponent<Button>();
        private void OnEnable() => AudioManager.Instance?.RegisterButton(button != null ? button : GetComponent<Button>());
    }
}
