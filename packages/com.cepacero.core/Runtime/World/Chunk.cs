namespace CepaCero.World
{
    /// <summary>
    /// A square of <see cref="Size"/>×<see cref="Size"/> blocks. Unit of
    /// network snapshots and of view refresh (one Tilemap update per dirty chunk).
    /// </summary>
    public sealed class Chunk
    {
        public const int Size = 32;
        public const int CellCount = Size * Size;

        internal readonly ushort[] Types = new ushort[CellCount];
        internal readonly ushort[] Health = new ushort[CellCount];

        public Chunk(int chunkX, int chunkY)
        {
            ChunkX = chunkX;
            ChunkY = chunkY;
        }

        public int ChunkX { get; }

        public int ChunkY { get; }

        /// <summary>Increments on every change. Views and caches compare it to know when to refresh.</summary>
        public int Version { get; internal set; }

        public static int LocalIndex(int localX, int localY) => localY * Size + localX;
    }
}
