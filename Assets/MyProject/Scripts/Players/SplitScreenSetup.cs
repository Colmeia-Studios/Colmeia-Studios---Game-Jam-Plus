using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public enum ScreenSide
    {
        Left,
        Right
    }

    public class SplitScreenSetup : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private ScreenSide screenSide = ScreenSide.Left;

        private void Reset()
        {
            targetCamera = GetComponent<Camera>();
        }

        private void Start()
        {
            ApplySplit();
        }

        public void ApplySplit()
        {
            if (targetCamera == null)
            {
                return;
            }

            if (screenSide == ScreenSide.Left)
            {
                targetCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
            }
            else
            {
                targetCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            }
        }
    }
}
