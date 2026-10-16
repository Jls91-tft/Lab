namespace CepaCero.Rules
{
    /// <summary>How much building costs (D-022): the normal price, half of it, or nothing (creative).</summary>
    public enum BuildCostMode : byte
    {
        Normal = 0,
        Cheap = 1,
        Free = 2,
    }
}
