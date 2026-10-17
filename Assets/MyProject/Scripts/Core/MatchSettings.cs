using UnityEngine;

namespace ColmeiaStudios.SoMaisUmPedido
{
    public enum PlayerRole
    {
        Cook,
        Delivery
    }

    public static class MatchSettings
    {
        public static PlayerRole PlayerOneRole { get; private set; } = PlayerRole.Cook;
        public static PlayerRole PlayerTwoRole { get; private set; } = PlayerRole.Delivery;

        public static void SetRoles(PlayerRole playerOneRole, PlayerRole playerTwoRole)
        {
            PlayerOneRole = playerOneRole;
            PlayerTwoRole = playerTwoRole;
        }

        public static void ResetToDefault()
        {
            SetRoles(PlayerRole.Cook, PlayerRole.Delivery);
        }
    }
}
