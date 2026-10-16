using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.AI
{
    /// <summary>Zombies know which level they are on and only see, chase and hurt the people on it (D-022 phase G1).</summary>
    public class ZombieLevelsTests
    {
        private const float Dt = 0.05f;
        private ZombieCatalog _catalog;
        private byte _walker;
        private AsciiMap _map;

        [SetUp]
        public void SetUp()
        {
            _catalog = Fixtures.Catalog();
            _walker = _catalog.IndexOf("walker");
            var rows = new string[32];
            for (int r = 0; r < rows.Length; r++)
            {
                rows[r] = new string('.', 32);
            }
            _map = new AsciiMap(BlockRegistry.CreateDefault(Fixtures.Construction()), rows);
        }

        private ZombieSimulation Sim() => new ZombieSimulation(_map.Blocks, _map.Ground, _catalog);

        private static void Run(ZombieSimulation sim, List<Float2> players, List<int> levels, float seconds)
        {
            for (float t = 0; t < seconds; t += Dt)
            {
                sim.Tick(Dt, players, levels);
            }
        }

        [Test]
        public void ZombiesHaveALevelAndCannotSpawnInsideUndugGround()
        {
            ZombieSimulation sim = Sim();
            Assert.That(sim.GetLevel(sim.Spawn(_walker, new Float2(5.5f, 5.5f))), Is.Zero);
            Assert.That(sim.Spawn(_walker, new Float2(6.5f, 5.5f), level: -1), Is.EqualTo(-1), "undug ground is solid");
            _map.Blocks.SetBlock(7, 5, -1, BuildLayer.Floor, _map.Blocks.Registry.Get("cave").Id);
            int z = sim.Spawn(_walker, new Float2(7.5f, 5.5f), level: -1);
            Assert.That(z, Is.GreaterThanOrEqualTo(0));
            Assert.That(sim.GetLevel(z), Is.EqualTo(-1));
            Assert.That(sim.Spawn(_walker, new Float2(7.5f, 5.5f), level: 9), Is.EqualTo(-1), "no such level");
        }

        [Test]
        public void AZombieIgnoresAPlayerOnAnotherLevel()
        {
            ZombieSimulation sim = Sim();
            int z = sim.Spawn(_walker, new Float2(5.5f, 5.5f));
            var players = new List<Float2> { new Float2(6.0f, 5.5f) };            // right next to it, but one level down
            Run(sim, players, new List<int> { -1 }, 3f);
            var hits = new List<PlayerHit>();
            sim.DrainPlayerHits(hits);
            Assert.That(hits, Is.Empty);
            Assert.That(sim.GetPosition(z).X, Is.EqualTo(5.5f).Within(0.5f), "it did not walk towards them");
        }

        [Test]
        public void AZombieOnTheSameLevelStillHurtsAndOthersDoNot()
        {
            ZombieSimulation sim = Sim();
            sim.Spawn(_walker, new Float2(5.5f, 5.5f));
            var players = new List<Float2> { new Float2(5.9f, 5.5f), new Float2(5.9f, 5.5f) };
            Run(sim, players, new List<int> { -1, 0 }, 2f);
            var hits = new List<PlayerHit>();
            sim.DrainPlayerHits(hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits.TrueForAll(h => h.PlayerIndex == 1), Is.True, "only the player on the zombie's level");
        }

        [Test]
        public void TheStreetFieldOnlyChasesPeopleOnTheStreet()
        {
            ZombieSimulation sim = Sim();
            int z = sim.Spawn(_walker, new Float2(5.5f, 5.5f), aware: true);
            var players = new List<Float2> { new Float2(25.5f, 5.5f), new Float2(5.5f, 25.5f) };
            Run(sim, players, new List<int> { 0, -2 }, 6f);
            Float2 p = sim.GetPosition(z);
            Assert.That(p.X, Is.GreaterThan(8f), "towards the player on the street");
            Assert.That(p.Y, Is.EqualTo(5.5f).Within(2f));
        }

        [Test]
        public void ADugOutZombieChasesStraightAtSomeoneOnItsLevel()
        {
            for (int x = 5; x <= 12; x++)
            {
                _map.Blocks.SetBlock(x, 5, -1, BuildLayer.Floor, _map.Blocks.Registry.Get("cave").Id);
            }
            ZombieSimulation sim = Sim();
            int z = sim.Spawn(_walker, new Float2(5.5f, 5.5f), level: -1);
            var players = new List<Float2> { new Float2(12.5f, 5.5f) };
            Run(sim, players, new List<int> { -1 }, 4f);
            Assert.That(sim.GetPosition(z).X, Is.GreaterThan(7f), "down the tunnel");
            Assert.That(sim.GetPosition(z).Y, Is.EqualTo(5.5f).Within(0.6f), "the undug walls hold it");
        }

        [Test]
        public void TheSnapshotCarriesLevelsAndOnlyShowsTheViewersLevel()
        {
            ZombieSimulation sim = Sim();
            sim.Spawn(_walker, new Float2(5.5f, 5.5f));
            _map.Blocks.SetBlock(8, 5, -1, BuildLayer.Floor, _map.Blocks.Registry.Get("cave").Id);
            int below = sim.Spawn(_walker, new Float2(8.5f, 5.5f), level: -1);
            var street = new ByteWriter();
            Assert.That(ZombieSnapshotCodec.Write(sim, 1, new Float2(5, 5), 50f, street), Is.EqualTo(1));
            var underground = new ByteWriter();
            Assert.That(ZombieSnapshotCodec.Write(sim, 1, new Float2(5, 5), 50f, underground, viewerLevel: -1), Is.EqualTo(1));
            var entries = new List<ZombieSnapshotEntry>();
            ZombieSnapshotCodec.Read(new ByteReader(underground.Segment), entries);
            Assert.That(entries[0].Level, Is.EqualTo((sbyte)-1));
            Assert.That(below, Is.GreaterThanOrEqualTo(0));
            Assert.That(underground.Length, Is.EqualTo(ZombieSnapshotCodec.HeaderBytes + ZombieSnapshotCodec.BytesPerZombie));
        }
    }
}
