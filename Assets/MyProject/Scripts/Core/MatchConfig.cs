using System.Collections.Generic;
using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    [CreateAssetMenu(menuName = "Só Mais Um Pedido/Configuração da Partida", fileName = "MatchConfig")]
    public sealed class MatchConfig : ScriptableObject
    {
        [Header("Player slots (input ownership remains attached to each slot)")] [SerializeField]
        private PlayerConfig playerOne;

        [SerializeField] private PlayerConfig playerTwo;

        [Header("Orders")] [SerializeField] private List<OrderData> orderCatalog = new List<OrderData>();
        [SerializeField] private List<HouseData> houseCatalog = new List<HouseData>();
        [SerializeField, Range(4, 6)] private int maximumActiveOrders = 5;
        [SerializeField, Min(1f)] private float orderDeadlineSeconds = 120f;

        [Header("Day")] [SerializeField, Range(0, 23)]
        private int startHour = 11;

        [SerializeField, Range(1, 24)] private int endHour = 19;
        [SerializeField, Min(0.1f)] private float realSecondsPerGameHour = 40f;
        [SerializeField, Min(0f)] private float startingBalance = 500f;
        [SerializeField, Range(0, 5)] private int startingReputation = 5;
        [SerializeField, Min(1)] private int dailyDeliveryGoal = 7;

        public PlayerConfig PlayerOne => playerOne;
        public PlayerConfig PlayerTwo => playerTwo;
        public IReadOnlyList<OrderData> OrderCatalog => orderCatalog;
        public IReadOnlyList<HouseData> HouseCatalog => houseCatalog;
        public int MaximumActiveOrders => maximumActiveOrders;
        public float OrderDeadlineSeconds => orderDeadlineSeconds;
        public int StartHour => startHour;
        public int EndHour => endHour;
        public float RealSecondsPerGameHour => realSecondsPerGameHour;
        public float StartingBalance => startingBalance;
        public int StartingReputation => startingReputation;
        public int DailyDeliveryGoal => dailyDeliveryGoal;

        private void OnValidate()
        {
            orderCatalog ??= new List<OrderData>();
            houseCatalog ??= new List<HouseData>();
            maximumActiveOrders = Mathf.Clamp(maximumActiveOrders, 4, 6);
            startHour = Mathf.Clamp(startHour, 0, 23);
            endHour = Mathf.Clamp(endHour, startHour + 1, 24);
            orderDeadlineSeconds = float.IsNaN(orderDeadlineSeconds) || float.IsInfinity(orderDeadlineSeconds)
                ? 120f
                : Mathf.Max(1f, orderDeadlineSeconds);
            realSecondsPerGameHour = float.IsNaN(realSecondsPerGameHour) || float.IsInfinity(realSecondsPerGameHour)
                ? 40f
                : Mathf.Max(0.1f, realSecondsPerGameHour);
            startingBalance = float.IsNaN(startingBalance) || float.IsInfinity(startingBalance)
                ? 500f
                : Mathf.Max(0f, startingBalance);
            startingReputation = Mathf.Clamp(startingReputation, 0, 5);
            dailyDeliveryGoal = Mathf.Max(1, dailyDeliveryGoal);
        }
    }
}