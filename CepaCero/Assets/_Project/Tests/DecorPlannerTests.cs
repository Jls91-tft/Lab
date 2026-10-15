#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using CepaCero.Map;
using CepaCero.Unity.Prototype;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Unity.Tests
{
    public class DecorPlannerTests
    {
        private const string MapPath = "packages/com.cepacero.core/Tests/Fixtures/synthetic.map.json";

        private static (MapImportResult map, List<DecorItem> plan) Plan()
        {
            MapImportResult map = RepoData.LoadMap(MapPath);
            return (map, DecorPlanner.Plan(map.Blocks, map.Ground, map.Buildings, DecorPlanner.SeedFor(map.Name)));
        }

        [Test]
        public void SameMapGivesTheSameStreet()
        {
            List<DecorItem> a = Plan().plan;
            List<DecorItem> b = Plan().plan;
            Assert.That(a.Count, Is.GreaterThan(0));
            Assert.That(b.Select(i => (i.Sprite, i.X, i.Y)), Is.EqualTo(a.Select(i => (i.Sprite, i.X, i.Y))));
        }

        [Test]
        public void NothingStandsOnBlocksOrNextToDoors()
        {
            (MapImportResult map, List<DecorItem> plan) = Plan();
            foreach (DecorItem item in plan.Where(i => !i.Flat))
            {
                for (int dy = 0; dy < item.Height; dy++)
                {
                    for (int dx = 0; dx < item.Width; dx++)
                    {
                        int x = item.X + dx;
                        int y = item.Y + dy;
                        Assert.That(map.Blocks.GetBlock(x, y).IsAir, $"{item.Sprite} on a block at {x},{y}");
                        for (int ny = y - 1; ny <= y + 1; ny++)
                        {
                            for (int nx = x - 1; nx <= x + 1; nx++)
                            {
                                if (map.Blocks.InBounds(nx, ny))
                                {
                                    string key = map.Blocks.GetBlock(nx, ny).Key;
                                    Assert.That(key != "door" && key != "door_open", $"{item.Sprite} blocks a door at {nx},{ny}");
                                }
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void CarsParkOnRoadsAndHomesGetFurniture()
        {
            (MapImportResult map, List<DecorItem> plan) = Plan();
            foreach (DecorItem car in plan.Where(i => i.Sprite.StartsWith("decor_car_")))
            {
                Assert.That(map.Ground.Get(car.X, car.Y), Is.EqualTo(GroundType.Asphalt), car.Sprite);
            }
            Assert.That(plan.Any(i => !i.Flat && map.Ground.Get(i.X, i.Y) == GroundType.Floor), "no furniture indoors");
        }
    }
}
#endif
