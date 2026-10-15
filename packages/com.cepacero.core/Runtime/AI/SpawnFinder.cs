using System.Collections.Generic;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.AI
{
    /// <summary>
    /// Picks where the horde enters: outdoors, on walkable free ground, on a
    /// ring around the players (out of sight but not too far) and reachable
    /// through the flow field.
    /// </summary>
    public static class SpawnFinder
    {
        public static bool TryFind(GroundLayer ground, BlockWorld blocks, FlowField field, IReadOnlyList<Float2> players,
            DeterministicRandom rng, float minRadius, float maxRadius, out Float2 position, int attempts = 64)
        {
            position = default;
            if (players.Count == 0)
            {
                return false;
            }
            for (int a = 0; a < attempts; a++)
            {
                Float2 anchor = players[rng.Range(0, players.Count)];
                float angle = rng.NextFloat() * 2f * System.MathF.PI;
                float radius = minRadius + rng.NextFloat() * (maxRadius - minRadius);
                var p = new Float2(anchor.X + System.MathF.Cos(angle) * radius, anchor.Y + System.MathF.Sin(angle) * radius);
                Int2 t = p.Tile;
                if (!ground.InBounds(t.X, t.Y))
                {
                    continue;
                }
                GroundType g = ground.Get(t.X, t.Y);
                if (g == GroundType.Water || g == GroundType.Floor || blocks.IsSolid(t.X, t.Y))
                {
                    continue; // never inside buildings or in the river
                }
                if (field.GetDistance(t.X, t.Y) == FlowField.Unreachable)
                {
                    continue;
                }
                if (TooClose(p, players, minRadius))
                {
                    continue;
                }
                position = new Float2(t.X + 0.5f, t.Y + 0.5f);
                return true;
            }
            return false;
        }

        private static bool TooClose(Float2 p, IReadOnlyList<Float2> players, float minRadius)
        {
            foreach (Float2 player in players)
            {
                if ((player - p).LengthSquared < minRadius * minRadius)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
