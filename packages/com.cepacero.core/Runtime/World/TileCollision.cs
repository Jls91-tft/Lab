using System;
using CepaCero.Util;

namespace CepaCero.World
{
    /// <summary>
    /// Circle-vs-tile movement shared by players and zombies. Moves in
    /// sub-steps no longer than a quarter tile (no tunnelling through thin
    /// walls at sprint speed) and slides along walls axis by axis.
    /// Deterministic: the predicting client and the host get the same result.
    /// </summary>
    public static class TileCollision
    {
        private const float MaxSubStep = 0.25f;

        public static Float2 Move(Float2 position, Float2 delta, float radius, BlockWorld blocks, GroundLayer ground, int level = 0)
        {
            float length = delta.Length;
            if (length < 1e-6f)
            {
                return position;
            }
            int steps = Math.Max(1, (int)MathF.Ceiling(length / MaxSubStep));
            Float2 step = delta * (1f / steps);
            Float2 p = position;
            for (int s = 0; s < steps; s++)
            {
                var both = p + step;
                if (IsFree(both, radius, blocks, ground, level))
                {
                    p = both;
                    continue;
                }
                var xOnly = new Float2(p.X + step.X, p.Y);
                var yOnly = new Float2(p.X, p.Y + step.Y);
                if (step.X != 0f && IsFree(xOnly, radius, blocks, ground, level))
                {
                    p = xOnly;
                }
                else if (step.Y != 0f && IsFree(yOnly, radius, blocks, ground, level))
                {
                    p = yOnly;
                }
                else
                {
                    break;
                }
            }
            return p;
        }

        /// <summary>True if a circle at <paramref name="p"/> overlaps no solid block, water or map edge.</summary>
        public static bool IsFree(Float2 p, float radius, BlockWorld blocks, GroundLayer ground, int level = 0)
        {
            int minX = (int)MathF.Floor(p.X - radius);
            int maxX = (int)MathF.Floor(p.X + radius);
            int minY = (int)MathF.Floor(p.Y - radius);
            int maxY = (int)MathF.Floor(p.Y + radius);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!blocks.InBounds(x, y) || blocks.IsSolidAt(x, y, level) || (level == 0 && GroundLayer.WalkCost(ground.Get(x, y)) == 0))
                    {
                        if (CircleTouchesCell(p, radius, x, y))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>True if the circle overlaps the tile's square (not just its bounding box).</summary>
        public static bool CircleTouchesCell(Float2 c, float radius, int x, int y)
        {
            float nx = Math.Clamp(c.X, x, x + 1f);
            float ny = Math.Clamp(c.Y, y, y + 1f);
            float dx = c.X - nx;
            float dy = c.Y - ny;
            return dx * dx + dy * dy < radius * radius;
        }

        /// <summary>No solid block on the straight segment (sampled every quarter tile). Used for sight and melee.</summary>
        public static bool HasClearLine(Float2 from, Float2 to, BlockWorld blocks, int level = 0)
        {
            Float2 delta = to - from;
            int samples = Math.Max(1, (int)MathF.Ceiling(delta.Length * 4f));
            for (int s = 1; s < samples; s++)
            {
                Int2 t = (from + delta * (s / (float)samples)).Tile;
                if (blocks.IsSolidAt(t.X, t.Y, level))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
