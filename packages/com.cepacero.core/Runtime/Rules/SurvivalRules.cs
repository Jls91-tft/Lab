using System;
using CepaCero.Player;

namespace CepaCero.Rules
{
    /// <summary>
    /// Hunger and thirst (D-015). Pure functions over <see cref="PlayerState"/>,
    /// run by the host every tick; the owner gets the bars in a Vitals message.
    /// </summary>
    public static class SurvivalRules
    {
        /// <summary>A full bar.</summary>
        public const float Full = 100f;

        /// <summary>
        /// Lowers the bars by one tick and returns the health the player loses on
        /// this tick (Hard mode, empty bars). Downed and dead players do not get hungrier.
        /// </summary>
        public static int Tick(ref PlayerState state, GameSettings settings, long tick)
        {
            if (settings.Survival == SurvivalMode.Off || state.IsDowned)
            {
                return 0;
            }
            float dt = 1f / settings.TickRate;
            state.Food = Math.Max(0f, state.Food - DrainPerSecond(settings.FoodLastsDays, settings) * dt);
            state.Water = Math.Max(0f, state.Water - DrainPerSecond(settings.WaterLastsDays, settings) * dt);
            if (settings.Survival != SurvivalMode.Hard)
            {
                return 0;
            }
            int damage = 0;
            if (state.Food <= 0f && IsDue(tick, settings.StarvingSecondsPerHealth, settings.TickRate))
            {
                damage++;
            }
            if (state.Water <= 0f && IsDue(tick, settings.ThirstSecondsPerHealth, settings.TickRate))
            {
                damage++;
            }
            return damage;
        }

        /// <summary>Health cap: an empty food bar lowers it (Soft and Hard).</summary>
        public static int MaxHealth(in PlayerState state, GameSettings settings, PlayerSettings player)
        {
            if (settings == null || settings.Survival == SurvivalMode.Off || state.Food > 0f)
            {
                return player.MaxHealth;
            }
            return Math.Max(1, (int)Math.Round(player.MaxHealth * settings.HungryMaxHealthFactor, MidpointRounding.AwayFromZero));
        }

        /// <summary>Stamina regeneration multiplier: an empty water bar slows it (Soft and Hard).</summary>
        public static float StaminaRegenFactor(in PlayerState state, GameSettings settings)
        {
            if (settings == null || settings.Survival == SurvivalMode.Off || state.Water > 0f)
            {
                return 1f;
            }
            return settings.ThirstyStaminaRegenFactor;
        }

        /// <summary>Bar points lost per second for a bar that lasts <paramref name="days"/> full day cycles.</summary>
        public static float DrainPerSecond(float days, GameSettings settings)
        {
            float cycleSeconds = settings.DaySeconds + settings.DuskSeconds + settings.NightSeconds + settings.DawnSeconds;
            return days <= 0f ? 0f : Full / (days * cycleSeconds);
        }

        private static bool IsDue(long tick, float seconds, int tickRate)
        {
            long every = Math.Max(1L, (long)Math.Round(seconds * tickRate, MidpointRounding.AwayFromZero));
            return tick % every == 0;
        }
    }
}
