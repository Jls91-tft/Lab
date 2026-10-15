using System;
using System.Collections.Generic;
using System.Globalization;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.AI
{
    /// <summary>
    /// Host-authoritative simulation of every zombie, stored as parallel
    /// arrays (no GameObject per zombie). Clients never run this: they get
    /// snapshots (<see cref="ZombieSnapshotCodec"/>) and events.
    ///
    /// Behaviour: a zombie is unaware until it sees a player (half range at
    /// night, see <see cref="SightMultiplier"/>) or hears a noise
    /// (<see cref="EmitNoise"/>). Aware zombies follow the shared
    /// <see cref="FlowField"/> towards the players, smash blocks in the way
    /// and hit players in range. Specials add their abilities on top
    /// (docs/bestiario.md): the Screamer screams, the Spitter spits acid and
    /// La Mole charges through walls and is stunned by metal.
    /// </summary>
    public sealed class ZombieSimulation
    {
        private const float PlayerRadius = 0.4f;
        private const float SeparationStrength = 2.5f;
        private const float RestSpeedFactor = 0.3f;
        private const float ProjectileSpeed = 8f;
        private const float DirectHitRadius = 0.8f;
        private const float ChargeDistance = 8f;
        private const float InterruptedScreamCooldown = 3f;
        private const ushort DestroyDamage = ushort.MaxValue;

        private enum Kind : byte
        {
            Basic,
            Screamer,
            Spitter,
            Brute,
        }

        private struct Projectile
        {
            public ushort ZombieId;
            public byte Type;
            public Float2 Target;
            public float TimeLeft;
        }

        private struct AcidPool
        {
            public ushort ZombieId;
            public byte Type;
            public Float2 Center;
            public float Radius;
            public float TimeLeft;
            public float Dps;
            public float Accumulated;
        }

        private struct Noise
        {
            public Float2 Position;
            public float Radius;
        }

        private readonly BlockWorld _blocks;
        private readonly GroundLayer _ground;
        private readonly ZombieCatalog _catalog;
        private readonly PathPolicy _policy;

        // Per-slot state.
        private readonly bool[] _alive;
        private readonly bool[] _aware;
        private readonly ushort[] _netId;
        private readonly byte[] _type;
        private readonly Float2[] _pos;
        private readonly int[] _health;
        private readonly ZombieState[] _state;
        private readonly float[] _cooldown;      // basic attack
        private readonly float[] _ability;       // scream / spit / charge cooldown
        private readonly float[] _ability2;      // slam cooldown
        private readonly float[] _windup;        // scream or charge wind-up left
        private readonly float[] _stun;
        private readonly float[] _buff;
        private readonly float[] _buffMultiplier;
        private readonly float[] _chargeLeft;
        private readonly Float2[] _chargeDir;
        private readonly bool[] _chargeHit;
        private readonly float[] _sprintTime;
        private readonly float[] _restTime;
        private readonly byte[] _facing;
        private readonly int[] _nextInCell;
        private readonly Stack<int> _free = new Stack<int>();

        // Per-type data parsed once from the catalog.
        private readonly Kind[] _kind;
        private readonly float[] _exhaustAfter;
        private readonly float[] _restFor;
        private readonly bool[] _sprints;
        private readonly float[] _radius;
        private readonly float[] _keepDistance;
        private readonly AbilityData[] _scream;
        private readonly AbilityData[] _acid;
        private readonly AbilityData[] _charge;
        private readonly AbilityData[] _stunAbility;
        private readonly AbilityData[] _slam;

        private readonly int[] _cellHead;
        private readonly List<Int2> _targets = new List<Int2>();
        private readonly List<PlayerHit> _hits = new List<PlayerHit>();
        private readonly List<ZombieDeath> _deaths = new List<ZombieDeath>();
        private readonly List<ZombieEvent> _events = new List<ZombieEvent>();
        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly List<AcidPool> _pools = new List<AcidPool>();
        private readonly List<Noise> _noises = new List<Noise>();

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
            _aware = new bool[capacity];
            _netId = new ushort[capacity];
            _type = new byte[capacity];
            _pos = new Float2[capacity];
            _health = new int[capacity];
            _state = new ZombieState[capacity];
            _cooldown = new float[capacity];
            _ability = new float[capacity];
            _ability2 = new float[capacity];
            _windup = new float[capacity];
            _stun = new float[capacity];
            _buff = new float[capacity];
            _buffMultiplier = new float[capacity];
            _chargeLeft = new float[capacity];
            _chargeDir = new Float2[capacity];
            _chargeHit = new bool[capacity];
            _sprintTime = new float[capacity];
            _restTime = new float[capacity];
            _facing = new byte[capacity];
            _nextInCell = new int[capacity];
            for (int i = capacity - 1; i >= 0; i--)
            {
                _free.Push(i);
            }

            int types = catalog.Count;
            _kind = new Kind[types];
            _exhaustAfter = new float[types];
            _restFor = new float[types];
            _sprints = new bool[types];
            _radius = new float[types];
            _keepDistance = new float[types];
            _scream = new AbilityData[types];
            _acid = new AbilityData[types];
            _charge = new AbilityData[types];
            _stunAbility = new AbilityData[types];
            _slam = new AbilityData[types];
            for (byte t = 0; t < types; t++)
            {
                ZombieDefinition def = catalog[t];
                _sprints[t] = def.HasBehavior("chase_sprint");
                _exhaustAfter[t] = TagNumber(def, "exhaust_after_seconds_", float.PositiveInfinity);
                _restFor[t] = TagNumber(def, "rest_seconds_", 0f);
                _keepDistance[t] = TagNumber(def, "keep_distance_", 0f);
                _radius[t] = Math.Max(0.2f, def.size_tiles * 0.5f);
                _scream[t] = def.GetAbility("scream");
                _acid[t] = def.GetAbility("acid_pool");
                _charge[t] = def.GetAbility("charge");
                _stunAbility[t] = def.GetAbility("stun");
                _slam[t] = def.GetAbility("slam");
                _kind[t] = _charge[t] != null ? Kind.Brute
                    : _scream[t] != null ? Kind.Screamer
                    : def.HasBehavior("spit_projectile") ? Kind.Spitter
                    : Kind.Basic;
            }

            Field = new FlowField(blocks.Width, blocks.Height);
            _cellHead = new int[blocks.Width * blocks.Height];
        }

        public int Capacity { get; }

        public int Count => Capacity - _free.Count;

        public FlowField Field { get; }

        /// <summary>Minimum seconds between flow field rebuilds.</summary>
        public float FlowRebuildInterval { get; set; } = 0.5f;

        /// <summary>
        /// Search bound for the flow field (10 = one tile, breaking walls costs extra).
        /// Zombies farther away idle until they get closer; the horde spawns well inside it.
        /// </summary>
        public int FlowMaxDistance { get; set; } = 10 * 80;

        /// <summary>Scales every zombie's sight range. The match sets 0.5 at night (docs/bestiario.md).</summary>
        public float SightMultiplier { get; set; } = 1f;

        public int FlowRebuilds { get; private set; }

        public int ActiveAcidPools => _pools.Count;

        public bool IsAlive(int slot) => _alive[slot];

        public bool IsAware(int slot) => _aware[slot];

        public ushort GetNetId(int slot) => _netId[slot];

        public byte GetTypeIndex(int slot) => _type[slot];

        public Float2 GetPosition(int slot) => _pos[slot];

        public int GetHealth(int slot) => _health[slot];

        public ZombieState GetState(int slot) => _state[slot];

        /// <summary>Facing as one of 16 directions (0 = east, counter-clockwise).</summary>
        public byte GetFacing(int slot) => _facing[slot];

        /// <summary>
        /// Adds a zombie. The night horde spawns <paramref name="aware"/> (it knows
        /// where the players are); zombies placed in the map during the day do not.
        /// Returns its slot, or −1 when full or the cell is blocked.
        /// </summary>
        public int Spawn(byte typeIndex, Float2 position, int players = 1, bool aware = true)
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
            _aware[slot] = aware;
            _netId[slot] = _nextNetId;
            _nextNetId = (ushort)(_nextNetId == ushort.MaxValue ? 1 : _nextNetId + 1);
            _type[slot] = typeIndex;
            _pos[slot] = position;
            _health[slot] = _catalog[typeIndex].HealthFor(players);
            _state[slot] = aware ? ZombieState.Idle : ZombieState.Unaware;
            _cooldown[slot] = 0f;
            _ability[slot] = 0f;
            _ability2[slot] = 0f;
            _windup[slot] = 0f;
            _stun[slot] = 0f;
            _buff[slot] = 0f;
            _buffMultiplier[slot] = 1f;
            _chargeLeft[slot] = 0f;
            _chargeHit[slot] = false;
            _sprintTime[slot] = 0f;
            _restTime[slot] = 0f;
            _facing[slot] = 0;
            return slot;
        }

        /// <summary>
        /// A sound at <paramref name="position"/> heard up to <paramref name="radius"/>
        /// tiles plus each zombie's own hearing (docs/bestiario.md). Processed next tick.
        /// </summary>
        public void EmitNoise(Float2 position, float radius)
        {
            if (radius > 0f)
            {
                _noises.Add(new Noise { Position = position, Radius = radius });
            }
        }

        /// <summary>Applies damage of a type ("fire", "ballistic"...). Returns true if the zombie died.</summary>
        public bool Damage(int slot, int amount, string damageType)
        {
            if (!_alive[slot] || amount <= 0)
            {
                return false;
            }
            byte type = _type[slot];
            DamageMultipliers m = _catalog[type].damage_taken;
            float factor = damageType switch
            {
                "blunt" => m.blunt,
                "sharp" => m.sharp,
                "ballistic" => m.ballistic,
                "fire" => m.fire,
                "acid" => m.acid,
                _ => 1f,
            };
            if (_stun[slot] > 0f && _stunAbility[type] != null)
            {
                factor *= _stunAbility[type].multiplier;
            }
            _health[slot] -= (int)Math.Round(amount * factor, MidpointRounding.AwayFromZero);
            if (_kind[type] == Kind.Screamer && _windup[slot] > 0f)
            {
                _windup[slot] = 0f;
                _ability[slot] = InterruptedScreamCooldown;
                _events.Add(new ZombieEvent(ZombieEventType.ScreamInterrupted, _netId[slot], _pos[slot]));
            }
            if (!_aware[slot])
            {
                MakeAware(slot);
            }
            if (_health[slot] > 0)
            {
                return false;
            }
            _deaths.Add(new ZombieDeath(_netId[slot], type, _pos[slot]));
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

        public void DrainEvents(List<ZombieEvent> into)
        {
            into.AddRange(_events);
            _events.Clear();
        }

        /// <summary>One simulation step. <paramref name="players"/> are the living players' positions.</summary>
        public void Tick(float dt, IReadOnlyList<Float2> players)
        {
            _time += dt;
            ProcessNoises();
            UpdateFlowField(players);
            BuildSpatialHash();

            for (int i = 0; i < Capacity; i++)
            {
                if (_alive[i])
                {
                    TickZombie(i, dt, players);
                }
            }
            UpdateProjectiles(dt, players);
            UpdatePools(dt, players);
        }

        // ---------------------------------------------------------------- senses

        private void ProcessNoises()
        {
            foreach (Noise noise in _noises)
            {
                for (int i = 0; i < Capacity; i++)
                {
                    if (!_alive[i] || _aware[i])
                    {
                        continue;
                    }
                    float reach = noise.Radius + _catalog[_type[i]].detect_hearing;
                    if ((_pos[i] - noise.Position).LengthSquared <= reach * reach)
                    {
                        MakeAware(i);
                    }
                }
            }
            _noises.Clear();
        }

        private void MakeAware(int i)
        {
            _aware[i] = true;
            _state[i] = ZombieState.Idle;
            _events.Add(new ZombieEvent(ZombieEventType.Noticed, _netId[i], _pos[i]));
        }

        private bool Sees(int i, Float2 target, float distance, float range) =>
            distance <= range && TileCollision.HasClearLine(_pos[i], target, _blocks);

        // ------------------------------------------------------------ per zombie

        private void TickZombie(int i, float dt, IReadOnlyList<Float2> players)
        {
            byte type = _type[i];
            ZombieDefinition def = _catalog[type];
            _cooldown[i] -= dt;
            _ability[i] -= dt;
            _ability2[i] -= dt;
            if (_buff[i] > 0f)
            {
                _buff[i] -= dt;
            }
            if (_stun[i] > 0f)
            {
                _stun[i] -= dt;
                _state[i] = ZombieState.Stunned;
                return;
            }

            int nearest = NearestPlayer(_pos[i], players, out float distance);
            if (!_aware[i])
            {
                if (nearest >= 0 && Sees(i, players[nearest], distance, def.detect_sight * SightMultiplier))
                {
                    MakeAware(i);
                }
                else
                {
                    _state[i] = ZombieState.Unaware;
                    return;
                }
            }

            switch (_kind[type])
            {
                case Kind.Brute:
                    if (TickBrute(i, def, dt, nearest, distance, players))
                    {
                        return;
                    }
                    break;
                case Kind.Screamer:
                    TickScreamer(i, def, dt, nearest, distance, players);
                    return;
                case Kind.Spitter:
                    if (TickSpitter(i, def, dt, nearest, distance, players))
                    {
                        return;
                    }
                    break;
            }

            if (_kind[type] != Kind.Spitter && nearest >= 0 && def.damage > 0
                && distance <= def.attack_range + PlayerRadius + _radius[type]
                && TileCollision.HasClearLine(_pos[i], players[nearest], _blocks))
            {
                Face(i, players[nearest] - _pos[i]);
                _state[i] = ZombieState.AttackingPlayer;
                if (_cooldown[i] <= 0f)
                {
                    _hits.Add(new PlayerHit(_netId[i], nearest, def.damage, type));
                    _cooldown[i] = def.attack_interval;
                }
                return;
            }
            FollowField(i, def, dt, nearest, distance, players);
        }

        private void FollowField(int i, ZombieDefinition def, float dt, int nearest, float distance, IReadOnlyList<Float2> players)
        {
            byte type = _type[i];
            Float2 pos = _pos[i];
            Int2 cell = pos.Tile;
            Float2 desired;
            if (Field.GetDistance(cell.X, cell.Y) == 0 && nearest >= 0)
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
                speed = SprintSpeed(i, def, dt, nearest >= 0 && distance <= def.detect_sight);
            }
            else
            {
                _state[i] = ZombieState.Chasing;
            }
            if (_buff[i] > 0f)
            {
                speed *= _buffMultiplier[i];
            }
            Move(i, desired.Normalized * speed + Separation(i, pos) * SeparationStrength, dt);
            Face(i, desired);
        }

        private void Move(int i, Float2 velocity, float dt)
        {
            // Bodies are a bit smaller than their sprite so crowds flow through doors.
            _pos[i] = TileCollision.Move(_pos[i], velocity * dt, _radius[_type[i]] * 0.8f, _blocks, _ground);
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

        // --------------------------------------------------------------- Screamer

        private void TickScreamer(int i, ZombieDefinition def, float dt, int nearest, float distance, IReadOnlyList<Float2> players)
        {
            AbilityData scream = _scream[_type[i]];
            if (_windup[i] > 0f)
            {
                _windup[i] -= dt;
                _state[i] = ZombieState.Screaming;
                if (_windup[i] <= 0f)
                {
                    Scream(i, scream);
                }
                return;
            }
            bool sees = nearest >= 0 && Sees(i, players[nearest], distance, def.detect_sight);
            if (sees)
            {
                Face(i, players[nearest] - _pos[i]);
            }
            if (sees && _ability[i] <= 0f)
            {
                _windup[i] = Math.Max(0.05f, scream.windup);
                _state[i] = ZombieState.Screaming;
                return;
            }
            float keep = _keepDistance[_type[i]];
            if (sees && distance < keep)
            {
                // Back off to keep the group in earshot but out of reach.
                _state[i] = ZombieState.Chasing;
                Move(i, (_pos[i] - players[nearest]).Normalized * def.speed + Separation(i, _pos[i]) * SeparationStrength, dt);
                return;
            }
            if (sees && distance <= keep + 3f)
            {
                _state[i] = ZombieState.Idle;
                return;
            }
            FollowField(i, def, dt, nearest, distance, players);
        }

        private void Scream(int i, AbilityData scream)
        {
            EmitNoise(_pos[i], scream.radius);
            float buffRadius = scream.radius * 0.25f;
            for (int j = 0; j < Capacity; j++)
            {
                if (_alive[j] && j != i && (_pos[j] - _pos[i]).LengthSquared <= buffRadius * buffRadius)
                {
                    _buff[j] = scream.seconds;
                    _buffMultiplier[j] = scream.multiplier;
                }
            }
            _ability[i] = scream.cooldown;
            _events.Add(new ZombieEvent(ZombieEventType.Scream, _netId[i], _pos[i]));
        }

        // --------------------------------------------------------------- Spitter

        private bool TickSpitter(int i, ZombieDefinition def, float dt, int nearest, float distance, IReadOnlyList<Float2> players)
        {
            if (nearest < 0 || !Sees(i, players[nearest], distance, def.attack_range))
            {
                return false; // approach through the flow field (and break what is in the way)
            }
            Float2 target = players[nearest];
            Face(i, target - _pos[i]);
            float keep = _keepDistance[_type[i]];
            if (distance < keep * 0.6f)
            {
                _state[i] = ZombieState.Chasing;
                Move(i, (_pos[i] - target).Normalized * def.speed, dt);
                return true;
            }
            _state[i] = ZombieState.AttackingPlayer;
            if (_cooldown[i] <= 0f)
            {
                _projectiles.Add(new Projectile
                {
                    ZombieId = _netId[i],
                    Type = _type[i],
                    Target = target,
                    TimeLeft = distance / ProjectileSpeed,
                });
                _cooldown[i] = def.attack_interval;
                _events.Add(new ZombieEvent(ZombieEventType.Spit, _netId[i], _pos[i]));
            }
            return true;
        }

        private void UpdateProjectiles(float dt, IReadOnlyList<Float2> players)
        {
            for (int k = _projectiles.Count - 1; k >= 0; k--)
            {
                Projectile p = _projectiles[k];
                p.TimeLeft -= dt;
                if (p.TimeLeft > 0f)
                {
                    _projectiles[k] = p;
                    continue;
                }
                _projectiles.RemoveAt(k);
                ZombieDefinition def = _catalog[p.Type];
                _events.Add(new ZombieEvent(ZombieEventType.AcidSplash, p.ZombieId, p.Target));
                for (int pl = 0; pl < players.Count; pl++)
                {
                    if ((players[pl] - p.Target).LengthSquared <= DirectHitRadius * DirectHitRadius)
                    {
                        _hits.Add(new PlayerHit(p.ZombieId, pl, def.damage, p.Type));
                    }
                }
                AbilityData acid = _acid[p.Type];
                if (acid != null && acid.seconds > 0f)
                {
                    _pools.Add(new AcidPool
                    {
                        ZombieId = p.ZombieId,
                        Type = p.Type,
                        Center = p.Target,
                        Radius = acid.radius,
                        TimeLeft = acid.seconds,
                        Dps = acid.dps,
                    });
                }
            }
        }

        private void UpdatePools(float dt, IReadOnlyList<Float2> players)
        {
            for (int k = _pools.Count - 1; k >= 0; k--)
            {
                AcidPool pool = _pools[k];
                pool.TimeLeft -= dt;
                pool.Accumulated += pool.Dps * dt;
                int whole = (int)pool.Accumulated;
                if (whole > 0)
                {
                    pool.Accumulated -= whole;
                    for (int pl = 0; pl < players.Count; pl++)
                    {
                        if ((players[pl] - pool.Center).LengthSquared <= pool.Radius * pool.Radius)
                        {
                            _hits.Add(new PlayerHit(pool.ZombieId, pl, whole, pool.Type));
                        }
                    }
                }
                if (pool.TimeLeft <= 0f)
                {
                    _pools.RemoveAt(k);
                }
                else
                {
                    _pools[k] = pool;
                }
            }
        }

        // ---------------------------------------------------------------- La Mole

        private bool TickBrute(int i, ZombieDefinition def, float dt, int nearest, float distance, IReadOnlyList<Float2> players)
        {
            byte type = _type[i];
            AbilityData charge = _charge[type];
            AbilityData slam = _slam[type];

            if (_windup[i] > 0f)
            {
                _windup[i] -= dt;
                _state[i] = ZombieState.Charging;
                if (_windup[i] <= 0f)
                {
                    _chargeLeft[i] = ChargeDistance;
                    _chargeHit[i] = false;
                }
                return true;
            }
            if (_chargeLeft[i] > 0f)
            {
                RunCharge(i, def, charge, dt, players);
                return true;
            }
            if (slam != null && nearest >= 0 && _ability2[i] <= 0f && distance <= slam.radius
                && TileCollision.HasClearLine(_pos[i], players[nearest], _blocks))
            {
                for (int pl = 0; pl < players.Count; pl++)
                {
                    if ((players[pl] - _pos[i]).LengthSquared <= slam.radius * slam.radius
                        && TileCollision.HasClearLine(_pos[i], players[pl], _blocks))
                    {
                        _hits.Add(new PlayerHit(_netId[i], pl, (int)slam.damage, type));
                    }
                }
                _ability2[i] = slam.cooldown;
                _cooldown[i] = def.attack_interval;
                _state[i] = ZombieState.AttackingPlayer;
                _events.Add(new ZombieEvent(ZombieEventType.Slam, _netId[i], _pos[i]));
                return true;
            }
            if (charge == null || _ability[i] > 0f)
            {
                return false;
            }
            Float2 direction = Float2.Zero;
            if (nearest >= 0 && distance >= 3f && distance <= ChargeDistance
                && TileCollision.HasClearLine(_pos[i], players[nearest], _blocks))
            {
                direction = players[nearest] - _pos[i];
            }
            else
            {
                Int2 cell = _pos[i].Tile;
                Int2 step = Field.GetStep(cell.X, cell.Y);
                Int2 next = cell + step;
                if (step != new Int2(0, 0) && _blocks.InBounds(next.X, next.Y) && _blocks.IsSolid(next.X, next.Y))
                {
                    direction = new Float2(next.X + 0.5f, next.Y + 0.5f) - _pos[i]; // blocked: smash through
                }
            }
            if (direction.LengthSquared < 1e-6f)
            {
                return false;
            }
            _chargeDir[i] = direction.Normalized;
            _windup[i] = Math.Max(0.05f, charge.windup);
            _ability[i] = charge.cooldown;
            _state[i] = ZombieState.Charging;
            Face(i, direction);
            _events.Add(new ZombieEvent(ZombieEventType.ChargeStart, _netId[i], _pos[i]));
            return true;
        }

        private void RunCharge(int i, ZombieDefinition def, AbilityData charge, float dt, IReadOnlyList<Float2> players)
        {
            byte type = _type[i];
            _state[i] = ZombieState.Charging;
            float step = charge.speed * dt;
            Float2 ahead = _pos[i] + _chargeDir[i] * step;
            float r = _radius[type] * 0.8f;
            int minX = (int)MathF.Floor(ahead.X - r);
            int maxX = (int)MathF.Floor(ahead.X + r);
            int minY = (int)MathF.Floor(ahead.Y - r);
            int maxY = (int)MathF.Floor(ahead.Y + r);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!TileCollision.CircleTouchesCell(ahead, r, x, y))
                    {
                        continue;
                    }
                    if (!_blocks.InBounds(x, y) || GroundLayer.WalkCost(_ground.Get(x, y)) == 0)
                    {
                        _chargeLeft[i] = 0f; // map edge or water
                        return;
                    }
                    BlockType block = _blocks.GetBlock(x, y);
                    if (!block.IsSolid)
                    {
                        continue;
                    }
                    string material = MaterialName(block.Material);
                    if (charge.IsStunnedBy(material))
                    {
                        _chargeLeft[i] = 0f;
                        _stun[i] = _stunAbility[type] != null ? _stunAbility[type].seconds : 2f;
                        _state[i] = ZombieState.Stunned;
                        _events.Add(new ZombieEvent(ZombieEventType.Stunned, _netId[i], _pos[i]));
                        return;
                    }
                    if (!charge.Breaks(material) || block.IsIndestructible)
                    {
                        _chargeLeft[i] = 0f; // trees and the like stop it cold
                        return;
                    }
                    _blocks.Damage(x, y, DestroyDamage);
                }
            }
            Float2 before = _pos[i];
            _pos[i] = TileCollision.Move(before, _chargeDir[i] * step, r, _blocks, _ground);
            float moved = (_pos[i] - before).Length;
            _chargeLeft[i] -= step;
            if (moved < step * 0.5f)
            {
                _chargeLeft[i] = 0f;
            }
            if (!_chargeHit[i])
            {
                for (int pl = 0; pl < players.Count; pl++)
                {
                    float reach = _radius[type] + PlayerRadius;
                    if ((players[pl] - _pos[i]).LengthSquared <= reach * reach)
                    {
                        _hits.Add(new PlayerHit(_netId[i], pl, def.damage, type));
                        _chargeHit[i] = true;
                        _chargeLeft[i] = 0f;
                    }
                }
            }
        }

        private static string MaterialName(BlockMaterial material) => material switch
        {
            BlockMaterial.Wood => "wood",
            BlockMaterial.Metal => "metal",
            BlockMaterial.Concrete => "concrete",
            _ => "",
        };

        // ---------------------------------------------------------------- shared

        private void UpdateFlowField(IReadOnlyList<Float2> players)
        {
            bool targetsChanged = players.Count != _targets.Count;
            for (int p = 0; !targetsChanged && p < players.Count; p++)
            {
                targetsChanged = players[p].Tile != _targets[p];
            }
            bool stale = Field.BuiltFromRevision != _blocks.StructureRevision || targetsChanged;
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
