using System;
using System.Collections.Generic;
using System.Linq;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Map;
using CepaCero.Net;
using CepaCero.Net.Protocol;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Net
{
    /// <summary>
    /// Host and client talk only through bytes, as they will through FishNet.
    /// The client must end up seeing exactly what the host simulates.
    /// </summary>
    public class ProtocolTests
    {
        private const string MapName = "synthetic";

        private GameData _data;

        [SetUp]
        public void SetUp() => _data = Fixtures.GameData();

        private static GameSettings Fast() => new GameSettings
        {
            TickRate = 20, DaySeconds = 4, DuskSeconds = 1, NightSeconds = 20, DawnSeconds = 1, HordeBase = 10,
        };

        private MapImportResult LoadMap() => MapImporter.Import(Fixtures.LoadMap("synthetic.map.json"), BlockRegistry.CreateDefault());

        private sealed class Link
        {
            public GameSession Host;
            public MapImportResult HostMap;
            public HostBroadcaster Broadcaster;
            public ClientMatchState Client;
            public int Remote;
            public long BytesToClient;
            public long BytesToHost;
            public int Ticks;

            /// <summary>One network tick: client → host, host simulates, host → client.</summary>
            public void Tick(PlayerInput input, params PlayerIntent[] intents)
            {
                var up = new ByteWriter();
                Messages.WriteInput(up, (uint)Ticks, input);
                foreach (PlayerIntent i in intents)
                {
                    Messages.WriteIntent(up, i);
                }
                BytesToHost += up.Length;
                Assert.That(Broadcaster.Receive(Remote, up.Segment), Is.True);

                Host.Step();
                Broadcaster.CollectTick();
                var down = new ByteWriter();
                Broadcaster.WriteFor(Remote, down);
                BytesToClient += down.Length;
                Client.Receive(down.Segment, Ticks * 0.05);
                Ticks++;
            }
        }

        private Link Connect(GameSettings settings = null, Float2? remoteSpawn = null)
        {
            MapImportResult hostMap = LoadMap();
            var host = new GameSession(settings ?? Fast(), new PlayerSettings { MaxHealth = 60000 }, hostMap.Blocks, hostMap.Ground, _data, hostMap.Buildings);
            host.AddPlayer(new Float2(30.5f, 25.5f));       // the host's own player
            int remote = host.AddPlayer(remoteSpawn ?? new Float2(32.5f, 25.5f));
            var broadcaster = new HostBroadcaster(host, MapName);

            MapImportResult clientMap = LoadMap();             // the client loads the same map file
            var client = new ClientMatchState(MapName, clientMap.Blocks, clientMap.Ground, _data);
            var join = new ByteWriter();
            broadcaster.WriteJoin(remote, join);
            client.Receive(join.Segment, 0);
            return new Link { Host = host, HostMap = hostMap, Broadcaster = broadcaster, Client = client, Remote = remote };
        }

        private static void AssertSameBlocks(BlockWorld a, BlockWorld b)
        {
            for (int y = 0; y < a.Height; y++)
            {
                for (int x = 0; x < a.Width; x++)
                {
                    Assert.That(b.GetTypeId(x, y), Is.EqualTo(a.GetTypeId(x, y)), $"block type at ({x},{y})");
                    Assert.That(b.GetHealth(x, y), Is.EqualTo(a.GetHealth(x, y)), $"block health at ({x},{y})");
                }
            }
        }

        [Test]
        public void JoinGivesTheClientTheWholeMatch()
        {
            Link link = Connect();
            Assert.That(link.Client.HasJoined, Is.True);
            Assert.That(link.Client.MyPlayer, Is.EqualTo(1));
            Assert.That(link.Client.Players.Count, Is.EqualTo(2));
            Assert.That(link.Client.Clock.Tick, Is.EqualTo(link.Host.Clock.Tick));
            AssertSameBlocks(link.Host.Blocks, link.Client.Blocks);
            Assert.That(link.Client.ContainerCells, Is.EquivalentTo(link.Host.Containers.Positions), "clients know where containers are");
        }

        [Test]
        public void BuiltChestsShowUpInTheClientsContainerList()
        {
            Link link = Connect();
            link.Host.Inventories[1].Add(_data.Items.IndexOf("wood"), 8);
            Float2 me = link.Host.Players[1].Position;
            var cell = new Int2((int)me.X + 1, (int)me.Y + 1);
            link.Tick(default, PlayerIntent.Place(cell.X, cell.Y, link.Host.Blocks.Registry.Get("chest").Id));
            Assert.That(link.Host.Containers.Has(cell), Is.True);
            Assert.That(link.Client.ContainerCells, Does.Contain(cell));
        }

        [Test]
        public void ClientMirrorsAWholeNightThroughBytesOnly()
        {
            Link link = Connect();
            ushort wood = link.Host.Blocks.Registry.Get("wood_wall").Id;
            link.Host.Inventories[1].Add(_data.Items.IndexOf("wood"), 40);

            // Walk a bit, build two walls, then let the night play out.
            for (int t = 0; t < 20; t++)
            {
                link.Tick(PlayerInput.FromVector(new Float2(0, 1), sprint: false));
            }
            Float2 me = link.Host.Players[1].Position;
            link.Tick(default, PlayerIntent.Place((int)me.X + 1, (int)me.Y, wood));
            for (int t = 0; t < 10; t++)
            {
                link.Tick(default);
            }
            link.Tick(default, PlayerIntent.Place((int)me.X - 1, (int)me.Y, wood));
            while (link.Host.Clock.Phase != DayPhase.Dawn)
            {
                link.Tick(default);
            }
            link.Tick(default);
            link.Tick(default); // one more snapshot (they go every 2 ticks)

            AssertSameBlocks(link.Host.Blocks, link.Client.Blocks);
            for (int p = 0; p < link.Host.Players.Count; p++)
            {
                Assert.That(link.Client.Players[p].Position, Is.EqualTo(link.Host.Players[p].Position), "positions are exact floats");
                Assert.That(link.Client.Players[p].Health, Is.EqualTo(link.Host.Players[p].Health));
            }
            Assert.That(link.Client.Clock.Phase, Is.EqualTo(DayPhase.Dawn));
            Assert.That(link.Client.MyInventory.Count(_data.Items.IndexOf("wood")), Is.EqualTo(link.Host.Inventories[1].Count(_data.Items.IndexOf("wood"))));
            Assert.That(link.Host.Inventories[1].Count(_data.Items.IndexOf("wood")), Is.EqualTo(32), "two walls paid");

            int nearHost = 0;
            for (int i = 0; i < link.Host.Zombies.Capacity; i++)
            {
                if (link.Host.Zombies.IsAlive(i) && (link.Host.Zombies.GetPosition(i) - link.Host.Players[1].Position).Length <= 40f)
                {
                    nearHost++;
                    Assert.That(link.Client.Zombies.TrySample(link.Host.Zombies.GetNetId(i), link.Ticks * 0.05, out var z), Is.True);
                    Assert.That((z.Position - link.Host.Zombies.GetPosition(i)).Length, Is.LessThan(0.5f), "within one tick of motion");
                }
            }
            Assert.That(link.Client.Zombies.Count, Is.EqualTo(nearHost), "exactly the zombies within the interest radius");

            double seconds = link.Ticks / 20.0;
            double downKBs = link.BytesToClient / seconds / 1024.0;
            double upKBs = link.BytesToHost / seconds / 1024.0;
            TestContext.WriteLine($"{link.Ticks} ticks, {nearHost} zombies near: host→client {downKBs:0.00} KB/s, client→host {upKBs:0.00} KB/s");
            Assert.That(downKBs, Is.LessThan(64), "network budget per client (docs/arquitectura.md)");
        }

        [Test]
        public void IntentResultsAndInventoryChangesReachOnlyTheirOwner()
        {
            Link link = Connect();
            link.Tick(default, PlayerIntent.Place(5, 5, link.Host.Blocks.Registry.Get("wood_wall").Id)); // far: out of reach
            var events = new List<SessionEvent>();
            link.Client.DrainEvents(events);
            SessionEvent result = events.Single(e => e.Type == SessionEventType.IntentResolved);
            Assert.That(result.Player, Is.EqualTo(1));
            Assert.That(result.Result, Is.EqualTo(IntentResult.OutOfReach));

            link.Host.QueueIntent(0, PlayerIntent.Melee(new Float2(1, 0))); // the host's own action
            link.Tick(default);
            events.Clear();
            link.Client.DrainEvents(events);
            Assert.That(events.Any(e => e.Type == SessionEventType.IntentResolved), Is.False, "not the client's business");
        }

        [Test]
        public void ContainersAreSeenWhileOpenAndClosedWhenLeaving()
        {
            // Container positions depend only on the map and the seed: find one, then start the remote player there.
            Int2 box = Connect().Host.Containers.Positions.First();
            Link link = Connect(remoteSpawn: new Float2(box.X + 0.5f, box.Y + 0.5f));
            Assert.That(link.Host.Containers.Has(box), Is.True);

            link.Tick(default, PlayerIntent.Open(box.X, box.Y));
            Assert.That(link.Client.OpenContainer, Is.EqualTo(box));
            Assert.That(link.Client.OpenContainerContents.Bag.Count, Is.EqualTo(link.Host.OpenContainerContents(1).Bag.Count));

            if (link.Client.OpenContainerContents.Bag.Count > 0)
            {
                ushort item = link.Client.OpenContainerContents.Bag[0].Item;
                link.Tick(default, PlayerIntent.Take(box.X, box.Y, 0));
                Assert.That(link.Client.MyInventory.Count(item), Is.GreaterThan(0), "the taken item shows up in my inventory");
                Assert.That(link.Client.OpenContainerContents.Bag.Count, Is.EqualTo(link.Host.OpenContainerContents(1).Bag.Count));
            }

            link.Tick(default, PlayerIntent.Open(0, 0)); // nothing there / out of reach
            Assert.That(link.Client.OpenContainer, Is.EqualTo(GameSession.NoContainer));
            Assert.That(link.Client.OpenContainerContents, Is.Null);
        }

        [Test]
        public void GroundPilesAreReplicated()
        {
            Link link = Connect();
            link.Host.Inventories[1].Add(_data.Items.IndexOf("nails"), 9);
            link.Tick(default);
            link.Tick(default, PlayerIntent.Drop(0));
            Assert.That(link.Client.Piles.Count, Is.EqualTo(1));
            Assert.That(link.Client.Piles[0].Stacks[0].Count, Is.EqualTo(9));
            Assert.That(link.Client.MyInventory.Count(_data.Items.IndexOf("nails")), Is.EqualTo(0));
        }

        [Test]
        public void ALateJoinerSeesTheModifiedWorld()
        {
            Link link = Connect();
            Int2 door = link.HostMap.Buildings[0].Doors[0];
            link.Host.Blocks.Damage(door.X, door.Y, 50);
            link.Host.Blocks.SetBlock(1, 1, link.Host.Blocks.Registry.Get("metal_wall").Id);
            link.Tick(default);

            MapImportResult freshMap = LoadMap();
            var late = new ClientMatchState(MapName, freshMap.Blocks, freshMap.Ground, _data);
            var join = new ByteWriter();
            link.Broadcaster.WriteJoin(1, join);
            late.Receive(join.Segment, 0);
            AssertSameBlocks(link.Host.Blocks, late.Blocks);
            TestContext.WriteLine($"join payload for a 65x60 map: {join.Length} bytes");
        }

        [Test]
        public void MismatchedMapsOrDataAreRefused()
        {
            Link link = Connect();
            var join = new ByteWriter();
            link.Broadcaster.WriteJoin(1, join);

            MapImportResult map = LoadMap();
            var wrongMap = new ClientMatchState("other-map", map.Blocks, map.Ground, _data);
            Assert.Throws<FormatException>(() => wrongMap.Receive(join.Segment, 0));

            var registry = BlockRegistry.CreateDefault();
            registry.Add(new BlockType((ushort)registry.Count, "laser_fence", 10, BlockMaterial.Metal, true));
            var otherData = new ClientMatchState(MapName, new BlockWorld(map.Blocks.Width, map.Blocks.Height, registry), map.Ground, _data);
            Assert.Throws<FormatException>(() => otherData.Receive(join.Segment, 0), "different game data");
        }

        [Test]
        public void TheHostIgnoresGarbageFromClients()
        {
            Link link = Connect();
            var rng = new DeterministicRandom(99);
            for (int k = 0; k < 200; k++)
            {
                var bytes = new byte[rng.Range(1, 40)];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = (byte)rng.Range(0, 256);
                }
                link.Broadcaster.Receive(1, new ArraySegment<byte>(bytes)); // must never throw
            }
            var forged = new ByteWriter();
            Messages.WriteClock(forged, 999999); // a client pretending to be the host
            Assert.That(link.Broadcaster.Receive(1, forged.Segment), Is.False);
            Assert.That(link.Broadcaster.Receive(7, forged.Segment), Is.False, "unknown player");
            link.Tick(default);
            Assert.That(link.Host.Clock.Tick, Is.LessThan(1000));
        }

        [Test]
        public void ClientsSkipMessageTypesTheyDoNotKnow()
        {
            Link link = Connect();
            var w = new ByteWriter();
            w.WriteByte(200); // a future message type
            w.WriteUShort(3);
            w.WriteByte(1);
            w.WriteByte(2);
            w.WriteByte(3);
            Messages.WriteClock(w, 1234);
            link.Client.Receive(w.Segment, 1);
            Assert.That(link.Client.Clock.Tick, Is.EqualTo(1234));
        }
    }
}
