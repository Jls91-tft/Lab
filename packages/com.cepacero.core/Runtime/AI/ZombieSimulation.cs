using System;
using System.Collections.Generic;
using System.Globalization;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.AI
{
    /// <summary>
    /// Host-authoritative simulation of every zombie, stored as parallel
    /// arrays (no GameObject per zombie). Zombies follow a shared
    /// <see cref="FlowField"/> towards the players, smash blocks in the way
    /// and hit players in range. Clients never run this: they get
    /// snapshots (<see cref="ZombieSnapshotCodec"/>).
    /// </summary>
    public sealed class ZombieSimulation
    {
        private const float PlayerRadius = 0.4f;
        private const float SeparationStrength = 2.5f;
        private const float RestSpeedFactor = 0.3f;

        private readonly BlockWorld _blocks;
        private readonly GroundLayer _ground;
        private readonly ZombieCatalog _catalog;
        private readonly PathPolicy _policy;

        // Per-slot state.
        private readonly bool[] _alive;
        private readonly ushort[] _netId;
        private readonly byte[] _type;
        private readonly Float2[] _pos;
        private readonly int[] _health;
        private readonly ZombieState[] _state;
        private readonly float[] _cooldown;
        private readonly float[] _sprintTime;
        private readonly float[] _restTime;
        private readonly byte[] _facing;
        private readonly int[] _nextInCell;
        private readonly Stack<int> _free = new Stack<int>();

        // Per-type data parsed once from the catalog.
        private readonly float[] _exhaustAfter;
        private readonly float[] _restFor;
        private readonly bool[] _sprints;
        private readonly float[] _radius;

        private readonly int[] _cellHead;
        private readonly List<Int2> _targets = new List<Int2>();
        private readonly List<PlayerHit> _hits = new List<PlayerHit>();
        private readonly List<ZombieDeath> _deaths = new List<ZombieDeath>();

        private ushort _nextNetId = 1;
        private float _time;
        private float _lastRebuild = float.NegativeInfinity;

        public ZombieSimulation(BlockWorld blocks, GroundLayer ground, ZombieCatalog catalog, int capacity = 512,
            PathPolicy policy = null)
        {
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _policy = policy ?? PathPolicy.Zombie;
            if (capacity < 1 || capacity > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            Capacity = capacity;
            _alive = new bool[capacity];
            _netId = new ushort[capacity];
            _type = new byte[capacity];
            _pos = new Float2[capacity];
            _health = new int[capacity];
            _state = new ZombieState[capacity];
            _cooldown = new float[capacity];
            _sprintTime = new float[capacity];
            _restTime = new float[capacity];
            _facing = new byte[capacity];
            _nextInCell = new int[capacity];
            for (int i = capacity - 1; i >= 0; i--)
            {
                _free.Push(i);
            }

            _exhaustAfter = new float[catalog.Count];
            _restFor = new float[catalog.Count];
            _sprints = new bool[catalog.Count];
            _radius = new float[catalog.Count];
            for (byte t = 0; t < catalog.Count; t++)
            {
                ZombieDefinition def = catalog[t];
                _sprints[t] = def.HasBehavior("chase_sprint");
                _exhaustAfter[t] = TagNumber(def, "exhaust_after_seconds_", float.PositiveInfinity);
                _restFor[t] = TagNumber(def, "rest_seconds_", 0f);
                _radius[t] = Math.Max(0.2f, def.size_tiles * 0.5f);
            }

            Field = new FlowField(blocks.Width, blocks.Height);
            _cellHead = new int[blocks.Width * blocks.Height];
        }

        public int Capacity { get; }

        public int Count => Capacity - _free.Count;

        public FlowField Field { get; }

        /// <summary>Minimum seconds between flow field rebuilds.</summary>
        public float FlowRebuildInterval { get; set; } = 0.5f;

        /// <summary>Search bound for the flow field (10 = one tile). Zombies farther away idle.</summary>
        public int FlowMaxDistance { get; set; } = 10 * 150;

        public int FlowRebuilds { get; private set; }

        public bool IsAlive(int slot) => _alive[slot];

        public ushort GetNetId(int slot) => _netId[slot];

        public byte GetTypeIndex(int slot) => _type[slot];

        public Float2 GetPosition(int slot) => _pos[slot];

        public int GetHealth(int slot) => _health[slot];

        public ZombieState GetState(int slot) => _state[slot];

        /// <summary>Facing as one of 16 directions (0 = east, counter-clockwise).</summary>
        public byte GetFacing(int slot) => _facing[slot];

        /// <summary>Adds a zombie. Returns its slot, or −1 when full or the cell is blocked.</summary>
        public int Spawn(byte typeIndex, Float2 position, int players = 1)
        {
            if (_free.Count == 0 || typeIndex >= _catalog.Count)
            {
                return -1;
            }
            Int2 tile = position.Tile;
            if (_blocks.IsSolid(tile.X, tile.Y) || GroundLayer.WalkCost(_ground.Get(tile.X, tile.Y)) == 0)
            {
                return -1;
            }
            int slot = _free.Pop();
            _alive[slot] = true;
            _netId[slot] = _nextNetId;
            _nextNetId = (ushort)(_nextNetId == ushort.MaxValue ? 1 : _nextNetId + 1);
            _type[slot] = typeIndex;
            _pos[slot] = position;
            _health[slot] = _catalog[typeIndex].HealthFor(players);
            _state[slot] = ZombieState.Idle;
            _cooldown[slot] = 0f;
            _sprintTime[slot] = 0f;
            _restTime[slot] = 0f;
            _facing[slot] = 0;
            return slot;
        }

        /// <summary>Applies damage of a type ("fire", "ballistic"...). Returns true if the zombie died.</summary>
        public bool Damage(int slot, int amount, string damageType)
        {
            if (!_alive[slot] || amount <= 0)
            {
                return false;
            }
            DamageMultipliers m = _catalog[_type[slot]].damage_taken;
            float factor = damageType switch
            {
                "blunt" => m.blunt,
                "sharp" => m.sharp,
                "ballistic" => m.ballistic,
                "fire" => m.fire,
                "acid" => m.acid,
                _ => 1f,
            };
            _health[slot] -= (int)Math.Round(amount * factor, MidpointRounding.AwayFromZero);
            if (_health[slot] > 0)
            {
                return false;
            }
            _deaths.Add(new ZombieDeath(_netId[slot], _type[slot], _pos[slot]));
            _alive[slot] = false;
            _free.Push(slot);
            return true;
        }

        public void DrainPlayerHits(List<PlayerHit> into)
        {
            into.AddRange(_hits);
            _hits.Clear();
        }

        public void DrainDeaths(List<ZombieDeath> into)
        {
            into.AddRange(_deaths);
            _deaths.Clear();
        }

        /// <summary>One simulation step. <paramref name="players"/> are the living players' positions.</summary>
        public void Tick(float dt, IReadOnlyList<Float2> players)
        {
            _time += dt;
            UpdateFlowField(players);
            BuildSpatialHash();

            for (int i = 0; i < Capacity; i++)
            {
                if (_alive[i])
                {
                    TickZombie(i, dt, players);
                }
            }
        }

        private void UpdateFlowField(IReadOnlyList<Float2> players)
        {
            bool targetsChanged = players.Count != _targets.Count;
            for (int p = 0; !targetsChanged && p < players.Count; p++)
            {
                targetsChanged = players[p].Tile != _targets[p];
            }
            bool stale = Field.BuiltFromRevision != _blocks.Revision || targetsChanged;
            if (!stale || _time - _lastRebuild < FlowRebuildInterval)
            {
                return;
            }
            _targets.Clear();
            for (int p = 0; p < players.Count; p++)
            {
                _targets.Add(players[p].Tile);
            }
            Field.Build(_ground, _blocks, _policy, _targets, FlowMaxDistance);
            _lastRebuild = _time;
            FlowRebuilds++;
        }

        private void BuildSpatialHash()
        {
            Array.Fill(_cellHead, -1);
            for (int i = 0; i < Capacity; i++)
            {
                if (!_alive[i])
                {
                    continue;
                }
                Int2 t = _pos[i].Tile;
                if (!_blocks.InBounds(t.X, t.Y))
                {
                    continue;
                }
                int cell = t.Y * _blocks.Width + t.X;
                _nextInCell[i] = _cellHead[cell];
                _cellHead[cell] = i;
            }
        }

        private void TickZombie(int i, float dt, IReadOnlyList<Float2> players)
        {
            byte type = _type[i];
            ZombieDefinition def = _catalog[type];
            Float2 pos = _pos[i];
            _cooldown[i] -= dt;

            int nearest = NearestPlayer(pos, players, out float playerDistance);
            if (nearest >= 0 && playerDistance <= def.attack_range + PlayerRadius + _radius[type])
            {
                Face(i, players[nearest] - pos);
                _state[i] = ZombieState.AttackingPlayer;
                if (_cooldown[i] <= 0f && def.damage > 0)
                {
                    _hits.Add(new PlayerHit(_netId[i], nearest, def.damage, type));
                    _cooldown[i] = def.attack_interval;
                }
                return;
            }

            Int2 cell = pos.Tile;
            Float2 desired;
            int fieldDistance = Field.GetDistance(cell.X, cell.Y);
            if (fieldDistance == 0 && nearest >= 0)
            {
                desired = players[nearest] - pos; // same tile as the player: go straight for them
            }
            else
            {
                Int2 step = Field.GetStep(cell.X, cell.Y);
                if (step == new Int2(0, 0))
                {
                    _state[i] = ZombieState.Idle;
                    return;
                }
                Int2 next = cell + step;
                if (_blocks.IsSolid(next.X, next.Y))
                {
                    AttackBlock(i, def, next);
                    return;
                }
                desired = new Float2(next.X + 0.5f, next.Y + 0.5f) - pos;
            }

            float speed = def.speed;
            if (_sprints[type])
            {
                speed = SprintSpeed(i, def, dt, nearest >= 0 && playerDistance <= def.detect_sight);
            }
            else
            {
                _state[i] = ZombieState.Chasing;
            }

            Float2 velocity = desired.Normalized * speed + Separation(i, pos) * SeparationStrength;
            Face(i, desired);
            _pos[i] = MoveWithCollision(pos, velocity * dt, _radius[type]);
        }

        private float SprintSpeed(int i, ZombieDefinition def, float dt, bool seesPlayer)
        {
            byte type = _type[i];
            if (_restTime[i] > 0f)
            {
                _restTime[i] -= dt;
                _state[i] = ZombieState.Resting;
                return def.speed * RestSpeedFactor;
            }
            _state[i] = ZombieState.Chasing;
            if (!seesPlayer)
            {
                _sprintTime[i] = Math.Max(0f, _sprintTime[i] - dt);
                return def.speed * 0.5f; // jog until it spots someone
            }
            _sprintTime[i] += dt;
            if (_sprintTime[i] >= _exhaustAfter[type])
            {
                _sprintTime[i] = 0f;
                _restTime[i] = _restFor[type];
            }
            return def.speed;
        }

        private void AttackBlock(int i, ZombieDefinition def, Int2 target)
        {
            _state[i] = ZombieState.AttackingBlock;
            Face(i, new Float2(target.X + 0.5f, target.Y + 0.5f) - _pos[i]);
            if (_cooldown[i] > 0f || !_blocks.InBounds(target.X, target.Y))
            {
                return;
            }
            BlockType block = _blocks.GetBlock(target.X, target.Y);
            if (!_policy.CanBreak(block.Material))
            {
                return;
            }
            int damage = ZombieCatalog.StructureDamage(def, block.Material);
            if (damage > 0)
            {
                _blocks.Damage(target.X, target.Y, damage);
            }
            _cooldown[i] = Math.Max(0.1f, def.attack_interval);
        }

        private Float2 Separation(int i, Float2 pos)
        {
            Float2 push = Float2.Zero;
            Int2 t = pos.Tile;
            float myRadius = _radius[_type[i]];
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = t.X + dx;
                    int y = t.Y + dy;
                    if (!_blocks.InBounds(x, y))
                    {
                        continue;
                    }
                    for (int j = _cellHead[y * _blocks.Width + x]; j >= 0; j = _nextInCell[j])
                    {
                        if (j == i)
                        {
                            continue;
                        }
                        Float2 away = pos - _pos[j];
                        float min = myRadius + _radius[_type[j]];
                        float d2 = away.LengthSquared;
                        if (d2 >= min * min)
                        {
                            continue;
                        }
                        if (d2 < 1e-8f)
                        {
                            // Exactly on top of each other: split deterministically by slot.
                            away = new Float2(i < j ? 1f : -1f, (i * 7 + j) % 3 - 1f);
                            d2 = away.LengthSquared;
                        }
                        float d = MathF.Sqrt(d2);
                        push += away * ((min - d) / (min * d));
                    }
                }
            }
            return push;
        }

        private Float2 MoveWithCollision(Float2 pos, Float2 delta, float radius)
        {
            Float2 target = pos + delta;
            if (IsFree(target, radius))
            {
                return target;
            }
            var xOnly = new Float2(target.X, pos.Y);
            if (IsFree(xOnly, radius))
            {
                return xOnly;
            }
            var yOnly = new Float2(pos.X, target.Y);
            return IsFree(yOnly, radius) ? yOnly : pos;
        }

        // A zombie is a circle; check the cells under its bounding box.
        private bool IsFree(Float2 p, float radius)
        {
            float r = radius * 0.8f;
            int minX = (int)MathF.Floor(p.X - r);
            int maxX = (int)MathF.Floor(p.X + r);
            int minY = (int)MathF.Floor(p.Y - r);
            int maxY = (int)MathF.Floor(p.Y + r);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (_blocks.IsSolid(x, y) || GroundLayer.WalkCost(_ground.Get(x, y)) == 0)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void Face(int i, Float2 direction)
        {
            if (direction.LengthSquared < 1e-8f)
            {
                return;
            }
            float angle = MathF.Atan2(direction.Y, direction.X);
            int sector = (int)MathF.Round(angle / (2f * MathF.PI) * 16f);
            _facing[i] = (byte)((sector % 16 + 16) % 16);
        }

        private static int NearestPlayer(Float2 pos, IReadOnlyList<Float2> players, out float distance)
        {
            int best = -1;
            float bestD2 = float.PositiveInfinity;
            for (int p = 0; p < players.Count; p++)
            {
                float d2 = (players[p] - pos).LengthSquared;
                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    best = p;
                }
            }
            distance = best >= 0 ? MathF.Sqrt(bestD2) : float.PositiveInfinity;
            return best;
        }

        // Behaviour tags carry numbers, e.g. "exhaust_after_seconds_6" (docs/bestiario.md).
        private static float TagNumber(ZombieDefinition def, string prefix, float fallback)
        {
            if (def.behavior == null)
            {
                return fallback;
            }
            foreach (string tag in def.behavior)
            {
                if (tag.StartsWith(prefix, StringComparison.Ordinal)
                    && float.TryParse(tag.Substring(prefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    return value;
                }
            }
            return fallback;
        }
    }
}
