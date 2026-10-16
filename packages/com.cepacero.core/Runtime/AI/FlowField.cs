using System;
using System.Collections.Generic;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.AI
{
    /// <summary>
    /// Shared pathfinding for crowds: one Dijkstra from the targets (players,
    /// noise, the base) gives every cell its cost-to-target, and each zombie
    /// just steps to its cheapest neighbour. Cost is independent of how many
    /// zombies use it. Host only.
    ///
    /// Cell costs are computed lazily and both caches are invalidated with a
    /// stamp instead of being cleared, so a build bounded by
    /// <c>maxDistance</c> only touches the cells it explores.
    /// </summary>
    /// <summary>A starting cell for the search with the cost already paid to get there (0 for a player; more for a stairwell leading to one).</summary>
    public readonly struct FlowSeed
    {
        public FlowSeed(Int2 cell, int cost)
        {
            Cell = cell;
            Cost = cost;
        }

        public Int2 Cell { get; }

        public int Cost { get; }
    }

    public sealed class FlowField
    {
        public const int Unreachable = int.MaxValue;

        private const int StraightFactor = 10;
        private const int DiagonalFactor = 14;

        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private readonly int[] _distance;
        private readonly int[] _distanceStamp;
        private readonly int[] _cellCost;
        private readonly int[] _costStamp;
        private readonly bool[] _open;
        private readonly MinHeap _heap;

        private int _stamp;
        private GroundLayer _ground;
        private BlockWorld _blocks;
        private PathPolicy _policy;

        public FlowField(int width, int height, int level = 0)
        {
            Level = level;
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }
            Width = width;
            Height = height;
            int cells = width * height;
            _distance = new int[cells];
            _distanceStamp = new int[cells];
            _cellCost = new int[cells];
            _costStamp = new int[cells];
            _open = new bool[cells];
            _heap = new MinHeap(width * 4);
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>The level this field covers (D-022): every level has its own, linked through stairwells.</summary>
        public int Level { get; }

        /// <summary>World structure revision this field was built from (see <see cref="BlockWorld.StructureRevision"/>).</summary>
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
            _scratchSeeds.Clear();
            for (int t = 0; t < targets.Count; t++)
            {
                _scratchSeeds.Add(new FlowSeed(targets[t], 0));
            }
            Build(ground, blocks, policy, _scratchSeeds, maxDistance);
        }

        private readonly List<FlowSeed> _scratchSeeds = new List<FlowSeed>();

        /// <summary>As above, but each starting cell carries the cost already paid (a stairwell towards the players is a seed with that cost).</summary>
        public void Build(GroundLayer ground, BlockWorld blocks, PathPolicy policy, IReadOnlyList<FlowSeed> seeds,
            int maxDistance = Unreachable)
        {
            if (ground.Width != Width || ground.Height != Height || blocks.Width != Width || blocks.Height != Height)
            {
                throw new ArgumentException("Ground, blocks and field must have the same size.");
            }
            _ground = ground;
            _blocks = blocks;
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            NextStamp();

            _heap.Clear();
            for (int t = 0; t < seeds.Count; t++)
            {
                Int2 target = seeds[t].Cell;
                if (InBounds(target.X, target.Y) && seeds[t].Cost <= maxDistance)
                {
                    int i = target.Y * Width + target.X;
                    if (seeds[t].Cost < Distance(i))
                    {
                        SetDistance(i, seeds[t].Cost);
                        _heap.Push(seeds[t].Cost, i);
                    }
                }
            }

            int visited = 0;
            while (_heap.Count > 0)
            {
                int current = _heap.Pop(out int dist);
                if (dist > Distance(current))
                {
                    continue; // stale heap entry
                }
                visited++;
                int cx = current % Width;
                int cy = current / Width;
                // The walker moves from a neighbour into current, so it pays to enter current
                // (targets themselves are free: zombies attack whatever stands there).
                int enter = dist == 0 ? StraightFactor : Cost(current);
                if (enter == 0)
                {
                    continue;
                }
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Dx[d];
                    int ny = cy + Dy[d];
                    if (!InBounds(nx, ny))
                    {
                        continue;
                    }
                    int n = ny * Width + nx;
                    if (Cost(n) == 0)
                    {
                        continue;
                    }
                    bool diagonal = d >= 4;
                    if (diagonal && (!IsOpen(cx, ny) || !IsOpen(nx, cy)))
                    {
                        continue; // no corner cutting through walls
                    }
                    int candidate = dist + (diagonal ? enter * DiagonalFactor / StraightFactor : enter);
                    if (candidate < Distance(n) && candidate <= maxDistance)
                    {
                        SetDistance(n, candidate);
                        _heap.Push(candidate, n);
                    }
                }
            }
            LastBuildVisited = visited;
            BuiltFromRevision = blocks.StructureRevision;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>Cost to the nearest target, or <see cref="Unreachable"/>.</summary>
        public int GetDistance(int x, int y) => InBounds(x, y) ? Distance(y * Width + x) : Unreachable;

        /// <summary>
        /// Direction (each component −1, 0 or 1) towards the next cell on the
        /// cheapest path. Zero when at a target or unreachable. The next cell may
        /// be a breakable block: the caller attacks it instead of moving.
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
                int n = ny * Width + nx;
                int dist = Distance(n);
                if (dist == Unreachable)
                {
                    continue;
                }
                bool diagonal = d >= 4;
                if (diagonal && (!IsOpen(x, ny) || !IsOpen(nx, y)))
                {
                    continue;
                }
                int enter = dist == 0 ? StraightFactor : Cost(n);
                int total = dist + (diagonal ? enter * DiagonalFactor / StraightFactor : enter);
                if (total < best)
                {
                    best = total;
                    bestDir = d;
                }
            }
            return bestDir < 0 ? new Int2(0, 0) : new Int2(Dx[bestDir], Dy[bestDir]);
        }

        private void NextStamp()
        {
            _stamp++;
            if (_stamp == int.MaxValue)
            {
                // After ~2 billion builds: wipe and start over.
                Array.Clear(_distanceStamp, 0, _distanceStamp.Length);
                Array.Clear(_costStamp, 0, _costStamp.Length);
                _stamp = 1;
            }
        }

        private int Distance(int i) => _stamp != 0 && _distanceStamp[i] == _stamp ? _distance[i] : Unreachable;

        private void SetDistance(int i, int value)
        {
            _distance[i] = value;
            _distanceStamp[i] = _stamp;
        }

        private int Cost(int i)
        {
            if (_costStamp[i] != _stamp)
            {
                int x = i % Width;
                int y = i / Width;
                _cellCost[i] = _policy.CellCost(_ground, _blocks, x, y, Level);
                _open[i] = _cellCost[i] > 0 && !_blocks.IsSolidAt(x, y, Level);
                _costStamp[i] = _stamp;
            }
            return _cellCost[i];
        }

        // Open = walkable without breaking anything.
        private bool IsOpen(int x, int y)
        {
            int i = y * Width + x;
            Cost(i);
            return _open[i];
        }
    }
}
