using System.Collections.Generic;
using System.Linq;
using CepaCero.Core;
using CepaCero.Map;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Map
{
    /// <summary>Sewers under the long streets and the zombies that wait in them (D-022 phase H).</summary>
    public class SewerNetworkTests
    {
        private BlockRegistry _registry;

        [SetUp]
        public void SetUp() => _registry = BlockRegistry.CreateDefault(Fixtures.Construction());

        /// <summary>80x60 grass with an east-west road 4 tiles thick (y 28..31) and a north-south one 4 thick (x 38..41).</summary>
        private (BlockWorld, GroundLayer) City(BlockRegistry registry)
        {
            var world = new BlockWorld(80, 60, registry);
            var ground = new GroundLayer(80, 60);
            for (int y = 0; y < 60; y++)
            {
                for (int x = 0; x < 80; x++)
                {
                    ground.Set(x, y, GroundType.Grass);
                    if ((y >= 28 && y <= 31) || (x >= 38 && x <= 41))
                    {
                        ground.Set(x, y, GroundType.Asphalt);
                    }
                }
            }
            return (world, ground);
        }

        [Test]
        public void ATunnelRunsUnderTheMiddleOfEachLongStreet()
        {
            var (world, ground) = City(_registry);
            int dug = SewerNetwork.Generate(world, ground);
            Assert.That(dug, Is.GreaterThan(80), "two streets");
            Assert.That(world.IsSolidAt(10, 29, -1) && world.IsSolidAt(10, 30, -1), Is.False.Or.False, "one of the two middle rows is open");
            int openRows = Enumerable.Range(28, 4).Count(y => !world.IsSolidAt(10, y, -1));
            Assert.That(openRows, Is.EqualTo(1), "narrow: one cell wide");
            int openCols = Enumerable.Range(38, 4).Count(x => !world.IsSolidAt(x, 10, -1));
            Assert.That(openCols, Is.EqualTo(1));
            Assert.That(world.IsSolidAt(10, 10, -1), Is.True, "nothing under the grass");
            Assert.That(world.IsSolidAt(10, 29, -2), Is.True, "only one level down");
        }

        [Test]
        public void TheTunnelsOfTheTwoStreetsMeet()
        {
            var (world, ground) = City(_registry);
            SewerNetwork.Generate(world, ground);
            var start = Enumerable.Range(28, 4).Select(y => new Int2(5, y)).First(c => !world.IsSolidAt(c.X, c.Y, -1));
            var seen = new HashSet<Int2> { start };
            var queue = new Queue<Int2>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Int2 c = queue.Dequeue();
                foreach (Int2 d in new[] { new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1) })
                {
                    var n = new Int2(c.X + d.X, c.Y + d.Y);
                    if (n.X >= 0 && n.Y >= 0 && n.X < 80 && n.Y < 60 && !world.IsSolidAt(n.X, n.Y, -1) && seen.Add(n))
                    {
                        queue.Enqueue(n);
                    }
                }
            }
            Assert.That(seen.Any(c => c.X >= 38 && c.X <= 41 && c.Y < 10), Is.True, "you can walk from one street's tunnel to the other's");
            Assert.That(seen.Any(c => c.X > 70), Is.True);
        }

        [Test]
        public void ManholesLeadDownAndBackUp()
        {
            var (world, ground) = City(_registry);
            SewerNetwork.Generate(world, ground);
            var manholes = new List<Int2>();
            for (int y = 0; y < 60; y++)
            {
                for (int x = 0; x < 80; x++)
                {
                    if (world.GetBlock(x, y, 0, BuildLayer.Floor).Key == "hatch_steel")
                    {
                        manholes.Add(new Int2(x, y));
                    }
                }
            }
            Assert.That(manholes.Count, Is.GreaterThanOrEqualTo(4));
            foreach (Int2 m in manholes)
            {
                Assert.That(LevelRules.CanDescend(world, m.X, m.Y, 0), Is.True, $"manhole at {m.X},{m.Y}");
                Assert.That(LevelRules.CanAscend(world, m.X, m.Y, -1), Is.True);
            }
        }

        [Test]
        public void ARegistryWithoutTheConstructionBlocksGetsNoSewers()
        {
            var (world, ground) = City(BlockRegistry.CreateDefault());
            Assert.That(SewerNetwork.Generate(world, ground), Is.Zero);
        }

        [Test]
        public void TheImportedMapGetsSewersToo()
        {
            var data = new CepaMapData
            {
                format = "cepacero.map", version = 2, name = "t", attribution = "(c) OpenStreetMap contributors", meters_per_tile = 2f, width = 60, height = 20,
                legend = new CepaMapLegend { ground = new[] { "dirt", "asphalt", "sidewalk", "grass", "water", "floor", "rail" }, blocks = new[] { "none", "building_wall", "tree", "door", "fence" } },
                layers = new CepaMapLayers { ground = Enumerable.Range(0, 1200).Select(i => i / 60 >= 8 && i / 60 <= 11 ? 1 : 3).ToArray(), blocks = new int[1200] },
                buildings = new CepaMapBuilding[0],
            };
            MapImportResult map = MapImporter.Import(data, _registry);
            Assert.That(Enumerable.Range(8, 4).Count(y => !map.Blocks.IsSolidAt(30, y, -1)), Is.EqualTo(1));
            Assert.That(map.Blocks.PendingChangeCount, Is.Zero, "a starting point, not a delta");
        }

        [Test]
        public void ZombiesAreLeftWaitingUnderground()
        {
            var (world, ground) = City(_registry);
            SewerNetwork.Generate(world, ground);
            var settings = new GameSettings { TickRate = 20, HordeBase = 0, MapZombiesPer1000Tiles = 0f, UndergroundZombiesPer1000Cells = 40f };
            var session = new GameSession(settings, new PlayerSettings(), world, ground, Fixtures.GameDataWithConstruction());
            session.AddPlayer(new Float2(5.5f, 5.5f));
            int placed = session.PopulateMap();
            Assert.That(placed, Is.GreaterThan(0));
            int below = 0;
            for (int i = 0; i < session.Zombies.Capacity; i++)
            {
                if (session.Zombies.IsAlive(i))
                {
                    Assert.That(session.Zombies.GetLevel(i), Is.EqualTo(-1));
                    Assert.That(world.IsSolidAt(session.Zombies.GetPosition(i).Tile.X, session.Zombies.GetPosition(i).Tile.Y, -1), Is.False);
                    below++;
                }
            }
            Assert.That(below, Is.EqualTo(placed));
        }
    }
}
