namespace CepaCero.World
{
    /// <summary>
    /// A square of <see cref="Size"/>×<see cref="Size"/> cells. Unit of network snapshots and of view refresh.
    /// Each chunk holds one <see cref="Plane"/> per (level, layer), allocated only when something is built there, so an
    /// empty level costs nothing (D-022). The ground-level structure plane always exists: it is what pathfinding reads.
    /// </summary>
    public sealed class Chunk
    {
        public const int Size = 32;
        public const int CellCount = Size * Size;
        public const int Layers = 4;

        /// <summary>The cells of one (level, layer) in this chunk.</summary>
        internal sealed class Plane
        {
            public readonly ushort[] Types = new ushort[CellCount];
            public readonly ushort[] Health = new ushort[CellCount];

            /// <summary>Colour and rotation per cell (<see cref="BlockVariant"/>); null until something is painted or rotated.</summary>
            public byte[] Variant;
        }

        private readonly Plane[] _planes;

        public Chunk(int chunkX, int chunkY, int levelCount = 1, int groundIndex = 0)
        {
            ChunkX = chunkX;
            ChunkY = chunkY;
            LevelCount = levelCount;
            GroundIndex = groundIndex;
            _planes = new Plane[levelCount * Layers];
            Main = new Plane();
            _planes[PlaneIndex(groundIndex, BuildLayer.Structure)] = Main;
        }

        public int ChunkX { get; }

        public int ChunkY { get; }

        public int LevelCount { get; }

        /// <summary>Index of level 0 among the levels (the world's lowest level is index 0).</summary>
        public int GroundIndex { get; }

        /// <summary>Increments on every change. Views and caches compare it to know when to refresh.</summary>
        public int Version { get; internal set; }

        /// <summary>Ground-level structure: walls, doors, trees. The legacy single plane.</summary>
        internal Plane Main { get; }

        internal ushort[] Types => Main.Types;

        internal ushort[] Health => Main.Health;

        public static int LocalIndex(int localX, int localY) => localY * Size + localX;

        internal static int PlaneIndex(int levelIndex, BuildLayer layer) => levelIndex * Layers + (int)layer;

        internal Plane GetPlane(int levelIndex, BuildLayer layer, bool create)
        {
            int i = PlaneIndex(levelIndex, layer);
            Plane plane = _planes[i];
            if (plane == null && create)
            {
                plane = new Plane();
                _planes[i] = plane;
            }
            return plane;
        }

        internal void ClearPlane(int levelIndex, BuildLayer layer)
        {
            int i = PlaneIndex(levelIndex, layer);
            if (_planes[i] == Main)
            {
                System.Array.Clear(Main.Types, 0, CellCount);
                System.Array.Clear(Main.Health, 0, CellCount);
                Main.Variant = null;
            }
            else
            {
                _planes[i] = null;
            }
        }

        /// <summary>True when the plane has at least one non-air cell (the ground structure plane always counts as present).</summary>
        internal bool HasContent(int levelIndex, BuildLayer layer)
        {
            Plane plane = _planes[PlaneIndex(levelIndex, layer)];
            if (plane == null)
            {
                return false;
            }
            for (int i = 0; i < CellCount; i++)
            {
                if (plane.Types[i] != 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
