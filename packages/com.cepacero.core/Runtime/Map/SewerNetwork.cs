using System;
using System.Collections.Generic;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Map
{
    /// <summary>
    /// A sewer under the roads of an imported map (D-022 phase H): one narrow tunnel on level −1 under the middle of every long street, with a
    /// steel manhole in the pavement now and then. Players get down through them (open them, climb down); zombies have to break the steel to
    /// follow, so the sewers are a way across the city and a place to hide, not a free bunker. Needs the construction blocks.
    /// </summary>
    public static class SewerNetwork
    {
        private const int MinRoad = 3;           // tiles of carriageway
        private const int MaxRoad = 8;
        private const int MinLength = 10;        // a crossing street is not a spine
        private const int ManholeEvery = 16;

        /// <summary>Digs the tunnels and places the manholes. Returns how many tunnel cells were dug.</summary>
        public static int Generate(BlockWorld world, GroundLayer ground)
        {
            BlockRegistry reg = world.Registry;
            if (world.MinLevel > -1 || !reg.TryGet("cave", out BlockType cave) || !reg.TryGet("hatch_steel", out BlockType hatch))
            {
                return 0;
            }
            var spine = new HashSet<Int2>();
            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    if (!IsRoad(ground, x, y))
                    {
                        continue;
                    }
                    if (IsCentre(ground, x, y, 0, 1) && Extent(ground, x, y, 1, 0) >= MinLength)
                    {
                        spine.Add(new Int2(x, y));                                  // a street that runs east-west
                    }
                    if (IsCentre(ground, x, y, 1, 0) && Extent(ground, x, y, 0, 1) >= MinLength)
                    {
                        spine.Add(new Int2(x, y));                                  // one that runs north-south
                    }
                }
            }
            Bridge(ground, spine);
            int dug = 0;
            foreach (Int2 c in spine)
            {
                world.SetBlock(c.X, c.Y, -1, BuildLayer.Floor, cave.Id);
                dug++;
            }
            foreach (Int2 c in spine)
            {
                if ((c.X + 3 * c.Y) % ManholeEvery == 0 && world.GetTypeId(c.X, c.Y, 0, BuildLayer.Structure) == BlockRegistry.AirId
                    && world.GetTypeId(c.X, c.Y, 0, BuildLayer.Floor) == BlockRegistry.AirId)
                {
                    world.SetBlock(c.X, c.Y, 0, BuildLayer.Floor, hatch.Id);
                }
            }
            return dug;
        }

        /// <summary>A junction breaks both spines (the crossing street is too long to be a centre): join them across it.</summary>
        private static void Bridge(GroundLayer ground, HashSet<Int2> spine)
        {
            var added = new List<Int2>();
            foreach (Int2 c in spine)
            {
                foreach (Int2 d in new[] { new Int2(1, 0), new Int2(0, 1) })
                {
                    for (int step = 1; step <= MaxRoad; step++)
                    {
                        var n = new Int2(c.X + d.X * step, c.Y + d.Y * step);
                        if (!IsRoad(ground, n.X, n.Y))
                        {
                            break;
                        }
                        if (spine.Contains(n))
                        {
                            for (int k = 1; k < step; k++)
                            {
                                added.Add(new Int2(c.X + d.X * k, c.Y + d.Y * k));
                            }
                            break;
                        }
                    }
                }
            }
            foreach (Int2 c in added)
            {
                spine.Add(c);
            }
        }

        private static bool IsRoad(GroundLayer ground, int x, int y) =>
            x >= 0 && y >= 0 && x < ground.Width && y < ground.Height && ground.Get(x, y) == GroundType.Asphalt;

        /// <summary>The cell is the middle of a run of road of MinRoad..MaxRoad cells along (dx,dy) (a run too long is a junction or a square).</summary>
        private static bool IsCentre(GroundLayer ground, int x, int y, int dx, int dy)
        {
            int before = 0, after = 0;
            while (IsRoad(ground, x - dx * (before + 1), y - dy * (before + 1)) && before < MaxRoad)
            {
                before++;
            }
            while (IsRoad(ground, x + dx * (after + 1), y + dy * (after + 1)) && after < MaxRoad)
            {
                after++;
            }
            int length = before + after + 1;
            return length >= MinRoad && length <= MaxRoad && before == length / 2;
        }

        /// <summary>How far the road continues both ways along (dx,dy) from a cell (capped).</summary>
        private static int Extent(GroundLayer ground, int x, int y, int dx, int dy)
        {
            int n = 1;
            for (int s = 1; s <= MinLength && IsRoad(ground, x + dx * s, y + dy * s); s++)
            {
                n++;
            }
            for (int s = 1; s <= MinLength && IsRoad(ground, x - dx * s, y - dy * s); s++)
            {
                n++;
            }
            return n;
        }
    }
}
