using System;
using System.Collections.Generic;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.AI
{
    /// <summary>
    /// Shared pathfinding for crowds: one Dijkstra from the targets (players,
    /// noise, the base) gives every cell its cost-to-target, and each zombie
    /// just steps to its cheapest neighbour. Cost is O(cells) per rebuild
    /// regardless of how many zombies use it. Host only.
    /// </summary>
    public sealed class FlowField
    {
        public const int Unreachable = int.MaxValue;

        private const int StraightFactor = 10;
        private const int DiagonalFactor = 14;

        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private readonly int[] _distance;
        private readonly int[] _cellCost;
        private readonly bool[] _open;
        private readonly MinHeap _heap;

        public FlowField(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }
            Width = width;
            Height = height;
            _distance = new int[width * height];
            _cellCost = new int[width * height];
            _open = new bool[width * height];
            _heap = new MinHeap(width * 4);
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>World revision this field was built from (see <see cref="BlockWorld.Revision"/>).</summary>
        public int BuiltFromRevision { get; private set; } = -1;

        /// <summary>Cells settled in the last build. Useful for profiling.</summary>
        public int LastBuildVisited { get; private set; }

        /// <summary>
        /// Recomputes the field. <paramref name="maxDistance"/> bounds the
        /// search (cells farther away stay unreachable) to keep big maps cheap.
        /// </summary>
        public void Build(GroundLayer ground, BlockWorld blocks, PathPolicy policy, IReadOnlyList<Int2> targets,
            int maxDistance = Unreachable)
        {
            if (ground.Width != Width || ground.Height != Height || blocks.Width != Width || blocks.Height != Height)
            {
                throw new ArgumentException("Ground, blocks and field must have the same size.");
            }
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    _cellCost[i] = policy.CellCost(ground, blocks, x, y);
                    _open[i] = _cellCost[i] > 0 && !blocks.GetBlock(x, y).IsSolid;
                    _distance[i] = Unreachable;
                }
            }

            _heap.Clear();
            for (int t = 0; t < targets.Count; t++)
            {
                Int2 target = targets[t];
                if (InBounds(target.X, target.Y))
                {
                    int i = target.Y * Width + target.X;
                    _distance[i] = 0;
                    _heap.Push(0, i);
                }
            }

            int visited = 0;
            while (_heap.Count > 0)
            {
                int current = _heap.Pop(out int dist);
                if (dist > _distance[current])
                {
                    continue; // stale heap entry
                }
                visited++;
                int cx = current % Width;
                int cy = current / Width;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Dx[d];
                    int ny = cy + Dy[d];
                    if (!InBounds(nx, ny))
                    {
                        continue;
                    }
                    int n = ny * Width + nx;
                    // The walker moves from n to current, so it pays to enter current
                    // (targets themselves are free: zombies attack whatever stands there).
                    int enter = dist == 0 ? StraightFactor : _cellCost[current];
                    if (enter == 0 || _cellCost[n] == 0)
                    {
                        continue;
                    }
                    bool diagonal = d >= 4;
                    if (diagonal && (!IsOpen(cx, ny) || !IsOpen(nx, cy)))
                    {
                        continue; // no corner cutting through walls
                    }
                    int step = diagonal ? enter * DiagonalFactor / StraightFactor : enter;
                    int candidate = dist + step;
                    if (candidate < _distance[n] && candidate <= maxDistance)
                    {
                        _distance[n] = candidate;
                        _heap.Push(candidate, n);
                    }
                }
            }
            LastBuildVisited = visited;
            BuiltFromRevision = blocks.Revision;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>Cost to the nearest target, or <see cref="Unreachable"/>.</summary>
        public int GetDistance(int x, int y) => InBounds(x, y) ? _distance[y * Width + x] : Unreachable;

        /// <summary>
        /// Direction (each component −1, 0 or 1) towards the cheapest neighbour.
        /// Zero when at a target or unreachable. The neighbour may be a breakable
        /// block: the caller attacks it instead of moving.
        /// </summary>
        public Int2 GetStep(int x, int y)
        {
            int own = GetDistance(x, y);
            if (own == Unreachable || own == 0)
            {
                return new Int2(0, 0);
            }
            // Pick the neighbour that minimises "its distance + the cost of entering it",
            // the same relation Build() used: entering a wall pays for breaking it.
            int best = Unreachable;
            int bestDir = -1;
            for (int d = 0; d < 8; d++)
            {
                int nx = x + Dx[d];
                int ny = y + Dy[d];
                if (!InBounds(nx, ny))
                {
                    continue;
                }
                bool diagonal = d >= 4;
                if (diagonal && (!IsOpen(x, ny) || !IsOpen(nx, y)))
                {
                    continue;
                }
                int n = ny * Width + nx;
                int dist = _distance[n];
                if (dist == Unreachable)
                {
                    continue;
                }
                int enter = dist == 0 ? StraightFactor : _cellCost[n];
                int total = dist + (diagonal ? enter * DiagonalFactor / StraightFactor : enter);
                if (total < best)
                {
                    best = total;
                    bestDir = d;
                }
            }
            return bestDir < 0 ? new Int2(0, 0) : new Int2(Dx[bestDir], Dy[bestDir]);
        }

        // Open = walkable without breaking anything.
        private bool IsOpen(int x, int y) => _open[y * Width + x];
    }
}
