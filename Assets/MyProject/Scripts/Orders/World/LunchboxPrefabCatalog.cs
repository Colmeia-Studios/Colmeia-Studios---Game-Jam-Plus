using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    [CreateAssetMenu(menuName = "Só Mais Um Pedido/Prefabs de Marmitas", fileName = "LunchboxPrefabCatalog")]
    public sealed class LunchboxPrefabCatalog : ScriptableObject
    {
        [Header("Marmitas vazias")]
        [SerializeField] private GameObject emptyOneCompartment;
        [SerializeField] private GameObject emptyTwoCompartments;
        [SerializeField] private GameObject emptyThreeCompartments;
        [SerializeField] private GameObject emptyFourCompartments;
        [SerializeField] private GameObject emptyFiveCompartments;
        [Header("Marmitas fechadas")]
        [SerializeField] private GameObject closedMealboxA;
        [SerializeField] private GameObject closedMealboxB;

        public GameObject GetEmptyPrefab(LunchboxType type)
        {
            switch (type)
            {
                case LunchboxType.OneCompartment: return emptyOneCompartment;
                case LunchboxType.TwoCompartments: return emptyTwoCompartments;
                case LunchboxType.ThreeCompartments: return emptyThreeCompartments;
                case LunchboxType.FourCompartments: return emptyFourCompartments;
                case LunchboxType.FiveCompartments: return emptyFiveCompartments;
                default: return null;
            }
        }

        public GameObject ChooseClosedPrefab()
        {
            if (!HasAllClosedPrefabs) return null;
            return Random.Range(0, 2) == 0 ? closedMealboxA : closedMealboxB;
        }

        public bool HasAllClosedPrefabs => closedMealboxA != null && closedMealboxB != null;
        public bool HasAllEmptyPrefabs => emptyOneCompartment != null && emptyTwoCompartments != null &&
                                          emptyThreeCompartments != null && emptyFourCompartments != null &&
                                          emptyFiveCompartments != null;
        public bool HasCompleteConfiguration => HasAllEmptyPrefabs && HasAllClosedPrefabs;

        private void OnValidate()
        {
            ValidateEmpty(LunchboxType.OneCompartment, emptyOneCompartment);
            ValidateEmpty(LunchboxType.TwoCompartments, emptyTwoCompartments);
            ValidateEmpty(LunchboxType.ThreeCompartments, emptyThreeCompartments);
            ValidateEmpty(LunchboxType.FourCompartments, emptyFourCompartments);
            ValidateEmpty(LunchboxType.FiveCompartments, emptyFiveCompartments);

            if (closedMealboxA == null || closedMealboxB == null)
                Debug.LogWarning($"{name}: configure os dois prefabs de marmita fechada para habilitar a seleção aleatória.", this);
        }

        private void ValidateEmpty(LunchboxType type, GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"{name}: falta o prefab vazio para {type}.", this);
                return;
            }
            MealboxPhysical physical = prefab.GetComponent<MealboxPhysical>();
            if (physical == null || !physical.HasCompartmentPoints(type))
                Debug.LogWarning($"Prefab '{prefab.name}' precisa de MealboxPhysical com pontos suficientes para {type}.", prefab);
        }
    }
}
