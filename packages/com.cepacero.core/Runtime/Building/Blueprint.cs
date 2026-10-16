using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CepaCero.Rules;
using CepaCero.World;

namespace CepaCero.Building
{
    /// <summary>One piece of a blueprint: where (relative to the lower-left corner), which block (by key, so plans survive data changes), layer and paint/turn.</summary>
    public readonly struct BlueprintPiece
    {
        public BlueprintPiece(int x, int y, string key, BuildLayer layer, byte variant)
        {
            X = x;
            Y = y;
            Key = key;
            Layer = layer;
            Variant = variant;
        }

        public int X { get; }

        public int Y { get; }

        public string Key { get; }

        public BuildLayer Layer { get; }

        public byte Variant { get; }
    }

    /// <summary>A piece of a blueprint placed on the map: the world cell, the block id and the variant.</summary>
    public readonly struct PlannedPiece
    {
        public PlannedPiece(int x, int y, ushort type, byte variant)
        {
            X = x;
            Y = y;
            Type = type;
            Variant = variant;
        }

        public int X { get; }

        public int Y { get; }

        public ushort Type { get; }

        public byte Variant { get; }
    }

    public readonly struct ItemShortfall
    {
        public ItemShortfall(string item, int missing)
        {
            Item = item;
            Missing = missing;
        }

        public string Item { get; }

        public int Missing { get; }
    }

    /// <summary>
    /// A copy of a piece of the player's construction (D-022 phase E): copy it, paste it somewhere else (turned if you like),
    /// save it as a `.cepaplan` text file and share it. Only what players built is copied, never map buildings. Ground level only for now.
    /// </summary>
    public sealed class Blueprint
    {
        public const string Header = "cepaplan 1";
        public const int MaxSide = 64;
        public const int MaxPieces = 4096;

        private readonly List<BlueprintPiece> _pieces = new List<BlueprintPiece>();

        public Blueprint(int width, int height)
        {
            if (width < 1 || height < 1 || width > MaxSide || height > MaxSide)
            {
                throw new ArgumentOutOfRangeException(nameof(width), $"A blueprint is 1..{MaxSide} tiles on each side.");
            }
            Width = width;
            Height = height;
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public IReadOnlyList<BlueprintPiece> Pieces => _pieces;

        public bool IsEmpty => _pieces.Count == 0;

        public void Add(BlueprintPiece piece)
        {
            if (piece.X < 0 || piece.Y < 0 || piece.X >= Width || piece.Y >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(piece), "The piece is outside the blueprint.");
            }
            if (_pieces.Count >= MaxPieces)
            {
                throw new InvalidOperationException("Too many pieces in the blueprint.");
            }
            _pieces.Add(piece);
        }

