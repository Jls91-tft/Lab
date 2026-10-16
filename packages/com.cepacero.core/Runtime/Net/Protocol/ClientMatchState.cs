using System;
using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Lore;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Net.Protocol
{
    /// <summary>
    /// What a client knows about the match, rebuilt from host messages only.
    /// It loads the same map and data as the host (the ground never travels),
    /// then mirrors blocks, players, zombies, items and the clock. Unity's view
    /// reads this; the client never simulates zombies or decides anything.
    /// </summary>
    public sealed class ClientMatchState
    {
        private readonly string _mapName;
        private readonly GameData _data;
        private readonly uint _fingerprint;
        private readonly List<PlayerState> _players = new List<PlayerState>();
        private readonly List<ItemPile> _piles = new List<ItemPile>();
        private readonly List<SessionEvent> _events = new List<SessionEvent>();
        private readonly List<ZombieEvent> _zombieEvents = new List<ZombieEvent>();
        private readonly List<BlockChange> _changes = new List<BlockChange>();
        private readonly float _maxStamina;
        private readonly HashSet<string> _unlockedSamples = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<Int2> _containerCells = new HashSet<Int2>();

        public ClientMatchState(string mapName, BlockWorld blocks, GroundLayer ground, GameData data, float maxStamina = 100f)
        {
            _mapName = mapName ?? "";
            Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            Ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _fingerprint = Messages.DataFingerprint(blocks.Registry, data);
            _maxStamina = maxStamina;
            if (data.HasItems)
            {
                MyInventory = Inventory.ForPlayer(data.Items);
            }
            if (data.Diary != null)
            {
                Diary = new TeamDiary(data.Diary);
            }
        }

        /// <summary>The group's diary as the host reports it (null without diary data).</summary>
        public TeamDiary Diary { get; }

        /// <summary>Samples the group has analysed (their recipes are unlocked).</summary>
        public IReadOnlyCollection<string> UnlockedSamples => _unlockedSamples;

        public bool HasJoined { get; private set; }

        public int MyPlayer { get; private set; } = -1;

        public BlockWorld Blocks { get; }

        public GroundLayer Ground { get; }

        public ZombieReplica Zombies { get; } = new ZombieReplica();

        public IReadOnlyList<PlayerState> Players => _players;

        public Inventory MyInventory { get; }

        /// <summary>This player's food bar, 0-100 in whole percents (D-015).</summary>
        public int MyFood { get; private set; } = 100;

        /// <summary>This player's water bar, 0-100 in whole percents.</summary>
        public int MyWater { get; private set; } = 100;

        /// <summary>Cell of the container this player is looking into, or <see cref="GameSession.NoContainer"/>.</summary>
        public Int2 OpenContainer { get; private set; } = GameSession.NoContainer;

        public Inventory OpenContainerContents { get; private set; }

        public IReadOnlyList<ItemPile> Piles => _piles;

        /// <summary>Cells holding a container (map cupboards and player chests).</summary>
        public IReadOnlyCollection<Int2> ContainerCells => _containerCells;

        /// <summary>Clock replica (created on Welcome with the host's settings).</summary>
        public DayNightCycle Clock { get; private set; }

        /// <summary>Last player-state tick received (for FishNet reconciliation of the local player).</summary>
        public uint LastStateTick { get; private set; }

        public void DrainEvents(List<SessionEvent> into)
        {
            into.AddRange(_events);
            _events.Clear();
        }

        public void DrainZombieEvents(List<ZombieEvent> into)
        {
            into.AddRange(_zombieEvents);
            _zombieEvents.Clear();
        }

        /// <summary>
        /// Applies a batch of host messages received at <paramref name="time"/> (client clock, seconds).
        /// Unknown message types are skipped. Throws <see cref="FormatException"/> on corrupt data
        /// or if the host runs a different map, data or protocol.
        /// </summary>
        public void Receive(ArraySegment<byte> data, double time)
        {
            var reader = new ByteReader(data);
            while (reader.Remaining > 0)
            {
                var type = (MessageType)reader.ReadByte();
                int length = reader.ReadUShort();
                ByteReader payload = reader.Slice(length);
                if (!HasJoined && type != MessageType.Welcome)
                {
                    throw new FormatException($"Message {type} before Welcome.");
                }
                switch (type)
                {
                    case MessageType.Welcome:
                        ReadWelcome(payload);
                        break;
                    case MessageType.ChunkSnapshot:
                        Blocks.ReadChunkPlane(payload);
                        break;
                    case MessageType.BlockDeltas:
                        _changes.Clear();
                        BlockChange.ReadBatch(payload, _changes);
                        foreach (BlockChange c in _changes)
                        {
                            Blocks.ApplyChange(c);
                        }
                        break;
                    case MessageType.PlayerStates:
                        ReadPlayers(payload);
                        break;
                    case MessageType.ZombieSnapshot:
                        Zombies.Apply(payload, time);
                        break;
                    case MessageType.ZombieEvents:
                        ReadZombieEvents(payload);
                        break;
                    case MessageType.SessionEvents:
                        ReadSessionEvents(payload);
                        break;
                    case MessageType.Clock:
                        Clock.SetTick(payload.ReadLong());
                        break;
                    case MessageType.Vitals:
                        MyFood = Math.Min(100, (int)payload.ReadByte());
                        MyWater = Math.Min(100, (int)payload.ReadByte());
                        break;
                    case MessageType.Inventory:
                        RequireItems(type);
                        MyInventory.Read(payload);
                        break;
                    case MessageType.ContainerContents:
                        RequireItems(type);
                        ReadContainer(payload);
                        break;
                    case MessageType.GroundPiles:
                        RequireItems(type);
                        ReadPiles(payload);
                        break;
                    case MessageType.TeamProgress:
                        ReadProgress(payload);
                        break;
                    case MessageType.ContainerList:
                        _containerCells.Clear();
                        int count = payload.ReadUShort();
                        for (int i = 0; i < count; i++)
                        {
                            _containerCells.Add(new Int2(payload.ReadUShort(), payload.ReadUShort()));
                        }
                        break;
                    default:
                        break; // newer host, older client: skip what we do not understand
                }
            }
        }

        private void ReadWelcome(ByteReader r)
        {
            ushort version = r.ReadUShort();
            if (version != Messages.ProtocolVersion)
            {
                throw new FormatException($"Host speaks protocol v{version}, this client v{Messages.ProtocolVersion}.");
            }
            string map = r.ReadString();
            int width = r.ReadUShort();
            int height = r.ReadUShort();
            uint fingerprint = r.ReadUInt();
            if (map != _mapName || width != Blocks.Width || height != Blocks.Height)
            {
                throw new FormatException($"Host is on map '{map}' ({width}x{height}), this client loaded '{_mapName}'.");
            }
            if (fingerprint != _fingerprint)
            {
                throw new FormatException("Host and client run different game data (update the game).");
            }
            MyPlayer = r.ReadByte();
            long tick = r.ReadLong();
            var settings = new GameSettings
            {
                TickRate = r.ReadByte(),
                DaySeconds = r.ReadInt(),
                DuskSeconds = r.ReadInt(),
                NightSeconds = r.ReadInt(),
                DawnSeconds = r.ReadInt(),
            };
            Clock = new DayNightCycle(settings);
            Clock.SetTick(tick);
            HasJoined = true;
        }

        private void ReadPlayers(ByteReader r)
        {
            LastStateTick = r.ReadUInt();
            int count = r.ReadByte();
            _players.Clear();
            for (int i = 0; i < count; i++)
            {
                var p = new PlayerState
                {
                    Position = new Float2(r.ReadFloat(), r.ReadFloat()),
                    Level = (sbyte)r.ReadByte(),
                    Health = r.ReadUShort(),
                    Stamina = r.ReadByte() / 255f * _maxStamina,
                };
                byte flags = r.ReadByte();
                p.IsSprinting = (flags & Messages.FlagSprinting) != 0;
                p.IsDead = (flags & Messages.FlagDead) != 0;
                _players.Add(p);
            }
        }

        private void ReadZombieEvents(ByteReader r)
        {
            int count = r.ReadUShort();
            for (int i = 0; i < count; i++)
            {
                var type = (ZombieEventType)r.ReadByte();
                ushort id = r.ReadUShort();
                _zombieEvents.Add(new ZombieEvent(type, id, new Float2(r.ReadFloat(), r.ReadFloat())));
            }
        }

        private void ReadSessionEvents(ByteReader r)
        {
            int count = r.ReadUShort();
            for (int i = 0; i < count; i++)
            {
                var type = (SessionEventType)r.ReadByte();
                int player = unchecked((sbyte)r.ReadByte());
                int value = r.ReadInt();
                var phase = (DayPhase)r.ReadByte();
                var result = (IntentResult)r.ReadByte();
                _events.Add(new SessionEvent(type, player, value, phase, result));
            }
        }

        private void ReadContainer(ByteReader r)
        {
            if (r.ReadByte() == 0)
            {
                OpenContainer = GameSession.NoContainer;
                OpenContainerContents = null;
                return;
            }
            OpenContainer = new Int2(r.ReadUShort(), r.ReadUShort());
            var contents = new Inventory(_data.Items, r.ReadByte());
            contents.Read(r);
            OpenContainerContents = contents;
        }

        private void ReadPiles(ByteReader r)
        {
            _piles.Clear();
            int count = r.ReadUShort();
            for (int i = 0; i < count; i++)
            {
                var pile = new ItemPile { Id = r.ReadUShort(), Position = new Float2(r.ReadFloat(), r.ReadFloat()) };
                int stacks = r.ReadByte();
                for (int k = 0; k < stacks; k++)
                {
                    var s = new ItemStack(r.ReadUShort(), r.ReadUShort(), r.ReadUShort());
                    if (s.Item >= _data.Items.Count)
                    {
                        throw new FormatException("Unknown item in a ground pile.");
                    }
                    pile.Stacks.Add(s);
                }
                _piles.Add(pile);
            }
        }

        private void ReadProgress(ByteReader r)
        {
            int samples = r.ReadUShort();
            for (int i = 0; i < samples; i++)
            {
                _unlockedSamples.Add(r.ReadString());
            }
            int diary = r.ReadUShort();
            for (int i = 0; i < diary; i++)
            {
                Diary?.Unlock(r.ReadString());
            }
        }

        private void RequireItems(MessageType type)
        {
            if (!_data.HasItems)
            {
                throw new FormatException($"Received {type} but this client has no item data.");
            }
        }
    }
}
