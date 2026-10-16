using System;
using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Items;
using CepaCero.Player;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Net
{
    /// <summary>A player hurt by another player (only with friendly fire on).</summary>
    public readonly struct PlayerDamage
    {
        public readonly int Attacker;
        public readonly int Target;
        public readonly int Damage;

        public PlayerDamage(int attacker, int target, int damage)
        {
            Attacker = attacker;
            Target = target;
            Damage = damage;
        }
    }

    /// <summary>
    /// The host's referee for world actions: checks every client intent
    /// (reach, rate limit, occupancy, allowed blocks, ammo) and applies it to
    /// the authoritative world. Damage comes from the equipped weapon or tool
    /// (or bare hands) and is always computed here, never by the client.
    /// The world records the resulting changes, which become the deltas sent
    /// to everyone (see <see cref="BlockWorld.DrainChanges"/>).
    /// </summary>
    public sealed class HostIntentRules
    {
        /// <summary>Share of the damage a tool does to a material it is not meant for (D-022).</summary>
        public float WrongToolFactor { get; set; } = 0.25f;

        /// <summary>Share of a piece's cost that comes back when it is dismantled or replaced (D-022).</summary>
        public float DismantleRefund { get; set; } = 0.5f;

        private const int HistorySize = 16;
        private const float BodyRadius = 0.4f;
        private const float DefaultHitNoise = 5f;
        private const float RayStep = 0.1f;

        private readonly BlockWorld _blocks;
        private readonly GroundLayer _ground;
        private readonly ZombieSimulation _zombies;
        private readonly PlayerSettings _settings;
        private readonly HashSet<ushort> _placeable = new HashSet<ushort>();
        private readonly float[] _nextActionTime;
        private readonly List<PlayerDamage> _playerDamage = new List<PlayerDamage>();
        private readonly PlacedPiece[][] _history;
        private readonly int[] _historyCount;

        public HostIntentRules(BlockWorld blocks, GroundLayer ground, ZombieSimulation zombies, PlayerSettings settings,
            int maxPlayers = 4)
        {
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _zombies = zombies;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _nextActionTime = new float[maxPlayers];
            _history = new PlacedPiece[maxPlayers][];
            _historyCount = new int[maxPlayers];
            for (int i = 0; i < maxPlayers; i++)
            {
                _history[i] = new PlacedPiece[HistorySize];
            }
            foreach (string key in new[] { "wood_wall", "metal_wall", "concrete_wall", "door", "fence", "workbench", "chest", "spike_trap" })
            {
                if (blocks.Registry.TryGet(key, out BlockType type))
                {
                    _placeable.Add(type.Id);
                }
            }
            for (ushort id = 0; id < blocks.Registry.Count; id++)                          // pieces from the construction data (D-022)
            {
                BlockType t = blocks.Registry[id];
                if (t.Build != null && !t.Build.IsOpenVariant && t.Build.Cost.Count > 0)
                {
                    _placeable.Add(t.Id);
                }
            }
        }

        /// <summary>
        /// Building cost hook: return false to refuse a placement (not enough materials).
        /// The match wires it to <see cref="RecipeBook.TryPayForBlock"/>; it pays when it returns true.
        /// </summary>
        public Func<int, ushort, bool> CanAffordBlock { get; set; } = (player, type) => true;

        /// <summary>Gives back part of a piece's cost to a player (dismantling, replacing, undoing). Arguments: player, block, share 0..1.</summary>
        public Action<int, BlockType, float> RefundBlock { get; set; } = (player, type, share) => { };

        /// <summary>Players' inventories, for equipped weapons, tools and ammo. Null: everyone uses bare hands.</summary>
        public IReadOnlyList<Inventory> Inventories { get; set; }

        /// <summary>Players can hurt each other (match option, off by default, D-012).</summary>
        public bool FriendlyFire { get; set; }

        /// <summary>Noise radius made by the last action (for zombie hearing).</summary>
        public float LastNoise { get; private set; }

        /// <summary>Targets hit by the last attack.</summary>
        public int LastMeleeHits { get; private set; }

        /// <summary>Block type a player just broke with HitBlock (for harvesting), or <see cref="ushort.MaxValue"/>.</summary>
        public ushort LastBrokenBlock { get; private set; } = ushort.MaxValue;

        public void DrainPlayerDamage(List<PlayerDamage> into)
        {
            into.AddRange(_playerDamage);
            _playerDamage.Clear();
        }

        public IntentResult Apply(int playerIndex, in PlayerIntent intent, IReadOnlyList<PlayerState> players, float time)
        {
            LastNoise = 0f;
            LastMeleeHits = 0;
            LastBrokenBlock = ushort.MaxValue;
            if (playerIndex < 0 || playerIndex >= players.Count || playerIndex >= _nextActionTime.Length)
            {
                return IntentResult.NotAllowed;
            }
            PlayerState player = players[playerIndex];
            if (player.IsDowned)
            {
                return IntentResult.PlayerDowned;
            }
            if (time < _nextActionTime[playerIndex])
            {
                return IntentResult.TooSoon;
            }

            float cooldown = _settings.ActionCooldown;
            IntentResult result;
            switch (intent.Type)
            {
                case IntentType.PlaceBlock:
                    result = Place(playerIndex, intent, players);
                    break;
                case IntentType.HitBlock:
                    result = HitBlock(playerIndex, player, intent);
                    break;
                case IntentType.Dismantle:
                    result = Dismantle(playerIndex, player, intent);
                    break;
                case IntentType.UndoPlace:
                    result = Undo(playerIndex, player);
                    break;
                case IntentType.ToggleDoor:
                    result = ToggleDoor(player, intent, players);
                    break;
                case IntentType.MeleeAttack:
                    result = Melee(playerIndex, players, intent, ref cooldown);
                    break;
                case IntentType.RangedAttack:
                    result = Ranged(playerIndex, players, intent, ref cooldown);
                    break;
                default:
                    return IntentResult.NotAllowed;
            }
            if (result == IntentResult.Ok || result == IntentResult.Missed)
            {
                _nextActionTime[playerIndex] = time + cooldown;
            }
            return result;
        }

        private Inventory InventoryOf(int player) =>
            Inventories != null && player < Inventories.Count ? Inventories[player] : null;

        private ItemDefinition Equipped(int player) => InventoryOf(player)?.EquippedDefinition;

        private bool InReach(PlayerState player, int x, int y)
        {
            var center = new Float2(x + 0.5f, y + 0.5f);
            return (center - player.Position).LengthSquared <= _settings.Reach * _settings.Reach;
        }

        private IntentResult Place(int playerIndex, in PlayerIntent intent, IReadOnlyList<PlayerState> players)
        {
            int x = intent.X;
            int y = intent.Y;
            if (!_blocks.InBounds(x, y) || !_placeable.Contains(intent.Arg) || intent.Z != 0)       // players act on the ground level until they can climb (D-022 phase F)
            {
                return IntentResult.InvalidTarget;
            }
            if (!InReach(players[playerIndex], x, y))
            {
                return IntentResult.OutOfReach;
            }
            BlockType placing = _blocks.Registry[intent.Arg];
            byte variant = BlockVariant.Sanitize(intent.Variant, placing);
            ushort existing = _blocks.GetTypeId(x, y, 0, placing.Layer);
            BlockType old = existing == BlockRegistry.AirId ? null : _blocks.Registry[existing];
            if (old != null)
            {
                if (old.Build == null)
                {
                    return IntentResult.Occupied;                           // map buildings and legacy blocks are not replaced
                }
                if (existing == intent.Arg)
                {
                    if (_blocks.GetVariant(x, y, 0, placing.Layer) == variant)
                    {
                        return IntentResult.Occupied;
                    }
                    _blocks.SetVariant(x, y, 0, placing.Layer, variant);    // same piece, other colour or turn: free, health stays
                    LastNoise = 1f;
                    return IntentResult.Ok;
                }
            }
            if (!GroundLayer.IsBuildable(_ground.Get(x, y)))
            {
                return IntentResult.InvalidTarget;
            }
            if (placing.IsSolid && (old == null || !old.IsSolid) && IsOccupiedByBody(x, y, players))
            {
                return IntentResult.Occupied;
            }
            // Last check, because it pays: everything else has been validated.
            if (!CanAffordBlock(playerIndex, intent.Arg))
            {
                return IntentResult.MissingIngredients;
            }
            if (old != null)
            {
                RefundBlock(playerIndex, old, DismantleRefund);             // replacing in place: the old piece goes back to the bag
                _blocks.SetBlock(x, y, 0, placing.Layer, intent.Arg, variant);
            }
            else
            {
                _blocks.TryPlace(x, y, 0, intent.Arg, variant, _ground);
                Remember(playerIndex, x, y, placing.Layer, intent.Arg);
            }
            LastNoise = 3f;
            return IntentResult.Ok;
        }

        /// <summary>The piece on top of a cell: structure, then object, floor and roof.</summary>
        private bool TopPiece(int x, int y, int layerArg, out BuildLayer layer)
        {
            if (layerArg > 0)
            {
                layer = (BuildLayer)(layerArg - 1);
                return _blocks.GetTypeId(x, y, 0, layer) != BlockRegistry.AirId;
            }
            foreach (BuildLayer l in TopOrder)
            {
                if (_blocks.GetTypeId(x, y, 0, l) != BlockRegistry.AirId)
                {
                    layer = l;
                    return true;
                }
            }
            layer = BuildLayer.Structure;
            return false;
        }

        private static readonly BuildLayer[] TopOrder = { BuildLayer.Structure, BuildLayer.Object, BuildLayer.Floor, BuildLayer.Roof };

        private IntentResult Dismantle(int playerIndex, PlayerState player, in PlayerIntent intent)
        {
            if (!_blocks.InBounds(intent.X, intent.Y) || intent.Arg > 4 || !TopPiece(intent.X, intent.Y, intent.Arg, out BuildLayer layer))
            {
                return IntentResult.InvalidTarget;
            }
            BlockType type = _blocks.GetBlock(intent.X, intent.Y, 0, layer);
            if (type.Build == null)
            {
                return IntentResult.InvalidTarget;                          // only what players built
            }
            if (!InReach(player, intent.X, intent.Y))
            {
                return IntentResult.OutOfReach;
            }
            RefundBlock(playerIndex, type, DismantleRefund);
            _blocks.SetBlock(intent.X, intent.Y, 0, layer, BlockRegistry.AirId);
            LastNoise = type.Build.Noise > 0f ? type.Build.Noise * 0.5f : DefaultHitNoise * 0.5f;
            return IntentResult.Ok;
        }

        private IntentResult Undo(int playerIndex, PlayerState player)
        {
            while (_historyCount[playerIndex] > 0)
            {
                PlacedPiece last = _history[playerIndex][--_historyCount[playerIndex]];
                if (_blocks.GetTypeId(last.X, last.Y, 0, last.Layer) != last.Type)
                {
                    continue;                                               // already broken or replaced: look further back
                }
                if (!InReach(player, last.X, last.Y))
                {
                    _historyCount[playerIndex]++;                           // keep it: walk back to it
                    return IntentResult.OutOfReach;
                }
                RefundBlock(playerIndex, _blocks.Registry[last.Type], 1f);
                _blocks.SetBlock(last.X, last.Y, 0, last.Layer, BlockRegistry.AirId);
                LastNoise = 1f;
                return IntentResult.Ok;
            }
            return IntentResult.InvalidTarget;
        }

        private void Remember(int player, int x, int y, BuildLayer layer, ushort type)
        {
            PlacedPiece[] ring = _history[player];
            if (_historyCount[player] == ring.Length)
            {
                Array.Copy(ring, 1, ring, 0, ring.Length - 1);              // forget the oldest
                _historyCount[player]--;
            }
            ring[_historyCount[player]++] = new PlacedPiece { X = x, Y = y, Layer = layer, Type = type };
        }

        private struct PlacedPiece
        {
            public int X;
            public int Y;
            public BuildLayer Layer;
            public ushort Type;
        }

        private IntentResult HitBlock(int playerIndex, PlayerState player, in PlayerIntent intent)
        {
            if (!_blocks.InBounds(intent.X, intent.Y) || _blocks.GetBlock(intent.X, intent.Y).IsAir)
            {
                return IntentResult.InvalidTarget;
            }
            if (!InReach(player, intent.X, intent.Y))
            {
                return IntentResult.OutOfReach;
            }
            ItemDefinition tool = Equipped(playerIndex);
            int damage = tool != null && tool.tool_power > 0 ? tool.tool_power : _settings.BlockHitDamage;
            ushort type = _blocks.GetTypeId(intent.X, intent.Y);
            BlockBuildInfo build = _blocks.Registry[type].Build;
            if (build != null && build.Tool.Length > 0 && (tool == null || tool.tool_kind != build.Tool))
            {
                damage = Math.Max(1, (int)Math.Round(damage * WrongToolFactor, MidpointRounding.AwayFromZero));   // the wrong tool: much slower
            }
            DamageResult hit = _blocks.Damage(intent.X, intent.Y, damage);
            if (hit == DamageResult.None)
            {
                return IntentResult.InvalidTarget;
            }
            if (hit == DamageResult.Destroyed)
            {
                LastBrokenBlock = type;
            }
            if (tool != null && tool.tool_power > 0)
            {
                InventoryOf(playerIndex).WearEquipped();
            }
            LastNoise = build != null && build.Noise > 0f ? build.Noise : DefaultHitNoise;
            return IntentResult.Ok;
        }

        private IntentResult ToggleDoor(PlayerState player, in PlayerIntent intent, IReadOnlyList<PlayerState> players)
        {
            if (!_blocks.InBounds(intent.X, intent.Y))
            {
                return IntentResult.InvalidTarget;
            }
            if (!InReach(player, intent.X, intent.Y))
            {
                return IntentResult.OutOfReach;
            }
            BlockType type = _blocks.GetBlock(intent.X, intent.Y);
            // Closing a door on someone would trap them inside a solid block.
            if (!type.IsSolid && type.ToggleTo != 0 && IsOccupiedByBody(intent.X, intent.Y, players))
            {
                return IntentResult.Occupied;
            }
            return _blocks.Toggle(intent.X, intent.Y) ? IntentResult.Ok : IntentResult.InvalidTarget;
        }

        private IntentResult Melee(int playerIndex, IReadOnlyList<PlayerState> players, in PlayerIntent intent, ref float cooldown)
        {
            ItemDefinition weapon = Equipped(playerIndex);
            bool armed = weapon != null && weapon.IsMelee;
            float range = armed ? weapon.range : _settings.MeleeRange;
            float arc = armed ? weapon.arc : _settings.MeleeArcDegrees;
            int damage = armed ? weapon.damage : _settings.MeleeDamage;
            string type = armed ? weapon.damage_type : _settings.MeleeDamageType;
            LastNoise = armed ? weapon.noise : _settings.MeleeNoise;
            if (armed)
            {
                cooldown = Math.Max(cooldown, weapon.cooldown);
            }

            Float2 origin = players[playerIndex].Position;
            Float2 aim = intent.AimDirection;
            float cosHalfArc = MathF.Cos(arc * 0.5f * MathF.PI / 180f);
            int hits = 0;
            if (_zombies != null)
            {
                for (int i = 0; i < _zombies.Capacity; i++)
                {
                    if (_zombies.IsAlive(i) && InArc(origin, aim, _zombies.GetPosition(i), range, cosHalfArc))
                    {
                        _zombies.Damage(i, damage, type);
                        hits++;
                    }
                }
            }
            if (FriendlyFire)
            {
                for (int p = 0; p < players.Count; p++)
                {
                    if (p != playerIndex && !players[p].IsDowned && InArc(origin, aim, players[p].Position, range, cosHalfArc))
                    {
                        _playerDamage.Add(new PlayerDamage(playerIndex, p, damage));
                        hits++;
                    }
                }
            }
            if (hits > 0 && armed)
            {
                InventoryOf(playerIndex).WearEquipped();
            }
            LastMeleeHits = hits;
            return hits > 0 ? IntentResult.Ok : IntentResult.Missed;
        }

        private bool InArc(Float2 origin, Float2 aim, Float2 target, float range, float cosHalfArc)
        {
            Float2 to = target - origin;
            float d2 = to.LengthSquared;
            if (d2 > range * range)
            {
                return false;
            }
            bool inArc = d2 < 0.04f || (to.X * aim.X + to.Y * aim.Y) / MathF.Sqrt(d2) >= cosHalfArc;
            return inArc && TileCollision.HasClearLine(origin, target, _blocks);
        }

        /// <summary>Hitscan: the first zombie (or player, with friendly fire) on the line; walls stop the bullet.</summary>
        private IntentResult Ranged(int playerIndex, IReadOnlyList<PlayerState> players, in PlayerIntent intent, ref float cooldown)
        {
            ItemDefinition weapon = Equipped(playerIndex);
            Inventory inventory = InventoryOf(playerIndex);
            if (weapon == null || !weapon.IsRanged)
            {
                return IntentResult.InvalidTarget;
            }
            ushort ammo = inventory.Catalog.IndexOf(weapon.ammo);
            if (inventory.Remove(ammo, 1) == 0)
            {
                return IntentResult.NoAmmo;
            }
            inventory.WearEquipped();
            cooldown = Math.Max(cooldown, weapon.cooldown);
            LastNoise = weapon.noise;

            Float2 origin = players[playerIndex].Position;
            Float2 aim = intent.AimDirection;
            for (float t = 0.3f; t <= weapon.range; t += RayStep)
            {
                Float2 p = origin + aim * t;
                Int2 cell = p.Tile;
                if (_blocks.IsSolid(cell.X, cell.Y))
                {
                    break;
                }
                if (_zombies != null)
                {
                    for (int i = 0; i < _zombies.Capacity; i++)
                    {
                        if (_zombies.IsAlive(i) && (_zombies.GetPosition(i) - p).LengthSquared <= BodyRadius * BodyRadius)
                        {
                            _zombies.Damage(i, weapon.damage, weapon.damage_type);
                            LastMeleeHits = 1;
                            return IntentResult.Ok;
                        }
                    }
                }
                if (FriendlyFire)
                {
                    for (int pl = 0; pl < players.Count; pl++)
                    {
                        if (pl != playerIndex && !players[pl].IsDowned && (players[pl].Position - p).LengthSquared <= BodyRadius * BodyRadius)
                        {
                            _playerDamage.Add(new PlayerDamage(playerIndex, pl, weapon.damage));
                            LastMeleeHits = 1;
                            return IntentResult.Ok;
                        }
                    }
                }
            }
            return IntentResult.Missed;
        }

        private bool IsOccupiedByBody(int x, int y, IReadOnlyList<PlayerState> players)
        {
            if (players != null)
            {
                foreach (PlayerState p in players)
                {
                    if (!p.IsDowned && TileCollision.CircleTouchesCell(p.Position, _settings.Radius, x, y))
                    {
                        return true;
                    }
                }
            }
            if (_zombies != null)
            {
                for (int i = 0; i < _zombies.Capacity; i++)
                {
                    if (_zombies.IsAlive(i) && TileCollision.CircleTouchesCell(_zombies.GetPosition(i), 0.35f, x, y))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
