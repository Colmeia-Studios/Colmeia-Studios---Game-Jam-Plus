using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public sealed class LunchboxStation : Interactable
    {
        [SerializeField] private LunchboxType lunchboxType = LunchboxType.TwoCompartments;
        [SerializeField] private LunchboxPrefabCatalog prefabCatalog;
        [SerializeField] private GameObject lockIndicator;

        public LunchboxType Type => lunchboxType;
        public bool HasConfiguredPrefab
        {
            get
            {
                if (prefabCatalog == null || !prefabCatalog.HasCompleteConfiguration) return false;
                GameObject prefab = prefabCatalog != null ? prefabCatalog.GetEmptyPrefab(lunchboxType) : null;
                MealboxPhysical physical = prefab != null ? prefab.GetComponent<MealboxPhysical>() : null;
                return physical != null && physical.HasCompartmentPoints(lunchboxType);
            }
        }
        private bool IsConfigured => System.Enum.IsDefined(typeof(LunchboxType), lunchboxType);
        private bool HasUsableLockIndicator => lockIndicator != null && lockIndicator != gameObject;
        private bool IsUnlocked => IsConfigured && GameManager.Instance != null &&
                                   GameManager.Instance.MatchConfiguration != null &&
                                   GameManager.Instance.MatchConfiguration.IsLunchboxUnlocked(
                                       lunchboxType, Mathf.Max(1, GameManager.Instance.CurrentDay));

        public override string GetInteractionText()
        {
            if (!IsConfigured) return "Mealbox station has no valid type.";
            if (!IsUnlocked) return $"The {CompartmentCount()}-compartment mealbox is locked.";
            return $"Interact to collect the {CompartmentCount()}-compartment mealbox.";
        }

        public override bool CanInteract(PlayerController player)
        {
            return base.CanInteract(player) && player.AssignedRole == PlayerRole.Cook &&
                   IsConfigured;
        }

        public override void Interact(PlayerController player)
        {
            if (player == null || player.AssignedRole != PlayerRole.Cook) return;
            if (!IsConfigured)
            {
                player.ShowGameplayMessage("This mealbox station is configured incorrectly.");
                return;
            }
            if (GameManager.Instance == null)
            {
                player.ShowGameplayMessage("Match configuration is unavailable, so this mealbox cannot be validated.");
                return;
            }
            if (GameManager.Instance.MatchConfiguration == null)
            {
                Debug.LogError($"LunchboxStation '{name}' cannot validate unlocks because GameManager has no MatchConfig.", this);
                player.ShowGameplayMessage("Match unlock configuration is missing.");
                return;
            }
            if (!IsUnlocked)
            {
                player.ShowGameplayMessage("This mealbox type is still locked.");
                return;
            }
            if (string.IsNullOrWhiteSpace(player.SelectedOrderId) ||
                !GameManager.Instance.TryGetOrder(player.SelectedOrderId, out OrderRecord order) ||
                order.State != OrderState.InPreparation)
            {
                player.ShowGameplayMessage("Select an order on the phone before collecting a mealbox.");
                return;
            }
            if (order.LunchboxType != lunchboxType || player.SelectedLunchboxType != lunchboxType)
            {
                player.ShowGameplayMessage($"Order #{order.OrderId} requires a {CompartmentCount(order.LunchboxType)}-compartment mealbox.");
                return;
            }
            if (player.PreparationMealbox != null)
            {
                player.ShowGameplayMessage("You already collected this order’s mealbox.");
                return;
            }
            if (prefabCatalog == null)
            {
                Debug.LogError($"LunchboxStation '{name}' não tem LunchboxPrefabCatalog atribuído.", this);
                player.ShowGameplayMessage("The mealbox catalog is not assigned to this station.");
                return;
            }
            if (!prefabCatalog.HasCompleteConfiguration)
            {
                Debug.LogError($"LunchboxPrefabCatalog '{prefabCatalog.name}' está incompleto. Configure os cinco modelos vazios e os dois fechados.", prefabCatalog);
                player.ShowGameplayMessage("The mealbox catalog is incomplete. Check all seven prefabs in the Inspector.");
                return;
            }

            GameObject prefab = prefabCatalog.GetEmptyPrefab(lunchboxType);
            if (prefab == null)
            {
                Debug.LogError($"LunchboxPrefabCatalog '{prefabCatalog.name}' não tem prefab vazio para {lunchboxType}.", prefabCatalog);
                player.ShowGameplayMessage("No empty mealbox is configured for this station.");
                return;
            }

            GameObject instance = Instantiate(prefab, transform.position, transform.rotation);
            if (instance == null) return;
            MealboxPhysical mealbox = instance.GetComponent<MealboxPhysical>();
            if (mealbox == null)
            {
                Debug.LogError($"Prefab vazio '{prefab.name}' precisa conter MealboxPhysical e cinco slots configurados.", prefab);
                Destroy(instance);
                player.ShowGameplayMessage("The empty mealbox prefab has no compartment points assigned.");
                return;
            }
            if (!mealbox.HasCompartmentPoints(order.LunchboxType))
            {
                Debug.LogError($"Empty prefab '{prefab.name}' has no configured compartment points for {order.LunchboxType}.", prefab);
                Destroy(instance);
                player.ShowGameplayMessage("The empty mealbox has no compartment placement points.");
                return;
            }

            mealbox.ConfigureEmptyForOrder(order.OrderId, order.DisplayName, order.LunchboxType);
            if (!player.TryTakeEmptyMealbox(mealbox, order, out string result))
            {
                Destroy(instance);
                player.ShowGameplayMessage(result);
                return;
            }

            player.ShowGameplayMessage($"Selected the {CompartmentCount()}-compartment mealbox.");
            AudioManager.Instance?.PlaySfx(AudioCue.EmptyMealboxPickup);
        }

        private void Awake()
        {
            if (!IsConfigured)
                Debug.LogError($"LunchboxStation '{name}' tem um LunchboxType inválido.", this);
            if (prefabCatalog == null)
                Debug.LogError($"LunchboxStation '{name}' precisa de um LunchboxPrefabCatalog.", this);
            if (lockIndicator == gameObject)
            {
                Debug.LogError($"LunchboxStation '{name}': Lock Indicator aponta para a própria estação. O indicador será ignorado para manter a estação ativa.", this);
                lockIndicator = null;
            }
            if (HasUsableLockIndicator) lockIndicator.SetActive(!IsUnlocked);
        }

        private void Update()
        {
            if (HasUsableLockIndicator)
                lockIndicator.SetActive(!IsUnlocked);
        }

        private int CompartmentCount() => CompartmentCount(lunchboxType);

        private static int CompartmentCount(LunchboxType type)
        {
            switch (type)
            {
                case LunchboxType.OneCompartment: return 1;
                case LunchboxType.TwoCompartments: return 2;
                case LunchboxType.ThreeCompartments: return 3;
                case LunchboxType.FourCompartments: return 4;
                case LunchboxType.FiveCompartments: return 5;
                default: return 0;
            }
        }
    }
}
