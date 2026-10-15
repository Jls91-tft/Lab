namespace CepaCero.World
{
    public enum DamageResult : byte
    {
        /// <summary>Out of bounds, air, or indestructible.</summary>
        None = 0,
        Damaged = 1,
        Destroyed = 2,
    }
}
