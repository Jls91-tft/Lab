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
            return Furnish(world, b, interior, keepClear, null);
        }

        /// <summary>As above, and fills <paramref name="paint"/> with the wall paint of every room's floor cell.</summary>
        public static List<Furniture> Furnish(BlockWorld world, BuildingInfo b, HashSet<Int2> interior, IReadOnlyCollection<Int2> keepClear, Dictionary<Int2, string> paint)
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
                if (paint != null)
                {
                    string p = PaintFor(r.Kind, rng);
                    if (p != null)
                    {
                        foreach (Int2 c in r.Cells)
                        {
                            paint[c] = p;
                        }
                    }
                }
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
        private static readonly string[] LivingPaint = { "cream", "ochre", "sage", "cream" };
        private static readonly string[] BedroomPaint = { "blue", "rose", "sage", "cream" };
        private static readonly string[] WorkPaint = { "grey", "cream", "grey" };

        /// <summary>What a room's walls look like from inside (spritegen iso.PAPERS): tiles in kitchens and bathrooms, paper or paint elsewhere, bare in stores.</summary>
        private static string PaintFor(RoomKind kind, DeterministicRandom rng)
        {
            switch (kind)
            {
                case RoomKind.Kitchen: return "kitchen";
                case RoomKind.Bathroom: return "bath";
                case RoomKind.Living: return LivingPaint[rng.Range(0, LivingPaint.Length)];
                case RoomKind.Bedroom: return BedroomPaint[rng.Range(0, BedroomPaint.Length)];
                case RoomKind.Storage: return null;
                default: return WorkPaint[rng.Range(0, WorkPaint.Length)];
            }
        }

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
                    // A kitchen counter along one wall, fridge, stove and cupboards side by side, and a table with its chairs in the middle.
                    PutRun(world, room, taken, into, "fridge", "cupboard", "sink", "stove", "cupboard", "washer", "cupboard");
                    if (size >= 14)
                    {
                        PutTable(room, taken, into, "dining_table", "chair", 2);
                    }
                    break;
                case RoomKind.Bedroom:
                    PutBed(world, interior, into, taken, wallCells, openCells);
                    Put(into, taken, Free(wallCells, taken), "wardrobe", 1);
                    Put(into, taken, Free(wallCells, taken), "nightstand", 1);
                    Put(into, taken, Free(wallCells, taken), "plant", size / 45);
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
                    // The sofa against a wall with the coffee table in front of it; a bookshelf on another wall.
                    PutRun(world, room, taken, into, "sofa", "sofa");
                    if (size >= 12)
                    {
                        PutInFront(world, room, taken, into, "sofa", "coffee_table");
                    }
                    Put(into, taken, Free(wallCells, taken), "lamp", 1);
                    PutRun(world, room, taken, into, "tv");
                    Put(into, taken, Free(wallCells, taken), "bookshelf", 1);
                    Put(into, taken, Free(wallCells, taken), "plant", size / 40);
                    break;
                case RoomKind.Shop:
                    PutRun(world, room, taken, into, Repeat("shelf", Math.Max(2, size / 6)));
                    PutRun(world, room, taken, into, Repeat("counter", 1 + size / 30));
                    Put(into, taken, Free(wallCells, taken), "crate", size / 25);
                    break;
                case RoomKind.Office:
                    PutRun(world, room, taken, into, Repeat("desk", 1 + size / 14));
                    PutInFront(world, room, taken, into, "desk", "chair");
                    Put(into, taken, Free(wallCells, taken), "shelf", 1 + size / 20);
                    break;
                case RoomKind.Storage:
                    PutRun(world, room, taken, into, Repeat("boxes", 1 + size / 5));
                    Put(into, taken, Free(wallCells, taken), "crate", size / 10);
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

        private static string[] Repeat(string kind, int n)
        {
            var a = new string[Math.Max(0, n)];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = kind;
            }
            return a;
        }

        private static List<Int2> Free(List<Int2> cells, HashSet<Int2> taken)
        {
            var free = new List<Int2>();
            foreach (Int2 c in cells)
            {
                if (!taken.Contains(c))
                {
                    free.Add(c);
                }
            }
            return free;
        }

        private static bool IsRealWall(BlockWorld world, int x, int y)
        {
            if (!world.InBounds(x, y))
            {
                return true;
            }
            BlockType t = world.GetBlock(x, y);
            return !t.IsAir && t.IsSolid && !t.Key.StartsWith("stairs") && t.ToggleTo == 0;          // doors are not wall to lean on
        }

        /// <summary>
        /// Pieces side by side along one wall, the longest free stretch of the room (the back walls, north and east, win a tie: they are the
        /// ones in view). Like a kitchen counter or a row of shelves in Project Zomboid (D-027).
        /// </summary>
        private static void PutRun(BlockWorld world, Room room, HashSet<Int2> taken, List<Furniture> into, params string[] kinds)
        {
            if (kinds.Length == 0)
            {
                return;
            }
            var cells = new HashSet<Int2>(room.Cells);
            List<Int2> best = null;
            int[] dxs = { 0, 1, -1, 0 };                                  // north, east, west, south
            int[] dys = { 1, 0, 0, -1 };
            for (int side = 0; side < 4; side++)
            {
                bool alongX = dys[side] != 0;
                var lines = new Dictionary<int, List<Int2>>();
                foreach (Int2 c in room.Cells)
                {
                    if (!taken.Contains(c) && IsRealWall(world, c.X + dxs[side], c.Y + dys[side]))
                    {
                        int line = alongX ? c.Y : c.X;
                        if (!lines.TryGetValue(line, out List<Int2> l))
                        {
                            lines[line] = l = new List<Int2>();
                        }
                        l.Add(c);
                    }
                }
                foreach (List<Int2> line in lines.Values)
                {
                    line.Sort((a, b) => alongX ? a.X.CompareTo(b.X) : b.Y.CompareTo(a.Y));
                    var run = new List<Int2>();
                    for (int i = 0; i < line.Count; i++)
                    {
                        bool next = i > 0 && (alongX ? line[i].X == line[i - 1].X + 1 : line[i].Y == line[i - 1].Y - 1);
                        if (!next)
                        {
                            run = new List<Int2>();
                        }
                        run.Add(line[i]);
                        if (best == null || run.Count > best.Count)
                        {
                            best = new List<Int2>(run);
                        }
                    }
                }
            }
            if (best == null)
            {
                return;
            }
            for (int i = 0; i < kinds.Length && i < best.Count; i++)
            {
                taken.Add(best[i]);
                into.Add(new Furniture(best[i], kinds[i]));
            }
        }

        /// <summary>A table near the middle of the room with its chairs on either side.</summary>
        private static void PutTable(Room room, HashSet<Int2> taken, List<Furniture> into, string table, string chair, int chairs)
        {
            int cx = 0, cy = 0;
            foreach (Int2 c in room.Cells)
            {
                cx += c.X;
                cy += c.Y;
            }
            cx /= room.Cells.Count;
            cy /= room.Cells.Count;
            var cells = new HashSet<Int2>(room.Cells);
            Int2 at = default;
            int bestD = int.MaxValue;
            foreach (Int2 c in room.Cells)
            {
                var w = new Int2(c.X - 1, c.Y);
                var e = new Int2(c.X + 1, c.Y);
                bool ok = !taken.Contains(c) && cells.Contains(w) && !taken.Contains(w) && cells.Contains(e) && !taken.Contains(e);
                int d = Math.Abs(c.X - cx) + Math.Abs(c.Y - cy);
                if (ok && d < bestD)
                {
                    at = c;
                    bestD = d;
                }
            }
            if (bestD == int.MaxValue)
            {
                return;
            }
            taken.Add(at);
            into.Add(new Furniture(at, table));
            var sides = new[] { new Int2(at.X - 1, at.Y), new Int2(at.X + 1, at.Y), new Int2(at.X, at.Y - 1), new Int2(at.X, at.Y + 1) };
            for (int i = 0, placed = 0; i < sides.Length && placed < chairs; i++)
            {
                if (cells.Contains(sides[i]) && !taken.Contains(sides[i]))
                {
                    taken.Add(sides[i]);
                    into.Add(new Furniture(sides[i], chair));
                    placed++;
                }
            }
        }

        /// <summary>One piece on the free cell in front of the first piece of <paramref name="behind"/> (a coffee table before the sofa, a chair at a desk).</summary>
        private static void PutInFront(BlockWorld world, Room room, HashSet<Int2> taken, List<Furniture> into, string behind, string kind)
        {
            var cells = new HashSet<Int2>(room.Cells);
            foreach (Furniture f in into)
            {
                if (f.Kind != behind || !cells.Contains(f.Cell))
                {
                    continue;
                }
                for (int k = 0; k < 4; k++)
                {
                    var wall = new Int2(f.Cell.X + Dx4[k], f.Cell.Y + Dy4[k]);
                    var front = new Int2(f.Cell.X - Dx4[k], f.Cell.Y - Dy4[k]);
                    if (IsRealWall(world, wall.X, wall.Y) && cells.Contains(front) && !taken.Contains(front))
                    {
                        taken.Add(front);
                        into.Add(new Furniture(front, kind));
                        return;
                    }
                }
            }
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
                // A real wall, window or door next to it: not just the edge of the flooded interior, nor a stairwell (D-027).
                var n = new Int2(c.X + Dx4[k], c.Y + Dy4[k]);
                if (!world.InBounds(n.X, n.Y))
                {
                    return true;
                }
                BlockType t = world.GetBlock(n.X, n.Y);
                if (!t.IsAir && t.IsSolid && !t.Key.StartsWith("stairs"))
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
