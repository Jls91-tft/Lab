using System;

namespace CepaCero.Player
{
    /// <summary>Player tuning. Speeds are the references used by docs/bestiario.md.</summary>
    [Serializable]
    public sealed class PlayerSettings
    {
        public int MaxHealth = 100;
        public float WalkSpeed = 3.5f;
        public float SprintSpeed = 5.5f;
        public float Radius = 0.35f;

        public float MaxStamina = 100f;
        public float SprintCostPerSecond = 20f;
        public float StaminaRegenPerSecond = 12f;

        /// <summary>Stamina needed to start sprinting again (avoids sprint flicker at 0).</summary>
        public float StaminaToStartSprint = 15f;

        /// <summary>Noise radius (tiles) of footsteps. Zombies add their hearing (docs/bestiario.md).</summary>
        public float WalkNoise = 0f;
        public float SprintNoise = 4f;

        /// <summary>Distance (tiles, from the player's centre to the tile's centre) for building and hitting blocks.</summary>
        public float Reach = 2.5f;

        // Bare hands (docs/bestiario.md: "Puño: 8"). Weapons and tools override these.
        public float MeleeRange = 1.3f;
        public float MeleeArcDegrees = 100f;
        public int MeleeDamage = 8;
        public string MeleeDamageType = "blunt";
        public int BlockHitDamage = 8;

        /// <summary>Health lost per level fallen (D-022).</summary>
        public int FallDamagePerLevel = 15;

        /// <summary>Seconds between two level changes by stairs or trapdoor.</summary>
        public float ClimbCooldown = 0.4f;

        public float PickUpReach = 1.5f;
        public float ReviveReach = 1.5f;

        /// <summary>Health a revived player gets back.</summary>
        public int ReviveHealth = 30;

        /// <summary>Minimum seconds between two actions of the same player (host-side rate limit).</summary>
        public float ActionCooldown = 0.35f;

        public float MeleeNoise = 6f;
    }
}
