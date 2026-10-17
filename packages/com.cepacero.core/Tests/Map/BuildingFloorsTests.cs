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
    /// <summary>Upper storeys, basements and roofs of imported buildings (D-022 phase H).</summary>
    public class BuildingFloorsTests
    {
        private BlockRegistry _registry;

        [SetUp]
        public void SetUp() => _registry = BlockRegistry.CreateDefault(Fixtures.Construction());

        /// <summary>A 30x20 map with one building: walls on x 5..16 and y 4..11, a door in the middle of the south wall.</summary>
        private static CepaMapData Map(int levels, int underground, string roof, int version = 2, bool withDoor = true)
        {
            const int w = 30, h = 20;
            var ground = new int[w * h];
            var blocks = new int[w * h];
            for (int i = 0; i < ground.Length; i++)
            {
                ground[i] = 3;                                                    // grass
            }
            for (int y = 4; y <= 11; y++)
            {
                for (int x = 5; x <= 16; x++)
                {
                    ground[y * w + x] = 5;                                        // floor
                    bool edge = x == 5 || x == 16 || y == 4 || y == 11;
                    blocks[y * w + x] = edge ? 1 : 0;                             // building_wall
                }
            }
            if (withDoor)
            {
                blocks[4 * w + 10] = 3;                                           // door
            }
            return new CepaMapData
            {
                format = "cepacero.map", version = version, name = "t", attribution = "(c) OpenStreetMap contributors", meters_per_tile = 2f,
                width = w, height = h,
                legend = new CepaMapLegend
                {
                    ground = new[] { "dirt", "asphalt", "sidewalk", "grass", "water", "floor", "rail" },
                    blocks = new[] { "none", "building_wall", "tree", "door", "fence" },
                    building_kinds = new[] { "generic", "residential" },
                },
                layers = new CepaMapLayers { ground = ground, blocks = blocks },
                buildings = new[]
                {
                    new CepaMapBuilding { id = 1, kind = "residential", bbox = new[] { 5, 4, 16, 11 }, tiles = 96, doors = withDoor ? new[] { 10, 4 } : new int[0],
                        levels = levels, underground = underground, roof = roof },
                },
            };
        }

        private MapImportResult Import(CepaMapData data) => MapImporter.Import(data, _registry);

        [Test]
        public void ASingleStoreyBuildingStaysAsItWas()
        {
            MapImportResult map = Import(Map(1, 0, "tile"));
            Assert.That(map.Warnings, Is.Empty);
            for (int z = -3; z <= 3; z++)
            {
                if (z == 0)
                {
                    continue;
                }
                Assert.That(Enumerable.Range(0, 20).SelectMany(y => Enumerable.Range(0, 30).Select(x => map.Blocks.GetBlock(x, y, z, BuildLayer.Floor))).All(t => t.IsAir), Is.True, $"level {z}");
            }
        }

        [Test]
        public void EachUpperStoreyGetsFloorsWallsWindowsAndARoofOnTheTop()
        {
            MapImportResult map = Import(Map(3, 0, "tile"));
            BlockWorld w = map.Blocks;
            foreach (int z in new[] { 1, 2 })
            {
                Assert.That(w.GetBlock(8, 7, z, BuildLayer.Floor).Key, Is.EqualTo("floor_concrete"), $"floor at {z}");
                Assert.That(w.GetBlock(5, 7, z, BuildLayer.Structure).Key, Does.StartWith("wall_").Or.StartWith("window_"), $"wall at {z}");
            }
            var perimeter = Enumerable.Range(4, 8).Select(y => w.GetBlock(5, y, 1, BuildLayer.Structure).Key).ToList();
            Assert.That(perimeter.Any(k => k.StartsWith("window_")), Is.True, "some windows");
            Assert.That(perimeter.Any(k => k.StartsWith("wall_")), Is.True, "and some wall");
            Assert.That(w.GetBlock(10, 4, 1, BuildLayer.Structure).Key, Does.StartWith("wall_"), "no door upstairs");
            Assert.That(w.GetBlock(8, 7, 2, BuildLayer.Roof).Key, Is.EqualTo("roof_tile"));
            Assert.That(w.GetBlock(8, 7, 1, BuildLayer.Roof).IsAir, Is.True, "only the top has a roof");
            Assert.That(w.IsSolidAt(8, 7, 1), Is.False, "you can walk on the new floor");
        }

        [Test]
        public void AStairwellGoesFromTheStreetToTheTopWithoutBlockingTheDoor()
        {
            MapImportResult map = Import(Map(3, 0, "flat"));
            BlockWorld w = map.Blocks;
            Int2 stairs = default;
            bool found = false;
            for (int y = 5; y <= 10 && !found; y++)
            {
                for (int x = 6; x <= 15 && !found; x++)
                {
                    if (w.GetBlock(x, y, 0, BuildLayer.Structure).Key == "stairs_concrete")
                    {
                        stairs = new Int2(x, y);
                        found = true;
                    }
                }
            }
            Assert.That(found, Is.True);
            Assert.That(Math.Abs(stairs.X - 10) > 1 || Math.Abs(stairs.Y - 4) > 1, Is.True, "away from the door");
            Assert.That(w.GetBlock(stairs.X, stairs.Y, 1, BuildLayer.Structure).Key, Is.EqualTo("stairs_concrete"), "stairs again to reach level 2");
            Assert.That(w.GetBlock(stairs.X, stairs.Y, 2, BuildLayer.Structure).IsAir, Is.True, "nothing on the top landing");
            Assert.That(w.GetBlock(stairs.X, stairs.Y, 1, BuildLayer.Floor).IsAir, Is.True, "a hole in each floor");
            Assert.That(LevelRules.CanAscend(w, stairs.X, stairs.Y, 0), Is.True);
            Assert.That(LevelRules.CanAscend(w, stairs.X, stairs.Y, 1), Is.True);
            Assert.That(LevelRules.CanAscend(w, stairs.X, stairs.Y, 2), Is.False);
            Assert.That(LevelRules.Supports(w, stairs.X, stairs.Y, 2), Is.True, "the landing is held up by the stairs below");
        }

        [Test]
        public void BasementsAreDugOutWithTheirOwnStairs()
        {
            MapImportResult map = Import(Map(1, 2, "flat"));
            BlockWorld w = map.Blocks;
            Assert.That(w.IsSolidAt(8, 7, -1), Is.False);
            Assert.That(w.IsSolidAt(8, 7, -2), Is.False);
            Assert.That(w.IsSolidAt(8, 7, -3), Is.True, "only the two levels asked for");
            Assert.That(w.IsSolidAt(2, 7, -1), Is.True, "outside the footprint it is untouched ground");
            Assert.That(w.GetBlock(8, 7, -1, BuildLayer.Floor).Key, Is.EqualTo("floor_concrete"));
            bool stairsDown = false;
            for (int y = 5; y <= 10; y++)
            {
                for (int x = 6; x <= 15; x++)
                {
                    if (w.GetBlock(x, y, -1, BuildLayer.Structure).Key == "stairs_concrete")
                    {
                        stairsDown = LevelRules.CanDescend(w, x, y, 0) && LevelRules.CanDescend(w, x, y, -1) == true;
                    }
                }
            }
            Assert.That(stairsDown, Is.True, "you can walk down to the second level");
        }

        [Test]
        public void EveryBuildingWithAnInteriorHasARoofSoNothingIsSeenFromOutside()
        {
            // D-024: a house is not seen from the street until you go in, whatever its height.
            MapImportResult basementOnly = Import(Map(1, 1, "tile"));
            Assert.That(basementOnly.Blocks.GetBlock(8, 7, 0, BuildLayer.Roof).Key, Is.EqualTo("roof_tile"), "a basement does not make a roof necessary");

            MapImportResult singleStorey = Import(Map(1, 0, "tile"));
            BlockWorld w = singleStorey.Blocks;
            Assert.That(w.GetBlock(8, 7, 0, BuildLayer.Roof).Key, Is.EqualTo("roof_tile"), "over the interior");
            Assert.That(w.GetBlock(5, 7, 0, BuildLayer.Roof).Key, Is.EqualTo("roof_tile"), "and over its walls");
            Assert.That(w.GetBlock(2, 2, 0, BuildLayer.Roof).IsAir, Is.True, "but not over the street");
            Assert.That(w.GetBlock(10, 4, 0, BuildLayer.Roof).IsAir, Is.True, "the doorway stays uncovered so the way in is seen from outside");
        }

        [Test]
        public void ABuildingWithNoRoofKindGetsTheCommonTileRoof()
        {
            MapImportResult map = Import(Map(1, 0, ""));
            Assert.That(map.Blocks.GetBlock(8, 7, 0, BuildLayer.Roof).Key, Is.EqualTo("roof_tile"));
        }

        [Test]
        public void ABuildingWithoutADoorKeepsOneStoreyAndSaysSo()
        {
            MapImportResult map = Import(Map(3, 0, "tile", withDoor: false));
            Assert.That(map.Blocks.GetBlock(8, 7, 1, BuildLayer.Floor).IsAir, Is.True);
            Assert.That(map.Warnings.Any(m => m.Contains("no usable interior")), Is.True);
            Assert.That(map.Blocks.GetBlock(8, 7, 0, BuildLayer.Roof).Key, Is.EqualTo("roof_tile"), "a building without a door is roofed too");
        }

        [Test]
        public void VersionOneMapsStillLoadAsSingleStoreys()
        {
            CepaMapData data = Map(0, 0, null, version: 1);
            MapImportResult map = Import(data);
            Assert.That(map.Buildings.Single().Levels, Is.EqualTo(1));
            Assert.That(map.Buildings.Single().Roof, Is.Empty);
            Assert.That(map.Blocks.GetBlock(8, 7, 1, BuildLayer.Floor).IsAir, Is.True);
        }

        [Test]
        public void TheLegacyRegistryJustKeepsTheGroundFloor()
        {
            MapImportResult map = MapImporter.Import(Map(3, 1, "tile"), BlockRegistry.CreateDefault());
            Assert.That(map.Warnings, Is.Empty);
            Assert.That(map.Buildings.Single().Levels, Is.EqualTo(3));
        }

        [Test]
        public void TheWorldGeneratedIsTheSameEveryTimeAndNotADelta()
        {
            MapImportResult a = Import(Map(3, 1, "tile"));
            MapImportResult b = Import(Map(3, 1, "tile"));
            Assert.That(a.Blocks.PendingChangeCount, Is.EqualTo(0));
            for (int z = -1; z <= 2; z++)
            {
                for (int y = 3; y <= 12; y++)
                {
                    for (int x = 4; x <= 17; x++)
                    {
                        foreach (BuildLayer l in new[] { BuildLayer.Structure, BuildLayer.Floor, BuildLayer.Roof })
                        {
                            Assert.That(a.Blocks.GetTypeId(x, y, z, l), Is.EqualTo(b.Blocks.GetTypeId(x, y, z, l)));
                        }
                    }
                }
            }
        }
    }
}
