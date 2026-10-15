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
        private const float BodyRadius = 0.4f;
        private const float RayStep = 0.1f;

        private readonly BlockWorld _blocks;
        private readonly GroundLayer _ground;
        private readonly ZombieSimulation _zombies;
        private readonly PlayerSettings _settings;
        private readonly HashSet<ushort> _placeable = new HashSet<ushort>();
        private readonly float[] _nextActionTime;
        private readonly List<PlayerDamage> _playerDamage = new List<PlayerDamage>();

        public HostIntentRules(BlockWorld blocks, GroundLayer ground, ZombieSimulation zombies, PlayerSettings settings,
            int maxPlayers = 4)
        {
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _zombies = zombies;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _nextActionTime = new float[maxPlayers];
            foreach (string key in new[] { "wood_wall", "metal_wall", "concrete_wall", "door", "fence", "workbench", "chest" })
            {
                if (blocks.Registry.TryGet(key, out BlockType type))
                {
                    _placeable.Add(type.Id);
                }
            }
        }

        /// <summary>
        /// Building cost hook: return false to refuse a placement (not enough materials).
        /// The match wires it to <see cref="RecipeBook.TryPayForBlock"/>; it pays when it returns true.
        /// </summary>
        public Func<int, ushort, bool> CanAffordBlock { get; set; } = (player, type) => true;

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
            if (!_blocks.InBounds(x, y) || !_placeable.Contains(intent.Arg))
            {
                return IntentResult.InvalidTarget;
            }
            if (!InReach(players[playerIndex], x, y))
            {
                return IntentResult.OutOfReach;
            }
            if (!_blocks.GetBlock(x, y).IsAir)
            {
                return IntentResult.Occupied;
            }
            if (!GroundLayer.IsBuildable(_ground.Get(x, y)))
            {
                return IntentResult.InvalidTarget;
            }
            if (IsOccupiedByBody(x, y, players))
            {
                return IntentResult.Occupied;
            }
            // Last check, because it pays: everything else has been validated.
            if (!CanAffordBlock(playerIndex, intent.Arg))
            {
                return IntentResult.MissingIngredients;
            }
            _blocks.TryPlace(x, y, intent.Arg, _ground);
            LastNoise = 3f;
            return IntentResult.Ok;
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
            LastNoise = 5f;
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
