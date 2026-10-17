using System.Collections.Generic;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Path for the debug bridge's "walk" (D-023): breadth-first over cells, 4 neighbours,
    /// on the player's level, where a cell is walkable when a player standing at its centre
    /// would be free by the same rule as movement (<see cref="TileCollision.IsFree"/>).
    /// Debug only: allocates, runs once per walk.
    /// </summary>
    public static class DebugPathfinder
    {
        private static readonly Int2[] Steps = { new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1) };

        /// <summary>Cells from the one after <paramref name="from"/> to <paramref name="to"/>; empty if already there, null if unreachable.</summary>
        public static List<Int2> Find(BlockWorld blocks, GroundLayer ground, int level, float radius, Int2 from, Int2 to, int maxCells = 200000)
        {
            if (!Walkable(blocks, ground, level, radius, to))
            {
                return null;
            }
            if (from == to)
            {
                return new List<Int2>();
            }
            int w = blocks.Width;
            var cameFrom = new Dictionary<int, int> { [from.Y * w + from.X] = -1 };
            var open = new Queue<Int2>();
            open.Enqueue(from);
            while (open.Count > 0 && cameFrom.Count < maxCells)
            {
                Int2 c = open.Dequeue();
                foreach (Int2 s in Steps)
                {
                    var n = new Int2(c.X + s.X, c.Y + s.Y);
                    int key = n.Y * w + n.X;
                    if (!blocks.InBounds(n.X, n.Y) || cameFrom.ContainsKey(key) || !Walkable(blocks, ground, level, radius, n))
                    {
                        continue;
                    }
                    cameFrom[key] = c.Y * w + c.X;
                    if (n == to)
                    {
                        return Rebuild(cameFrom, key, w);
                    }
                    open.Enqueue(n);
                }
            }
            return null;
        }

        public static bool Walkable(BlockWorld blocks, GroundLayer ground, int level, float radius, Int2 cell) =>
            blocks.InBounds(cell.X, cell.Y) && TileCollision.IsFree(new Float2(cell.X + 0.5f, cell.Y + 0.5f), radius, blocks, ground, level);

        private static List<Int2> Rebuild(Dictionary<int, int> cameFrom, int key, int w)
        {
            var path = new List<Int2>();
            while (cameFrom[key] != -1)
            {
                path.Add(new Int2(key % w, key / w));
                key = cameFrom[key];
            }
            path.Reverse();
            return path;
        }
    }
}
