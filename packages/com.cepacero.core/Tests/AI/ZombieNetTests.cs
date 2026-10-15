using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using NUnit.Framework;

namespace CepaCero.Tests.AI
{
    public class ZombieNetTests
    {
        private ZombieCatalog _catalog;

        [SetUp]
        public void SetUp() => _catalog = Fixtures.Catalog();

        private ZombieSimulation SimWithZombies(int count, out AsciiMap map)
        {
            var rows = new string[64];
            for (int r = 0; r < rows.Length; r++)
            {
                rows[r] = new string('.', 64);
            }
            map = new AsciiMap(rows);
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            byte walker = _catalog.IndexOf("walker");
            for (int k = 0; k < count; k++)
            {
                sim.Spawn(walker, new Float2(1.3f + (k % 60), 1.7f + (k / 60) * 2f));
            }
            return sim;
        }

        [Test]
        public void SnapshotRoundTripsWithinQuantization()
        {
            ZombieSimulation sim = SimWithZombies(100, out _);
            var writer = new ByteWriter();
            int sent = ZombieSnapshotCodec.Write(sim, 1234, new Float2(32, 32), 100f, writer);
            Assert.That(sent, Is.EqualTo(100));
            Assert.That(writer.Length, Is.EqualTo(ZombieSnapshotCodec.HeaderBytes + 100 * ZombieSnapshotCodec.BytesPerZombie));

            var entries = new List<ZombieSnapshotEntry>();
            uint tick = ZombieSnapshotCodec.Read(new ByteReader(writer.ToArray()), entries);
            Assert.That(tick, Is.EqualTo(1234));
            var bySlotId = new Dictionary<ushort, int>();
            for (int i = 0; i < sim.Capacity; i++)
            {
                if (sim.IsAlive(i))
                {
                    bySlotId[sim.GetNetId(i)] = i;
                }
            }
            foreach (ZombieSnapshotEntry e in entries)
            {
                int slot = bySlotId[e.NetId];
                Assert.That((e.Position - sim.GetPosition(slot)).Length, Is.LessThanOrEqualTo(0.5f / ZombieSnapshotCodec.PositionScale * 1.5f));
                Assert.That(e.Type, Is.EqualTo(sim.GetTypeIndex(slot)));
                Assert.That(e.State, Is.EqualTo(sim.GetState(slot)));
            }
        }

        [Test]
        public void OnlyNearbyZombiesAreSent()
        {
            ZombieSimulation sim = SimWithZombies(100, out _);
            var writer = new ByteWriter();
            int sent = ZombieSnapshotCodec.Write(sim, 1, new Float2(1, 1), 10f, writer);
            Assert.That(sent, Is.InRange(1, 20));
            writer.Clear();
            Assert.That(ZombieSnapshotCodec.Write(sim, 1, new Float2(1, 1), 1000f, writer, maxZombies: 30), Is.EqualTo(30));
        }

        [Test]
        public void ReplicaInterpolatesAndDespawnsMissingZombies()
        {
            var replica = new ZombieReplica();
            var a = new ZombieSnapshotEntry { NetId = 7, Position = new Float2(0, 0) };
            var b = new ZombieSnapshotEntry { NetId = 8, Position = new Float2(5, 5) };
            replica.Apply(new[] { a, b }, 0.0);
            a.Position = new Float2(1, 0);
            replica.Apply(new[] { a }, 0.1);

            Assert.That(replica.Count, Is.EqualTo(1), "zombie 8 left the snapshot");
            Assert.That(replica.TrySample(7, 0.05, out ZombieSnapshotEntry mid), Is.True);
            Assert.That(mid.Position.X, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(replica.TrySample(7, 1.0, out ZombieSnapshotEntry late), Is.True);
            Assert.That(late.Position.X, Is.EqualTo(1f), "no extrapolation past the newest snapshot");
            Assert.That(replica.TrySample(8, 0.1, out _), Is.False);
        }

        [Test]
        public void HostToClientLoopworksThroughBytes()
        {
            ZombieSimulation sim = SimWithZombies(50, out AsciiMap map);
            var players = new List<Float2> { new Float2(60.5f, 60.5f) };
            var replica = new ZombieReplica();
            var writer = new ByteWriter();
            for (int tick = 0; tick < 40; tick++)
            {
                sim.Tick(0.05f, players);
                if (tick % 2 == 0)
                {
                    writer.Clear();
                    ZombieSnapshotCodec.Write(sim, (uint)tick, players[0], 200f, writer);
                    replica.Apply(new ByteReader(writer.ToArray()), tick * 0.05);
                }
            }
            Assert.That(replica.Count, Is.EqualTo(sim.Count));
            Assert.That(map.Width, Is.EqualTo(64));
        }

        [Test]
        public void SpawnFinderPicksReachableOutdoorCellsOnTheRing()
        {
            var rows = new string[60];
            for (int r = 0; r < rows.Length; r++)
            {
                rows[r] = new string('.', 60);
            }
            var map = new AsciiMap(rows);
            var players = new List<Float2> { new Float2(30, 30) };
            var field = new FlowField(map.Width, map.Height);
            field.Build(map.Ground, map.Blocks, PathPolicy.Zombie, new List<Int2> { new Int2(30, 30) });
            var rng = new DeterministicRandom(3);
            for (int k = 0; k < 50; k++)
            {
                Assert.That(SpawnFinder.TryFind(map.Ground, map.Blocks, field, players, rng, 15f, 25f, out Float2 p), Is.True);
                float d = (p - players[0]).Length;
                Assert.That(d, Is.InRange(14f, 26f));
            }
        }
    }
}
