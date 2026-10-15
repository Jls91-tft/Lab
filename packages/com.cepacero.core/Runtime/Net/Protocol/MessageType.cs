namespace CepaCero.Net.Protocol
{
    /// <summary>
    /// Every message of the game protocol. Each one travels as
    /// [type (byte)][payload length (ushort)][payload], so a client can skip
    /// types it does not know. FishNet only carries the bytes.
    /// </summary>
    public enum MessageType : byte
    {
        // Host → client
        Welcome = 1,
        ChunkSnapshot = 2,
        BlockDeltas = 3,
        PlayerStates = 4,
        ZombieSnapshot = 5,
        ZombieEvents = 6,
        SessionEvents = 7,
        Clock = 8,
        Inventory = 9,
        ContainerContents = 10,
        GroundPiles = 11,

        /// <summary>What the group has unlocked: analysed samples (recipes) and diary entries.</summary>
        TeamProgress = 12,

        /// <summary>Cells of every container (map cupboards and player chests), so clients can show and open them.</summary>
        ContainerList = 13,

        /// <summary>The receiving player's own food and water bars (D-015), sent when they change by 1 %.</summary>
        Vitals = 14,

        // Client → host
        Input = 20,
        Intent = 21,
    }
}
