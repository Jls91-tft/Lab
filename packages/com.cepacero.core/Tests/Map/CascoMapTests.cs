using System.Collections.Generic;
using System.IO;
using System.Linq;
using CepaCero.Map;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Map
{
    /// <summary>The prototype's map (D-029, D-031): every building can be walked into from the street.</summary>
    public class CascoMapTests
    {
        [Test]
        public void EveryBuildingCanBeWalkedIntoFromTheStreet()
        {
            string dir = TestContext.CurrentContext.TestDirectory, path = null;
            for (int i = 0; i < 10 && dir != null; i++, dir = Path.GetDirectoryName(dir))
            {
                string p = Path.Combine(dir, "data", "maps", "casco-pilar.map.json");
                if (File.Exists(p))
                {
                    path = p;
                    break;
                }
            }
            if (path == null)
            {
                Assert.Ignore("data/maps/casco-pilar.map.json not found.");
            }
            MapImportResult map = MapImporter.Import(Fixtures.FromJson<CepaMapData>(File.ReadAllText(path)), BlockRegistry.CreateDefault(Fixtures.Construction()));
            BlockWorld w = map.Blocks;
            var reach = new bool[w.Width * w.Height];
            var queue = new Queue<Int2>();
            for (int y = 0; y < w.Height; y++)
            {
                for (int x = 0; x < w.Width; x++)
                {
                    if ((x == 0 || y == 0 || x == w.Width - 1 || y == w.Height - 1) && w.GetBlock(x, y).IsAir)
                    {
                        reach[y * w.Width + x] = true;
                        queue.Enqueue(new Int2(x, y));
                    }
                }
            }
            while (queue.Count > 0)
            {
                Int2 c = queue.Dequeue();
                foreach (Int2 d in new[] { new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1) })
                {
                    int nx = c.X + d.X, ny = c.Y + d.Y;
                    if (!w.InBounds(nx, ny) || reach[ny * w.Width + nx])
                    {
                        continue;
                    }
                    BlockType t = w.GetBlock(nx, ny);
                    if (t.IsAir || t.ToggleTo != 0)
                    {
                        reach[ny * w.Width + nx] = true;
                        queue.Enqueue(new Int2(nx, ny));
                    }
                }
            }
            int sealedIn = map.Buildings.Count(b =>
            {
                for (int y = b.Min.Y; y <= b.Max.Y; y++)
                {
                    for (int x = b.Min.X; x <= b.Max.X; x++)
                    {
                        if (map.Ground.Get(x, y) == GroundType.Floor && w.GetBlock(x, y).IsAir && reach[y * w.Width + x])
                        {
                            return false;
                        }
                    }
                }
                return true;
            });
            TestContext.WriteLine($"buildings {map.Buildings.Count}, sealed in {sealedIn}");
            Assert.That(sealedIn, Is.Zero, "every building can be walked into");
        }
    }
}
