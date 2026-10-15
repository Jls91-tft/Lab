using System.Collections.Generic;
using System.Linq;
using CepaCero.AI;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using NUnit.Framework;

namespace CepaCero.Tests.AI
{
    /// <summary>Awareness, noise and the abilities of the specials (docs/bestiario.md).</summary>
    public class ZombieSpecialsTests
    {
        private const float Dt = 0.05f;
        private ZombieCatalog _catalog;

        [SetUp]
        public void SetUp() => _catalog = Fixtures.Catalog();

        private static AsciiMap Open(int w, int h)
        {
            var rows = new string[h];
            for (int r = 0; r < h; r++)
            {
                rows[r] = new string('.', w);
            }
            return new AsciiMap(rows);
        }

        private static void Run(ZombieSimulation sim, List<Float2> players, float seconds)
        {
            for (float t = 0; t < seconds; t += Dt)
            {
                sim.Tick(Dt, players);
            }
        }

        private static List<ZombieEvent> Events(ZombieSimulation sim)
        {
            var list = new List<ZombieEvent>();
            sim.DrainEvents(list);
            return list;
        }

        private static int TotalDamage(ZombieSimulation sim)
        {
            var hits = new List<PlayerHit>();
            sim.DrainPlayerHits(hits);
            return hits.Sum(h => h.Damage);
        }

        // ------------------------------------------------------------ awareness

        [Test]
        public void UnawareZombiesWaitUntilTheySeeSomeone()
        {
            AsciiMap map = Open(40, 3);
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            int slot = sim.Spawn(_catalog.IndexOf("walker"), new Float2(1.5f, 1.5f), aware: false);
            var player = new List<Float2> { new Float2(20.5f, 1.5f) }; // 19 tiles: beyond sight (8)
            Run(sim, player, 2f);
            Assert.That(sim.IsAware(slot), Is.False);
            Assert.That(sim.GetPosition(slot).X, Is.LessThan(3f), "it shuffles around but does not come for you");
            Assert.That(sim.GetState(slot), Is.AnyOf(ZombieState.Unaware, ZombieState.Wandering));

            player[0] = new Float2(8.5f, 1.5f); // 7 tiles: seen in daylight
            sim.Tick(Dt, player);
            Assert.That(sim.IsAware(slot), Is.True);
            Assert.That(Events(sim).Any(e => e.Type == ZombieEventType.Noticed));
        }

        [Test]
        public void UnawareZombiesShuffleAroundSlowly()
        {
            AsciiMap map = Open(40, 40);
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            var starts = new List<Float2>();
            for (int k = 0; k < 20; k++)
            {
                var p = new Float2(5.5f + (k % 5) * 6, 5.5f + (k / 5) * 6);
                sim.Spawn(_catalog.IndexOf("walker"), p, aware: false);
                starts.Add(p);
            }
            var states = new HashSet<ZombieState>();
            for (int t = 0; t < 20 * 20; t++)
            {
                sim.Tick(Dt, new List<Float2>());
                for (int i = 0; i < 20; i++)
                {
                    states.Add(sim.GetState(i));
                }
            }
            Assert.That(states, Is.EquivalentTo(new[] { ZombieState.Unaware, ZombieState.Wandering }));
            int moved = 0;
            for (int i = 0; i < 20; i++)
            {
                float d = (sim.GetPosition(i) - starts[i]).Length;
                Assert.That(d, Is.LessThan(1.2f * 0.3f * 20f + 0.01f), "never faster than 30 % of its speed");
                moved += d > 0.5f ? 1 : 0;
                Assert.That(sim.IsAware(i), Is.False);
            }
            Assert.That(moved, Is.GreaterThan(10), "most of them wandered somewhere in 20 s");
        }

        [Test]
        public void SightIsHalvedAtNight()
        {
            AsciiMap map = Open(40, 3);
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog) { SightMultiplier = 0.5f };
            int slot = sim.Spawn(_catalog.IndexOf("walker"), new Float2(1.5f, 1.5f), aware: false);
            Run(sim, new List<Float2> { new Float2(7.5f, 1.5f) }, 1f); // 6 tiles > 8 × 0.5
            Assert.That(sim.IsAware(slot), Is.False);
        }

