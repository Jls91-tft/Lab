using System.Collections.Generic;
using System.Diagnostics;
using CepaCero.AI;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.AI
{
    public class FlowFieldTests
    {
        private static FlowField Build(AsciiMap map, int maxDistance = FlowField.Unreachable)
        {
            var field = new FlowField(map.Width, map.Height);
            field.Build(map.Ground, map.Blocks, PathPolicy.Zombie, map.Targets, maxDistance);
            return field;
        }

        /// <summary>Follows the field from a start cell; stops at a target, a solid block, or after maxSteps.</summary>
        private static List<Int2> Walk(AsciiMap map, FlowField field, Int2 start, int maxSteps = 200)
        {
            var path = new List<Int2> { start };
            Int2 p = start;
            for (int i = 0; i < maxSteps; i++)
            {
                Int2 step = field.GetStep(p.X, p.Y);
                if (step == new Int2(0, 0))
                {
                    break;
                }
                p += step;
                path.Add(p);
                if (map.Blocks.IsSolid(p.X, p.Y))
                {
                    break;
                }
            }
            return path;
        }

        [Test]
        public void OpenGroundLeadsStraightToTheTarget()
        {
            var map = new AsciiMap(
                "Z.......",
                "........",
                ".......P");
            FlowField field = Build(map);
            List<Int2> path = Walk(map, field, map.Markers[0]);
            Assert.That(path[path.Count - 1], Is.EqualTo(map.Targets[0]));
            Assert.That(path.Count - 1, Is.EqualTo(7), "diagonals allowed in the open");
        }

        [Test]
        public void ZombiesWalkAroundAWallWhenTheGapIsClose()
        {
            var map = new AsciiMap(
                "...Z...",
                ".......",
                "#####..",
                ".......",
                "...P...");
            FlowField field = Build(map);
            List<Int2> path = Walk(map, field, map.Markers[0]);
            Assert.That(path[path.Count - 1], Is.EqualTo(map.Targets[0]));
            foreach (Int2 p in path)
            {
                Assert.That(map.Blocks.IsSolid(p.X, p.Y), Is.False, $"walked through a wall at {p}");
            }
        }

        [Test]
        public void ZombiesBreakInWhenThereIsNoOtherWay()
        {
            var map = new AsciiMap(
                "..Z....",
                ".#####.",
                ".#...#.",
                ".#.P.#.",
                ".#####.");
            FlowField field = Build(map);
            Assert.That(field.GetDistance(map.Markers[0].X, map.Markers[0].Y), Is.LessThan(FlowField.Unreachable));
            List<Int2> path = Walk(map, field, map.Markers[0]);
            Int2 end = path[path.Count - 1];
            Assert.That(map.Blocks.GetBlock(end.X, end.Y).Key, Is.EqualTo("wood_wall"), "the walk stops at the wall to attack");
        }

        [Test]
        public void ZombiesPreferBreakingWoodOverMetal()
        {
            var map = new AsciiMap(
                "MMMMMMM",
                "M.....M",
                "M..P..M",
                "M.....M",
                "M###MMM",
                ".......",
                "..Z....");
            FlowField field = Build(map);
            List<Int2> path = Walk(map, field, map.Markers[0]);
            Int2 end = path[path.Count - 1];
            Assert.That(map.Blocks.GetBlock(end.X, end.Y).Key, Is.EqualTo("wood_wall"));
        }

        [Test]
        public void WaterAndTreesAreNotCrossed()
        {
            var map = new AsciiMap(
                "Z.~....",
                "..~....",
                "TTT..P.");
            FlowField field = Build(map);
            Assert.That(field.GetDistance(0, 2), Is.EqualTo(FlowField.Unreachable));
            Assert.That(field.GetStep(0, 2), Is.EqualTo(new Int2(0, 0)));
        }

        [Test]
        public void NoCornerCuttingBetweenTwoWalls()
        {
            var map = new AsciiMap(
                "Z#.",
                "#..",
                "..P");
            FlowField field = Build(map);
            Int2 step = field.GetStep(0, 2);
            Assert.That(step, Is.Not.EqualTo(new Int2(1, -1)), "squeezing diagonally between two walls is not allowed");
        }

        [Test]
        public void MaxDistanceBoundsTheSearch()
        {
            var map = new AsciiMap("P.........................Z");
            FlowField field = Build(map, maxDistance: 100);
            Assert.That(field.GetDistance(5, 0), Is.EqualTo(50));
            Assert.That(field.GetDistance(26, 0), Is.EqualTo(FlowField.Unreachable));
        }

        [Test]
        public void RebuildsAfterAWallIsDestroyed()
        {
            var map = new AsciiMap(
                "Z#P");
            FlowField field = Build(map);
            int before = field.GetDistance(0, 0);
            map.Blocks.Damage(1, 0, 1000);
            Assert.That(field.BuiltFromRevision, Is.LessThan(map.Blocks.Revision), "callers can detect the stale field");
            field.Build(map.Ground, map.Blocks, PathPolicy.Zombie, map.Targets);
            Assert.That(field.GetDistance(0, 0), Is.LessThan(before));
        }

        [Test]
        public void DeliciasSizedFieldBuildsFastEnough()
        {
            const int w = 375;
            const int h = 335;
            var registry = BlockRegistry.CreateDefault();
            var ground = new GroundLayer(w, h);
            var blocks = new BlockWorld(w, h, registry);
            ushort wall = registry.Get("building_wall").Id;
            for (int by = 5; by < h - 20; by += 40)
            {
                for (int bx = 5; bx < w - 20; bx += 40)
                {
                    for (int k = 0; k < 30; k++)
                    {
                        blocks.SetBlock(bx + k, by, wall);
                        blocks.SetBlock(bx + k, by + 29, wall);
                        blocks.SetBlock(bx, by + k, wall);
                        blocks.SetBlock(bx + 29, by + k, wall);
                    }
                }
            }
            var field = new FlowField(w, h);
            var targets = new List<Int2> { new Int2(w / 2, h / 2 + 18), new Int2(w / 2 + 3, h / 2 + 18) };
            field.Build(ground, blocks, PathPolicy.Zombie, targets); // warm-up
            var sw = Stopwatch.StartNew();
            const int runs = 5;
            for (int i = 0; i < runs; i++)
            {
                field.Build(ground, blocks, PathPolicy.Zombie, targets);
            }
            double ms = sw.Elapsed.TotalMilliseconds / runs;
            TestContext.WriteLine($"FlowField {w}x{h}: {ms:0.0} ms per build, {field.LastBuildVisited} cells");
            Assert.That(ms, Is.LessThan(250), "budget: rebuilt at most a few times per second on the host");
        }
    }
}
