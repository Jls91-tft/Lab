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

        // Client → host
        Input = 20,
        Intent = 21,
    }
}
