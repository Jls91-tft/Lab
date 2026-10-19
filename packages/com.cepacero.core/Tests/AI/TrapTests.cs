using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Player;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.AI
{
    public class TrapTests
    {
        private const float Dt = 0.05f;

        private static AsciiMap Corridor(out ushort trap)
        {
            var map = new AsciiMap(
                "####################",
                "Z..................P",
                "####################");
            trap = map.Registry.Get("spike_trap").Id;
            for (int x = 8; x <= 10; x++)
            {
                map.Blocks.SetBlock(x, 1, trap);
            }
            return map;
        }

        [Test]
        public void ZombiesWalkIntoTrapsAndGetHurt()
        {
            AsciiMap map = Corridor(out _);
            ZombieCatalog catalog = Fixtures.Catalog();
            var sim = new ZombieSimulation(map.Blocks, map.Ground, catalog);
            int walker = sim.Spawn(catalog.IndexOf("walker"), new Float2(1.5f, 1.5f));
            var player = new List<Float2> { new Float2(19.5f, 1.5f) };
            for (int t = 0; t < 20 * 15 && sim.IsAlive(walker) && sim.GetPosition(walker).X < 12f; t++)
            {
                sim.Tick(Dt, player);
            }
            Assert.That(sim.IsAlive(walker), Is.False, "a slow walker crossing 3 spike tiles takes 15 every 0.5 s (60 hp)");
            Assert.That(map.Blocks.GetHealth(8, 1), Is.LessThan(120), "the traps wore down");
        }

        [Test]
        public void TrapsWearOutAndBreak()
        {
            AsciiMap map = Corridor(out ushort trap);
            ZombieCatalog catalog = Fixtures.Catalog();
            var sim = new ZombieSimulation(map.Blocks, map.Ground, catalog) { TrapInterval = 0.05f };
            int brute = sim.Spawn(catalog.IndexOf("brute"), new Float2(9.5f, 1.5f), aware: false); // big and tough, standing on it
            for (int t = 0; t < 20 && map.Blocks.GetTypeId(9, 1) == trap; t++)
            {
                sim.Tick(Dt, new List<Float2>());
            }
            Assert.That(map.Blocks.GetBlock(9, 1).IsAir, Is.True, "120 hp at 10 per hit: 12 hits");
            Assert.That(sim.IsAlive(brute), Is.True);
        }

        [Test]
        public void TrapsDoNotBlockAnyoneAndDoNotHurtPlayers()
        {
            AsciiMap map = Corridor(out _);
            Assert.That(map.Blocks.IsSolid(9, 1), Is.False);
            var settings = new PlayerSettings();
            var state = PlayerState.Spawn(new Float2(6.5f, 1.5f), settings);
            for (int t = 0; t < 60; t++)
            {
                PlayerMotor.Step(ref state, PlayerInput.FromVector(new Float2(1, 0), false), Dt, map.Blocks, map.Ground, settings);
            }
            Assert.That(state.Position.X, Is.GreaterThan(11f), "players walk over their own traps");
            Assert.That(state.Health, Is.EqualTo(100));
        }
    }
}
