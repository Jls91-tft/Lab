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

        // ------------------------------------------------------------------ between levels (phase G2)
        private ushort Id(string key) => _map.Blocks.Registry.Get(key).Id;

        private void Cave(int x0, int x1, int y, int level)
        {
            for (int x = x0; x <= x1; x++)
            {
                _map.Blocks.SetBlock(x, y, level, BuildLayer.Floor, Id("cave"));
            }
        }

        private void FloorRun(int x0, int x1, int y, int level)
        {
            for (int x = x0; x <= x1; x++)
            {
                _map.Blocks.SetBlock(x, y, level, BuildLayer.Floor, Id("floor_wood"));
            }
        }

        private int RunUntil(ZombieSimulation sim, List<Float2> players, List<int> levels, System.Func<bool> done, float maxSeconds = 40f)
        {
            int ticks = 0;
            for (float t = 0; t < maxSeconds && !done(); t += Dt, ticks++)
            {
                sim.Tick(Dt, players, levels);
            }
            return ticks;
        }

        [Test]
        public void AZombieFallsThroughAPitAndHurtsItself()
        {
            _map.Blocks.SetBlock(10, 5, 0, BuildLayer.Structure, Id("pit"));
            Cave(10, 16, 5, -1);
            ZombieSimulation sim = Sim();
            int z = sim.Spawn(_walker, new Float2(5.5f, 5.5f));
            int full = sim.GetHealth(z);
            var players = new List<Float2> { new Float2(15.5f, 5.5f) };
            var levels = new List<int> { -1 };
            RunUntil(sim, players, levels, () => sim.GetLevel(z) == -1);
            Assert.That(sim.GetLevel(z), Is.EqualTo(-1));
            Assert.That(sim.GetHealth(z), Is.LessThan(full), "falling hurts");
            RunUntil(sim, players, levels, () => sim.GetPosition(z).X > 12.5f);
            Assert.That(sim.GetPosition(z).X, Is.GreaterThan(12.5f), "and it carries on down the tunnel");
        }

        [Test]
        public void ZombiesCannotClimbOutOfAPit()
        {
            _map.Blocks.SetBlock(10, 5, 0, BuildLayer.Structure, Id("pit"));
            Cave(10, 16, 5, -1);
            ZombieSimulation sim = Sim();
            int z = sim.Spawn(_walker, new Float2(14.5f, 5.5f), level: -1);
            var players = new List<Float2> { new Float2(3.5f, 5.5f) };
            var levels = new List<int> { 0 };
            Run(sim, players, levels, 15f);
            Assert.That(sim.GetLevel(z), Is.EqualTo(-1), "nothing to climb");
            Assert.That(sim.GetPosition(z).X, Is.LessThan(17f));
        }

        [Test]
        public void ZombiesUseStairsInBothDirections()
        {
            _map.Blocks.SetBlock(10, 5, 0, BuildLayer.Structure, Id("stairs_wood"));
            FloorRun(11, 16, 5, 1);
            ZombieSimulation sim = Sim();
            int up = sim.Spawn(_walker, new Float2(5.5f, 5.5f));
            var players = new List<Float2> { new Float2(15.5f, 5.5f) };
            RunUntil(sim, players, new List<int> { 1 }, () => sim.GetLevel(up) == 1);
            Assert.That(sim.GetLevel(up), Is.EqualTo(1), "up the stairs after the player");
            Assert.That(sim.GetHealth(up), Is.EqualTo(_catalog[_walker].HealthFor(1)), "stairs do not hurt");

            ZombieSimulation sim2 = Sim();
            int down = sim2.Spawn(_walker, new Float2(14.5f, 5.5f), level: 1);
            var onStreet = new List<Float2> { new Float2(3.5f, 5.5f) };
            RunUntil(sim2, onStreet, new List<int> { 0 }, () => sim2.GetLevel(down) == 0);
            Assert.That(sim2.GetLevel(down), Is.Zero, "and down again");
        }

        private int TicksToFallThrough(string hatch)
        {
            var rows = new string[32];
            for (int r = 0; r < rows.Length; r++)
            {
                rows[r] = new string('.', 32);
            }
            _map = new AsciiMap(BlockRegistry.CreateDefault(Fixtures.Construction()), rows);
            _map.Blocks.SetBlock(10, 5, 0, BuildLayer.Floor, Id(hatch));
            Cave(10, 16, 5, -1);
            ZombieSimulation sim = Sim();
            int z = sim.Spawn(_walker, new Float2(5.5f, 5.5f));
            return RunUntil(sim, new List<Float2> { new Float2(15.5f, 5.5f) }, new List<int> { -1 }, () => sim.GetLevel(z) == -1, 120f);
        }

        [Test]
        public void AClosedTrapdoorIsBashedOpenAndTheMaterialSetsHowLongThatTakes()
        {
            int wood = TicksToFallThrough("hatch_wood");
            Assert.That(wood, Is.LessThan(2400), "a wooden trapdoor gives way");
            int steel = TicksToFallThrough("hatch_steel");
            Assert.That(steel, Is.GreaterThan(wood * 2), "steel holds much longer (or for ever)");
        }

        [Test]
        public void AShutTrapdoorLeavesAnOpenOneBehindIt()
        {
            _map.Blocks.SetBlock(10, 5, 0, BuildLayer.Floor, Id("hatch_wood"));
            Cave(10, 16, 5, -1);
            ZombieSimulation sim = Sim();
            int z = sim.Spawn(_walker, new Float2(5.5f, 5.5f));
            RunUntil(sim, new List<Float2> { new Float2(15.5f, 5.5f) }, new List<int> { -1 }, () => sim.GetLevel(z) == -1, 120f);
            Assert.That(_map.Blocks.GetBlock(10, 5, 0, BuildLayer.Floor).Key, Is.EqualTo("hatch_wood_open"));
        }

        [Test]
        public void AZombieUpstairsFallsWhenItsFloorIsTakenAway()
        {
            FloorRun(10, 12, 5, 1);
            ZombieSimulation sim = Sim();
            int z = sim.Spawn(_walker, new Float2(11.5f, 5.5f), aware: false, level: 1);
            int full = sim.GetHealth(z);
            Assert.That(z, Is.GreaterThanOrEqualTo(0));
            sim.Tick(Dt, new List<Float2>(), new List<int>());
            Assert.That(sim.GetLevel(z), Is.EqualTo(1), "held up by the floor");
            _map.Blocks.SetBlock(11, 5, 1, BuildLayer.Floor, BlockRegistry.AirId);
            sim.Tick(Dt, new List<Float2>(), new List<int>());
            Assert.That(sim.GetLevel(z), Is.Zero);
            Assert.That(sim.GetHealth(z), Is.LessThan(full));
        }

        [Test]
        public void AnOpenTrapdoorIsAFall()
        {
            _map.Blocks.SetBlock(10, 5, 0, BuildLayer.Floor, Id("hatch_wood_open"));
            Cave(10, 16, 5, -1);
            ZombieSimulation sim = Sim();
            int z = sim.Spawn(_walker, new Float2(5.5f, 5.5f));
            var levels = new List<int> { -1 };
            RunUntil(sim, new List<Float2> { new Float2(15.5f, 5.5f) }, levels, () => sim.GetLevel(z) == -1);
            Assert.That(sim.GetLevel(z), Is.EqualTo(-1));
        }

        [Test]
        public void ThePlayersLevelDecidesWhichFieldAZombieFollows()
        {
            // two people: one on the street far to the east, one in a cave with a pit to the west; the zombie in the middle goes for the nearer way
            _map.Blocks.SetBlock(8, 5, 0, BuildLayer.Structure, Id("pit"));
            Cave(8, 10, 5, -1);
            ZombieSimulation sim = Sim();
            int z = sim.Spawn(_walker, new Float2(6.5f, 5.5f));
            var players = new List<Float2> { new Float2(9.5f, 5.5f), new Float2(30.5f, 5.5f) };
            var levels = new List<int> { -1, 0 };
            RunUntil(sim, players, levels, () => sim.GetLevel(z) == -1);
            Assert.That(sim.GetLevel(z), Is.EqualTo(-1), "the person below is closer");
        }

        // ------------------------------------------------------------------ noise (phase G3)
        private bool Alerted(ZombieSimulation sim, int z) => sim.GetState(z) != ZombieState.Unaware && sim.GetState(z) != ZombieState.Wandering;

        [Test]
        public void ANoiseWakesZombiesOnItsOwnLevelAndMuffledOnTheNextOneOnly()
        {
            Cave(5, 9, 5, -1);
            Cave(5, 9, 5, -2);
            _map.Blocks.SetBlock(5, 5, -1, BuildLayer.Structure, Id("pit"));
            ZombieSimulation sim = Sim();
            float hearing = _catalog[_walker].detect_hearing;
            float radius = 6f;
            float muffled = radius * sim.AdjacentLevelNoise + hearing;
            Assert.That(sim.AdjacentLevelNoise, Is.GreaterThan(0.5f).And.LessThan(1f));

            int same = sim.Spawn(_walker, new Float2(5.5f, 5.5f + radius * 0.9f), aware: false);
            int near = sim.Spawn(_walker, new Float2(5.5f, 5.5f), aware: false, level: -1);
            int far = sim.Spawn(_walker, new Float2(8.5f, 5.5f), aware: false, level: -2);
            int grounded = sim.Spawn(_walker, new Float2(25.5f, 25.5f), aware: false);
            Assert.That(near, Is.GreaterThanOrEqualTo(0));
            Assert.That(far, Is.GreaterThanOrEqualTo(0));
            _ = muffled;

            // a noise on the street at (5.5, 5.5): the street one hears it, the one below hears it muffled, the one two levels down does not
            sim.EmitNoise(new Float2(5.5f, 5.5f), radius, 0);
            sim.Tick(Dt, new List<Float2>(), new List<int>());
            Assert.That(Alerted(sim, same), Is.True);
            Assert.That(Alerted(sim, near), Is.True, "right underneath");
            Assert.That(Alerted(sim, far), Is.False, "two levels apart");
            Assert.That(Alerted(sim, grounded), Is.False, "too far away");
        }

        [Test]
        public void MuffledNoiseReachesLessFarThanTheSameNoiseOnYourLevel()
        {
            Cave(5, 30, 5, -1);
            ZombieSimulation sim = Sim();
            float hearing = _catalog[_walker].detect_hearing;
            float distance = (6f + hearing + 6f * 0.7f + hearing) / 2f;       // inside the full radius, outside the muffled one
            int onSameLevel = sim.Spawn(_walker, new Float2(5.5f + distance, 25.5f), aware: false);
            int below = sim.Spawn(_walker, new Float2(5.5f + distance, 5.5f), aware: false, level: -1);
            sim.EmitNoise(new Float2(5.5f, 25.5f), 6f, 0);
            sim.EmitNoise(new Float2(5.5f, 5.5f), 6f, 0);
            sim.Tick(Dt, new List<Float2>(), new List<int>());
            Assert.That(Alerted(sim, onSameLevel), Is.True);
            Assert.That(Alerted(sim, below), Is.False, "the floor swallows part of it");
        }
    }
}
