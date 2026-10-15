namespace CepaCero.Net
{
    /// <summary>Outcome of a validated intent. Sent back to the requesting client so its UI can react.</summary>
    public enum IntentResult : byte
    {
        Ok = 0,
        OutOfReach = 1,
        TooSoon = 2,
        Occupied = 3,
        InvalidTarget = 4,
        NotAllowed = 5,
        PlayerDowned = 6,
        Missed = 7,
        NoAmmo = 8,
        NoRoom = 9,
        MissingIngredients = 10,
        NeedsWorkbench = 11,
        Locked = 12,
        NothingToUse = 13,
    }
}
