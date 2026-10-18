using System;
using System.Collections.Generic;
using CepaCero.Map;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Unity.Prototype
{
    /// <summary>One piece of decoration: an atlas sprite on a rectangle of cells.</summary>
    public readonly struct DecorItem
    {
        public DecorItem(string sprite, int x, int y, int width, int height, bool flat, bool flipX, float shiftX = 0f, float shiftY = 0f)
        {
            ShiftX = shiftX;
            ShiftY = shiftY;
            Sprite = sprite;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Flat = flat;
            FlipX = flipX;
        }

        public string Sprite { get; }

        /// <summary>Bottom-left cell of the footprint.</summary>
        public int X { get; }

        public int Y { get; }

        /// <summary>Footprint in cells (what it stands on; a lamp is 1x1 but drawn taller).</summary>
        public int Width { get; }

        public int Height { get; }

        /// <summary>Flat marks (blood, rugs) lie on the ground; the rest stands and is sorted by height.</summary>
        public bool Flat { get; }

        public bool FlipX { get; }

        /// <summary>A push off the cell centre, in cells: furniture stands against its wall, not in the middle of its square (D-027).</summary>
        public float ShiftX { get; }

        public float ShiftY { get; }
    }

    /// <summary>
    /// Dresses a map with decoration (D-019): parked and burnt cars on roads, street
    /// furniture on pavements, trees in parks, furniture inside buildings by kind, and
    /// sparse wear (blood, stains, rubbish, glass). Deterministic for a map and seed, so
    /// every player sees the same street without sending a byte. Visual only: nothing
    /// here blocks movement, and doors and their surroundings are always left clear.
    /// </summary>
    public static class DecorPlanner
    {
        private static readonly string[] CarColours = { "green", "grey", "orange" };

        public static List<DecorItem> Plan(BlockWorld blocks, GroundLayer ground, IReadOnlyList<BuildingInfo> buildings, int seed)
        {
            var plan = new Planner(blocks, ground, new Random(seed));
            plan.Outdoors();
            if (buildings != null)
            {
                foreach (BuildingInfo b in buildings)
                {
                    if (b.Furniture.Count > 0)
                    {
                        plan.Furnished(b);                    // rooms and furniture of the importer (D-026)
                    }
                    else
                    {
                        plan.Interior(b);
                    }
                }
            }
            plan.Wear();
            return plan.Items;
        }

        /// <summary>A stable seed from the map name, so the same map always looks the same.</summary>
        public static int SeedFor(string mapName)
        {
            unchecked
            {
                int h = 17;
                foreach (char ch in mapName ?? "")
                {
                    h = h * 31 + ch;
                }
                return h;
            }
        }

        private sealed class Planner
        {
            private readonly BlockWorld _blocks;
            private readonly GroundLayer _ground;
            private readonly Random _rng;
            private readonly bool[] _taken;
            private readonly int _w;
            private readonly int _h;

            public readonly List<DecorItem> Items = new List<DecorItem>();

            public Planner(BlockWorld blocks, GroundLayer ground, Random rng)
            {
                _blocks = blocks;
                _ground = ground;
                _rng = rng;
                _w = blocks.Width;
                _h = blocks.Height;
                _taken = new bool[_w * _h];
            }

            public void Outdoors()
            {
                for (int y = 0; y < _h; y++)
                {
                    for (int x = 0; x < _w; x++)
                    {
                        if (!Free(x, y))
                        {
                            continue;
                        }
                        double p = _rng.NextDouble();
                        switch (_ground.Get(x, y))
                        {
                            case GroundType.Asphalt:
                                if (p < 0.02)
                                {
                                    Car(x, y);
                                }
                                break;
                            case GroundType.Sidewalk:
                                Pick(x, y, p, 0.05, "decor_lamp", 0.08, Any("decor_bin_0", "decor_bin_1", "decor_trash_bag"),
                                    0.092, "decor_hydrant", 0.104, "decor_bench", 0.114, "decor_mailbox", 0.124, "decor_cone",
                                    0.134, Any("decor_boxes_0", "decor_boxes_1"), 0.1375, "decor_barrel_fire_0");
                                break;
                            case GroundType.Grass:
                                Pick(x, y, p, 0.05, Any("decor_tree_0", "decor_tree_1", "decor_tree_0", "decor_tree_dead"),
                                    0.10, Any("decor_bush_0", "decor_bush_1", "decor_bush_dead"));
                                break;
                            case GroundType.Dirt:
                                Pick(x, y, p, 0.012, "decor_bush_dead", 0.02, "decor_tree_dead", 0.026, "decor_barrel",
                                    0.032, Any("decor_boxes_0", "decor_boxes_1"), 0.0345, "decor_barrel_fire_0");
                                break;
                        }
                    }
                }
            }

            public void Furnished(BuildingInfo b)
            {
                foreach (Furniture f in b.Furniture)
                {
                    if (f.OnWall)
                    {
                        continue;                                 // pictures and curtains hang on the walls (PrototypeWorldView)
                    }
                    FurnitureKinds.Info info = FurnitureKinds.Of(f.Kind);
                    float sx = 0f, sy = 0f;
                    bool flip = false;
                    if (!info.Flat && info.Width == 1 && info.Height == 1)
                    {
                        // Against the wall it touches (the back walls first, they are the ones you see), facing into the room.
                        const float Push = 0.3f;
                        if (Solid(f.Cell.X, f.Cell.Y + 1)) { sy = Push; }
                        else if (Solid(f.Cell.X + 1, f.Cell.Y)) { sx = Push; flip = true; }
                        else if (Solid(f.Cell.X - 1, f.Cell.Y)) { sx = -Push; }
                        else if (Solid(f.Cell.X, f.Cell.Y - 1)) { sy = -Push; flip = true; }
                    }
                    Items.Add(new DecorItem(info.Sprite, f.Cell.X, f.Cell.Y, info.Width, info.Height, info.Flat, flip, sx, sy));
                    for (int dy = 0; dy < info.Height; dy++)
                    {
                        for (int dx = 0; dx < info.Width; dx++)
                        {
                            if (In(f.Cell.X + dx, f.Cell.Y + dy))
                            {
                                _taken[(f.Cell.Y + dy) * _w + f.Cell.X + dx] = true;
                            }
                        }
                    }
                }
            }

            private bool Solid(int x, int y)
            {
                if (!In(x, y))
                {
                    return true;
                }
                BlockType t = _blocks.GetBlock(x, y);
                return !t.IsAir && t.IsSolid && !t.Key.StartsWith("stairs") && t.ToggleTo == 0;
            }

            public void Interior(BuildingInfo b)
            {
                var cells = new List<Int2>();
                for (int y = b.Min.Y; y <= b.Max.Y; y++)
                {
                    for (int x = b.Min.X; x <= b.Max.X; x++)
                    {
                        if (In(x, y) && _ground.Get(x, y) == GroundType.Floor && Free(x, y))
                        {
                            cells.Add(new Int2(x, y));
                        }
                    }
                }
                if (cells.Count == 0)
                {
                    return;
                }

                string[] wall;
                string[] scatter;
                switch (b.Kind)
                {
                    case "food":
                    case "commercial":
                        wall = new[] { "decor_shelf_0", "decor_shelf_1", "decor_counter_0" };
                        scatter = new[] { "decor_shelf_0", "decor_shelf_1", "decor_boxes_0", "decor_boxes_1", "decor_counter_1" };
                        break;
                    case "medical":
                        wall = new[] { "decor_counter_0", "decor_counter_2", "decor_plant_0" };
                        scatter = new[] { "decor_bed_1", "decor_bed_1", "decor_chair_0", "decor_plant_1" };
                        break;
                    case "hardware":
                    case "industrial":
                    case "garage":
                        wall = new[] { "decor_counter_0", "decor_boxes_1", "decor_barrel" };
                        scatter = new[] { "decor_boxes_0", "decor_boxes_1", "decor_barrel", "decor_table_long" };
                        break;
                    case "office":
                    case "school":
                    case "police":
                    case "religious":
                        wall = new[] { "decor_counter_0", "decor_plant_0", "decor_plant_1" };
                        scatter = new[] { "decor_table_long", "decor_chair_0", "decor_chair_1", "decor_plant_1" };
                        break;
                    default: // homes
                        wall = new[] { "decor_counter_0", "decor_counter_1", "decor_counter_2", "decor_counter_3", "decor_stove" };
                        scatter = new[] { "decor_bed_0", "decor_table_oval", "decor_chair_0", "decor_chair_1", "decor_armchair", "decor_plant_0", "decor_table_round" };
                        break;
                }

                // Along the inside of the north wall: counters, shelves.
                foreach (Int2 c in cells)
                {
                    if (IsWallLike(c.X, c.Y + 1) && Free(c.X, c.Y) && _rng.NextDouble() < 0.55)
                    {
                        Place(wall[_rng.Next(wall.Length)], c.X, c.Y, 1, 1, false, false);
                    }
                }

                // A rug in the middle of bigger rooms.
                if (cells.Count >= 12 && _rng.NextDouble() < 0.6)
                {
                    Int2 c = cells[_rng.Next(cells.Count)];
                    if (Fits(c.X, c.Y, 3, 1))
                    {
                        Items.Add(new DecorItem(_rng.Next(2) == 0 ? "decal_rug_0" : "decal_rug_1", c.X, c.Y, 3, 1, true, false));
                    }
                }

                // Furniture scattered over the room: about one piece per ten cells.
                int wanted = Math.Max(1, cells.Count / 10);
                int placed = 0;
                for (int attempt = 0; attempt < wanted * 4 && placed < wanted; attempt++)
                {
                    Int2 c = cells[_rng.Next(cells.Count)];
                    string name = scatter[_rng.Next(scatter.Length)];
                    Footprint(name, out int fw, out int fh);
                    if (Fits(c.X, c.Y, fw, fh))
                    {
                        Place(name, c.X, c.Y, fw, fh, false, _rng.Next(2) == 0 && fw == 1);
                        placed++;
                    }
                }
            }

            public void Wear()
            {
                for (int y = 0; y < _h; y++)
                {
                    for (int x = 0; x < _w; x++)
                    {
                        if (_blocks.IsSolid(x, y))
                        {
                            continue;
                        }
                        GroundType g = _ground.Get(x, y);
                        if (g == GroundType.Water || g == GroundType.Grass || g == GroundType.Rail)
                        {
                            continue;
                        }
                        double p = _rng.NextDouble();
                        double blood = g == GroundType.Floor ? 0.010 : 0.005;
                        string decal =
                            p < blood ? "decal_blood_" + _rng.Next(3)
                            : p < blood + 0.010 ? "decal_stain_" + _rng.Next(3)
                            : p < blood + 0.018 ? "decal_rubbish_" + _rng.Next(3)
                            : p < blood + 0.022 ? "decal_glass_" + _rng.Next(2)
                            : null;
                        if (decal != null)
                        {
                            Items.Add(new DecorItem(decal, x, y, 1, 1, true, _rng.Next(2) == 0));
                        }
                    }
                }
            }

            // ------------------------------------------------------- helpers

            private void Car(int x, int y)
            {
                int along = Count(x - 2, y, 1, 0, 5) - Count(x, y - 2, 0, 1, 5);
                bool horizontal = along >= 0;
                int fw = horizontal ? 2 : 1;
                int fh = horizontal ? 1 : 2;
                if (!Fits(x, y, fw, fh, GroundType.Asphalt))
                {
                    return;
                }
                string colour = _rng.NextDouble() < 0.3 ? "burnt" : CarColours[_rng.Next(CarColours.Length)];
                Place($"decor_car_{colour}_{(horizontal ? "h" : "v")}", x, y, fw, fh, false, horizontal && _rng.Next(2) == 0, spacing: 2);
            }

            private int Count(int x, int y, int dx, int dy, int n)
            {
                int c = 0;
                for (int i = 0; i < n; i++)
                {
                    int cx = x + dx * i;
                    int cy = y + dy * i;
                    if (In(cx, cy) && _ground.Get(cx, cy) == GroundType.Asphalt)
                    {
                        c++;
                    }
                }
                return c;
            }

            /// <summary>Chooses by cumulative thresholds: (limit, sprite) pairs over p.</summary>
            private void Pick(int x, int y, double p, params object[] pairs)
            {
                for (int i = 0; i < pairs.Length; i += 2)
                {
                    if (p < (double)pairs[i])
                    {
                        string name = pairs[i + 1] is string[] options ? options[_rng.Next(options.Length)] : (string)pairs[i + 1];
                        Footprint(name, out int fw, out int fh);
                        if (Fits(x, y, fw, fh))
                        {
                            Place(name, x, y, fw, fh, false, _rng.Next(2) == 0, spacing: 1);
                        }
                        return;
                    }
                }
            }

            private static string[] Any(params string[] options) => options;

            private static void Footprint(string name, out int w, out int h)
            {
                switch (name)
                {
                    case "decor_table_long": w = 3; h = 1; return;
                    case "decor_table_oval": w = 2; h = 1; return;
                    case "decor_bed_0":
                    case "decor_bed_1": w = 2; h = 2; return;
                    case "decor_armchair": w = 1; h = 2; return;
                    default: w = 1; h = 1; return;
                }
            }

            private void Place(string sprite, int x, int y, int w, int h, bool flat, bool flip, int spacing = 0)
            {
                Items.Add(new DecorItem(sprite, x, y, w, h, flat, flip));
                for (int dy = -spacing; dy < h + spacing; dy++)
                {
                    for (int dx = -spacing; dx < w + spacing; dx++)
                    {
                        if (In(x + dx, y + dy))
                        {
                            _taken[(y + dy) * _w + x + dx] = true;
                        }
                    }
                }
            }

            private bool Fits(int x, int y, int w, int h, GroundType? only = null)
            {
                GroundType first = _ground.Get(x, y);
                for (int dy = 0; dy < h; dy++)
                {
                    for (int dx = 0; dx < w; dx++)
                    {
                        int cx = x + dx;
                        int cy = y + dy;
                        if (!Free(cx, cy) || _ground.Get(cx, cy) != (only ?? first))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            /// <summary>Inside the map, nothing built on it, nothing placed yet, no door within one cell.</summary>
            private bool Free(int x, int y)
            {
                if (!In(x, y) || _taken[y * _w + x] || !_blocks.GetBlock(x, y).IsAir)
                {
                    return false;
                }
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (In(x + dx, y + dy) && IsDoor(x + dx, y + dy))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            private bool IsDoor(int x, int y)
            {
                string key = _blocks.GetBlock(x, y).Key;
                return key == "door" || key == "door_open";
            }

            private bool IsWallLike(int x, int y) => In(x, y) && (_blocks.GetBlock(x, y).Key == "building_wall" || IsDoor(x, y));

            private bool In(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h;
        }
    }
}