        /// <summary>Copies the player-built pieces of a rectangle (corners in any order, clipped to the map and to the maximum size).</summary>
        public static Blueprint Capture(BlockWorld world, int ax, int ay, int bx, int by)
        {
            int x0 = Math.Max(0, Math.Min(ax, bx)), x1 = Math.Min(world.Width - 1, Math.Max(ax, bx));
            int y0 = Math.Max(0, Math.Min(ay, by)), y1 = Math.Min(world.Height - 1, Math.Max(ay, by));
            x1 = Math.Min(x1, x0 + MaxSide - 1);
            y1 = Math.Min(y1, y0 + MaxSide - 1);
            var plan = new Blueprint(Math.Max(1, x1 - x0 + 1), Math.Max(1, y1 - y0 + 1));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    for (int l = 0; l < Chunk.Layers; l++)
                    {
                        var layer = (BuildLayer)l;
                        BlockType type = world.GetBlock(x, y, 0, layer);
                        if (type.IsAir || type.Build == null)
                        {
                            continue;
                        }
                        if (type.Build.IsOpenVariant && type.ToggleTo != 0)
                        {
                            type = world.Registry[type.ToggleTo];                  // a plan always holds the closed door
                        }
                        plan.Add(new BlueprintPiece(x - x0, y - y0, type.Key, layer, world.GetVariant(x, y, 0, layer)));
                    }
                }
            }
            return plan;
        }

        /// <summary>A quarter turn clockwise on the map (y grows north); every piece's own turn advances too.</summary>
        public Blueprint RotatedClockwise()
        {
            var turned = new Blueprint(Height, Width);
            foreach (BlueprintPiece p in _pieces)
            {
                byte variant = BlockVariant.Make(BlockVariant.Color(p.Variant), BlockVariant.Rotation(p.Variant) + 1);
                turned.Add(new BlueprintPiece(p.Y, Width - 1 - p.X, p.Key, p.Layer, variant));
            }
            return turned;
        }

        /// <summary>The pieces at a map position (lower-left corner), floors first, then structures, objects and roofs; blocks the registry does not know are counted in <paramref name="unknown"/>.</summary>
        public void PlanAt(int originX, int originY, BlockRegistry registry, List<PlannedPiece> into, out int unknown)
        {
            unknown = 0;
            foreach (BuildLayer layer in PasteOrder)
            {
                foreach (BlueprintPiece p in _pieces)
                {
                    if (p.Layer != layer)
                    {
                        continue;
                    }
                    if (!registry.TryGet(p.Key, out BlockType type) || type.Layer != p.Layer)
                    {
                        unknown++;
                        continue;
                    }
                    into.Add(new PlannedPiece(originX + p.X, originY + p.Y, type.Id, p.Variant));
                }
            }
        }

        private static readonly BuildLayer[] PasteOrder = { BuildLayer.Floor, BuildLayer.Structure, BuildLayer.Object, BuildLayer.Roof };

        /// <summary>What the plan costs in items (full price, summed by item), at the match's price.</summary>
        public Dictionary<string, int> TotalCost(BlockRegistry registry, BuildCostMode mode = BuildCostMode.Normal)
        {
            var total = new Dictionary<string, int>(StringComparer.Ordinal);
            if (mode == BuildCostMode.Free)
            {
                return total;
            }
            foreach (BlueprintPiece p in _pieces)
            {
                if (!registry.TryGet(p.Key, out BlockType type) || type.Build == null)
                {
                    continue;
                }
                foreach (BlockCostItem c in type.Build.Cost)
                {
                    int n = mode == BuildCostMode.Cheap ? (c.Count + 1) / 2 : c.Count;
                    total[c.Item] = (total.TryGetValue(c.Item, out int have) ? have : 0) + n;
                }
            }
            return total;
        }

        /// <summary>What is missing to build the whole plan, given how many of an item the player has.</summary>
        public List<ItemShortfall> Shortfall(BlockRegistry registry, Func<string, int> have, BuildCostMode mode = BuildCostMode.Normal)
        {
            var missing = new List<ItemShortfall>();
            foreach (KeyValuePair<string, int> need in TotalCost(registry, mode))
            {
                int short_ = need.Value - have(need.Key);
                if (short_ > 0)
                {
                    missing.Add(new ItemShortfall(need.Key, short_));
                }
            }
            missing.Sort((a, b) => string.CompareOrdinal(a.Item, b.Item));
            return missing;
        }

        /// <summary>The `.cepaplan` text: a header, the size and one line per piece (key, x, y, layer, variant).</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            sb.Append("size ").Append(Width).Append(' ').Append(Height).Append('\n');
            foreach (BlueprintPiece p in _pieces)
            {
                sb.Append(p.Key).Append(' ').Append(p.X).Append(' ').Append(p.Y).Append(' ')
                    .Append((int)p.Layer).Append(' ').Append(p.Variant).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Reads a `.cepaplan`; throws <see cref="FormatException"/> with the line that is wrong (plans come from other people).</summary>
        public static Blueprint FromText(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            string[] lines = text.Replace("\r", "").Split('\n');
            if (lines.Length < 2 || lines[0].Trim() != Header)
            {
                throw new FormatException("Not a cepaplan file (the first line must be '" + Header + "').");
            }
            string[] size = lines[1].Split(' ');
            if (size.Length != 3 || size[0] != "size" || !Int(size[1], out int w) || !Int(size[2], out int h) || w < 1 || h < 1 || w > MaxSide || h > MaxSide)
            {
                throw new FormatException("Line 2: expected 'size W H' with each side 1.." + MaxSide + ".");
            }
            var plan = new Blueprint(w, h);
            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                string[] f = line.Split(' ');
                if (f.Length != 5 || !Int(f[1], out int x) || !Int(f[2], out int y) || !Int(f[3], out int layer) || !Int(f[4], out int variant)
                    || x < 0 || y < 0 || x >= w || y >= h || layer < 0 || layer >= Chunk.Layers || variant < 0 || variant > 255 || f[0].Length > 64)
                {
                    throw new FormatException($"Line {i + 1}: expected 'key x y layer variant' inside the plan.");
                }
                if (plan.Pieces.Count >= MaxPieces)
                {
                    throw new FormatException("Too many pieces.");
                }
                plan.Add(new BlueprintPiece(x, y, f[0], (BuildLayer)layer, (byte)variant));
            }
            return plan;
        }

        private static bool Int(string s, out int value) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
