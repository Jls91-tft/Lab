using System;
using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Player;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Net.Protocol
{
    /// <summary>
    /// Host side of the protocol. After each <see cref="GameSession.Step"/>, call
    /// <see cref="CollectTick"/> once and then <see cref="WriteFor"/> for every
    /// remote player; send the bytes with FishNet. Incoming client bytes go to
    /// <see cref="Receive"/>, which validates them before touching the session.
    /// </summary>
    public sealed class HostBroadcaster
    {
        private readonly GameSession _session;
        private readonly string _mapName;
        private readonly uint _fingerprint;
        private readonly List<BlockChange> _changes = new List<BlockChange>();
        private readonly List<SessionEvent> _events = new List<SessionEvent>();
        private readonly List<SessionEvent> _forPlayer = new List<SessionEvent>();
        private readonly List<ZombieEvent> _zombieEvents = new List<ZombieEvent>();
        private readonly List<int> _scratch = new List<int>();
        private readonly int[] _sentInventory = new int[GameSession.MaxPlayers];
        private readonly Int2[] _sentContainerCell = new Int2[GameSession.MaxPlayers];
        private readonly int[] _sentContainerVersion = new int[GameSession.MaxPlayers];
        private readonly bool[] _joined = new bool[GameSession.MaxPlayers];
        private bool _pilesChanged;
        private bool _phaseChanged;

        public HostBroadcaster(GameSession session, string mapName)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _mapName = mapName ?? "";
            _fingerprint = Messages.DataFingerprint(session.Blocks.Registry, session.Data);
            for (int p = 0; p < GameSession.MaxPlayers; p++)
            {
                _sentInventory[p] = -1;
                _sentContainerCell[p] = GameSession.NoContainer;
                _sentContainerVersion[p] = -1;
            }
        }

        /// <summary>Radius (tiles) around each player whose zombies are sent to them.</summary>
        public float ZombieInterestRadius { get; set; } = 40f;

        public int ZombieSnapshotEveryTicks { get; set; } = 2;

        public int ClockEveryTicks { get; set; } = 20;

        /// <summary>Session events of the last tick, for the host's own UI.</summary>
        public IReadOnlyList<SessionEvent> LastEvents => _events;

        /// <summary>Zombie events of the last tick, for the host's own sound and effects.</summary>
        public IReadOnlyList<ZombieEvent> LastZombieEvents => _zombieEvents;

        /// <summary>Drains the shared state of this tick (block deltas and events). Call once per tick.</summary>
        public void CollectTick()
        {
            _changes.Clear();
            _session.Blocks.DrainChanges(_changes);
            _events.Clear();
            _session.DrainEvents(_events);
            _zombieEvents.Clear();
            _session.Zombies.DrainEvents(_zombieEvents);
            _pilesChanged = false;
            _phaseChanged = false;
            foreach (SessionEvent e in _events)
            {
                _pilesChanged |= e.Type == SessionEventType.GroundItemsChanged;
                _phaseChanged |= e.Type == SessionEventType.PhaseChanged;
            }
        }

        /// <summary>Everything a newly connected player needs: welcome, the whole world, piles, its inventory.</summary>
        public void WriteJoin(int player, ByteWriter w)
        {
            CheckPlayer(player);
            BlockWorld blocks = _session.Blocks;
            Messages.WriteWelcome(w, _mapName, blocks, _session.Settings, _fingerprint, player, _session.Clock.Tick);
            for (int cy = 0; cy < blocks.ChunksY; cy++)
            {
                for (int cx = 0; cx < blocks.ChunksX; cx++)
                {
                    Messages.WriteChunk(w, blocks, cx, cy);
                }
            }
            Messages.WritePlayerStates(w, (uint)_session.Clock.Tick, _session.Players, MaxStamina);
            if (_session.Data.HasItems)
            {
                Messages.WriteGroundPiles(w, _session.GroundItems.Piles);
                Messages.WriteInventory(w, _session.Inventories[player]);
                _sentInventory[player] = _session.Inventories[player].Version;
            }
            _joined[player] = true;
        }

        /// <summary>What a joined remote player receives this tick.</summary>
        public void WriteFor(int player, ByteWriter w)
        {
            CheckPlayer(player);
            if (!_joined[player])
            {
                throw new InvalidOperationException($"Player {player} has not joined: call WriteJoin first.");
            }
            long tick = _session.Clock.Tick;
            if (_changes.Count > 0)
            {
                Messages.WriteBlockDeltas(w, _changes);
            }
            Messages.WritePlayerStates(w, (uint)tick, _session.Players, MaxStamina);
            if (tick % ZombieSnapshotEveryTicks == 0)
            {
                Messages.WriteZombieSnapshot(w, _session.Zombies, (uint)tick, _session.Players[player].Position, ZombieInterestRadius, _scratch);
            }
            if (_zombieEvents.Count > 0)
            {
                Messages.WriteZombieEvents(w, _zombieEvents);
            }
            WriteEventsFor(player, w);
            if (_phaseChanged || tick % ClockEveryTicks == 0)
            {
                Messages.WriteClock(w, tick);
            }
            if (_session.Data.HasItems)
            {
                WriteItemsFor(player, w);
            }
        }

        /// <summary>
        /// Bytes from a remote player. Malformed or unexpected messages are ignored
        /// (returns false): a client can never crash or cheat the host with bad data.
        /// </summary>
        public bool Receive(int player, ArraySegment<byte> data)
        {
            if (player < 0 || player >= _session.Players.Count)
            {
                return false;
            }
            var reader = new ByteReader(data);
            bool allOk = true;
            try
            {
                while (reader.Remaining > 0)
                {
                    var type = (MessageType)reader.ReadByte();
                    int length = reader.ReadUShort();
                    ByteReader payload = reader.Slice(length);
                    switch (type)
                    {
                        case MessageType.Input:
                            payload.ReadUInt(); // client tick, for FishNet reconciliation
                            _session.SetInput(player, PlayerInput.Read(payload));
                            break;
                        case MessageType.Intent:
                            PlayerIntent intent = PlayerIntent.Read(payload);
                            if (Enum.IsDefined(typeof(IntentType), intent.Type))
                            {
                                _session.QueueIntent(player, intent);
                            }
                            else
                            {
                                allOk = false;
                            }
                            break;
                        default:
                            allOk = false; // clients only send inputs and intents
                            break;
                    }
                }
            }
            catch (FormatException)
            {
                return false;
            }
            return allOk;
        }

        private float MaxStamina => _session.PlayerSettings.MaxStamina;

        private void WriteEventsFor(int player, ByteWriter w)
        {
            _forPlayer.Clear();
            foreach (SessionEvent e in _events)
            {
                // Intent results only matter to whoever asked; everything else is for everyone.
                if (e.Type != SessionEventType.IntentResolved || e.Player == player)
                {
                    _forPlayer.Add(e);
                }
            }
            if (_forPlayer.Count > 0)
            {
                Messages.WriteSessionEvents(w, _forPlayer);
            }
        }

        private void WriteItemsFor(int player, ByteWriter w)
        {
            Inventory inventory = _session.Inventories[player];
            if (inventory.Version != _sentInventory[player])
            {
                Messages.WriteInventory(w, inventory);
                _sentInventory[player] = inventory.Version;
            }
            Int2 cell = _session.OpenContainerOf(player);
            Inventory contents = _session.OpenContainerContents(player);
            if (cell != _sentContainerCell[player] || (contents != null && contents.Version != _sentContainerVersion[player]))
            {
                Messages.WriteContainer(w, cell, contents);
                _sentContainerCell[player] = cell;
                _sentContainerVersion[player] = contents?.Version ?? -1;
            }
            if (_pilesChanged)
            {
                Messages.WriteGroundPiles(w, _session.GroundItems.Piles);
            }
        }

        private void CheckPlayer(int player)
        {
            if (player < 0 || player >= _session.Players.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(player));
            }
        }
    }
}
