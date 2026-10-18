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
            if (!IsConfigured) return "Estação de marmita sem tipo válido.";
            if (!IsUnlocked) return $"Marmita de {CompartmentCount()} compartimento(s) bloqueada.";
            return $"Interagir para pegar a marmita de {CompartmentCount()} compartimento(s).";
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
                player.ShowGameplayMessage("Esta estação de marmita está configurada incorretamente.");
                return;
            }
            if (GameManager.Instance == null)
            {
                player.ShowGameplayMessage("A configuração da partida não está disponível para validar esta marmita.");
                return;
            }
            if (GameManager.Instance.MatchConfiguration == null)
            {
                Debug.LogError($"LunchboxStation '{name}' cannot validate unlocks because GameManager has no MatchConfig.", this);
                player.ShowGameplayMessage("A configuração de desbloqueios da partida está ausente.");
                return;
            }
            if (!IsUnlocked)
            {
                player.ShowGameplayMessage("Esse tipo de marmita ainda está bloqueado.");
                return;
            }
            if (string.IsNullOrWhiteSpace(player.SelectedOrderId) ||
                !GameManager.Instance.TryGetOrder(player.SelectedOrderId, out OrderRecord order) ||
                order.State != OrderState.InPreparation)
            {
                player.ShowGameplayMessage("Selecione um pedido no telefone antes de pegar uma marmita.");
                return;
            }
            if (order.LunchboxType != lunchboxType || player.SelectedLunchboxType != lunchboxType)
            {
                player.ShowGameplayMessage($"O pedido #{order.OrderId} exige uma marmita de {CompartmentCount(order.LunchboxType)} compartimento(s).");
                return;
            }
            if (player.PreparationMealbox != null)
            {
                player.ShowGameplayMessage("Você já pegou a marmita deste pedido.");
                return;
            }
            if (prefabCatalog == null)
            {
                Debug.LogError($"LunchboxStation '{name}' não tem LunchboxPrefabCatalog atribuído.", this);
                player.ShowGameplayMessage("A estação está sem o catálogo de marmitas configurado.");
                return;
            }
            if (!prefabCatalog.HasCompleteConfiguration)
            {
                Debug.LogError($"LunchboxPrefabCatalog '{prefabCatalog.name}' está incompleto. Configure os cinco modelos vazios e os dois fechados.", prefabCatalog);
                player.ShowGameplayMessage("O catálogo de marmitas está incompleto. Confira os sete prefabs no Inspector.");
                return;
            }

            GameObject prefab = prefabCatalog.GetEmptyPrefab(lunchboxType);
            if (prefab == null)
            {
                Debug.LogError($"LunchboxPrefabCatalog '{prefabCatalog.name}' não tem prefab vazio para {lunchboxType}.", prefabCatalog);
                player.ShowGameplayMessage("Não há uma marmita vazia configurada para esta estação.");
                return;
            }

            GameObject instance = Instantiate(prefab, transform.position, transform.rotation);
            if (instance == null) return;
            MealboxPhysical mealbox = instance.GetComponent<MealboxPhysical>();
            if (mealbox == null)
            {
                Debug.LogError($"Prefab vazio '{prefab.name}' precisa conter MealboxPhysical e cinco slots configurados.", prefab);
                Destroy(instance);
                player.ShowGameplayMessage("O prefab da marmita vazia está sem os pontos de compartimento configurados.");
                return;
            }
            if (!mealbox.HasCompartmentPoints(order.LunchboxType))
            {
                Debug.LogError($"Empty prefab '{prefab.name}' has no configured compartment points for {order.LunchboxType}.", prefab);
                Destroy(instance);
                player.ShowGameplayMessage("A marmita vazia está sem os pontos de posicionamento dos compartimentos.");
                return;
            }

            mealbox.ConfigureEmptyForOrder(order.OrderId, order.DisplayName, order.LunchboxType);
            if (!player.TryTakeEmptyMealbox(mealbox, order, out string result))
            {
                Destroy(instance);
                player.ShowGameplayMessage(result);
                return;
            }

            player.ShowGameplayMessage($"Marmita de {CompartmentCount()} compartimento(s) selecionada.");
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
