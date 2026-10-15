using System;
using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Net;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Core
{
    /// <summary>
    /// One match on the host: the fixed-tick loop that ties players, intents,
    /// the clock, the horde director and the zombies together. Unity calls
    /// <see cref="Step"/> from its fixed update and forwards the recorded
    /// changes and events through FishNet; nothing here knows about either.
    /// </summary>
    public sealed class GameSession
    {
        public const int MaxPlayers = 4;

        private const float NightSight = 0.5f;
        private const float SpawnMinRadius = 20f;
        private const float SpawnMaxRadius = 40f;
        private const float LoudNoise = 5f;
        private const int LoudActionsForFullBonus = 100;

        private readonly PlayerSettings _playerSettings;
        private readonly List<PlayerState> _players = new List<PlayerState>();
        private readonly List<PlayerInput> _inputs = new List<PlayerInput>();
        private readonly List<KeyValuePair<int, PlayerIntent>> _intents = new List<KeyValuePair<int, PlayerIntent>>();
        private readonly List<SessionEvent> _events = new List<SessionEvent>();
        private readonly List<Float2> _livingPositions = new List<Float2>();
        private readonly List<int> _livingIndex = new List<int>();
        private readonly List<string> _due = new List<string>();
        private readonly List<string> _backlog = new List<string>();
        private readonly List<PlayerHit> _hits = new List<PlayerHit>();
        private readonly List<ZombieDeath> _deaths = new List<ZombieDeath>();
        private readonly DeterministicRandom _rng;
        private readonly float _dt;

        private int _loudActions;

        public GameSession(GameSettings settings, PlayerSettings playerSettings, BlockWorld blocks, GroundLayer ground,
            ZombieCatalog catalog, int zombieCapacity = 512)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            _playerSettings = playerSettings ?? throw new ArgumentNullException(nameof(playerSettings));
            Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            Ground = ground ?? throw new ArgumentNullException(nameof(ground));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Clock = new DayNightCycle(settings);
            Director = new HordeDirector(settings);
            Zombies = new ZombieSimulation(blocks, ground, catalog, zombieCapacity);
            Rules = new HostIntentRules(blocks, ground, Zombies, playerSettings, MaxPlayers);
            _rng = new DeterministicRandom(settings.Seed);
            _dt = 1f / settings.TickRate;
        }

        public GameSettings Settings { get; }

        public BlockWorld Blocks { get; }

        public GroundLayer Ground { get; }

        public ZombieCatalog Catalog { get; }

        public DayNightCycle Clock { get; }

        public HordeDirector Director { get; }

        public ZombieSimulation Zombies { get; }

        public HostIntentRules Rules { get; }

        public IReadOnlyList<PlayerState> Players => _players;

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
            return _players.Count - 1;
        }

        /// <summary>Latest movement input of a player (from the network or the local device).</summary>
        public void SetInput(int player, PlayerInput input) => _inputs[player] = input;

        /// <summary>Queues an action intent; it is validated during the next <see cref="Step"/>.</summary>
        public void QueueIntent(int player, PlayerIntent intent) => _intents.Add(new KeyValuePair<int, PlayerIntent>(player, intent));

        public void DrainEvents(List<SessionEvent> into)
        {
            into.AddRange(_events);
            _events.Clear();
        }

        /// <summary>
        /// Puts a fresh session (same map, same settings) in a saved state. Zombies are
        /// not saved: loading during a night restarts that night's horde.
        /// </summary>
        internal void Restore(long tick, int nightsSurvived, int zombiesKilled, IReadOnlyList<PlayerState> players)
        {
            if (players.Count > MaxPlayers)
            {
                throw new ArgumentException($"A match has at most {MaxPlayers} players.", nameof(players));
            }
            _players.Clear();
            _inputs.Clear();
            foreach (PlayerState p in players)
            {
                _players.Add(p);
                _inputs.Add(default);
            }
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

        /// <summary>Advances the match by one fixed tick (1 / TickRate seconds).</summary>
        public void Step()
        {
            if (IsGameOver)
            {
                return;
            }
            MovePlayers();
            ResolveIntents();
            AdvanceClock();
            SpawnHorde();
            CollectLiving();
            Zombies.Tick(_dt, _livingPositions);
            ApplyZombieHits();
            CountDeaths();
        }

        private void MovePlayers()
        {
            for (int p = 0; p < _players.Count; p++)
            {
                PlayerState state = _players[p];
                float noise = PlayerMotor.Step(ref state, _inputs[p], _dt, Blocks, Ground, _playerSettings);
                _players[p] = state;
                Zombies.EmitNoise(state.Position, noise);
            }
        }

        private void ResolveIntents()
        {
            foreach (KeyValuePair<int, PlayerIntent> entry in _intents)
            {
                IntentResult result = Rules.Apply(entry.Key, entry.Value, _players, Time);
                if (Rules.LastNoise > 0f)
                {
                    Zombies.EmitNoise(_players[entry.Key].Position, Rules.LastNoise);
                    if (Rules.LastNoise >= LoudNoise && Clock.Phase == DayPhase.Day)
                    {
                        _loudActions++;
                    }
                }
                _events.Add(new SessionEvent(SessionEventType.IntentResolved, entry.Key, (int)entry.Value.Type, result: result));
            }
            _intents.Clear();
        }

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
            }
            else if (phase == DayPhase.Dawn && AnyoneStanding())
            {
                NightsSurvived++;
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

        private void ApplyZombieHits()
        {
            _hits.Clear();
            Zombies.DrainPlayerHits(_hits);
            foreach (PlayerHit hit in _hits)
            {
                if (hit.PlayerIndex < 0 || hit.PlayerIndex >= _livingIndex.Count)
                {
                    continue;
                }
                int p = _livingIndex[hit.PlayerIndex];
                PlayerState state = _players[p];
                if (state.IsDowned)
                {
                    continue;
                }
                state.Health = Math.Max(0, state.Health - hit.Damage);
                _players[p] = state;
                _events.Add(new SessionEvent(SessionEventType.PlayerHurt, p, hit.Damage));
                if (state.IsDowned)
                {
                    _events.Add(new SessionEvent(SessionEventType.PlayerDowned, p));
                }
            }
            if (_players.Count > 0 && !AnyoneStanding())
            {
                IsGameOver = true;
                _events.Add(new SessionEvent(SessionEventType.GameOver, value: NightsSurvived));
            }
        }

        private void CountDeaths()
        {
            _deaths.Clear();
            Zombies.DrainDeaths(_deaths);
            ZombiesKilled += _deaths.Count;
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
    }
}
