using System;
using System.Collections.Generic;
using System.Linq;
using CepaCero.Building;
using CepaCero.Core;
using CepaCero.Rules;
using CepaCero.Tests.TestSupport;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Core
{
    /// <summary>Blueprints (D-022 phase E): capture, turn, paste plan, cost and the .cepaplan text.</summary>
    public class BlueprintTests
    {
        private BlockRegistry _registry;
        private BlockWorld _world;

        [SetUp]
        public void SetUp()
        {
            _registry = BlockRegistry.CreateDefault(Fixtures.Construction());
            _world = new BlockWorld(40, 40, _registry);
        }

        private ushort Id(string key) => _registry.Get(key).Id;

        private Blueprint Hut()
        {
            // a 3x2 hut: wooden walls with a door in the middle of the south side and a floor behind it
            _world.SetBlock(10, 10, Id("wall_wood"));
            _world.SetBlock(11, 10, Id("door_wood"));
            _world.SetBlock(12, 10, Id("wall_wood"));
            _world.SetBlock(10, 11, 0, BuildLayer.Floor, Id("floor_wood"), BlockVariant.Make(3, 0));
            _world.SetBlock(12, 11, Id("wall_brick"));
            return Blueprint.Capture(_world, 12, 11, 10, 10);              // corners in any order
        }

        [Test]
        public void CaptureKeepsPlayerPiecesRelativeToTheCorner()
        {
            Blueprint plan = Hut();
            Assert.That((plan.Width, plan.Height), Is.EqualTo((3, 2)));
            Assert.That(plan.Pieces.Count, Is.EqualTo(5));
            BlueprintPiece floor = plan.Pieces.Single(p => p.Layer == BuildLayer.Floor);
            Assert.That((floor.X, floor.Y, floor.Key), Is.EqualTo((0, 1, "floor_wood")));
            Assert.That(BlockVariant.Color(floor.Variant), Is.EqualTo(3));
        }

        [Test]
        public void MapBuildingsAndOpenDoorsAreHandledOnCapture()
        {
            _world.SetBlock(5, 5, Id("building_wall"));
            _world.SetBlock(6, 5, Id("door_wood"));
            _world.Toggle(6, 5);
            Blueprint plan = Blueprint.Capture(_world, 5, 5, 6, 5);
            Assert.That(plan.Pieces.Count, Is.EqualTo(1), "the map wall is not copied");
            Assert.That(plan.Pieces[0].Key, Is.EqualTo("door_wood"), "an open door is saved closed");
        }

        [Test]
        public void ARectangleIsClippedToTheMapAndTheMaximumSize()
        {
            Blueprint plan = Blueprint.Capture(_world, -5, -5, 200, 200);
            Assert.That(plan.Width, Is.EqualTo(40));
            var big = new BlockWorld(100, 100, _registry);
            Assert.That(Blueprint.Capture(big, 0, 0, 99, 99).Width, Is.EqualTo(Blueprint.MaxSide));
        }

        [Test]
        public void TurningFourTimesGivesTheSamePlanAndSwapsTheSides()
        {
            Blueprint plan = Hut();
            Blueprint once = plan.RotatedClockwise();
            Assert.That((once.Width, once.Height), Is.EqualTo((2, 3)));
            Blueprint again = once.RotatedClockwise().RotatedClockwise().RotatedClockwise();
            Assert.That(again.ToText(), Is.EqualTo(plan.ToText()));
            // the south-west wall of the hut ends up at the north-west after a clockwise quarter turn
            Assert.That(once.Pieces.Any(p => p.Key == "wall_wood" && p.X == 0 && p.Y == 2), Is.True);
            Assert.That(BlockVariant.Rotation(once.Pieces.First().Variant), Is.EqualTo(1));
        }

        [Test]
        public void PastingPlansFloorsFirstAndMovesTheOrigin()
        {
            Blueprint plan = Hut();
            var cells = new List<PlannedPiece>();
            plan.PlanAt(20, 30, _registry, cells, out int unknown);
            Assert.That(unknown, Is.Zero);
            Assert.That(cells.Count, Is.EqualTo(5));
            Assert.That(cells[0].Type, Is.EqualTo(Id("floor_wood")));
            Assert.That((cells[0].X, cells[0].Y), Is.EqualTo((20, 31)));
            Assert.That(cells.Any(c => c.X == 22 && c.Y == 31 && c.Type == Id("wall_brick")), Is.True);
        }

        [Test]
        public void UnknownBlocksAreSkippedAndCounted()
        {
            Blueprint plan = Blueprint.FromText("cepaplan 1\nsize 2 1\nwall_wood 0 0 0 0\nwall_unobtainium 1 0 0 0\nfloor_wood 1 0 0 0\nfloor_wood 0 0 0 0\n");
            var cells = new List<PlannedPiece>();
            plan.PlanAt(0, 0, _registry, cells, out int unknown);
            Assert.That(unknown, Is.EqualTo(3), "unknown key, and floor_wood written on the structure layer");
            Assert.That(cells.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheCostSumsPiecesAndFollowsThePrice()
        {
            Blueprint plan = Hut();
            var normal = plan.TotalCost(_registry);
            int wood = Enumerable.Sum(new[] { "wall_wood", "door_wood", "wall_wood", "floor_wood" },
                k => _registry.Get(k).Build.Cost.Where(c => c.Item == "wood").Sum(c => c.Count));
            Assert.That(normal["wood"], Is.EqualTo(wood));
            Assert.That(normal.ContainsKey("brick"), Is.True);
            Assert.That(plan.TotalCost(_registry, BuildCostMode.Free), Is.Empty);
            Assert.That(plan.TotalCost(_registry, BuildCostMode.Cheap)["wood"], Is.LessThan(normal["wood"]));
        }

        [Test]
        public void TheShortfallSaysWhatIsMissing()
        {
            Blueprint plan = Hut();
            var have = new Dictionary<string, int> { { "wood", 1 }, { "nails", 1000 }, { "brick", 1000 } };
            List<ItemShortfall> missing = plan.Shortfall(_registry, item => have.TryGetValue(item, out int n) ? n : 0);
            Assert.That(missing.Count, Is.EqualTo(1));
            Assert.That(missing[0].Item, Is.EqualTo("wood"));
            Assert.That(missing[0].Missing, Is.EqualTo(plan.TotalCost(_registry)["wood"] - 1));
            Assert.That(plan.Shortfall(_registry, item => 1000), Is.Empty);
        }

        [Test]
        public void TheTextRoundTripsAndIsPlain()
        {
            Blueprint plan = Hut();
            string text = plan.ToText();
            Assert.That(text, Does.StartWith("cepaplan 1\nsize 3 2\n"));
            Assert.That(Blueprint.FromText(text).ToText(), Is.EqualTo(text));
            Assert.That(Blueprint.FromText(text.Replace("\n", "\r\n")).Pieces.Count, Is.EqualTo(plan.Pieces.Count), "Windows line endings");
        }

        [Test]
        public void BadFilesAreRefusedWithAReason()
        {
            Assert.Throws<FormatException>(() => Blueprint.FromText("hello"));
            Assert.Throws<FormatException>(() => Blueprint.FromText("cepaplan 2\nsize 1 1\n"));
            Assert.Throws<FormatException>(() => Blueprint.FromText("cepaplan 1\nsize 0 5\n"));
            Assert.Throws<FormatException>(() => Blueprint.FromText("cepaplan 1\nsize 99 5\n"));
            Assert.Throws<FormatException>(() => Blueprint.FromText("cepaplan 1\nsize 2 2\nwall_wood 5 0 0 0\n"));
            Assert.Throws<FormatException>(() => Blueprint.FromText("cepaplan 1\nsize 2 2\nwall_wood 0 0 9 0\n"));
            Assert.Throws<FormatException>(() => Blueprint.FromText("cepaplan 1\nsize 2 2\nwall_wood 0 0 0 300\n"));
            Assert.Throws<FormatException>(() => Blueprint.FromText("cepaplan 1\nsize 2 2\nwall_wood zero 0 0 0\n"));
            FormatException e = Assert.Throws<FormatException>(() => Blueprint.FromText("cepaplan 1\nsize 2 2\nwall_wood 0 0 0\n"));
            Assert.That(e.Message, Does.Contain("Line 3"));
        }

        [Test]
        public void AnEmptyAreaMakesAnEmptyPlan()
        {
            Assert.That(Blueprint.Capture(_world, 1, 1, 3, 3).IsEmpty, Is.True);
        }
    }
}
