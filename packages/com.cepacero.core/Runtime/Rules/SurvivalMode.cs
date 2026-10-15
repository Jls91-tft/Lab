namespace CepaCero.Rules
{
    /// <summary>How much hunger and thirst matter (D-015).</summary>
    public enum SurvivalMode : byte
    {
        /// <summary>No bars: food only heals a little.</summary>
        Off = 0,

        /// <summary>Empty bars weaken you but never kill you.</summary>
        Soft = 1,

        /// <summary>Empty bars weaken you and slowly drain your health (default).</summary>
        Hard = 2,
    }
}
