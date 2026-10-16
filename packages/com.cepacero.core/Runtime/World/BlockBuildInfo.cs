using System.Collections.Generic;

namespace CepaCero.World
{
    /// <summary>Where a piece lives in a cell (D-020): the ground you walk on, the structure that closes space, or the roof.</summary>
    public enum BuildLayer : byte
    {
        Structure = 0,
        Floor = 1,
        Roof = 2,
        Object = 3,
    }

    /// <summary>One line of a cost or of what a block drops: an item id and how many.</summary>
    public readonly struct BlockCostItem
    {
        public BlockCostItem(string item, int count)
        {
            Item = item;
            Count = count;
        }

        public string Item { get; }

        public int Count { get; }
    }

    /// <summary>What the construction data (D-022) says about a block: its cost, tool, noise, size and layer. Null for the legacy blocks.</summary>
    public sealed class BlockBuildInfo
    {
        public static readonly IReadOnlyList<BlockCostItem> NoCost = new BlockCostItem[0];

        public BlockBuildInfo(string shape, string materialId, BuildLayer layer, int width, int height, bool seeThrough, string tool,
            float noise, bool paintable, float buildSeconds, string connects, bool isOpenVariant, IReadOnlyList<BlockCostItem> cost,
            IReadOnlyList<BlockCostItem> drops)
        {
            Shape = shape;
            MaterialId = materialId;
            Layer = layer;
            Width = width;
            Height = height;
            SeeThrough = seeThrough;
            Tool = tool ?? "";
            Noise = noise;
            Paintable = paintable;
            BuildSeconds = buildSeconds;
            Connects = connects ?? "";
            IsOpenVariant = isOpenVariant;
            Cost = cost ?? NoCost;
            Drops = drops ?? NoCost;
        }

        public string Shape { get; }

        public string MaterialId { get; }

        public BuildLayer Layer { get; }

        /// <summary>Tiles wide and tall (a double gate is 2×1).</summary>
        public int Width { get; }

        public int Height { get; }

        /// <summary>You can see and shoot through it (windows, fences, low walls, glass).</summary>
        public bool SeeThrough { get; }

        /// <summary>The tool kind that breaks it properly (axe, pickaxe, shovel, hammer); another one works much slower. Empty: any.</summary>
        public string Tool { get; }

        /// <summary>Noise radius (tiles) when it is hit.</summary>
        public float Noise { get; }

        public bool Paintable { get; }

        public float BuildSeconds { get; }

        /// <summary>"up" or "down" for stairs and hatches, otherwise empty.</summary>
        public string Connects { get; }

        public bool IsOpenVariant { get; }

        public IReadOnlyList<BlockCostItem> Cost { get; }

        public IReadOnlyList<BlockCostItem> Drops { get; }
    }
}
