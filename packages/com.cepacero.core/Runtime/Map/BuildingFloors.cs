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
                        skipped++;                                           // single-storey buildings with no interior just stay as they are
                    }
                    continue;
                }
                Int2 up = PickStairs(interior, b.Doors, null);
                Int2 down = PickStairs(interior, b.Doors, up);
                var set = new HashSet<Int2>(interior);
                List<Int2> walls = FindWalls(world, set, legacyWall, legacyDoor);
                if (above > 0)
                {
                    BuildUpstairs(world, b, set, walls, up, above, reg, floor, stairs, legacyDoor);
                }
                if (below > 0)
                {
                    BuildBasement(world, set, down, below, floor, stairs);
                }
                PlaceRoof(world, b, set, walls, Math.Max(above, 0), reg);          // every building has a roof, so no interior is seen from outside (D-024)
                built++;
            }
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

        /// <summary>A roof over the interior and its walls, on the top storey (the ground floor for a single-storey building); doors are left uncovered.</summary>
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
                if (IsDoor(b, w))
                {
                    continue;                                          // the doorway stays open to the sky: the way in is seen from outside
                }
                world.SetBlock(w.X, w.Y, top, BuildLayer.Roof, roof.Id);
            }
        }

        private static bool IsDoor(BuildingInfo b, Int2 cell)
        {
            for (int i = 0; i < b.Doors.Count; i++)
            {
                if (b.Doors[i] == cell)
                {
                    return true;
                }
            }
            return false;
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
