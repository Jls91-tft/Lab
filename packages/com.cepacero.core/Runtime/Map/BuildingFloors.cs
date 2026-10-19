using System;
using System.Collections.Generic;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Map
{
    /// <summary>
    /// Gives the buildings of an imported map their upper storeys, basements and roofs (D-022 phase H). The map only has the ground floor:
    /// from the interior (found from the doors), this builds the floors above with walls and windows, a stairwell, a roof, and the basements
    /// dug out below with their own stairs. Needs the construction blocks; with the legacy registry the map stays as it was.
    /// </summary>
    public static class BuildingFloors
    {
        private const int MinInterior = 6;

        private static readonly int[] Dx4 = { 1, -1, 0, 0 };
        private static readonly int[] Dy4 = { 0, 0, 1, -1 };

        public static void Generate(BlockWorld world, GroundLayer ground, IReadOnlyList<BuildingInfo> buildings, List<string> warnings)
        {
            BlockRegistry reg = world.Registry;
            if (!reg.TryGet("floor_concrete", out BlockType floor) || !reg.TryGet("stairs_concrete", out BlockType stairs)
                || !reg.TryGet("building_wall", out BlockType legacyWall) || !reg.TryGet("door", out BlockType legacyDoor))
            {
                return;                                                      // a world without the construction data
            }
            int built = 0, skipped = 0;
            foreach (BuildingInfo b in buildings)
            {
                int above = Math.Min(b.Levels - 1, world.MaxLevel);
                int below = Math.Min(b.Underground, -world.MinLevel);
                bool storeys = above > 0 || below > 0;
                var interior = new List<Int2>();
                if (!FindInterior(world, ground, b, legacyWall, legacyDoor, interior) || interior.Count < MinInterior)
                {
                    if (storeys)
                    {
                        skipped++;                                           // several storeys need an interior; they stay one
                    }
                    RoofBareFootprint(world, ground, b, legacyWall, legacyDoor, reg);   // no door or no room found: still roof it, at the height of its storeys
                    continue;
                }
                Int2 up = PickStairs(interior, b.Doors, null);
                Int2 down = PickStairs(interior, b.Doors, up);
                var set = new HashSet<Int2>(interior);
                List<Int2> walls = FindWalls(world, set, legacyWall, legacyDoor);
                var keepClear = new List<Int2>();
                foreach (Int2 st in new[] { up, down })
                {
                    keepClear.Add(st);
                    for (int k = 0; k < 4; k++)
                    {
                        keepClear.Add(new Int2(st.X + Dx4[k], st.Y + Dy4[k]));
                    }
                }
                var paint = new Dictionary<Int2, string>();
                b.SetFurniture(InteriorPlanner.Furnish(world, b, new HashSet<Int2>(set), keepClear, paint));
                b.SetWallPaint(paint);
                if (above > 0)
                {
                    BuildUpstairs(world, b, set, walls, up, above, reg, floor, stairs, legacyDoor);
                }
                if (below > 0)
                {
                    BuildBasement(world, set, down, below, floor, stairs);
                }
                PlaceGroundWindows(world, ground, b, set, walls, legacyWall, reg);
                PlaceRoof(world, b, set, walls, Math.Max(above, 0), reg);          // every building has a roof, so no interior is seen from outside (D-024)
                built++;
            }
            OpenSealedBuildings(world, ground, buildings, legacyDoor);
            RoofLooseWalls(world, ground, legacyWall, reg, buildings);
            if (skipped > 0)
            {
                warnings.Add($"{skipped} building(s) with several storeys were left as one: no usable interior found.");
            }
            _ = built;
        }

        /// <summary>Floor cells connected to a door from the inside, inside the bounding box (party walls keep neighbours apart).</summary>
        private static bool FindInterior(BlockWorld world, GroundLayer ground, BuildingInfo b, BlockType wall, BlockType door, List<Int2> into)
        {
            var seen = new HashSet<Int2>();
            var queue = new Queue<Int2>();
            foreach (Int2 d in b.Doors)
            {
                for (int k = 0; k < 4; k++)
                {
                    var n = new Int2(d.X + Dx4[k], d.Y + Dy4[k]);
                    if (IsInterior(world, ground, b, n, wall, door) && seen.Add(n))
                    {
                        queue.Enqueue(n);
                    }
                }
            }
            while (queue.Count > 0)
            {
                Int2 c = queue.Dequeue();
                into.Add(c);
                for (int k = 0; k < 4; k++)
                {
                    var n = new Int2(c.X + Dx4[k], c.Y + Dy4[k]);
                    if (IsInterior(world, ground, b, n, wall, door) && seen.Add(n))
                    {
                        queue.Enqueue(n);
                    }
                }
            }
            into.Sort((a, c) => a.Y != c.Y ? a.Y.CompareTo(c.Y) : a.X.CompareTo(c.X));         // the same plan on every machine
            return into.Count > 0;
        }

        private static bool IsInterior(BlockWorld world, GroundLayer ground, BuildingInfo b, Int2 p, BlockType wall, BlockType door) =>
            p.X >= b.Min.X && p.X <= b.Max.X && p.Y >= b.Min.Y && p.Y <= b.Max.Y && world.InBounds(p.X, p.Y)
            && ground.Get(p.X, p.Y) == GroundType.Floor && world.GetBlock(p.X, p.Y).IsAir;

        /// <summary>A cell that is not next to a door (so the way in stays clear), nor the other stairwell; the first in reading order.</summary>
        private static Int2 PickStairs(List<Int2> interior, IReadOnlyList<Int2> doors, Int2? not)
        {
            foreach (Int2 c in interior)
            {
                if ((not.HasValue && c == not.Value) || NextToDoor(c, doors) || (not.HasValue && Math.Abs(c.X - not.Value.X) + Math.Abs(c.Y - not.Value.Y) < 2))
                {
                    continue;
                }
                return c;
            }
            foreach (Int2 c in interior)
            {
                if (!not.HasValue || c != not.Value)
                {
                    return c;
                }
            }
            return interior[0];
        }

        private static bool NextToDoor(Int2 c, IReadOnlyList<Int2> doors)
        {
            foreach (Int2 d in doors)
            {
                if (Math.Abs(c.X - d.X) <= 1 && Math.Abs(c.Y - d.Y) <= 1)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The map's wall and door blocks around an interior: the outline of the building, in reading order.</summary>
        private static List<Int2> FindWalls(BlockWorld world, HashSet<Int2> interior, BlockType legacyWall, BlockType legacyDoor)
        {
            var walls = new List<Int2>();
            var wallSet = new HashSet<Int2>();
            foreach (Int2 c in interior)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var n = new Int2(c.X + dx, c.Y + dy);
                        if (world.InBounds(n.X, n.Y) && !interior.Contains(n) && !wallSet.Contains(n)
                            && (world.GetTypeId(n.X, n.Y) == legacyWall.Id || world.GetTypeId(n.X, n.Y) == legacyDoor.Id))
                        {
                            wallSet.Add(n);
                            walls.Add(n);
                        }
                    }
                }
            }
            walls.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));
            return walls;
        }

        /// <summary>A roof over the interior and its walls, on the top storey (the ground floor for a single-storey building).</summary>
        private static void PlaceRoof(BlockWorld world, BuildingInfo b, HashSet<Int2> interior, List<Int2> walls, int top, BlockRegistry reg)
        {
            string kind = string.IsNullOrEmpty(b.Roof) ? "tile" : b.Roof;
            if (!reg.TryGet("roof_" + kind, out BlockType roof) || !world.InLevels(top))
            {
                return;
            }
            foreach (Int2 c in interior)
            {
                world.SetBlock(c.X, c.Y, top, BuildLayer.Roof, roof.Id);
            }
            foreach (Int2 w in walls)
            {
                world.SetBlock(w.X, w.Y, top, BuildLayer.Roof, roof.Id);
            }
        }

        /// <summary>
        /// Windows in the ground-floor walls (Project Zomboid look, D-024): every other cell of an outer wall that has room inside and street
        /// (not another building) outside, never next to a door. They are real blocks: zombies break them, players barricade them.
        /// </summary>
        private static void PlaceGroundWindows(BlockWorld world, GroundLayer ground, BuildingInfo b, HashSet<Int2> interior, List<Int2> walls, BlockType legacyWall, BlockRegistry reg)
        {
            if (!reg.TryGet("window_" + WallMaterial(b.Kind), out BlockType window))
            {
                return;
            }
            foreach (Int2 w in walls)
            {
                if ((w.X + w.Y) % 2 != 0 || world.GetTypeId(w.X, w.Y) != legacyWall.Id || NextToDoor(w, b.Doors) || NextToWindow(world, w, window))
                {
                    continue;
                }
                for (int k = 0; k < 4; k++)
                {
                    var inside = new Int2(w.X + Dx4[k], w.Y + Dy4[k]);
                    var outside = new Int2(w.X - Dx4[k], w.Y - Dy4[k]);
                    var along1 = new Int2(w.X + Dy4[k], w.Y + Dx4[k]);                 // the two cells along the wall
                    var along2 = new Int2(w.X - Dy4[k], w.Y - Dx4[k]);
                    if (interior.Contains(inside) && world.InBounds(outside.X, outside.Y) && ground.Get(outside.X, outside.Y) != GroundType.Floor
                        && world.GetTypeId(outside.X, outside.Y) != legacyWall.Id
                        && (IsWallCell(world, along1, legacyWall) || IsWallCell(world, along2, legacyWall)))
                    {
                        world.SetBlock(w.X, w.Y, 0, BuildLayer.Structure, window.Id);
                        break;
                    }
                }
            }
        }

        private static bool NextToWindow(BlockWorld world, Int2 c, BlockType window)
        {
            for (int k = 0; k < 4; k++)
            {
                int x = c.X + Dx4[k];
                int y = c.Y + Dy4[k];
                if (world.InBounds(x, y) && world.GetTypeId(x, y) == window.Id)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasAnyRoof(BlockWorld world, int x, int y)
        {
            for (int z = world.MinLevel; z <= world.MaxLevel; z++)
            {
                if (!world.GetBlock(x, y, z, BuildLayer.Roof).IsAir)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsWallCell(BlockWorld world, Int2 p, BlockType legacyWall) => world.InBounds(p.X, p.Y) && world.GetTypeId(p.X, p.Y) == legacyWall.Id;

        /// <summary>
        /// No building is sealed (D-031): a building whose rooms cannot be walked into from the street gets a door through the wall (one or two
        /// tiles thick, a party wall) between its floor and a place you can already reach. Repeats until nothing changes, so a block of houses
        /// opens one into the next.
        /// </summary>
        private static void OpenSealedBuildings(BlockWorld world, GroundLayer ground, IReadOnlyList<BuildingInfo> buildings, BlockType door)
        {
            int w = world.Width, h = world.Height;
            for (int pass = 0; pass < 8; pass++)
            {
                bool[] reach = Reachable(world);
                bool changed = false;
                foreach (BuildingInfo b in buildings)
                {
                    if (Entered(world, ground, b, reach))
                    {
                        continue;
                    }
                    changed |= Breach(world, ground, b, reach, door);
                }
                if (!changed)
                {
                    return;
                }
            }
        }

        private static bool[] Reachable(BlockWorld world)
        {
            int w = world.Width, h = world.Height;
            var reach = new bool[w * h];
            var queue = new Queue<int>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if ((x == 0 || y == 0 || x == w - 1 || y == h - 1) && Passable(world, x, y))
                    {
                        reach[y * w + x] = true;
                        queue.Enqueue(y * w + x);
                    }
                }
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w, y = i / w;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + Dx4[k], ny = y + Dy4[k];
                    if (world.InBounds(nx, ny) && !reach[ny * w + nx] && Passable(world, nx, ny))
                    {
                        reach[ny * w + nx] = true;
                        queue.Enqueue(ny * w + nx);
                    }
                }
            }
            return reach;
        }

        private static bool Passable(BlockWorld world, int x, int y)
        {
            BlockType t = world.GetBlock(x, y);
            return t.IsAir || t.ToggleTo != 0;
        }

        private static bool Entered(BlockWorld world, GroundLayer ground, BuildingInfo b, bool[] reach)
        {
            for (int y = b.Min.Y; y <= b.Max.Y; y++)
            {
                for (int x = b.Min.X; x <= b.Max.X; x++)
                {
                    if (world.InBounds(x, y) && ground.Get(x, y) == GroundType.Floor && world.GetBlock(x, y).IsAir && reach[y * world.Width + x])
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool Breach(BlockWorld world, GroundLayer ground, BuildingInfo b, bool[] reach, BlockType door)
        {
            int w = world.Width;
            for (int y = b.Min.Y; y <= b.Max.Y; y++)
            {
                for (int x = b.Min.X; x <= b.Max.X; x++)
                {
                    if (!world.InBounds(x, y) || ground.Get(x, y) != GroundType.Floor || !world.GetBlock(x, y).IsAir)
                    {
                        continue;                                    // a floor cell of this building
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        // One wall tile, then a reachable cell; or two wall tiles (a party wall), then a reachable cell.
                        for (int thick = 1; thick <= 2; thick++)
                        {
                            int ex = x + Dx4[k] * (thick + 1), ey = y + Dy4[k] * (thick + 1);
                            if (!world.InBounds(ex, ey) || !reach[ey * w + ex])
                            {
                                continue;
                            }
                            bool walls = true;
                            for (int t = 1; t <= thick && walls; t++)
                            {
                                BlockType wt = world.GetBlock(x + Dx4[k] * t, y + Dy4[k] * t);
                                walls = !wt.IsAir && wt.IsSolid && wt.ToggleTo == 0;
                            }
                            if (!walls)
                            {
                                continue;
                            }
                            for (int t = 1; t <= thick; t++)
                            {
                                world.SetBlock(x + Dx4[k] * t, y + Dy4[k] * t, door.Id);
                            }
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>Everything that is a building and still has no roof (floor not reached from a door, rooms cut off by walls, solid blocks, odd outlines): roofed too, so no house shows its inside from the street (D-028).</summary>
        private static void RoofLooseWalls(BlockWorld world, GroundLayer ground, BlockType wall, BlockRegistry reg, IReadOnlyList<BuildingInfo> buildings)
        {
            if (!reg.TryGet("roof_tile", out BlockType roof))
            {
                return;
            }
            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    bool covers = world.GetTypeId(x, y) == wall.Id || ground.Get(x, y) == GroundType.Floor
                        || world.GetBlock(x, y).Key.StartsWith("wall_") || world.GetBlock(x, y).Key.StartsWith("door_") || world.GetBlock(x, y).Key.StartsWith("window_");
                    if (covers && !HasAnyRoof(world, x, y))
                    {
                        // A shut-in room or patio inside a taller building takes that building's roof height (D-028), not a pit at ground level.
                        int top = 0;
                        foreach (BuildingInfo b in buildings)
                        {
                            if (x >= b.Min.X && x <= b.Max.X && y >= b.Min.Y && y <= b.Max.Y)
                            {
                                top = Math.Max(top, Math.Min(b.Levels - 1, world.MaxLevel));
                            }
                        }
                        if (top > 0 && world.GetBlock(x, y, top, BuildLayer.Roof).IsAir)
                        {
                            world.SetBlock(x, y, top, BuildLayer.Roof, roof.Id);
                        }
                        else if (top == 0)
                        {
                            world.SetBlock(x, y, 0, BuildLayer.Roof, roof.Id);
                        }
                    }
                }
            }
        }

        /// <summary>A building with no usable interior (no door, tiny): its floor cells and the walls around them get a roof at street level.</summary>
        private static void RoofBareFootprint(BlockWorld world, GroundLayer ground, BuildingInfo b, BlockType wall, BlockType door, BlockRegistry reg)
        {
            var set = new HashSet<Int2>();
            for (int y = b.Min.Y; y <= b.Max.Y; y++)
            {
                for (int x = b.Min.X; x <= b.Max.X; x++)
                {
                    if (world.InBounds(x, y) && ground.Get(x, y) == GroundType.Floor && world.GetBlock(x, y).IsAir
                        && !HasAnyRoof(world, x, y))
                    {
                        set.Add(new Int2(x, y));
                    }
                }
            }
            if (set.Count > 0)
            {
                PlaceRoof(world, b, set, FindWalls(world, set, wall, door), Math.Max(0, Math.Min(b.Levels - 1, world.MaxLevel)), reg);
                return;
            }
            // A solid building (all wall, no floor to stand on): the roof goes straight over its walls.
            var solid = new List<Int2>();
            for (int y = b.Min.Y; y <= b.Max.Y; y++)
            {
                for (int x = b.Min.X; x <= b.Max.X; x++)
                {
                    if (world.InBounds(x, y) && world.GetTypeId(x, y) == wall.Id && !HasAnyRoof(world, x, y))
                    {
                        solid.Add(new Int2(x, y));
                    }
                }
            }
            if (solid.Count > 0)
            {
                PlaceRoof(world, b, new HashSet<Int2>(), solid, Math.Max(0, Math.Min(b.Levels - 1, world.MaxLevel)), reg);
            }
        }

        private static void BuildUpstairs(BlockWorld world, BuildingInfo b, HashSet<Int2> interior, List<Int2> walls, Int2 stairwell, int above, BlockRegistry reg,
            BlockType floor, BlockType stairs, BlockType legacyDoor)
        {
            string material = WallMaterial(b.Kind);
            BlockType wall = reg.Get("wall_" + material);
            BlockType window = reg.Get("window_" + material);
            for (int z = 1; z <= above; z++)
            {
                foreach (Int2 c in interior)
                {
                    if (c == stairwell)
                    {
                        if (z < above)
                        {
                            world.SetBlock(c.X, c.Y, z, BuildLayer.Structure, stairs.Id);          // keep climbing
                        }
                        continue;                                                              // a hole in the floor: the stairs come up here
                    }
                    world.SetBlock(c.X, c.Y, z, BuildLayer.Floor, floor.Id);
                }
                foreach (Int2 w in walls)
                {
                    bool glazed = (w.X + w.Y) % 3 == 0 && world.GetTypeId(w.X, w.Y) != legacyDoor.Id;
                    world.SetBlock(w.X, w.Y, z, BuildLayer.Structure, glazed ? window.Id : wall.Id);
                }
            }
            world.SetBlock(stairwell.X, stairwell.Y, 0, BuildLayer.Structure, stairs.Id);
        }

        private static void BuildBasement(BlockWorld world, HashSet<Int2> interior, Int2 stairwell, int below, BlockType floor, BlockType stairs)
        {
            for (int k = 1; k <= below; k++)
            {
                int z = -k;
                foreach (Int2 c in interior)
                {
                    world.SetBlock(c.X, c.Y, z, BuildLayer.Floor, floor.Id);                   // dug out under the whole footprint
                }
                world.SetBlock(stairwell.X, stairwell.Y, z, BuildLayer.Structure, stairs.Id);  // up to the level above
            }
        }

        /// <summary>Brick for homes, schools and churches; concrete for the rest.</summary>
        private static string WallMaterial(string kind)
        {
            switch (kind)
            {
                case "residential":
                case "generic":
                case "school":
                case "religious":
                    return "brick";
                default:
                    return "concrete";
            }
        }
    }
}
