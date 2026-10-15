using System;

namespace CepaCero.World
{
    /// <summary>
    /// Static floor layer (comes from the map and does not change during a
    /// match, so it is never sent over the network: every peer loads the same map).
    /// </summary>
    public sealed class GroundLayer
    {
        private readonly GroundType[] _cells;

        public GroundLayer(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Ground layer must be at least 1x1.");
            }
            Width = width;
            Height = height;
            _cells = new GroundType[width * height];
        }

        public int Width { get; }

        public int Height { get; }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public GroundType Get(int x, int y) => _cells[y * Width + x];

        public void Set(int x, int y, GroundType type) => _cells[y * Width + x] = type;

        /// <summary>Base cost of walking one tile (10 = normal). 0 = impassable.</summary>
        public static int WalkCost(GroundType type)
        {
            switch (type)
            {
                case GroundType.Water:
                    return 0;
                case GroundType.Grass:
                case GroundType.Dirt:
                    return 12;
                case GroundType.Rail:
                    return 14;
                default:
                    return 10;
            }
        }

        public static bool IsBuildable(GroundType type) => type != GroundType.Water;
    }
}
