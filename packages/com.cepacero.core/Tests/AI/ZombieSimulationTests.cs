using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.AI
{
    public class ZombieSimulationTests
    {
        private const float Dt = 0.05f; // 20 Hz

        private ZombieCatalog _catalog;
        private byte _walker;
        private byte _runner;

        [SetUp]
        public void SetUp()
        {
            _catalog = Fixtures.Catalog();
            _walker = _catalog.IndexOf("walker");
            _runner = _catalog.IndexOf("runner");
        }

        private static Float2 Center(Int2 t) => new Float2(t.X + 0.5f, t.Y + 0.5f);

        private static List<Float2> PlayersOf(AsciiMap map)
        {
            var list = new List<Float2>();
            foreach (Int2 t in map.Targets)
            {
                list.Add(Center(t));
            }
            return list;
        }

        private static void Run(ZombieSimulation sim, List<Float2> players, float seconds)
        {
            for (float t = 0; t < seconds; t += Dt)
            {
                sim.Tick(Dt, players);
            }
        }

        [Test]
        public void WalkerReachesAndHitsThePlayer()
        {
            var map = new AsciiMap(
                "Z...........",
                "............",
                "...........P");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            int slot = sim.Spawn(_walker, Center(map.Markers[0]));
            List<Float2> players = PlayersOf(map);

            Run(sim, players, 15f);

            Assert.That(sim.GetState(slot), Is.EqualTo(ZombieState.AttackingPlayer));
            var hits = new List<PlayerHit>();
            sim.DrainPlayerHits(hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].Damage, Is.EqualTo(10));
            Assert.That(hits[0].PlayerIndex, Is.EqualTo(0));
        }

        [Test]
        public void HitsRespectTheAttackInterval()
        {
            var map = new AsciiMap("ZP");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            sim.Spawn(_walker, Center(map.Markers[0]));
            Run(sim, PlayersOf(map), 3.0f);
            var hits = new List<PlayerHit>();
            sim.DrainPlayerHits(hits);
            Assert.That(hits.Count, Is.EqualTo(2), "walker attacks every 1.5 s: at 0 s and 1.5 s");
        }

        [Test]
        public void WalkerBreaksIntoAClosedRoom()
        {
            var map = new AsciiMap(
                "Z......",
                ".#####.",
                ".#...#.",
                ".#.P.#.",
                ".#####.");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            int slot = sim.Spawn(_walker, Center(map.Markers[0]));
            List<Float2> players = PlayersOf(map);

            var hits = new List<PlayerHit>();
            float elapsed = 0f;
            while (hits.Count == 0 && elapsed < 60f)
            {
                sim.Tick(Dt, players);
                sim.DrainPlayerHits(hits);
                elapsed += Dt;
            }
            Assert.That(hits, Is.Not.Empty, "the walker should smash a 100 hp wall (8 dmg / 1.5 s ≈ 19 s) and reach the player");
            Assert.That(elapsed, Is.InRange(15f, 40f));
            int walls = 0;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    walls += map.Blocks.GetBlock(x, y).Key == "wood_wall" ? 1 : 0;
                }
            }
            Assert.That(walls, Is.EqualTo(13), "exactly one of the 14 wall blocks was broken");
            Assert.That(sim.IsAlive(slot), Is.True);
        }

        [Test]
        public void LongReachZombiesCannotHitThroughWalls()
        {
            var map = new AsciiMap(
                ".#.",
                "Z#P",
                ".#.");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            int slot = sim.Spawn(_catalog.IndexOf("brute"), Center(map.Markers[0]));
            sim.Tick(Dt, PlayersOf(map));
            var hits = new List<PlayerHit>();
            sim.DrainPlayerHits(hits);
            Assert.That(hits, Is.Empty, "La Mole reaches 2.9 tiles but the wall is in between");
            Assert.That(sim.GetState(slot), Is.Not.EqualTo(ZombieState.AttackingPlayer), "it goes for the wall (punching or charging)");
        }

        [Test]
        public void ZombiesNeverEnterSolidCells()
        {
            var map = new AsciiMap(
                "Z...#.....",
                "....#.....",
                "....#.....",
                ".........P");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            for (int k = 0; k < 10; k++)
            {
                sim.Spawn(_walker, new Float2(0.5f + k * 0.05f, 3.5f));
            }
            List<Float2> players = PlayersOf(map);
            for (int step = 0; step < 400; step++)
            {
                sim.Tick(Dt, players);
                for (int i = 0; i < sim.Capacity; i++)
                {
                    if (sim.IsAlive(i))
                    {
                        Int2 t = sim.GetPosition(i).Tile;
                        Assert.That(map.Blocks.IsSolid(t.X, t.Y), Is.False, $"zombie {i} inside a wall at {t}");
                    }
                }
            }
        }

        [Test]
        public void CrowdsSpreadOutInsteadOfStacking()
        {
            var map = new AsciiMap(
                "..........",
                "..........",
                "..........",
                ".........P");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            for (int k = 0; k < 12; k++)
            {
                sim.Spawn(_walker, new Float2(1.5f, 2.5f));
            }
            Run(sim, PlayersOf(map), 1.0f);
            float minDistance = float.MaxValue;
            for (int a = 0; a < sim.Capacity; a++)
            {
                for (int b = a + 1; b < sim.Capacity; b++)
                {
                    if (sim.IsAlive(a) && sim.IsAlive(b))
                    {
                        minDistance = System.Math.Min(minDistance, (sim.GetPosition(a) - sim.GetPosition(b)).Length);
                    }
                }
            }
            Assert.That(minDistance, Is.GreaterThan(0.15f));
        }

        [Test]
        public void RunnerSprintsWhenItSeesAPlayer()
        {
            var map = new AsciiMap(new string('.', 120) + "P");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            int slot = sim.Spawn(_runner, new Float2(109.5f, 0.5f));
            Float2 start = sim.GetPosition(slot);
            Run(sim, PlayersOf(map), 1.0f);
            float speed = (sim.GetPosition(slot) - start).Length / 1.0f;
            Assert.That(speed, Is.InRange(4.0f, 5.0f), "runner sprints at ~4.8 tiles/s");
        }

        [Test]
        public void RunnerGetsExhaustedChasingAFleeingPlayer()
        {
            var map = new AsciiMap(new string('.', 120) + "P");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog) { FlowRebuildInterval = 0f };
            int slot = sim.Spawn(_runner, new Float2(112.5f, 0.5f));
            var player = new List<Float2> { new Float2(119.5f, 0.5f) };
            var states = new HashSet<ZombieState>();
            for (int step = 0; step < 200; step++)
            {
                // The player sprints away at 5.5 tiles/s, faster than the runner.
                player[0] = new Float2(System.Math.Max(0.5f, 119.5f - step * 5.5f * Dt), 0.5f);
                sim.Tick(Dt, player);
                states.Add(sim.GetState(slot));
            }
            Assert.That(states, Does.Contain(ZombieState.Chasing));
            Assert.That(states, Does.Contain(ZombieState.Resting), "after ~6 s of sprinting it must rest");
        }

        [Test]
        public void DamageUsesWeaknessesAndReportsDeaths()
        {
            var map = new AsciiMap("Z.P");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog);
            int slot = sim.Spawn(_walker, Center(map.Markers[0]));
            ushort id = sim.GetNetId(slot);
            Assert.That(sim.Damage(slot, 20, "fire"), Is.False);
            Assert.That(sim.GetHealth(slot), Is.EqualTo(30), "walker takes ×1.5 from fire");
            Assert.That(sim.Damage(slot, 30, "blunt"), Is.True);
            Assert.That(sim.IsAlive(slot), Is.False);
            var deaths = new List<ZombieDeath>();
            sim.DrainDeaths(deaths);
            Assert.That(deaths.Count, Is.EqualTo(1));
            Assert.That(deaths[0].ZombieId, Is.EqualTo(id));

            int reused = sim.Spawn(_walker, Center(map.Markers[0]));
            Assert.That(reused, Is.EqualTo(slot), "slots are recycled");
            Assert.That(sim.GetNetId(reused), Is.Not.EqualTo(id), "net ids are not");
        }

        [Test]
        public void SpawnFailsWhenFullOrBlocked()
        {
            var map = new AsciiMap("Z#P");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog, capacity: 2);
            Assert.That(sim.Spawn(_walker, new Float2(1.5f, 0.5f)), Is.EqualTo(-1), "inside a wall");
            Assert.That(sim.Spawn(_walker, Center(map.Markers[0])), Is.GreaterThanOrEqualTo(0));
            Assert.That(sim.Spawn(_walker, Center(map.Markers[0])), Is.GreaterThanOrEqualTo(0));
            Assert.That(sim.Spawn(_walker, Center(map.Markers[0])), Is.EqualTo(-1), "capacity reached");
        }

        [Test]
        public void FlowFieldIsRebuiltWhenAWallFalls()
        {
            var map = new AsciiMap(
                "Z#P");
            var sim = new ZombieSimulation(map.Blocks, map.Ground, _catalog) { FlowRebuildInterval = 0f };
            sim.Spawn(_walker, Center(map.Markers[0]));
            List<Float2> players = PlayersOf(map);
            sim.Tick(Dt, players);
            int before = sim.FlowRebuilds;
            map.Blocks.Damage(1, 0, 1000);
            sim.Tick(Dt, players);
            Assert.That(sim.FlowRebuilds, Is.EqualTo(before + 1));
            Assert.That(sim.Field.BuiltFromRevision, Is.EqualTo(map.Blocks.StructureRevision));
        }
    }
}
