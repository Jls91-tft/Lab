using System;
using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Player;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Net
{
    /// <summary>
    /// The host's referee: checks every client intent (reach, rate limit,
    /// occupancy, allowed blocks) and applies it to the authoritative world.
    /// The world records the resulting changes, which become the deltas sent
    /// to everyone (see <see cref="BlockWorld.DrainChanges"/>).
    /// </summary>
    public sealed class HostIntentRules
    {
        private readonly BlockWorld _blocks;
        private readonly GroundLayer _ground;
        private readonly ZombieSimulation _zombies;
        private readonly PlayerSettings _settings;
        private readonly HashSet<ushort> _placeable = new HashSet<ushort>();
        private readonly float[] _nextActionTime;

        public HostIntentRules(BlockWorld blocks, GroundLayer ground, ZombieSimulation zombies, PlayerSettings settings,
            int maxPlayers = 4)
        {
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _zombies = zombies;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _nextActionTime = new float[maxPlayers];
            foreach (string key in new[] { "wood_wall", "metal_wall", "concrete_wall", "door", "fence" })
            {
                if (blocks.Registry.TryGet(key, out BlockType type))
                {
                    _placeable.Add(type.Id);
                }
            }
        }

        /// <summary>
        /// Inventory hook: return false to refuse a placement (not enough materials).
        /// Defaults to "always allowed" until the inventory exists.
        /// </summary>
        public Func<int, ushort, bool> CanAffordBlock { get; set; } = (player, type) => true;

        /// <summary>Noise radius made by the last successful action (for zombie hearing).</summary>
        public float LastNoise { get; private set; }

        /// <summary>Zombies hit by the last melee attack.</summary>
        public int LastMeleeHits { get; private set; }

        public IntentResult Apply(int playerIndex, in PlayerIntent intent, IReadOnlyList<PlayerState> players, float time)
        {
            LastNoise = 0f;
            LastMeleeHits = 0;
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

            IntentResult result;
            switch (intent.Type)
            {
                case IntentType.PlaceBlock:
                    result = Place(playerIndex, intent, players);
                    break;
                case IntentType.HitBlock:
                    result = HitBlock(player, intent);
                    break;
                case IntentType.ToggleDoor:
                    result = ToggleDoor(player, intent, players);
                    break;
                case IntentType.MeleeAttack:
                    result = Melee(player, intent);
                    break;
                default:
                    return IntentResult.NotAllowed;
            }
            if (result == IntentResult.Ok || result == IntentResult.Missed)
            {
                _nextActionTime[playerIndex] = time + _settings.ActionCooldown;
            }
            return result;
        }

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
            if (IsOccupiedByBody(x, y, players))
            {
                return IntentResult.Occupied;
            }
            if (!CanAffordBlock(playerIndex, intent.Arg))
            {
                return IntentResult.NotAllowed;
            }
            PlaceResult placed = _blocks.TryPlace(x, y, intent.Arg, _ground);
            if (placed == PlaceResult.Occupied)
            {
                return IntentResult.Occupied;
            }
            if (placed != PlaceResult.Placed)
            {
                return IntentResult.InvalidTarget;
            }
            LastNoise = 3f;
            return IntentResult.Ok;
        }

        private IntentResult HitBlock(PlayerState player, in PlayerIntent intent)
        {
            if (!_blocks.InBounds(intent.X, intent.Y) || _blocks.GetBlock(intent.X, intent.Y).IsAir)
            {
                return IntentResult.InvalidTarget;
            }
            if (!InReach(player, intent.X, intent.Y))
            {
                return IntentResult.OutOfReach;
            }
            if (_blocks.Damage(intent.X, intent.Y, _settings.BlockHitDamage) == DamageResult.None)
            {
                return IntentResult.InvalidTarget;
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

        private IntentResult Melee(PlayerState player, in PlayerIntent intent)
        {
            LastNoise = _settings.MeleeNoise;
            if (_zombies == null)
            {
                return IntentResult.Missed;
            }
            Float2 aim = intent.AimDirection;
            float cosHalfArc = MathF.Cos(_settings.MeleeArcDegrees * 0.5f * MathF.PI / 180f);
            float range = _settings.MeleeRange;
            int hits = 0;
            for (int i = 0; i < _zombies.Capacity; i++)
            {
                if (!_zombies.IsAlive(i))
                {
                    continue;
                }
                Float2 to = _zombies.GetPosition(i) - player.Position;
                float d2 = to.LengthSquared;
                if (d2 > range * range)
                {
                    continue;
                }
                bool inArc = d2 < 0.04f || (to.X * aim.X + to.Y * aim.Y) / MathF.Sqrt(d2) >= cosHalfArc;
                if (!inArc || !TileCollision.HasClearLine(player.Position, _zombies.GetPosition(i), _blocks))
                {
                    continue;
                }
                _zombies.Damage(i, _settings.MeleeDamage, _settings.MeleeDamageType);
                hits++;
            }
            LastMeleeHits = hits;
            return hits > 0 ? IntentResult.Ok : IntentResult.Missed;
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
