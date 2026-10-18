using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class IngredientStation : Interactable
    {
        [SerializeField] private IngredientData ingredientData;
        [Header("Bloqueio e apresentação opcional")]
        [SerializeField] private GameObject lockIndicator;
        [SerializeField] private Transform lidOrDrawer;
        [SerializeField] private Vector3 openLocalOffset;
        [SerializeField] private Vector3 openLocalEuler;
        [SerializeField, Min(0.1f)] private float movementSpeed = 5f;
        [SerializeField, Min(0.1f)] private float openDurationSeconds = 1f;

        private Vector3 closedPosition;
        private Quaternion closedRotation;
        private bool isOpen;
        private float closeAtTime;
        private bool lastUnlocked;
        private bool HasValidIngredient => ingredientData != null &&
                                           !string.IsNullOrWhiteSpace(ingredientData.Id);

        public IngredientData Data => ingredientData;
        public string IngredientId => HasValidIngredient ? ingredientData.Id.Trim() : string.Empty;
        public string DisplayName => HasValidIngredient && !string.IsNullOrWhiteSpace(ingredientData.DisplayName)
            ? ingredientData.DisplayName
            : "Ingrediente sem configuração";
        public Color IngredientColor => HasValidIngredient ? ingredientData.Color : Color.white;

        private bool IsUnlocked => HasValidIngredient && GameManager.Instance != null &&
                                   GameManager.Instance.IsIngredientUnlocked(ingredientData);

        public override string GetInteractionText()
        {
            if (!HasValidIngredient) return "Estação de ingrediente sem configuração.";
            return IsUnlocked ? $"Interagir para adicionar {DisplayName}." : $"{DisplayName} bloqueado.";
        }

        public override bool CanInteract(PlayerController player)
        {
            return base.CanInteract(player) && player.AssignedRole == PlayerRole.Cook &&
                   HasValidIngredient;
        }

        public override void Interact(PlayerController player)
        {
            if (player == null || player.AssignedRole != PlayerRole.Cook) return;
            if (!HasValidIngredient)
            {
                Debug.LogError($"IngredientStation '{name}' está sem IngredientData ou com ID vazio.", this);
                player.ShowGameplayMessage("Esta estação de ingrediente está sem configuração.");
                return;
            }
            if (GameManager.Instance == null)
            {
                player.ShowGameplayMessage("A configuração da partida não está disponível para validar este ingrediente.");
                return;
            }
            if (!IsUnlocked)
            {
                player.ShowGameplayMessage($"{DisplayName} ainda está bloqueado.");
                return;
            }
            if (string.IsNullOrWhiteSpace(player.SelectedOrderId))
            {
                player.ShowGameplayMessage("Selecione um pedido no telefone antes de adicionar ingredientes.");
                return;
            }

            bool added = player.TryAddIngredient(ingredientData);
            if (added)
            {
                isOpen = true;
                closeAtTime = Time.time + openDurationSeconds;
            }
        }

        private void Awake()
        {
            if (!HasValidIngredient)
                Debug.LogError($"IngredientStation '{name}' precisa de um IngredientData válido com ID configurado.", this);
            else if (ingredientData.VisualPrefab == null)
                Debug.LogWarning($"IngredientStation '{name}' usa '{ingredientData.name}', mas o campo Visual Prefab está vazio; o ingrediente será coletado sem representação 3D.", this);

            if (lidOrDrawer != null)
            {
                closedPosition = lidOrDrawer.localPosition;
                closedRotation = lidOrDrawer.localRotation;
            }
            lastUnlocked = IsUnlocked;
            RefreshLockVisual();
        }

        private void Update()
        {
            bool unlocked = IsUnlocked;
            if (unlocked != lastUnlocked) RefreshLockVisual();
            if (isOpen && Time.time >= closeAtTime) isOpen = false;
            if (lidOrDrawer == null) return;

            Vector3 targetPosition = closedPosition + (isOpen ? openLocalOffset : Vector3.zero);
            Quaternion targetRotation = closedRotation * Quaternion.Euler(isOpen ? openLocalEuler : Vector3.zero);
            lidOrDrawer.localPosition = Vector3.Lerp(lidOrDrawer.localPosition, targetPosition, Time.deltaTime * movementSpeed);
            lidOrDrawer.localRotation = Quaternion.Slerp(lidOrDrawer.localRotation, targetRotation, Time.deltaTime * movementSpeed);
        }

        private void RefreshLockVisual()
        {
            lastUnlocked = IsUnlocked;
            if (lockIndicator != null) lockIndicator.SetActive(!lastUnlocked);
        }

        public static string NormalizeLegacyId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;

            switch (id.Trim().ToLowerInvariant())
            {
                case "arroz": case "arooz": case "rice": case "01": case "1": return "rice";
                case "feijão": case "feijao": case "bean": case "02": case "2": return "bean";
                case "carne": case "meat": case "03": case "3": return "meat";
                case "frango": case "chicken": case "04": case "4": return "chicken";
                case "batata": case "potato": case "05": case "5": return "potato";
                default: return id.Trim();
            }
        }
    }
}
