using System;
using System.Linq;
using CepaCero.Map;
using CepaCero.Tests.TestSupport;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Map
{
    /// <summary>
    /// Contract tests against the JSON produced by tools/osm2tiles from its
    /// synthetic neighbourhood (see tools/osm2tiles/tests/conftest.py).
    /// </summary>
    public class MapImporterTests
    {
        private const float MetersPerTile = 2f;

        private static int T(float meters) => (int)(meters / MetersPerTile);

        [Test]
        public void ImportsTheOsm2TilesFixture()
        {
            CepaMapData data = Fixtures.LoadMap("synthetic.map.json");
            MapImportResult map = MapImporter.Import(data, BlockRegistry.CreateDefault());

            Assert.That(map.Warnings, Is.Empty);
            Assert.That(map.Blocks.Width, Is.EqualTo(65));
            Assert.That(map.Blocks.Height, Is.EqualTo(60));
            Assert.That(map.MetersPerTile, Is.EqualTo(MetersPerTile));
            StringAssert.Contains("OpenStreetMap contributors", map.Attribution);
            Assert.That(map.Blocks.PendingChangeCount, Is.EqualTo(0), "the map itself is not a network delta");
        }

        [Test]
        public void RowZeroIsSouthLikeUnity()
        {
            MapImportResult map = MapImporter.Import(Fixtures.LoadMap("synthetic.map.json"), BlockRegistry.CreateDefault());
            Assert.That(map.Ground.Get(T(30), T(20)), Is.EqualTo(GroundType.Grass), "park in the south");
            Assert.That(map.Ground.Get(T(25), T(80)), Is.EqualTo(GroundType.Floor), "building A in the north");
            Assert.That(map.Ground.Get(T(60), T(50)), Is.EqualTo(GroundType.Asphalt), "street in the middle");
            Assert.That(map.Ground.Get(T(90), T(100)), Is.EqualTo(GroundType.Water));
        }

        [Test]
        public void BuildingsBecomeBreakableWallsWithDoors()
        {
            MapImportResult map = MapImporter.Import(Fixtures.LoadMap("synthetic.map.json"), BlockRegistry.CreateDefault());

            Assert.That(map.Buildings.Select(b => b.Kind).OrderBy(k => k), Is.EqualTo(new[] { "food", "medical", "residential" }));
            foreach (BuildingInfo b in map.Buildings)
            {
                Assert.That(b.Doors, Is.Not.Empty);
                Assert.That(map.Blocks.GetBlock(b.Doors[0].X, b.Doors[0].Y).Key, Is.EqualTo("door"));
                Assert.That(map.Blocks.GetBlock(b.Min.X, b.Min.Y).Key, Is.EqualTo("building_wall"), "corner is wall");
            }
            Assert.That(map.Blocks.GetBlock(T(31), T(21)).Key, Is.EqualTo("tree"));
            Assert.That(map.Blocks.GetBlock(T(79), T(70)).Key, Is.EqualTo("fence"));
        }

        [Test]
        public void LegendIsMatchedByNameNotById()
        {
            CepaMapData data = Fixtures.LoadMap("synthetic.map.json");
            // Swap two legend entries and their ids in the layer: the result must not change.
            int asphalt = Array.IndexOf(data.legend.ground, "asphalt");
            int water = Array.IndexOf(data.legend.ground, "water");
            (data.legend.ground[asphalt], data.legend.ground[water]) = (data.legend.ground[water], data.legend.ground[asphalt]);
            for (int i = 0; i < data.layers.ground.Length; i++)
            {
                int g = data.layers.ground[i];
                data.layers.ground[i] = g == asphalt ? water : g == water ? asphalt : g;
            }
            MapImportResult map = MapImporter.Import(data, BlockRegistry.CreateDefault());
            Assert.That(map.Ground.Get(T(60), T(50)), Is.EqualTo(GroundType.Asphalt));
            Assert.That(map.Ground.Get(T(90), T(100)), Is.EqualTo(GroundType.Water));
        }

        [Test]
        public void UnknownLegendEntriesWarnInsteadOfFailing()
        {
            CepaMapData data = Fixtures.LoadMap("synthetic.map.json");
            data.legend.blocks[Array.IndexOf(data.legend.blocks, "fence")] = "laser_fence";
            MapImportResult map = MapImporter.Import(data, BlockRegistry.CreateDefault());
            Assert.That(map.Warnings.Count, Is.EqualTo(1));
            Assert.That(map.Blocks.GetBlock(T(79), T(70)).IsAir, Is.True);
        }

        [Test]
        public void InvalidDocumentsAreRejected()
        {
            var registry = BlockRegistry.CreateDefault();

            CepaMapData wrongVersion = Fixtures.LoadMap("synthetic.map.json");
            wrongVersion.version = 2;
            Assert.Throws<FormatException>(() => MapImporter.Import(wrongVersion, registry));

            CepaMapData truncated = Fixtures.LoadMap("synthetic.map.json");
            truncated.layers.blocks = truncated.layers.blocks.Take(10).ToArray();
            Assert.Throws<FormatException>(() => MapImporter.Import(truncated, registry));

            CepaMapData noAttribution = Fixtures.LoadMap("synthetic.map.json");
            noAttribution.attribution = "";
            Assert.Throws<FormatException>(() => MapImporter.Import(noAttribution, registry));

            CepaMapData badId = Fixtures.LoadMap("synthetic.map.json");
            badId.layers.ground[0] = 99;
            Assert.Throws<FormatException>(() => MapImporter.Import(badId, registry));
        }
    }
}
