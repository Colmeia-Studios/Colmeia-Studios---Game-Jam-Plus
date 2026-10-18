using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    [Serializable]
    public sealed class IngredientUnlockRule
    {
        [SerializeField] private IngredientData ingredient;
        [SerializeField, Min(1)] private int availableFromDay = 1;

        public IngredientData Ingredient => ingredient;
        public int AvailableFromDay => availableFromDay;
    }

    [Serializable]
    public sealed class LunchboxUnlockRule
    {
        [SerializeField] private LunchboxType lunchboxType;
        [SerializeField, Min(1)] private int availableFromDay = 1;

        public LunchboxType LunchboxType => lunchboxType;
        public int AvailableFromDay => availableFromDay;
    }

    [CreateAssetMenu(menuName = "Só Mais Um Pedido/Configuração da Partida", fileName = "MatchConfig")]
    public sealed class MatchConfig : ScriptableObject
    {
        [Header("Player slots (input ownership remains attached to each slot)")] [SerializeField]
        private PlayerConfig playerOne;

        [SerializeField] private PlayerConfig playerTwo;

        [Header("Orders")] [SerializeField] private List<OrderData> orderCatalog = new List<OrderData>();
        [SerializeField] private List<HouseData> houseCatalog = new List<HouseData>();
        [Tooltip("Mantido para compatibilidade dos assets. A regra da Etapa 2 usa 4 pedidos aguardando e 1 em preparação.")]
        [SerializeField, Range(4, 6)] private int maximumActiveOrders = 5;
        [SerializeField, Min(0.1f)] private float orderGenerationIntervalSeconds = 8f;
        [SerializeField, Min(1f)] private float orderDeadlineSeconds = 120f;

        [Header("Progression: availability by day")]
        [SerializeField] private List<IngredientUnlockRule> ingredientUnlocks = new List<IngredientUnlockRule>();
        [SerializeField] private List<LunchboxUnlockRule> lunchboxUnlocks = new List<LunchboxUnlockRule>();

        [Header("Day")] [SerializeField, Range(0, 23)]
        private int startHour = 11;

        [SerializeField, Range(1, 24)] private int endHour = 19;
        [SerializeField, Min(0.1f)] private float realSecondsPerGameHour = 40f;
        [SerializeField, Min(0f)] private float nextDayTransitionSeconds = 5f;
        [SerializeField, Min(0f)] private float startingBalance = 500f;
        [SerializeField, Range(0, 5)] private int startingReputation = 5;
        [SerializeField, Min(1)] private int dailyDeliveryGoal = 7;

        public PlayerConfig PlayerOne => playerOne;
        public PlayerConfig PlayerTwo => playerTwo;
        public IReadOnlyList<OrderData> OrderCatalog => orderCatalog;
        public IReadOnlyList<HouseData> HouseCatalog => houseCatalog;
        public int MaximumActiveOrders => maximumActiveOrders;
        public float OrderGenerationIntervalSeconds => orderGenerationIntervalSeconds;
        public float OrderDeadlineSeconds => orderDeadlineSeconds;
        public IReadOnlyList<IngredientUnlockRule> IngredientUnlocks => ingredientUnlocks;
        public IReadOnlyList<LunchboxUnlockRule> LunchboxUnlocks => lunchboxUnlocks;
        public int StartHour => startHour;
        public int EndHour => endHour;
        public float RealSecondsPerGameHour => realSecondsPerGameHour;
        public float NextDayTransitionSeconds => nextDayTransitionSeconds;
        public float StartingBalance => startingBalance;
        public int StartingReputation => startingReputation;
        public int DailyDeliveryGoal => dailyDeliveryGoal;

        private void OnValidate()
        {
            orderCatalog ??= new List<OrderData>();
            houseCatalog ??= new List<HouseData>();
            ingredientUnlocks ??= new List<IngredientUnlockRule>();
            lunchboxUnlocks ??= new List<LunchboxUnlockRule>();
            maximumActiveOrders = Mathf.Clamp(maximumActiveOrders, 4, 6);
            orderGenerationIntervalSeconds = IsFinite(orderGenerationIntervalSeconds)
                ? Mathf.Max(0.1f, orderGenerationIntervalSeconds)
                : 8f;
            startHour = Mathf.Clamp(startHour, 0, 23);
            endHour = Mathf.Clamp(endHour, startHour + 1, 24);
            orderDeadlineSeconds = float.IsNaN(orderDeadlineSeconds) || float.IsInfinity(orderDeadlineSeconds)
                ? 120f
                : Mathf.Max(1f, orderDeadlineSeconds);
            realSecondsPerGameHour = float.IsNaN(realSecondsPerGameHour) || float.IsInfinity(realSecondsPerGameHour)
                ? 40f
                : Mathf.Max(0.1f, realSecondsPerGameHour);
            nextDayTransitionSeconds = IsFinite(nextDayTransitionSeconds)
                ? Mathf.Max(0f, nextDayTransitionSeconds)
                : 5f;
            startingBalance = float.IsNaN(startingBalance) || float.IsInfinity(startingBalance)
                ? 500f
                : Mathf.Max(0f, startingBalance);
            startingReputation = Mathf.Clamp(startingReputation, 0, 5);
            dailyDeliveryGoal = Mathf.Max(1, dailyDeliveryGoal);
        }

        public bool IsIngredientUnlocked(IngredientData ingredient, int day)
        {
            if (ingredient == null || ingredientUnlocks == null)
                return false;

            foreach (IngredientUnlockRule rule in ingredientUnlocks)
            {
                if (rule != null && rule.Ingredient == ingredient && rule.AvailableFromDay <= day)
                    return true;
            }

            return false;
        }

        public bool IsIngredientIdUnlocked(string ingredientId, int day)
        {
            if (string.IsNullOrWhiteSpace(ingredientId) || ingredientUnlocks == null)
                return false;

            foreach (IngredientUnlockRule rule in ingredientUnlocks)
            {
                if (rule != null && rule.Ingredient != null &&
                    string.Equals(IngredientStation.NormalizeLegacyId(rule.Ingredient.Id),
                        IngredientStation.NormalizeLegacyId(ingredientId), StringComparison.OrdinalIgnoreCase) &&
                    rule.AvailableFromDay <= day)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsLunchboxUnlocked(LunchboxType type, int day)
        {
            if (!Enum.IsDefined(typeof(LunchboxType), type) || lunchboxUnlocks == null)
                return false;

            foreach (LunchboxUnlockRule rule in lunchboxUnlocks)
            {
                if (rule != null && rule.LunchboxType == type && rule.AvailableFromDay <= day)
                    return true;
            }

            return false;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
