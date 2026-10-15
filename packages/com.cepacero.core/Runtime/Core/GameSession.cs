using System;
using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Items;
using CepaCero.Lore;
using CepaCero.Map;
using CepaCero.Net;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Core
{
    /// <summary>
    /// One match on the host: the fixed-tick loop that ties players, intents,
    /// items, the clock, the horde director and the zombies together. Unity
    /// calls <see cref="Step"/> from its fixed update and forwards the recorded
    /// changes and events through FishNet; nothing here knows about either.
    ///
    /// Falling and dying (D-012): at 0 health a player goes down and a
    /// teammate can help them up; after <see cref="GameSettings.BleedOutSeconds"/>
    /// they die, leaving their backpack on the floor, and respawn at dawn.
    /// Alone, or if the whole team is down at once, death is immediate; with
    /// <see cref="GameSettings.PermaDeath"/> a team wipe ends the match.
    /// </summary>
    public sealed class GameSession
    {
        public const int MaxPlayers = 4;

        private const float NightSight = 0.5f;
        private const float SpawnMinRadius = 20f;
        private const float SpawnMaxRadius = 40f;
        private const float LoudNoise = 5f;
        private const int LoudActionsForFullBonus = 100;
        private const float LowBar = 25f;

        private readonly PlayerSettings _playerSettings;
        private readonly List<PlayerState> _players = new List<PlayerState>();
        private readonly List<PlayerInput> _inputs = new List<PlayerInput>();
        private readonly List<Inventory> _inventories = new List<Inventory>();
        private readonly List<int> _sentInventoryVersion = new List<int>();
        private readonly List<int> _revivingTarget = new List<int>();
        private readonly List<Int2> _openContainer = new List<Int2>();
        private readonly List<KeyValuePair<int, PlayerIntent>> _intents = new List<KeyValuePair<int, PlayerIntent>>();
        private readonly List<SessionEvent> _events = new List<SessionEvent>();
        private readonly List<Float2> _livingPositions = new List<Float2>();
        private readonly List<int> _livingIndex = new List<int>();
        private readonly List<string> _due = new List<string>();
        private readonly List<string> _backlog = new List<string>();
        private readonly List<PlayerHit> _hits = new List<PlayerHit>();
        private readonly List<PlayerDamage> _friendlyHits = new List<PlayerDamage>();
        private readonly List<ZombieDeath> _deaths = new List<ZombieDeath>();
        private readonly List<ItemStack> _spill = new List<ItemStack>();
        private readonly DeterministicRandom _rng;
        private readonly float _dt;
        private readonly ushort _chestId;

        private readonly List<int> _newDiary = new List<int>();
        private int _loudActions;
        private bool _respawnPointSet;

        /// <summary>A match without items (bare hands, free building): tests and benchmarks.</summary>
        public GameSession(GameSettings settings, PlayerSettings playerSettings, BlockWorld blocks, GroundLayer ground,
            ZombieCatalog catalog, int zombieCapacity = 512)
            : this(settings, playerSettings, blocks, ground, new GameData(catalog), null, zombieCapacity)
        {
        }

        /// <summary>A full match. Pass the map's buildings to place lootable containers inside them.</summary>
        public GameSession(GameSettings settings, PlayerSettings playerSettings, BlockWorld blocks, GroundLayer ground,
            GameData data, IReadOnlyList<BuildingInfo> buildings = null, int zombieCapacity = 512)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            _playerSettings = playerSettings ?? throw new ArgumentNullException(nameof(playerSettings));
            Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            Ground = ground ?? throw new ArgumentNullException(nameof(ground));
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Clock = new DayNightCycle(settings);
            Director = new HordeDirector(settings);
            Zombies = new ZombieSimulation(blocks, ground, data.Zombies, zombieCapacity, seed: settings.Seed ^ 0x5A5AUL);
            Rules = new HostIntentRules(blocks, ground, Zombies, playerSettings, MaxPlayers) { FriendlyFire = settings.FriendlyFire };
            _rng = new DeterministicRandom(settings.Seed);
            _dt = 1f / settings.TickRate;
            _chestId = blocks.Registry.TryGet("chest", out BlockType chest) ? chest.Id : ushort.MaxValue;

            GroundItems = new GroundItems();
            if (data.HasItems)
            {
                Recipes = new RecipeBook(data.RecipesData, data.Items);
                Containers = new ContainerStore(data.Items);
                ItemRules = new HostItemRules(blocks, playerSettings, data.Items, Recipes, data.Loot, Containers, GroundItems, _rng, settings);
                Rules.Inventories = _inventories;
                Rules.CanAffordBlock = (p, type) => Recipes.TryPayForBlock(_inventories[p], blocks.Registry[type].Key);
                if (buildings != null && data.Loot != null)
                {
                    Containers.PlaceInBuildings(buildings, blocks, ground, data.Loot, _rng);
                }
            }
            if (data.Diary != null)
            {
                Diary = new TeamDiary(data.Diary);
            }
            blocks.TypeChanged += OnBlockTypeChanged;
        }

        public GameSettings Settings { get; }

        public PlayerSettings PlayerSettings => _playerSettings;

        public BlockWorld Blocks { get; }

        public GroundLayer Ground { get; }

        public GameData Data { get; }

        public ZombieCatalog Catalog => Data.Zombies;

        public DayNightCycle Clock { get; }

        public HordeDirector Director { get; }

        public ZombieSimulation Zombies { get; }

        public HostIntentRules Rules { get; }

        /// <summary>Null in a match without items.</summary>
        public HostItemRules ItemRules { get; }

        /// <summary>The team's recipe book (null without items).</summary>
        public RecipeBook Recipes { get; }

        /// <summary>Map containers and player chests (null without items).</summary>
        public ContainerStore Containers { get; }

        public GroundItems GroundItems { get; }

        /// <summary>The group's shared diary (null without diary data).</summary>
        public TeamDiary Diary { get; }

        public IReadOnlyList<PlayerState> Players => _players;

        /// <summary>Players' inventories, same order as <see cref="Players"/> (empty without items).</summary>
        public IReadOnlyList<Inventory> Inventories => _inventories;

        /// <summary>Marker for "no container open".</summary>
        public static readonly Int2 NoContainer = new Int2(-1, -1);

        /// <summary>
        /// Container a player is looking into (the host keeps sending its contents), or
        /// <see cref="NoContainer"/>. Closed automatically when they walk out of reach.
        /// </summary>
        public Int2 OpenContainerOf(int player) => _openContainer[player];

        /// <summary>Contents of the container a player has open, or null.</summary>
        public Inventory OpenContainerContents(int player)
        {
            Int2 cell = _openContainer[player];
            return cell == NoContainer || ItemRules == null ? null : ItemRules.OpenInReach(_players[player], cell.X, cell.Y);
        }

        /// <summary>Where dead players come back at dawn (the fort). Defaults to the first player's spawn.</summary>
        public Float2 RespawnPoint { get; set; }

        /// <summary>Seconds of match time.</summary>
        public float Time => Clock.Tick * _dt;

        public bool IsGameOver { get; private set; }

        /// <summary>Nights fully survived (dawn reached with someone standing).</summary>
        public int NightsSurvived { get; private set; }

        public int ZombiesKilled { get; private set; }

        /// <summary>Zombies that entered during the current (or last) night.</summary>
        public int SpawnedTonight { get; private set; }

        public HordePlan CurrentPlan { get; private set; }

        public int AddPlayer(Float2 position)
        {
            if (_players.Count >= MaxPlayers)
            {
                throw new InvalidOperationException($"A match has at most {MaxPlayers} players.");
            }
            _players.Add(PlayerState.Spawn(position, _playerSettings));
            _inputs.Add(default);
            _revivingTarget.Add(-1);
            _openContainer.Add(NoContainer);
            if (Data.HasItems)
            {
                _inventories.Add(Inventory.ForPlayer(Data.Items));
                _sentInventoryVersion.Add(-1);
            }
            if (!_respawnPointSet)
            {
                RespawnPoint = position;
                _respawnPointSet = true;
            }
            return _players.Count - 1;
        }

        /// <summary>
        /// Places the day population (D-013): walkers with some runners, unaware, spread
        /// over walkable tiles (streets and inside buildings), away from the players. Call
        /// once after adding the players, when a match starts or is loaded. Returns how many.
        /// </summary>
        public int PopulateMap()
        {
            int walkable = 0;
            for (int y = 0; y < Ground.Height; y++)
            {
                for (int x = 0; x < Ground.Width; x++)
                {
                    walkable += IsSpawnable(x, y) ? 1 : 0;
                }
            }
            int wanted = Math.Min(Settings.MapZombiesMax, (int)(walkable / 1000f * Settings.MapZombiesPer1000Tiles));
            bool hasRunner = Catalog.TryGetIndex(HordeDirector.Runner, out byte runner);
            byte walker = Catalog.IndexOf(HordeDirector.Walker);
            float safe2 = Settings.MapZombiesSafeRadius * Settings.MapZombiesSafeRadius;
            int placed = 0;
            for (int attempt = 0; placed < wanted && attempt < wanted * 30; attempt++)
            {
                int x = _rng.Range(0, Ground.Width);
                int y = _rng.Range(0, Ground.Height);
                if (!IsSpawnable(x, y))
                {
                    continue;
                }
                var position = new Float2(x + 0.5f, y + 0.5f);
                bool tooClose = false;
                foreach (PlayerState p in _players)
                {
                    tooClose |= (p.Position - position).LengthSquared < safe2;
                }
                if (tooClose)
                {
                    continue;
                }
                byte type = hasRunner && _rng.Chance(0.1f) ? runner : walker;
                if (Zombies.Spawn(type, position, Math.Max(1, _players.Count), aware: false) >= 0)
                {
                    placed++;
                }
            }
            return placed;
        }

        private bool IsSpawnable(int x, int y) =>
            !Blocks.IsSolid(x, y) && GroundLayer.WalkCost(Ground.Get(x, y)) > 0 && !Blocks.GetBlock(x, y).IsTrap;

        /// <summary>Latest movement input of a player (from the network or the local device).</summary>
        public void SetInput(int player, PlayerInput input) => _inputs[player] = input;

        /// <summary>Queues an action intent; it is validated during the next <see cref="Step"/>.</summary>
        public void QueueIntent(int player, PlayerIntent intent) => _intents.Add(new KeyValuePair<int, PlayerIntent>(player, intent));

        public void DrainEvents(List<SessionEvent> into)
        {
            into.AddRange(_events);
            _events.Clear();
        }

        /// <summary>Advances the match by one fixed tick (1 / TickRate seconds).</summary>
        public void Step()
        {
            if (IsGameOver)
            {
                return;
            }
            if (Clock.Tick == 0)
            {
                FireDiary("match_start");
            }
            MovePlayers();
            ResolveIntents();
            CloseFarContainers();
            UpdateSurvival();
            UpdateDownedPlayers();
            AdvanceClock();
            SpawnHorde();
            CollectLiving();
            Zombies.Tick(_dt, _livingPositions);
            ApplyDamage();
            HandleZombieDeaths();
            CheckTeamWipe();
            ReportInventories();
        }

        // ------------------------------------------------------------- players

        private void MovePlayers()
        {
            for (int p = 0; p < _players.Count; p++)
            {
                PlayerState state = _players[p];
                float noise = PlayerMotor.Step(ref state, _inputs[p], _dt, Blocks, Ground, _playerSettings,
                    SurvivalRules.StaminaRegenFactor(state, Settings));
                _players[p] = state;
                Zombies.EmitNoise(state.Position, noise);
            }
        }

        private void ResolveIntents()
        {
            foreach (KeyValuePair<int, PlayerIntent> entry in _intents)
            {
                int p = entry.Key;
                PlayerIntent intent = entry.Value;
                if (p < 0 || p >= _players.Count)
                {
                    continue;
                }
                if (intent.Type == IntentType.Revive)
                {
                    _revivingTarget[p] = intent.X; // held this tick; checked in UpdateDownedPlayers
                    continue;
                }
                IntentResult result;
                if (intent.IsInventoryAction)
                {
                    result = ItemRules == null ? IntentResult.NotAllowed : ItemRules.Apply(p, intent, _players, _inventories);
                    if (result == IntentResult.Ok)
                    {
                        AfterItemAction(p, intent);
                    }
                    if (intent.UsesContainer)
                    {
                        _openContainer[p] = result == IntentResult.Ok ? new Int2(intent.X, intent.Y) : NoContainer;
                        string kind = result == IntentResult.Ok ? Containers.KindAt(new Int2(intent.X, intent.Y)) : null;
                        if (!string.IsNullOrEmpty(kind))
                        {
                            FireDiary("first_container");
                            FireDiary("building_kind:" + kind);
                        }
                    }
                }
                else
                {
                    result = Rules.Apply(p, intent, _players, Time);
                    if (result == IntentResult.Ok)
                    {
                        AfterWorldAction(p, intent);
                    }
                    if (Rules.LastNoise > 0f)
                    {
                        Zombies.EmitNoise(_players[p].Position, Rules.LastNoise);
                        if (Rules.LastNoise >= LoudNoise && Clock.Phase == DayPhase.Day)
                        {
                            _loudActions++;
                        }
                    }
                }
                _events.Add(new SessionEvent(SessionEventType.IntentResolved, p, (int)intent.Type, result: result));
            }
            _intents.Clear();
        }

        private void AfterWorldAction(int p, in PlayerIntent intent)
        {
            if (intent.Type == IntentType.PlaceBlock && intent.Arg == _chestId && Containers != null)
            {
                Containers.AddChest(new Int2(intent.X, intent.Y));
            }
            if (Rules.LastBrokenBlock != ushort.MaxValue && Data.HasItems
                && BlockDrops.TryGet(Blocks.Registry[Rules.LastBrokenBlock].Key, out string item, out int count))
            {
                ushort index = Data.Items.IndexOf(item);
                int added = _inventories[p].Add(index, count);
                if (added < count)
                {
                    DropPile(new Float2(intent.X + 0.5f, intent.Y + 0.5f), new[] { new ItemStack(index, (ushort)(count - added)) });
                }
            }
        }

        private void AfterItemAction(int p, in PlayerIntent intent)
        {
            if (ItemRules.LastHeal > 0)
            {
                _events.Add(new SessionEvent(SessionEventType.PlayerHealed, p, ItemRules.LastHeal));
            }
            if (ItemRules.LastUnlockedSample != null)
            {
                _events.Add(new SessionEvent(SessionEventType.RecipeUnlocked, p));
                FireDiary("sample:" + ItemRules.LastUnlockedSample);
            }
            if (intent.Type == IntentType.PickUp || intent.Type == IntentType.DropStack)
            {
                _events.Add(new SessionEvent(SessionEventType.GroundItemsChanged, p));
            }
        }

        private void CloseFarContainers()
        {
            for (int p = 0; p < _openContainer.Count; p++)
            {
                if (_openContainer[p] != NoContainer && OpenContainerContents(p) == null)
                {
                    _openContainer[p] = NoContainer;
                }
            }
        }

        private void UpdateSurvival()
        {
            for (int p = 0; p < _players.Count; p++)
            {
                PlayerState state = _players[p];
                float food = state.Food;
                float water = state.Water;
                int damage = SurvivalRules.Tick(ref state, Settings, Clock.Tick);
                state.Health = Math.Min(state.Health, SurvivalRules.MaxHealth(state, Settings, _playerSettings));
                _players[p] = state;
                ReportBar(SessionEventType.Hungry, p, food, state.Food);
                ReportBar(SessionEventType.Thirsty, p, water, state.Water);
                Hurt(p, damage);
            }
        }

        private void ReportBar(SessionEventType type, int p, float before, float after)
        {
            if (before > LowBar && after <= LowBar)
            {
                _events.Add(new SessionEvent(type, p, 0));
            }
            if (before > 0f && after <= 0f)
            {
                _events.Add(new SessionEvent(type, p, 1));
            }
        }

        private void UpdateDownedPlayers()
        {
            for (int p = 0; p < _players.Count; p++)
            {
                PlayerState state = _players[p];
                if (!state.IsDowned || state.IsDead)
                {
                    continue;
                }
                bool helped = false;
                for (int h = 0; h < _players.Count; h++)
                {
                    if (h != p && _revivingTarget[h] == p && !_players[h].IsDowned
                        && (_players[h].Position - state.Position).LengthSquared <= _playerSettings.ReviveReach * _playerSettings.ReviveReach)
                    {
                        helped = true;
                    }
                }
                if (helped)
                {
                    state.ReviveProgress += _dt;
                    if (state.ReviveProgress >= Settings.ReviveSeconds)
                    {
                        state.Health = _playerSettings.ReviveHealth;
                        state.DownedSeconds = 0f;
                        state.ReviveProgress = 0f;
                        _players[p] = state;
                        _events.Add(new SessionEvent(SessionEventType.PlayerRevived, p));
                        continue;
                    }
                }
                else
                {
                    state.ReviveProgress = 0f; // the key has to be held
                    state.DownedSeconds += _dt;
                }
                _players[p] = state;
                if (state.DownedSeconds >= Settings.BleedOutSeconds)
                {
                    Die(p);
                }
            }
            for (int h = 0; h < _revivingTarget.Count; h++)
            {
                _revivingTarget[h] = -1;
            }
        }

        private void Hurt(int p, int damage)
        {
            PlayerState state = _players[p];
            if (state.IsDowned || damage <= 0)
            {
                return;
            }
            state.Health = Math.Max(0, state.Health - damage);
            state.DownedSeconds = 0f;
            state.ReviveProgress = 0f;
            _players[p] = state;
            _events.Add(new SessionEvent(SessionEventType.PlayerHurt, p, damage));
            if (state.Health > 0)
            {
                return;
            }
            _events.Add(new SessionEvent(SessionEventType.PlayerDowned, p));
            if (_players.Count == 1)
            {
                Die(p); // alone there is nobody to help you up
            }
        }

        private void Die(int p)
        {
            PlayerState state = _players[p];
            state.IsDead = true;
            state.Health = 0;
            state.DownedSeconds = 0f;
            state.ReviveProgress = 0f;
            _players[p] = state;
            if (Data.HasItems)
            {
                _spill.Clear();
                _inventories[p].TakeAll(_spill);
                DropPile(state.Position, _spill); // the backpack stays where they fell
            }
            _events.Add(new SessionEvent(SessionEventType.PlayerDied, p));
        }

        private void RespawnTheDead()
        {
            for (int p = 0; p < _players.Count; p++)
            {
                if (!_players[p].IsDead)
                {
                    continue;
                }
                _players[p] = PlayerState.Spawn(RespawnPoint, _playerSettings);
                _events.Add(new SessionEvent(SessionEventType.PlayerRespawned, p));
            }
        }

        private void CheckTeamWipe()
        {
            if (_players.Count == 0 || AnyoneStanding())
            {
                return;
            }
            if (Settings.PermaDeath)
            {
                IsGameOver = true;
                _events.Add(new SessionEvent(SessionEventType.GameOver, value: NightsSurvived));
                return;
            }
            for (int p = 0; p < _players.Count; p++)
            {
                if (!_players[p].IsDead)
                {
                    Die(p); // nobody left to help: everyone waits for dawn
                }
            }
        }

        private void ReportInventories()
        {
            for (int p = 0; p < _inventories.Count; p++)
            {
                if (_inventories[p].Version != _sentInventoryVersion[p])
                {
                    _sentInventoryVersion[p] = _inventories[p].Version;
                    _events.Add(new SessionEvent(SessionEventType.InventoryChanged, p));
                }
            }
        }

        // ---------------------------------------------------------- clock, horde

        private void AdvanceClock()
        {
            if (!Clock.Step())
            {
                return;
            }
            DayPhase phase = Clock.Phase;
            _events.Add(new SessionEvent(SessionEventType.PhaseChanged, value: Clock.Day, phase: phase));
            Zombies.SightMultiplier = phase == DayPhase.Day ? 1f : NightSight;
            if (phase == DayPhase.Night)
            {
                // The louder the day, the bigger the night (adaptive pressure, docs/GDD.md).
                float noiseBonus = Math.Min(1f, _loudActions / (float)LoudActionsForFullBonus);
                CurrentPlan = Director.PlanNight(Clock.Day, noiseBonus);
                Director.BeginNight(CurrentPlan);
                SpawnedTonight = 0;
                _backlog.Clear();
                _loudActions = 0;
                _events.Add(new SessionEvent(SessionEventType.NightStarted, value: Clock.Day, phase: phase));
                FireDiary("night_started:" + Clock.Day);
            }
            else if (phase == DayPhase.Dawn)
            {
                if (AnyoneStanding())
                {
                    NightsSurvived++;
                    FireDiary("night_survived:" + Clock.Day);
                }
                RespawnTheDead();
                if (Settings.LootRespawn && Containers != null)
                {
                    Containers.ResetEmpty();
                }
            }
        }

        private void SpawnHorde()
        {
            if (Clock.Phase != DayPhase.Night || !AnyoneStanding())
            {
                return;
            }
            _due.Clear();
            Director.TakeDue(Clock.PhaseProgress, Zombies.Count + _backlog.Count, _due);
            _backlog.AddRange(_due);
            CollectLiving();
            for (int k = _backlog.Count - 1; k >= 0; k--)
            {
                if (!Catalog.TryGetIndex(_backlog[k], out byte type))
                {
                    _backlog.RemoveAt(k); // a type this bestiary does not have
                    continue;
                }
                if (!SpawnFinder.TryFind(Ground, Blocks, Zombies.Field, _livingPositions, _rng, SpawnMinRadius, SpawnMaxRadius,
                        out Float2 position, attempts: 8))
                {
                    break; // try again next tick (the flow field may not reach far enough yet)
                }
                if (Zombies.Spawn(type, position, _players.Count, aware: true) < 0)
                {
                    break; // full
                }
                _backlog.RemoveAt(k);
                SpawnedTonight++;
            }
        }

        private void CollectLiving()
        {
            _livingPositions.Clear();
            _livingIndex.Clear();
            for (int p = 0; p < _players.Count; p++)
            {
                if (!_players[p].IsDowned)
                {
                    _livingPositions.Add(_players[p].Position);
                    _livingIndex.Add(p);
                }
            }
        }

        private void ApplyDamage()
        {
            _hits.Clear();
            Zombies.DrainPlayerHits(_hits);
            foreach (PlayerHit hit in _hits)
            {
                if (hit.PlayerIndex >= 0 && hit.PlayerIndex < _livingIndex.Count)
                {
                    Hurt(_livingIndex[hit.PlayerIndex], hit.Damage);
                }
            }
            _friendlyHits.Clear();
            Rules.DrainPlayerDamage(_friendlyHits);
            foreach (PlayerDamage d in _friendlyHits)
            {
                Hurt(d.Target, d.Damage);
            }
        }

        private void HandleZombieDeaths()
        {
            _deaths.Clear();
            Zombies.DrainDeaths(_deaths);
            ZombiesKilled += _deaths.Count;
            if (Data.Loot == null)
            {
                return;
            }
            foreach (ZombieDeath death in _deaths)
            {
                _spill.Clear();
                Data.Loot.RollDrop(Catalog[death.ZombieType], _spill, _rng);
                DropPile(death.Position, _spill);
            }
        }

        private void FireDiary(string trigger)
        {
            if (Diary == null)
            {
                return;
            }
            _newDiary.Clear();
            Diary.Fire(trigger, _newDiary);
            foreach (int entry in _newDiary)
            {
                _events.Add(new SessionEvent(SessionEventType.DiaryUnlocked, value: entry));
            }
        }

        // ---------------------------------------------------------------- items

        private void DropPile(Float2 position, IReadOnlyList<ItemStack> stacks)
        {
            ItemPile pile = GroundItems.Drop(position, stacks);
            if (pile != null)
            {
                _events.Add(new SessionEvent(SessionEventType.GroundItemsChanged, value: pile.Id));
            }
        }

        private void OnBlockTypeChanged(int x, int y, ushort before, ushort after)
        {
            if (before != _chestId || after == _chestId || Containers == null)
            {
                return;
            }
            // A chest was destroyed: what was inside falls to the floor.
            _spill.Clear();
            Containers.Remove(new Int2(x, y), _spill);
            DropPile(new Float2(x + 0.5f, y + 0.5f), _spill);
        }

        private bool AnyoneStanding()
        {
            foreach (PlayerState p in _players)
            {
                if (!p.IsDowned)
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- saves

        /// <summary>
        /// Puts a fresh session (same map, same settings) in a saved state. Zombies are
        /// not saved: loading during a night restarts that night's horde.
        /// </summary>
        internal void Restore(long tick, int nightsSurvived, int zombiesKilled, IReadOnlyList<PlayerState> players, Float2 respawnPoint)
        {
            if (players.Count > MaxPlayers)
            {
                throw new ArgumentException($"A match has at most {MaxPlayers} players.", nameof(players));
            }
            _players.Clear();
            _inputs.Clear();
            _revivingTarget.Clear();
            _openContainer.Clear();
            _inventories.Clear();
            _sentInventoryVersion.Clear();
            foreach (PlayerState p in players)
            {
                _players.Add(p);
                _inputs.Add(default);
                _revivingTarget.Add(-1);
                _openContainer.Add(NoContainer);
                if (Data.HasItems)
                {
                    _inventories.Add(Inventory.ForPlayer(Data.Items));
                    _sentInventoryVersion.Add(-1);
                }
            }
            RespawnPoint = respawnPoint;
            _respawnPointSet = true;
            Clock.SetTick(tick);
            NightsSurvived = nightsSurvived;
            ZombiesKilled = zombiesKilled;
            IsGameOver = false;
            Zombies.SightMultiplier = Clock.Phase == DayPhase.Day ? 1f : NightSight;
            if (Clock.Phase == DayPhase.Night)
            {
                CurrentPlan = Director.PlanNight(Clock.Day);
                Director.BeginNight(CurrentPlan);
                SpawnedTonight = 0;
            }
        }
    }
}
