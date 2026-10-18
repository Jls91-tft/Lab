using System;
using System.Collections.Generic;
using System.Linq;
using CepaCero.Map;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Map
{
    /// <summary>Rooms and furniture of the buildings (D-026).</summary>
    public class InteriorPlannerTests
    {
        private BlockRegistry _registry;

        [SetUp]
        public void SetUp() => _registry = BlockRegistry.CreateDefault(Fixtures.Construction());

        /// <summary>A building with walls on x 4..(4+width-1) and y 4..(4+depth-1), a door in the south wall.</summary>
        private static CepaMapData Map(int width, int depth, string kind)
        {
            int w = width + 10, h = depth + 10;
            var ground = new int[w * h];
            var blocks = new int[w * h];
            for (int i = 0; i < ground.Length; i++)
            {
                ground[i] = 3;
            }
            for (int y = 4; y < 4 + depth; y++)
            {
                for (int x = 4; x < 4 + width; x++)
                {
                    ground[y * w + x] = 5;
                    bool edge = x == 4 || x == 3 + width || y == 4 || y == 3 + depth;
                    blocks[y * w + x] = edge ? 1 : 0;
                }
            }
            int doorX = 4 + width / 2;
            blocks[4 * w + doorX] = 3;
            return new CepaMapData
            {
                format = "cepacero.map", version = 2, name = "t", attribution = "(c) OpenStreetMap contributors", meters_per_tile = 1f,
                width = w, height = h,
                legend = new CepaMapLegend
                {
                    ground = new[] { "dirt", "asphalt", "sidewalk", "grass", "water", "floor", "rail" },
                    blocks = new[] { "none", "building_wall", "tree", "door", "fence" },
                    building_kinds = new[] { "generic", "residential", "commercial" },
                },
                layers = new CepaMapLayers { ground = ground, blocks = blocks },
                buildings = new[]
                {
                    new CepaMapBuilding { id = 7, kind = kind, bbox = new[] { 4, 4, 3 + width, 3 + depth }, tiles = width * depth, doors = new[] { doorX, 4 },
                        levels = 1, underground = 0, roof = "tile" },
                },
            };
        }

        private MapImportResult Import(CepaMapData data) => MapImporter.Import(data, _registry);

        [Test]
        public void ABigHomeIsDividedIntoRoomsWithDoorsBetweenThem()
        {
            MapImportResult map = Import(Map(24, 14, "residential"));
            BlockWorld w = map.Blocks;
            int inner = 0, doors = 0;
            for (int y = 5; y < 17; y++)
            {
                for (int x = 5; x < 27; x++)
                {
                    string key = w.GetBlock(x, y).Key;
                    inner += key == "wall_drywall" ? 1 : 0;
                    doors += key == "door_drywall" ? 1 : 0;
                }
            }
            Assert.That(inner, Is.GreaterThan(3), "interior walls");
            Assert.That(doors, Is.GreaterThanOrEqualTo(1), "and a door in each");
        }

        [Test]
        public void AHomeHasAKitchenWithAFridgeAndBedroomsWithBedsAndWardrobes()
        {
            IReadOnlyList<Furniture> f = Import(Map(24, 14, "residential")).Buildings[0].Furniture;
            Assert.That(f.Any(x => x.Kind == "fridge"), "fridge");
            Assert.That(f.Any(x => x.Kind == "bed"), "bed");
            Assert.That(f.Any(x => x.Kind == "wardrobe"), "wardrobe");
            Assert.That(f.Count(x => x.IsContainer), Is.GreaterThan(3));
        }

        [Test]
        public void FurnitureStandsOnFreeFloorAndNeverOnTheDoorways()
        {
            MapImportResult map = Import(Map(24, 14, "residential"));
            var seen = new HashSet<Int2>();
            foreach (Furniture f in map.Buildings[0].Furniture.Where(x => !x.OnWall))
            {
                FurnitureKinds.Info info = FurnitureKinds.Of(f.Kind);
                for (int dy = 0; dy < info.Height; dy++)
                {
                    for (int dx = 0; dx < info.Width; dx++)
                    {
                        var c = new Int2(f.Cell.X + dx, f.Cell.Y + dy);
                        Assert.That(map.Blocks.GetBlock(c.X, c.Y).IsAir, Is.True, $"{f.Kind} at {c} is in a wall");
                        Assert.That(map.Ground.Get(c.X, c.Y), Is.EqualTo(GroundType.Floor), $"{f.Kind} at {c}");
                        Assert.That(seen.Add(c), Is.True, $"two pieces at {c}");
                    }
                }
            }
            var door = new Int2(4 + 12, 4);
            Assert.That(map.Buildings[0].Furniture.Where(f => !f.OnWall).Any(f => Math.Abs(f.Cell.X - door.X) + Math.Abs(f.Cell.Y - door.Y) <= 1), Is.False, "the way in is clear");
        }

        [Test]
        public void RoomsGetRugsAndTheirWallsPicturesAndCurtains()
        {
            MapImportResult map = Import(Map(24, 14, "residential"));
            IReadOnlyList<Furniture> f = map.Buildings[0].Furniture;
            Assert.That(f.Any(x => x.IsFlat), "a rug");
            Assert.That(f.Any(x => x.OnWall && x.Kind != "curtain"), "something on a wall");
            foreach (Furniture w in f.Where(x => x.OnWall))
            {
                Assert.That(map.Blocks.GetBlock(w.Cell.X, w.Cell.Y).IsAir, Is.False, $"{w.Kind} hangs on a wall");
                Assert.That(w.Face == 's' || w.Face == 'w', Is.True);
            }
        }

        [Test]
        public void AShopGetsShelvesWithItsOwnLootTable()
        {
            IReadOnlyList<Furniture> f = Import(Map(24, 14, "commercial")).Buildings[0].Furniture;
            Assert.That(f.Any(x => x.Kind == "shelf" && x.Loot == ""), "shelves use the building's kind");
        }

        [Test]
        public void TheSamePlanComesOutEveryTime()
        {
            var a = Import(Map(24, 14, "residential")).Buildings[0].Furniture.Select(f => (f.Kind, f.Cell.X, f.Cell.Y)).ToList();
            var b = Import(Map(24, 14, "residential")).Buildings[0].Furniture.Select(f => (f.Kind, f.Cell.X, f.Cell.Y)).ToList();
            Assert.That(b, Is.EqualTo(a));
        }
    }
}
