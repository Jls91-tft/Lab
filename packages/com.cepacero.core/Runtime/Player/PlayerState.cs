using CepaCero.Util;

namespace CepaCero.Player
{
    /// <summary>Authoritative state of one player (host), predicted locally by its owner.</summary>
    public struct PlayerState
    {
        public Float2 Position;

        /// <summary>Level the player is on: 0 is the street, above are floors, below is underground (D-022).</summary>
        public int Level;
        public float Stamina;
        public bool IsSprinting;
        public int Health;

        /// <summary>Hunger and thirst bars, 0 (empty) to 100 (full). See SurvivalRules (D-015).</summary>
        public float Food;
        public float Water;

        /// <summary>Seconds spent on the ground (bleeds out at GameSettings.BleedOutSeconds).</summary>
        public float DownedSeconds;

        /// <summary>Seconds a teammate has been helping this player up.</summary>
        public float ReviveProgress;

        /// <summary>Bled out (or died alone): waits for the next dawn to respawn.</summary>
        public bool IsDead;

        /// <summary>Out of action: on the ground or dead. Cannot move or act; zombies ignore them.</summary>
        public bool IsDowned => Health <= 0 || IsDead;

        public static PlayerState Spawn(Float2 position, PlayerSettings settings) => new PlayerState
        {
            Position = position,
            Stamina = settings.MaxStamina,
            Health = settings.MaxHealth,
            Food = 100f,
            Water = 100f,
        };
    }
}
