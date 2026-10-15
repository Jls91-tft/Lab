namespace CepaCero.Rules
{
    public enum DayPhase : byte
    {
        /// <summary>Loot and explore.</summary>
        Day = 0,

        /// <summary>Last chance to fortify; the light fades.</summary>
        Dusk = 1,

        /// <summary>The horde arrives.</summary>
        Night = 2,

        /// <summary>Survivors are counted, loot is shared, repairs.</summary>
        Dawn = 3,
    }
}