        [Test]
        public void WallsBlockSight()
        {
            var map = new AsciiMap(
                "....#.....",
                "....#.....",
                "....#.....");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            int slot = sim.Spawn(_catalog.IndexOf("walker"), new Float2(0.5f, 1.5f), aware: false);
            var player = new List<Float2> { new Float2(6.5f, 1.5f) }; // 6 tiles (< 8) but behind the wall
            Run(sim, player, 1f);
            Assert.That(sim.IsAware(slot), Is.False);
        }

        [Test]
        public void NoiseAddsToHearing()
        {
            AsciiMap map = Open(40, 3);
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            int slot = sim.Spawn(_catalog.IndexOf("walker"), new Float2(1.5f, 1.5f), aware: false);
            var player = new List<Float2> { new Float2(16.5f, 1.5f) }; // 15 tiles; walker hears 12
            sim.EmitNoise(player[0], 2f);                             // 2 + 12 < 15
            sim.Tick(Dt, player);
            Assert.That(sim.IsAware(slot), Is.False);
            sim.EmitNoise(player[0], 4f);                             // sprinting: 4 + 12 ≥ 15
            sim.Tick(Dt, player);
            Assert.That(sim.IsAware(slot), Is.True);
        }

        [Test]
        public void HittingAZombieWakesItUp()
        {
            AsciiMap map = Open(10, 3);
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            int slot = sim.Spawn(_catalog.IndexOf("walker"), new Float2(1.5f, 1.5f), aware: false);
            sim.Damage(slot, 5, "blunt");
            Assert.That(sim.IsAware(slot), Is.True);
        }

        // ------------------------------------------------------------- Screamer

        [Test]
        public void ScreamerScreamsWakingTheAreaAndSpeedingUpNeighbours()
        {
            AsciiMap map = Open(80, 5);
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            sim.Spawn(_catalog.IndexOf("screamer"), new Float2(40.5f, 2.5f));
            int far = sim.Spawn(_catalog.IndexOf("walker"), new Float2(5.5f, 2.5f), aware: false);   // 35 tiles away
            int near = sim.Spawn(_catalog.IndexOf("walker"), new Float2(38.5f, 0.5f));
            var player = new List<Float2> { new Float2(50.5f, 2.5f) };

            Run(sim, player, 1.0f);
            Assert.That(Events(sim).Any(e => e.Type == ZombieEventType.Scream), Is.False, "1.5 s wind-up");
            Run(sim, player, 0.7f);
            Assert.That(Events(sim).Any(e => e.Type == ZombieEventType.Scream), Is.True);
            sim.Tick(Dt, player);
            Assert.That(sim.IsAware(far), Is.True, "the scream is heard 40 tiles away (+ hearing)");

            float before = sim.GetPosition(near).X;
            Run(sim, player, 1f);
            float speed = sim.GetPosition(near).X - before;
            Assert.That(speed, Is.GreaterThan(1.2f * 1.15f), "walker sped up ×1.3 by the scream");
        }

        [Test]
        public void HittingTheScreamerDuringTheWindupInterruptsIt()
        {
            AsciiMap map = Open(40, 3);
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            int screamer = sim.Spawn(_catalog.IndexOf("screamer"), new Float2(10.5f, 1.5f));
            var player = new List<Float2> { new Float2(19.5f, 1.5f) };
            Run(sim, player, 0.5f);
            Assert.That(sim.GetState(screamer), Is.EqualTo(ZombieState.Screaming));
            sim.Damage(screamer, 10, "ballistic");
            Run(sim, player, 2.0f);
            List<ZombieEvent> events = Events(sim);
            Assert.That(events.Any(e => e.Type == ZombieEventType.ScreamInterrupted), Is.True);
            Assert.That(events.Any(e => e.Type == ZombieEventType.Scream), Is.False, "3 s penalty before trying again");
        }

        [Test]
        public void ScreamerKeepsItsDistance()
        {
            AsciiMap map = Open(40, 3);
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            int screamer = sim.Spawn(_catalog.IndexOf("screamer"), new Float2(20.5f, 1.5f));
            var player = new List<Float2> { new Float2(25.5f, 1.5f) };
            Run(sim, player, 2.0f); // scream first (it is ready), then back off
            player[0] = new Float2(sim.GetPosition(screamer).X + 3f, 1.5f);
            float start = (player[0] - sim.GetPosition(screamer)).Length;
            Run(sim, player, 1.0f);
            Assert.That((player[0] - sim.GetPosition(screamer)).Length, Is.GreaterThan(start + 1f));
        }

        // -------------------------------------------------------------- Spitter

