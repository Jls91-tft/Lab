using System;
using System.Collections.Generic;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Map
{
    /// <summary>
    /// Divides the ground floor of a building into rooms and furnishes them (D-026, the Project Zomboid way): a home gets a living room by
    /// the entrance, a kitchen with fridge and cupboards (food), bedrooms with beds and wardrobes, a bathroom; shops get shelves and a back room,
    /// offices desks. Interior walls and doors are real blocks (zombies break them, players barricade them); the furniture is a plan that
    /// every peer derives from the same map, and its containers are the ones with a loot table. Deterministic: building id only.
    /// </summary>
    public static class InteriorPlanner
    {
        private const int MinRoomSide = 3;
        private const int MinSplitSide = 7;
        private const int MinRoomCells = 6;

        private enum RoomKind { Living, Kitchen, Bedroom, Bathroom, Storage, Shop, Office, Hall }

        private sealed class Room
        {
            public readonly List<Int2> Cells = new List<Int2>();
            public RoomKind Kind;
            public bool HasBed;                                         // a home with no bedroom sleeps in its living room
        }

        private static readonly int[] Dx4 = { 1, -1, 0, 0 };
        private static readonly int[] Dy4 = { 0, 0, 1, -1 };

        /// <summary>Carves the rooms into <paramref name="world"/> and returns the furniture. <paramref name="keepClear"/> are cells that must stay open (stairs).</summary>
        public static List<Furniture> Furnish(BlockWorld world, BuildingInfo b, HashSet<Int2> interior, IReadOnlyCollection<Int2> keepClear)
        {
            var furniture = new List<Furniture>();
            if (interior.Count < MinRoomCells)
            {
                return furniture;
            }
            var rng = new DeterministicRandom(unchecked((ulong)(b.Id * 7919L + 13L)));
            var clear = new HashSet<Int2>(keepClear);
            // The way in stays clear: the cells around every door of the building (listed or not).
            for (int y = b.Min.Y; y <= b.Max.Y; y++)
            {
                for (int x = b.Min.X; x <= b.Max.X; x++)
                {
                    if (!world.InBounds(x, y) || world.GetBlock(x, y).ToggleTo == 0)
                    {
                        continue;
                    }
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            clear.Add(new Int2(x + dx, y + dy));
                        }
                    }
                }
            }
            foreach (Int2 d in b.Doors)
            {
                clear.Add(d);
            }

            bool home = b.Kind == "residential" || b.Kind == "generic";
            var rooms = new List<Room>();
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (Int2 c in interior)
            {
                minX = Math.Min(minX, c.X); minY = Math.Min(minY, c.Y);
                maxX = Math.Max(maxX, c.X); maxY = Math.Max(maxY, c.Y);
            }
            // Shops and big halls keep one open floor with a back room; homes and offices are split into rooms.
            int depth = home ? 4 : (b.Kind == "office" || b.Kind == "school" || b.Kind == "medical") ? 3 : 1;
            Split(world, interior, clear, rng, rooms, minX, minY, maxX, maxY, depth, b);

            AssignKinds(rooms, b, home);
            var taken = new HashSet<Int2>(clear);
            foreach (Room r in rooms)
            {
                Floor(world, r);
                Place(world, interior, taken, rng, r, furniture, b);
                DecorateWalls(world, rng, r, furniture, home);
                PutRug(interior, taken, rng, r, furniture);
            }
            return furniture;
        }

        // ------------------------------------------------------------------ rooms

        private static void Split(BlockWorld world, HashSet<Int2> interior, HashSet<Int2> clear, DeterministicRandom rng, List<Room> rooms,
            int x0, int y0, int x1, int y1, int depth, BuildingInfo b)
        {
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            if (depth > 0 && Math.Max(w, h) >= MinSplitSide * 2 - 1 && CountIn(interior, x0, y0, x1, y1) >= MinRoomCells * 3)
            {
                bool vertical = w > h || (w == h && rng.Range(0, 2) == 0);            // a vertical wall splits the width
                int side = vertical ? w : h;
                int at = (vertical ? x0 : y0) + MinSplitSide - 1 + rng.Range(0, Math.Max(1, side - 2 * (MinSplitSide - 1)));
                if (TryWall(world, interior, clear, rng, vertical, at, x0, y0, x1, y1))
                {
                    if (vertical)
                    {
                        Split(world, interior, clear, rng, rooms, x0, y0, at - 1, y1, depth - 1, b);
                        Split(world, interior, clear, rng, rooms, at + 1, y0, x1, y1, depth - 1, b);
                    }
                    else
                    {
                        Split(world, interior, clear, rng, rooms, x0, y0, x1, at - 1, depth - 1, b);
                        Split(world, interior, clear, rng, rooms, x0, at + 1, x1, y1, depth - 1, b);
                    }
                    return;
                }
            }
            var room = new Room();
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var c = new Int2(x, y);
                    if (interior.Contains(c) && world.GetBlock(x, y).IsAir)
                    {
                        room.Cells.Add(c);
                    }
                }
            }
            if (room.Cells.Count >= MinRoomCells)
            {
                rooms.Add(room);
            }
        }

        /// <summary>A wall along the line `at` inside the rectangle, with one door where both sides are floor. False when it would not make two rooms.</summary>
        private static bool TryWall(BlockWorld world, HashSet<Int2> interior, HashSet<Int2> clear, DeterministicRandom rng, bool vertical, int at,
            int x0, int y0, int x1, int y1)
        {
            if (!world.Registry.TryGet("wall_drywall", out BlockType wall) || !world.Registry.TryGet("door_drywall", out BlockType door))
            {
                return false;
            }
            var line = new List<Int2>();
            var doorSpots = new List<Int2>();
            int from = vertical ? y0 : x0, to = vertical ? y1 : x1;
            for (int t = from; t <= to; t++)
            {
                Int2 c = vertical ? new Int2(at, t) : new Int2(t, at);
                if (!interior.Contains(c))
                {
                    continue;
                }
                line.Add(c);
                Int2 a = vertical ? new Int2(at - 1, t) : new Int2(t, at - 1);
                Int2 d = vertical ? new Int2(at + 1, t) : new Int2(t, at + 1);
                if (interior.Contains(a) && interior.Contains(d) && !clear.Contains(c))
                {
                    doorSpots.Add(c);
                }
            }
            if (line.Count < MinRoomSide || doorSpots.Count == 0)
            {
                return false;
            }
            Int2 doorAt = doorSpots[doorSpots.Count / 3 + rng.Range(0, Math.Max(1, doorSpots.Count - doorSpots.Count / 3 * 2))];
            foreach (Int2 c in line)
            {
                if (clear.Contains(c))
                {
                    continue;                                           // stairs and doorways stay open
                }
                world.SetBlock(c.X, c.Y, 0, BuildLayer.Structure, c == doorAt ? door.Id : wall.Id);
                interior.Remove(c);
                if (c == doorAt)
                {
                    interior.Add(c);                                    // a doorway is still walkable floor, but stays clear of furniture
                    clear.Add(c);
                    Int2 a = vertical ? new Int2(at - 1, c.Y) : new Int2(c.X, at - 1);
                    Int2 d = vertical ? new Int2(at + 1, c.Y) : new Int2(c.X, at + 1);
                    clear.Add(a);
                    clear.Add(d);
                }
            }
            return true;
        }

        private static int CountIn(HashSet<Int2> set, int x0, int y0, int x1, int y1)
        {
            int n = 0;
            foreach (Int2 c in set)
            {
                if (c.X >= x0 && c.X <= x1 && c.Y >= y0 && c.Y <= y1)
                {
                    n++;
                }
            }
            return n;
        }

        // ------------------------------------------------------------------ kinds

        private static void AssignKinds(List<Room> rooms, BuildingInfo b, bool home)
        {
            if (rooms.Count == 0)
            {
                return;
            }
            rooms.Sort((p, q) => q.Cells.Count != p.Cells.Count ? q.Cells.Count.CompareTo(p.Cells.Count) : p.Cells[0].Y != q.Cells[0].Y ? p.Cells[0].Y.CompareTo(q.Cells[0].Y) : p.Cells[0].X.CompareTo(q.Cells[0].X));
            if (!home)
            {
                RoomKind main = b.Kind == "office" || b.Kind == "school" || b.Kind == "medical" ? RoomKind.Office : RoomKind.Shop;
                for (int i = 0; i < rooms.Count; i++)
                {
                    rooms[i].Kind = i == 0 ? main : (i == rooms.Count - 1 && rooms.Count > 1 ? RoomKind.Storage : main);
                }
                return;
            }
            // The room next to the way in is the living room.
            Room entrance = rooms[0];
            foreach (Room r in rooms)
            {
                if (TouchesDoor(r, b))
                {
                    entrance = r;
                    break;
                }
            }
            entrance.Kind = RoomKind.Living;
            Room kitchen = null, bath = null;
            foreach (Room r in rooms)                                   // biggest free room: kitchen
            {
                if (r != entrance && kitchen == null)
                {
                    kitchen = r;
                }
            }
            for (int i = rooms.Count - 1; i >= 0 && rooms.Count >= 4; i--)       // smallest free room: bathroom (only when there are rooms to spare)
            {
                if (rooms[i] != entrance && rooms[i] != kitchen && rooms[i].Cells.Count <= 14)
                {
                    bath = rooms[i];
                    break;
                }
            }
            foreach (Room r in rooms)
            {
                if (r == entrance)
                {
                    continue;
                }
                r.Kind = r == kitchen ? RoomKind.Kitchen : r == bath ? RoomKind.Bathroom : RoomKind.Bedroom;
            }
            if (kitchen == null)
            {
                entrance.Kind = RoomKind.Kitchen;                       // a single room is a studio: kitchen first, a bed is added below
            }
            bool anyBedroom = false;
            foreach (Room r in rooms)
            {
                anyBedroom |= r.Kind == RoomKind.Bedroom;
            }
            if (!anyBedroom)
            {
                entrance.HasBed = true;
            }
        }

        private static bool TouchesDoor(Room r, BuildingInfo b)
        {
            foreach (Int2 c in r.Cells)
            {
                foreach (Int2 d in b.Doors)
                {
                    if (Math.Abs(c.X - d.X) + Math.Abs(c.Y - d.Y) <= 1)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ furniture

        /// <summary>Each room its own floor, as in Project Zomboid: boards in the living areas, tiles in kitchens and bathrooms, concrete in shops and stores.</summary>
        private static void Floor(BlockWorld world, Room room)
        {
            string material;
            switch (room.Kind)
            {
                case RoomKind.Kitchen:
                case RoomKind.Bathroom: material = "stone"; break;
                case RoomKind.Shop:
                case RoomKind.Storage:
                case RoomKind.Hall: material = "concrete"; break;
                case RoomKind.Office: material = "brick"; break;
                default: material = "wood"; break;
            }
            if (!world.Registry.TryGet("floor_" + material, out BlockType floor))
            {
                return;
            }
            foreach (Int2 c in room.Cells)
            {
                world.SetBlock(c.X, c.Y, 0, BuildLayer.Floor, floor.Id);
            }
        }

        private static void Place(BlockWorld world, HashSet<Int2> interior, HashSet<Int2> taken, DeterministicRandom rng, Room room, List<Furniture> into, BuildingInfo b)
        {
            var wallCells = new List<Int2>();
            var openCells = new List<Int2>();
            foreach (Int2 c in room.Cells)
            {
                if (taken.Contains(c))
                {
                    continue;
                }
                (TouchesWall(world, interior, c) ? wallCells : openCells).Add(c);
            }
            Shuffle(wallCells, rng);
            Shuffle(openCells, rng);
            int size = room.Cells.Count;
            switch (room.Kind)
            {
                case RoomKind.Kitchen:
                    if (room.HasBed)
                    {
                        PutBed(world, interior, into, taken, wallCells, openCells);
                        Put(into, taken, wallCells, "wardrobe", 1);
                    }
                    Put(into, taken, wallCells, "fridge", 1);
                    Put(into, taken, wallCells, "stove", 1);
                    Put(into, taken, wallCells, "cupboard", 1 + size / 12);
                    if (size >= 14)
                    {
                        Put(into, taken, openCells, "dining_table", 1);
                        Put(into, taken, openCells, "chair", 2);
                    }
                    break;
                case RoomKind.Bedroom:
                    PutBed(world, interior, into, taken, wallCells, openCells);
                    Put(into, taken, wallCells, "wardrobe", 1);
                    Put(into, taken, wallCells, "nightstand", 1);
                    break;
                case RoomKind.Bathroom:
                    Put(into, taken, wallCells, "toilet", 1);
                    Put(into, taken, wallCells, "bathroom_cabinet", 1);
                    break;
                case RoomKind.Living:
                    if (room.HasBed)
                    {
                        PutBed(world, interior, into, taken, wallCells, openCells);
                        Put(into, taken, wallCells, "wardrobe", 1);
                    }
                    Put(into, taken, openCells, "sofa", 1 + size / 25);
                    Put(into, taken, wallCells, "bookshelf", 1);
                    if (size >= 12)
                    {
                        Put(into, taken, openCells, "coffee_table", 1);
                    }
                    break;
                case RoomKind.Shop:
                    Put(into, taken, wallCells, "shelf", Math.Max(2, size / 6));
                    Put(into, taken, wallCells, "counter", 1 + size / 30);
                    Put(into, taken, openCells, "crate", size / 25);
                    break;
                case RoomKind.Office:
                    Put(into, taken, wallCells, "shelf", 1 + size / 20);
                    Put(into, taken, openCells, "desk", 1 + size / 14);
                    Put(into, taken, openCells, "chair", 1 + size / 14);
                    break;
                case RoomKind.Storage:
                    Put(into, taken, wallCells, "boxes", 1 + size / 5);
                    Put(into, taken, openCells, "crate", size / 10);
                    break;
                default:
                    break;
            }
            if (b.Kind != "residential" && b.Kind != "generic" && room.Kind == RoomKind.Hall)
            {
                Put(into, taken, wallCells, "shelf", 1);
            }
        }

        /// <summary>
        /// The walls you see from inside a room (north and east: the faces turned to the camera): curtains on every window, and in homes one to
        /// three pictures, a clock or a shelf on the plain wall (D-027).
        /// </summary>
        private static void DecorateWalls(BlockWorld world, DeterministicRandom rng, Room room, List<Furniture> into, bool home)
        {
            var plain = new List<Furniture>();
            var used = new HashSet<Int2>();
            foreach (Int2 c in room.Cells)
            {
                for (int side = 0; side < 2; side++)
                {
                    Int2 wall = side == 0 ? new Int2(c.X, c.Y + 1) : new Int2(c.X + 1, c.Y);
                    char face = side == 0 ? 's' : 'w';
                    if (!world.InBounds(wall.X, wall.Y) || used.Contains(wall))
                    {
                        continue;
                    }
                    string key = world.GetBlock(wall.X, wall.Y).Key;
                    if (key.StartsWith("window"))
                    {
                        used.Add(wall);
                        into.Add(new Furniture(wall, "curtain", face));
                    }
                    else if (key == "building_wall" || key.StartsWith("wall_"))
                    {
                        plain.Add(new Furniture(wall, "picture", face));
                    }
                }
            }
            if (!home || plain.Count == 0)
            {
                return;
            }
            string[] pieces = room.Kind == RoomKind.Kitchen ? new[] { "clock", "wall_shelf" }
                : room.Kind == RoomKind.Bathroom ? new[] { "wall_shelf" }
                : new[] { "picture", "picture_tall", "picture", "clock" };
            int count = Math.Min(plain.Count / 3 + 1, room.Kind == RoomKind.Living ? 3 : 2);
            for (int k = 0; k < count && plain.Count > 0; k++)
            {
                int i = rng.Range(0, plain.Count);
                Furniture spot = plain[i];
                plain.RemoveAt(i);
                if (used.Add(spot.Cell))
                {
                    into.Add(new Furniture(spot.Cell, pieces[rng.Range(0, pieces.Length)], spot.Face));
                }
            }
        }

        /// <summary>A rug near the middle of living rooms and bedrooms, where three free cells in a row are left.</summary>
        private static void PutRug(HashSet<Int2> interior, HashSet<Int2> taken, DeterministicRandom rng, Room room, List<Furniture> into)
        {
            if (room.Kind != RoomKind.Living && room.Kind != RoomKind.Bedroom)
            {
                return;
            }
            int cx = 0, cy = 0;
            foreach (Int2 c in room.Cells)
            {
                cx += c.X;
                cy += c.Y;
            }
            cx /= room.Cells.Count;
            cy /= room.Cells.Count;
            Int2 best = default;
            int bestDist = int.MaxValue;
            foreach (Int2 c in room.Cells)
            {
                bool free = true;
                for (int dx = 0; dx < 3 && free; dx++)
                {
                    var k = new Int2(c.X + dx, c.Y);
                    free = interior.Contains(k) && !taken.Contains(k);
                }
                int d = Math.Abs(c.X + 1 - cx) + Math.Abs(c.Y - cy);
                if (free && d < bestDist)
                {
                    best = c;
                    bestDist = d;
                }
            }
            if (bestDist == int.MaxValue)
            {
                return;
            }
            for (int dx = 0; dx < 3; dx++)
            {
                taken.Add(new Int2(best.X + dx, best.Y));                 // nothing stands on it
            }
            into.Add(new Furniture(best, rng.Range(0, 2) == 0 ? "rug" : "rug_small"));
        }

        private static void Put(List<Furniture> into, HashSet<Int2> taken, List<Int2> cells, string kind, int count)
        {
            for (int i = 0; i < cells.Count && count > 0; i++)
            {
                Int2 c = cells[i];
                if (taken.Contains(c))
                {
                    continue;
                }
                taken.Add(c);
                into.Add(new Furniture(c, kind));
                count--;
            }
        }

        /// <summary>A bed needs a 2x2 spot; the cell recorded is its south-west corner.</summary>
        private static void PutBed(BlockWorld world, HashSet<Int2> interior, List<Furniture> into, HashSet<Int2> taken, List<Int2> wallCells, List<Int2> openCells)
        {
            foreach (List<Int2> source in new[] { wallCells, openCells })
            {
                foreach (Int2 c in source)
                {
                    var cells = new[] { c, new Int2(c.X + 1, c.Y), new Int2(c.X, c.Y + 1), new Int2(c.X + 1, c.Y + 1) };
                    bool free = true;
                    foreach (Int2 k in cells)
                    {
                        if (!interior.Contains(k) || taken.Contains(k) || !world.GetBlock(k.X, k.Y).IsAir)
                        {
                            free = false;
                            break;
                        }
                    }
                    if (free)
                    {
                        foreach (Int2 k in cells)
                        {
                            taken.Add(k);
                        }
                        into.Add(new Furniture(c, "bed"));
                        return;
                    }
                }
            }
        }

        private static bool TouchesWall(BlockWorld world, HashSet<Int2> interior, Int2 c)
        {
            for (int k = 0; k < 4; k++)
            {
                var n = new Int2(c.X + Dx4[k], c.Y + Dy4[k]);
                if (!interior.Contains(n) || !world.GetBlock(n.X, n.Y).IsAir)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Shuffle(List<Int2> list, DeterministicRandom rng)
        {
            list.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));          // a stable start, then the building's own shuffle
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                Int2 t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }
    }
}
