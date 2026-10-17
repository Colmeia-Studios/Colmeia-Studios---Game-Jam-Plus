using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    [CreateAssetMenu(menuName = "Só Mais Um Pedido/Configuração de Jogador", fileName = "PlayerConfig_")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("Default role")]
        [SerializeField] private PlayerRole defaultRole;

        [Header("Input")]
        [SerializeField] private string actionMap = "PlayerOne";
        [SerializeField] private string controlScheme = "KeyboardMouse";
        [SerializeField] private bool useMouseLook = true;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField] private float gravity = -20f;
        [SerializeField, Min(0f)] private float lookSensitivity = 2.2f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        public PlayerRole DefaultRole => defaultRole;
        public string ActionMap => actionMap;
        public string ControlScheme => controlScheme;
        public bool UseMouseLook => useMouseLook;
        public float MoveSpeed => moveSpeed;
        public float Gravity => gravity;
        public float LookSensitivity => lookSensitivity;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            lookSensitivity = Mathf.Max(0f, lookSensitivity);
            minPitch = Mathf.Clamp(minPitch, -89f, 0f);
            maxPitch = Mathf.Clamp(maxPitch, 0f, 89f);
            if (minPitch > maxPitch)
            {
                minPitch = maxPitch;
            }
        }
    }
}
