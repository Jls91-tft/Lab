using System.Collections.Generic;
using System.Linq;
using CepaCero.Building;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Net;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Core
{
    /// <summary>Building with the item in the hand (D-022 phase D): palette, cursor, click resolver, room planner and the host's replace / dismantle / undo.</summary>
    public class HandBuildingTests
    {
        private GameData _data;
        private BlockRegistry _registry;
        private BuildPalette _palette;

        [SetUp]
        public void SetUp()
        {
            _data = Fixtures.GameDataWithConstruction();
            _registry = BlockRegistry.CreateDefault(_data.Construction);
            _palette = new BuildPalette(_data.Construction);
        }

        private ushort Id(string key) => _registry.Get(key).Id;

        private ushort Item(string id) => _data.Items.IndexOf(id);

        // ------------------------------------------------------------------ palette and cursor
        [Test]
        public void EveryMaterialHasItsOwnItemAndItsShapesInOrder()
        {
            Assert.That(_palette.MaterialOf("wood"), Is.EqualTo("wood"));
            Assert.That(_palette.MaterialOf("rebar"), Is.EqualTo("rebar"), "rebar, not the concrete mix it shares");
            Assert.That(_palette.MaterialOf("concrete_mix"), Is.EqualTo("concrete"));
            Assert.That(_palette.MaterialOf("nails"), Is.Null, "nails serve many materials");
            IReadOnlyList<BuildPiece> wood = _palette.PiecesFor("wood");
            Assert.That(wood[0].Key, Is.EqualTo("wall_wood"));
            Assert.That(wood.Select(p => p.ShapeId), Does.Contain("door").And.Contain("stairs"));
            foreach (BuildPiece p in wood.Concat(_palette.PiecesFor("brick")))
            {
                Assert.That(_registry.TryGet(p.Key, out _), Is.True, p.Key);
            }
        }

        [Test]
        public void RoofsComeWithTheirIngredientAndStrawBuildsOnlyRoofs()
        {
            Assert.That(_palette.PiecesFor("brick").Select(p => p.Key), Does.Contain("roof_tile"));
            Assert.That(_palette.PiecesFor("straw").Select(p => p.Key), Is.EqualTo(new[] { "roof_thatch" }));
            Assert.That(_palette.Builds("bat"), Is.False);
        }

        [Test]
        public void ABlockKnowsWhichItemBuildsIt()
        {
            Assert.That(_palette.ItemOf(_registry.Get("wall_brick")), Is.EqualTo("brick"));
            Assert.That(_palette.ItemOf(_registry.Get("door_wood")), Is.EqualTo("wood"));
            Assert.That(_palette.ItemOf(_registry.Get("wall_rebar")), Is.EqualTo("rebar"));
            Assert.That(_palette.ItemOf(_registry.Get("roof_thatch")), Is.EqualTo("straw"));
            Assert.That(_palette.ItemOf(_registry.Get("building_wall")), Is.Null);
        }

        [Test]
        public void TheCursorRemembersTheShapeAcrossMaterialsAndWraps()
        {
            var cursor = new BuildCursor();
            Assert.That(cursor.Current(_palette, "wood").Value.Key, Is.EqualTo("wall_wood"));
            cursor.CycleShape(_palette, "wood", 1);
            cursor.CycleShape(_palette, "wood", 1);
            string shape = cursor.ShapeId;
            Assert.That(cursor.Current(_palette, "brick").Value.ShapeId, Is.EqualTo(shape), "same shape with another material");
            cursor.CycleShape(_palette, "wood", -20);
            Assert.That(_palette.PiecesFor("wood").Select(p => p.ShapeId), Does.Contain(cursor.ShapeId));
            Assert.That(cursor.Current(_palette, "bat"), Is.Null);
        }

        [Test]
        public void ColourAndRotationWrapAndGoIntoTheVariant()
        {
            var cursor = new BuildCursor();
            cursor.CycleColor(-1);
            Assert.That(cursor.Color, Is.EqualTo(BuildCursor.ColorCount));
            cursor.CycleColor(1);
            Assert.That(cursor.Color, Is.Zero);
            cursor.CycleColor(3);
            cursor.Rotate(-1);
            Assert.That(cursor.Rotation, Is.EqualTo(3));
            Assert.That(cursor.Variant, Is.EqualTo(BlockVariant.Make(3, 3)));
        }

        [Test]
        public void MiddleClickCopiesShapeColourAndRotation()
        {
            var cursor = new BuildCursor();
            cursor.Copy(_registry.Get("door_brick"), BlockVariant.Make(5, 2));
            Assert.That(cursor.ShapeId, Is.EqualTo("door"));
            Assert.That(cursor.Color, Is.EqualTo(5));
            Assert.That(cursor.Rotation, Is.EqualTo(2));
            cursor.Copy(_registry.Get("roof_tile"), 0);
            Assert.That(cursor.ShapeId, Is.EqualTo(BuildPalette.RoofShape));
        }

        // ------------------------------------------------------------------ the hand decides the click
        private ItemDefinition Def(string id) => _data.Items[Item(id)];

        private static readonly ClickTarget Wall = new ClickTarget(true, false, true);
        private static readonly ClickTarget Door = new ClickTarget(true, true, true);
        private static readonly ClickTarget Nothing = new ClickTarget(false, false, false);
        private static readonly ClickTarget MapWall = new ClickTarget(true, false, false);

        [Test]
        public void WeaponsAttackToolsHitAndMaterialsPlace()
        {
            Assert.That(HandResolver.Resolve(Def("bat"), false, Wall, false), Is.EqualTo(HandAction.MeleeAttack));
            Assert.That(HandResolver.Resolve(Def("pistol"), false, Nothing, false), Is.EqualTo(HandAction.RangedAttack));
            Assert.That(HandResolver.Resolve(Def("axe"), false, Wall, false), Is.EqualTo(HandAction.HitBlock));
            Assert.That(HandResolver.Resolve(Def("axe"), false, Nothing, false), Is.EqualTo(HandAction.MeleeAttack), "the axe is a weapon too");
            Assert.That(HandResolver.Resolve(Def("shovel"), false, MapWall, false), Is.EqualTo(HandAction.HitBlock));
            Assert.That(HandResolver.Resolve(Def("wood"), true, Nothing, false), Is.EqualTo(HandAction.Place));
            Assert.That(HandResolver.Resolve(Def("wood"), true, Wall, false), Is.EqualTo(HandAction.Place), "placing over a piece replaces it");
        }

        [Test]
        public void BareHandsHitBlocksOrPunch()
        {
            Assert.That(HandResolver.Resolve(null, false, Wall, false), Is.EqualTo(HandAction.HitBlock));
            Assert.That(HandResolver.Resolve(null, false, Nothing, false), Is.EqualTo(HandAction.MeleeAttack));
        }

        [Test]
        public void TheHammerOnlyDismantlesPiecesPlayersBuilt()
        {
            Assert.That(HandResolver.Resolve(Def("hammer"), false, Wall, false), Is.EqualTo(HandAction.Dismantle));
            Assert.That(HandResolver.Resolve(Def("hammer"), false, MapWall, false), Is.EqualTo(HandAction.HitBlock), "a map wall is hit, not taken down");
        }

        [Test]
        public void ConsumablesAreUsedWithTheClick()
        {
            Assert.That(HandResolver.Resolve(Def("bandage"), false, Nothing, false), Is.EqualTo(HandAction.UseItem));
            Assert.That(HandResolver.Resolve(Def("food_can"), false, Wall, false), Is.EqualTo(HandAction.UseItem));
        }

        [Test]
        public void TheRightButtonInteractsFirstThenRemovesWithMaterial()
        {
            Assert.That(HandResolver.Resolve(Def("wood"), true, Door, true), Is.EqualTo(HandAction.Interact));
            Assert.That(HandResolver.Resolve(Def("wood"), true, Wall, true), Is.EqualTo(HandAction.Dismantle));
            Assert.That(HandResolver.Resolve(Def("wood"), true, MapWall, true), Is.EqualTo(HandAction.None));
            Assert.That(HandResolver.Resolve(Def("bat"), false, Wall, true), Is.EqualTo(HandAction.None));
            Assert.That(HandResolver.Resolve(null, false, Door, true), Is.EqualTo(HandAction.Interact));
        }

        // ------------------------------------------------------------------ rooms
        [Test]
        public void ARoomHasWallsAroundADoorAndFloorAndRoofInside()
        {
            var cells = new List<RoomCell>();
            RoomPlanner.Plan(new Int2(10, 10), new Int2(14, 13), RoomPlanner.DoorSide.South, true, true, cells);
            Assert.That(cells.Count(c => c.Role == RoomRole.Wall), Is.EqualTo(5 * 4 - 3 * 2 - 1), "the perimeter minus the door");
            Assert.That(cells.Count(c => c.Role == RoomRole.Door), Is.EqualTo(1));
            Assert.That(cells.Single(c => c.Role == RoomRole.Door).Cell, Is.EqualTo(new Int2(12, 10)));
            Assert.That(cells.Count(c => c.Role == RoomRole.Floor), Is.EqualTo(3 * 2));
            Assert.That(cells.Count(c => c.Role == RoomRole.Roof), Is.EqualTo(3 * 2));
            Assert.That(cells.Select(c => c.Cell).Where(c => c.X == 12 && c.Y == 10).Count(), Is.EqualTo(1), "no wall where the door is");
            Assert.That(cells.TakeWhile(c => c.Role != RoomRole.Floor).All(c => c.Role == RoomRole.Wall || c.Role == RoomRole.Door), Is.True, "walls and door come first");
        }

        [Test]
        public void ATinyRoomIsJustWalls()
        {
            var cells = new List<RoomCell>();
            RoomPlanner.Plan(new Int2(3, 3), new Int2(4, 4), RoomPlanner.DoorSide.North, true, true, cells);
            Assert.That(cells.Count, Is.EqualTo(4));
            Assert.That(cells.All(c => c.Role == RoomRole.Wall), Is.True);
        }

        [Test]
        public void TheDoorFacesThePlayer()
        {
            Assert.That(RoomPlanner.SideFacing(new Int2(10, 10), new Int2(14, 13), new Float2(12.5f, 5f)), Is.EqualTo(RoomPlanner.DoorSide.South));
            Assert.That(RoomPlanner.SideFacing(new Int2(10, 10), new Int2(14, 13), new Float2(20f, 11f)), Is.EqualTo(RoomPlanner.DoorSide.East));
            Assert.That(RoomPlanner.SideFacing(new Int2(10, 10), new Int2(14, 13), new Float2(12f, 30f)), Is.EqualTo(RoomPlanner.DoorSide.North));
            Assert.That(RoomPlanner.SideFacing(new Int2(10, 10), new Int2(14, 13), new Float2(2f, 11f)), Is.EqualTo(RoomPlanner.DoorSide.West));
        }

        // ------------------------------------------------------------------ host: replace, repaint, dismantle, undo
        private AsciiMap Open() => new AsciiMap(_registry, Enumerable.Repeat(new string('.', 24), 24).ToArray());

        private GameSession Session(AsciiMap map, BuildCostMode cost = BuildCostMode.Normal) =>
            new GameSession(new GameSettings { TickRate = 20, HordeBase = 0, BuildCost = cost }, new PlayerSettings(), map.Blocks, map.Ground, _data);

        private static IntentResult Do(GameSession s, int player, PlayerIntent intent)
        {
            s.QueueIntent(player, intent);
            var events = new List<SessionEvent>();
            for (int t = 0; t < 12; t++)
            {
                s.Step();
                s.DrainEvents(events);
            }
            return events.First(e => e.Type == SessionEventType.IntentResolved && e.Player == player).Result;
        }

        private int Count(GameSession s, int p, string item) => s.Inventories[p].Count(Item(item));

        private (GameSession s, AsciiMap map, int p) Match(BuildCostMode cost = BuildCostMode.Normal)
        {
            AsciiMap map = Open();
            GameSession s = Session(map, cost);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            s.Inventories[p].Add(Item("wood"), 40);
            s.Inventories[p].Add(Item("nails"), 20);
            s.Inventories[p].Add(Item("brick"), 20);
            return (s, map, p);
        }

        [Test]
        public void ADoorOnAWallReplacesItAndReturnsHalfTheWall()
        {
            var (s, map, p) = Match();
            Assert.That(Do(s, p, PlayerIntent.Place(6, 6, Id("wall_wood"))), Is.EqualTo(IntentResult.Ok));
            int wood = Count(s, p, "wood"), nails = Count(s, p, "nails");
            Assert.That(Do(s, p, PlayerIntent.Place(6, 6, Id("door_wood"))), Is.EqualTo(IntentResult.Ok));
            Assert.That(map.Blocks.GetBlock(6, 6).Key, Is.EqualTo("door_wood"));
            int doorWood = _registry.Get("door_wood").Build.Cost.First(c => c.Item == "wood").Count;
            int wallWood = _registry.Get("wall_wood").Build.Cost.First(c => c.Item == "wood").Count;
            Assert.That(Count(s, p, "wood"), Is.EqualTo(wood - doorWood + (int)System.Math.Round(wallWood * 0.5, System.MidpointRounding.AwayFromZero)));
            Assert.That(Count(s, p, "nails"), Is.LessThanOrEqualTo(nails));
        }

        [Test]
        public void AMapBuildingWallIsNeverReplaced()
        {
            var (s, map, p) = Match();
            map.Blocks.SetBlock(6, 6, _registry.Get("building_wall").Id);
            Assert.That(Do(s, p, PlayerIntent.Place(6, 6, Id("wall_wood"))), Is.EqualTo(IntentResult.Occupied));
            Assert.That(map.Blocks.GetBlock(6, 6).Key, Is.EqualTo("building_wall"));
        }

        [Test]
        public void RepaintingIsFreeAndKeepsTheHealth()
        {
            var (s, map, p) = Match();
            Do(s, p, PlayerIntent.Place(6, 6, Id("wall_wood")));
            map.Blocks.Damage(6, 6, 30);
            ushort health = map.Blocks.GetHealth(6, 6);
            int wood = Count(s, p, "wood");
            Assert.That(Do(s, p, PlayerIntent.Place(6, 6, Id("wall_wood"), BlockVariant.Make(4, 1))), Is.EqualTo(IntentResult.Ok));
            Assert.That(Count(s, p, "wood"), Is.EqualTo(wood));
            Assert.That(map.Blocks.GetHealth(6, 6), Is.EqualTo(health));
            Assert.That(BlockVariant.Color(map.Blocks.GetVariant(6, 6, 0, BuildLayer.Structure)), Is.EqualTo(4));
        }

        [Test]
        public void DismantlingRefundsHalfAndOnlyConstructionPieces()
        {
            var (s, map, p) = Match();
            Do(s, p, PlayerIntent.Place(6, 6, Id("wall_brick")));
            int bricks = Count(s, p, "brick");
            Assert.That(Do(s, p, PlayerIntent.Dismantle(6, 6)), Is.EqualTo(IntentResult.Ok));
            Assert.That(map.Blocks.GetBlock(6, 6).IsAir, Is.True);
            int cost = _registry.Get("wall_brick").Build.Cost.First(c => c.Item == "brick").Count;
            Assert.That(Count(s, p, "brick"), Is.EqualTo(bricks + (int)System.Math.Round(cost * 0.5, System.MidpointRounding.AwayFromZero)));

            map.Blocks.SetBlock(7, 6, _registry.Get("building_wall").Id);
            Assert.That(Do(s, p, PlayerIntent.Dismantle(7, 6)), Is.EqualTo(IntentResult.InvalidTarget));
            Assert.That(Do(s, p, PlayerIntent.Dismantle(8, 6)), Is.EqualTo(IntentResult.InvalidTarget), "nothing there");
        }

        [Test]
        public void DismantlingPicksTheTopPieceAndCanTargetALayer()
        {
            var (s, map, p) = Match();
            Do(s, p, PlayerIntent.Place(6, 6, Id("floor_wood")));
            Do(s, p, PlayerIntent.Place(6, 6, Id("wall_wood")));
            Assert.That(Do(s, p, PlayerIntent.Dismantle(6, 6)), Is.EqualTo(IntentResult.Ok));
            Assert.That(map.Blocks.GetBlock(6, 6).IsAir, Is.True, "the wall goes first");
            Assert.That(map.Blocks.GetBlock(6, 6, 0, BuildLayer.Floor).IsAir, Is.False);
            Do(s, p, PlayerIntent.Place(6, 6, Id("wall_wood")));
            Assert.That(Do(s, p, PlayerIntent.Dismantle(6, 6, (int)BuildLayer.Floor + 1)), Is.EqualTo(IntentResult.Ok));
            Assert.That(map.Blocks.GetBlock(6, 6, 0, BuildLayer.Floor).IsAir, Is.True);
            Assert.That(map.Blocks.GetBlock(6, 6).IsAir, Is.False, "the wall stays");
        }

        [Test]
        public void AnOpenDoorRefundsLikeAClosedOne()
        {
            var (s, map, p) = Match();
            Do(s, p, PlayerIntent.Place(6, 6, Id("door_wood")));
            Assert.That(Do(s, p, PlayerIntent.Door(6, 6)), Is.EqualTo(IntentResult.Ok));
            int wood = Count(s, p, "wood");
            Assert.That(Do(s, p, PlayerIntent.Dismantle(6, 6)), Is.EqualTo(IntentResult.Ok));
            Assert.That(Count(s, p, "wood"), Is.GreaterThan(wood));
        }

        [Test]
        public void UndoTakesBackTheLastPlacementsInOrderWithAFullRefund()
        {
            var (s, map, p) = Match();
            int wood = Count(s, p, "wood"), nails = Count(s, p, "nails");
            Do(s, p, PlayerIntent.Place(6, 6, Id("wall_wood")));
            Do(s, p, PlayerIntent.Place(7, 6, Id("wall_wood")));
            Assert.That(Do(s, p, PlayerIntent.Undo()), Is.EqualTo(IntentResult.Ok));
            Assert.That(map.Blocks.GetBlock(7, 6).IsAir, Is.True);
            Assert.That(map.Blocks.GetBlock(6, 6).IsAir, Is.False);
            Assert.That(Do(s, p, PlayerIntent.Undo()), Is.EqualTo(IntentResult.Ok));
            Assert.That(Count(s, p, "wood"), Is.EqualTo(wood));
            Assert.That(Count(s, p, "nails"), Is.EqualTo(nails));
            Assert.That(Do(s, p, PlayerIntent.Undo()), Is.EqualTo(IntentResult.InvalidTarget), "nothing left to undo");
        }

        [Test]
        public void UndoSkipsWhatWasBrokenAndDoesNotTouchOthersPieces()
        {
            var (s, map, p) = Match();
            int q = s.AddPlayer(new Float2(5.5f, 7.5f));
            s.Inventories[q].Add(Item("wood"), 10);
            s.Inventories[q].Add(Item("nails"), 10);
            Do(s, p, PlayerIntent.Place(6, 6, Id("wall_wood")));
            Do(s, q, PlayerIntent.Place(6, 7, Id("wall_wood")));
            map.Blocks.SetBlock(6, 6, BlockRegistry.AirId);                        // a zombie broke it
            Assert.That(Do(s, p, PlayerIntent.Undo()), Is.EqualTo(IntentResult.InvalidTarget));
            Assert.That(map.Blocks.GetBlock(6, 7).IsAir, Is.False, "the other player's wall stays");
        }

        [Test]
        public void ReplacingIsNotRecordedForUndo()
        {
            var (s, map, p) = Match();
            Do(s, p, PlayerIntent.Place(6, 6, Id("wall_wood")));
            Do(s, p, PlayerIntent.Place(6, 6, Id("door_wood")));
            Assert.That(Do(s, p, PlayerIntent.Undo()), Is.EqualTo(IntentResult.InvalidTarget), "the wall that was recorded is gone");
            Assert.That(map.Blocks.GetBlock(6, 6).Key, Is.EqualTo("door_wood"));
        }

        [Test]
        public void CheapBuildingHalvesTheCostAndFreeCostsNothing()
        {
            var (cheap, _, p) = Match(BuildCostMode.Cheap);
            int full = _registry.Get("wall_wood").Build.Cost.First(c => c.Item == "wood").Count;
            int wood = Count(cheap, p, "wood");
            Do(cheap, p, PlayerIntent.Place(6, 6, Id("wall_wood")));
            Assert.That(wood - Count(cheap, p, "wood"), Is.EqualTo((full + 1) / 2));

            var (free, map, q) = Match(BuildCostMode.Free);
            free.Inventories[q].Remove(Item("wood"), 40);
            free.Inventories[q].Remove(Item("nails"), 20);
            Assert.That(Do(free, q, PlayerIntent.Place(6, 6, Id("wall_wood"))), Is.EqualTo(IntentResult.Ok), "creative mode needs no material");
            Assert.That(Do(free, q, PlayerIntent.Dismantle(6, 6)), Is.EqualTo(IntentResult.Ok));
            Assert.That(Count(free, q, "wood"), Is.Zero, "and refunds nothing");
        }

        [Test]
        public void TheBagHasNineQuickSlots()
        {
            Inventory inv = Inventory.ForPlayer(_data.Items);
            Assert.That(inv.HotbarSlots, Is.EqualTo(9));
            Assert.That(inv.BagSlots, Is.EqualTo(27));
        }

        [Test]
        public void IntentsForDismantleAndUndoTravel()
        {
            var w = new ByteWriter();
            PlayerIntent.Dismantle(3, 4, 2).Write(w);
            PlayerIntent back = PlayerIntent.Read(new ByteReader(w.Segment));
            Assert.That(back.Type, Is.EqualTo(IntentType.Dismantle));
            Assert.That((back.X, back.Y, back.Arg), Is.EqualTo((3, 4, 2)));
            Assert.That(PlayerIntent.Undo().IsInventoryAction, Is.False);
        }
    }
}
