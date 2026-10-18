using System;
using System.IO;
using System.Linq;
using System.Text;
using CepaCero.Map;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Map
{
    /// <summary>The planner on the real map (D-026): every kind of home gets furniture and nothing is left in a wall.</summary>
    public class InteriorRealMapTests
    {
        private static string RepoMap()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 10 && dir != null; i++, dir = Path.GetDirectoryName(dir))
            {
                string p = Path.Combine(dir, "data", "maps", "delicias-centro.map.json");
                if (File.Exists(p))
                {
                    return p;
                }
            }
            return null;
        }

        [Test]
        public void TheRealMapIsFurnishedAndDumpedForLooking()
        {
            string path = RepoMap();
            if (path == null)
            {
                Assert.Ignore("data/maps/delicias-centro.map.json not found from the test folder.");
            }
            BlockRegistry reg = BlockRegistry.CreateDefault(Fixtures.Construction());
            CepaMapData data = Fixtures.FromJson<CepaMapData>(File.ReadAllText(path));
            MapImportResult map = MapImporter.Import(data, reg);

            int furnished = map.Buildings.Count(b => b.Furniture.Count > 0);
            int containers = map.Buildings.Sum(b => b.Furniture.Count(f => f.IsContainer));
            Console.WriteLine($"furnished {furnished}/{map.Buildings.Count}, containers {containers}");
            Assert.That(furnished, Is.GreaterThan(map.Buildings.Count / 2));
            foreach (BuildingInfo b in map.Buildings)
            {
                foreach (Furniture f in b.Furniture.Where(x => !x.OnWall))
                {
                    Assert.That(map.Blocks.GetBlock(f.Cell.X, f.Cell.Y).IsAir, Is.True, $"building {b.Id}: {f.Kind} at {f.Cell} is in a wall");
                }
            }

            // Dump the three biggest homes for a look (run with --logger "console;verbosity=detailed").
            foreach (BuildingInfo b in map.Buildings.Where(x => x.Min.X <= 309 && x.Max.X >= 309 && x.Min.Y <= 73 && x.Max.Y >= 73).Concat(map.Buildings.Where(x => x.Kind == "residential").OrderByDescending(x => x.Tiles).Take(1)))
            {
                var sb = new StringBuilder($"\nbuilding {b.Id} {b.Kind} tiles {b.Tiles}\n");
                for (int y = b.Max.Y; y >= b.Min.Y; y--)
                {
                    for (int x = b.Min.X; x <= b.Max.X; x++)
                    {
                        string key = map.Blocks.GetBlock(x, y).Key;
                        Furniture f = b.Furniture.FirstOrDefault(q => q.Cell.X == x && q.Cell.Y == y);
                        char ch = f.Kind != null ? char.ToUpperInvariant(f.Kind[0])
                            : key.StartsWith("door") ? '+' : key == "wall_drywall" ? '|' : key.Contains("wall") ? '#' : key.StartsWith("window") ? 'o'
                            : map.Ground.Get(x, y) == GroundType.Floor ? '.' : ' ';
                        sb.Append(ch);
                    }
                    sb.Append('\n');
                }
                Console.WriteLine(sb.ToString());
            }
        }
    }
}
