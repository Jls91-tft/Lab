using CepaCero.Util;

namespace CepaCero.Player
{
    /// <summary>Authoritative state of one player (host), predicted locally by its owner.</summary>
    public struct PlayerState
    {
        public Float2 Position;
        public float Stamina;
        public bool IsSprinting;
        public int Health;

        public bool IsDowned => Health <= 0;

        public static PlayerState Spawn(Float2 position, PlayerSettings settings) => new PlayerState
        {
            Position = position,
            Stamina = settings.MaxStamina,
            Health = settings.MaxHealth,
        };
    }
}