        [Test]
        public void SpitterHitsFromRangeAndLeavesAnAcidPool()
        {
            AsciiMap map = Open(40, 3);
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            sim.Spawn(_catalog.IndexOf("spitter"), new Float2(10.5f, 1.5f));
            var player = new List<Float2> { new Float2(16.5f, 1.5f) }; // 6 tiles: in range (7), beyond keep distance
            Run(sim, player, 1.0f);
            List<ZombieEvent> events = Events(sim);
            Assert.That(events.Any(e => e.Type == ZombieEventType.Spit), Is.True);
            Assert.That(events.Any(e => e.Type == ZombieEventType.AcidSplash), Is.True, "6 tiles at 8 tiles/s");
            int direct = TotalDamage(sim);
            Assert.That(direct, Is.GreaterThanOrEqualTo(15));
            Assert.That(sim.ActiveAcidPools, Is.GreaterThanOrEqualTo(1));

            player[0] = new Float2(30.5f, 1.5f); // walk out of range: only the pool remains, then it dries
            Run(sim, player, 6f);
            Assert.That(sim.ActiveAcidPools, Is.EqualTo(0));
        }

        [Test]
        public void StandingInAcidHurtsOverTime()
        {
            AsciiMap map = Open(40, 3);
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            int spitter = sim.Spawn(_catalog.IndexOf("spitter"), new Float2(10.5f, 1.5f));
            var player = new List<Float2> { new Float2(16.5f, 1.5f) };
            Run(sim, player, 1.0f);
            sim.Damage(spitter, 1000, "blunt"); // kill it: only its pool keeps hurting
            TotalDamage(sim);
            Run(sim, player, 3.0f);
            Assert.That(TotalDamage(sim), Is.InRange(15, 20), "6 dps for ~3 s");
        }

        // -------------------------------------------------------------- La Mole

        [Test]
        public void BruteChargesThroughWoodAndIsStunnedByMetal()
        {
            var map = new AsciiMap(
                "..........#.....M.....",
                "..........#.....M.....",
                "..........#.....M.....",
                "..........#.....M...P.",
                "..........#.....M.....",
                "..........#.....M.....");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog) { FlowMaxDistance = 100000 };
            int brute = sim.Spawn(_catalog.IndexOf("brute"), new Float2(7.5f, 2.5f));
            var player = new List<Float2> { new Float2(map.Targets[0].X + 0.5f, map.Targets[0].Y + 0.5f) };

            var events = new List<ZombieEvent>();
            for (int t = 0; t < 400 && !events.Any(e => e.Type == ZombieEventType.Stunned); t++)
            {
                sim.Tick(Dt, player);
                sim.DrainEvents(events);
            }
            Assert.That(events.Any(e => e.Type == ZombieEventType.ChargeStart), Is.True);
            int woodLeft = 0;
            int metalLeft = 0;
            for (int y = 0; y < map.Height; y++)
            {
                woodLeft += map.Blocks.GetBlock(10, y).Key == "wood_wall" ? 1 : 0;
                metalLeft += map.Blocks.GetBlock(16, y).Key == "metal_wall" ? 1 : 0;
            }
            Assert.That(woodLeft, Is.LessThan(6), "the charge smashed through the wooden wall");
            Assert.That(metalLeft, Is.EqualTo(6), "metal stops it");
            Assert.That(events.Any(e => e.Type == ZombieEventType.Stunned), Is.True);
            Assert.That(sim.GetState(brute), Is.EqualTo(ZombieState.Stunned));

            int health = sim.GetHealth(brute);
            sim.Damage(brute, 100, "sharp");
            Assert.That(health - sim.GetHealth(brute), Is.EqualTo(200), "stunned: double damage");
        }

        [Test]
        public void BruteSlamHitsEveryoneAround()
        {
            AsciiMap map = Open(20, 9);
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            sim.Spawn(_catalog.IndexOf("brute"), new Float2(10.5f, 4.5f));
            var players = new List<Float2> { new Float2(12.0f, 4.5f), new Float2(9.0f, 5.5f) };
            sim.Tick(Dt, players);
            var hits = new List<PlayerHit>();
            sim.DrainPlayerHits(hits);
            Assert.That(hits.Select(h => h.PlayerIndex).OrderBy(p => p), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(hits.All(h => h.Damage == 25), Is.True);
            Assert.That(Events(sim).Any(e => e.Type == ZombieEventType.Slam), Is.True);
        }
    }
}
