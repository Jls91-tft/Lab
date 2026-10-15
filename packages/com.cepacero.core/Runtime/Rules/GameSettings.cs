using System;

namespace CepaCero.Rules
{
    /// <summary>
    /// Match options chosen in the lobby ("opciones de partida configurables").
    /// The host owns them and sends them to clients when they join. Public
    /// fields so Unity's JsonUtility can save presets.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        /// <summary>Fixed simulation ticks per second.</summary>
        public int TickRate = 20;

        public int DaySeconds = 480;
        public int DuskSeconds = 60;
        public int NightSeconds = 300;
        public int DawnSeconds = 60;

        /// <summary>Zombies on the first night.</summary>
        public int HordeBase = 12;

        /// <summary>Multiplier per night: night n has HordeBase × HordeGrowth^(n−1) zombies.</summary>
        public float HordeGrowth = 1.35f;

        /// <summary>Upper bound for one night's horde.</summary>
        public int HordeMax = 400;

        /// <summary>Most zombies alive at once (performance and readability cap).</summary>
        public int MaxAliveZombies = 150;

        /// <summary>First night with special zombies.</summary>
        public int SpecialsFromNight = 2;

        /// <summary>Share of specials in a horde once they appear.</summary>
        public float SpecialShare = 0.08f;

        /// <summary>Nights on which a miniboss appears (every N nights). 0 disables.</summary>
        public int BossEveryNights = 5;

        /// <summary>Share of the night (from its start) during which the horde keeps arriving.</summary>
        public float SpawnWindow = 0.6f;

        public ulong Seed = 1;

        // ---- Options decided in D-012 -------------------------------------
        /// <summary>Players can hurt each other.</summary>
        public bool FriendlyFire = false;

        /// <summary>Empty map containers refill at dawn.</summary>
        public bool LootRespawn = false;

        /// <summary>Seconds a downed player can wait for help before dying.</summary>
        public float BleedOutSeconds = 30f;

        /// <summary>Seconds a teammate must keep helping to get someone up.</summary>
        public float ReviveSeconds = 3f;

        /// <summary>If everyone is down or dead at once, the match ends (otherwise all respawn at dawn).</summary>
        public bool PermaDeath = false;

        public void Validate()
        {
            if (TickRate < 1 || TickRate > 120)
            {
                throw new ArgumentOutOfRangeException(nameof(TickRate));
            }
            if (DaySeconds < 1 || DuskSeconds < 0 || NightSeconds < 1 || DawnSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(DaySeconds), "Phase durations must be positive.");
            }
            if (HordeBase < 0 || HordeGrowth < 1f || HordeMax < HordeBase || MaxAliveZombies < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(HordeGrowth), "Invalid horde settings.");
            }
            if (SpecialShare < 0f || SpecialShare > 1f || SpawnWindow <= 0f || SpawnWindow > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(SpecialShare));
            }
            if (BleedOutSeconds <= 0f || ReviveSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(BleedOutSeconds));
            }
        }
    }
}
