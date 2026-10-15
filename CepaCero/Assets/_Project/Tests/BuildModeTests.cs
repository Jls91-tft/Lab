#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Map;
using CepaCero.Net;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Unity.Prototype;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;
using Verdict = CepaCero.Unity.Prototype.PrototypeBuildMode.Verdict;

namespace CepaCero.Unity.Tests
{
    public class BuildModeTests
    {
        private const string MapPath = "packages/com.cepacero.core/Tests/Fixtures/synthetic.map.json";

        private MapImportResult _map;
        private GameSession _session;
        private PlayerSettings _settings;
        private Int2 _home;

        [SetUp]
        public void SetUp()
        {
            GameData data = RepoData.LoadGameData();
            _map = RepoData.LoadMap(MapPath);
            _settings = new PlayerSettings();
            _session = new GameSession(new GameSettings(), _settings, _map.Blocks, _map.Ground, data, _map.Buildings);
            _home = FindOpenDirt();
            _session.AddPlayer(new Float2(_home.X + 0.5f, _home.Y + 0.5f));
        }

        private PrototypeBuildMode.Context Ctx(bool stocked = true)
        {
            Inventory inv = _session.Inventories[0];
            if (stocked && inv.Bag.Count == 0)
            {
                RecipeDefinition r = _session.Recipes.BuildRecipe("wood_wall");
                foreach (Ingredient ing in r.ingredients)
                {
                    inv.Add(inv.Catalog.IndexOf(ing.item), ing.count * 20);
                }
            }
            return new PrototypeBuildMode.Context(_map.Blocks, _map.Ground, _settings, _session.Players[0].Position,
                _session.Players, inv, _session.Recipes);
        }

        private Int2 FindOpenDirt()
        {
            for (int y = 2; y < _map.Blocks.Height - 2; y++)
            {
                for (int x = 2; x < _map.Blocks.Width - 4; x++)
                {
                    bool open = true;
                    for (int dx = -1; dx <= 3 && open; dx++)
                    {
                        open = _map.Ground.Get(x + dx, y) == GroundType.Dirt && _map.Blocks.GetBlock(x + dx, y).IsAir;
                    }
                    if (open)
                    {
                        return new Int2(x, y);
                    }
                }
            }
            Assert.Fail("no open dirt in the fixture");
            return default;
        }

        [Test]
        public void DragMakesLinesAndRoomOutlines()
        {
            var cells = new List<Int2>();
            PrototypeBuildMode.Shape(new Int2(0, 0), new Int2(3, 1), false, cells);
            Assert.That(cells, Is.EqualTo(new[] { new Int2(0, 0), new Int2(1, 0), new Int2(2, 0), new Int2(3, 0) }));
            cells.Clear();
            PrototypeBuildMode.Shape(new Int2(0, 0), new Int2(2, 2), true, cells);
            Assert.That(cells.Count, Is.EqualTo(8), "3x3 outline");
            Assert.That(cells.Contains(new Int2(1, 1)), Is.False, "the inside stays free");
        }

        [Test]
        public void GhostTellsWhyNot()
        {
            // Before stocking: the bag starts empty in this session.
            Assert.That(PrototypeBuildMode.Affordable("wood_wall", Ctx(stocked: false)), Is.EqualTo(0),
                "an empty bag pays for nothing");

            PrototypeBuildMode.Context ctx = Ctx();
            Assert.That(PrototypeBuildMode.Affordable("wood_wall", ctx), Is.EqualTo(20));
            Assert.That(PrototypeBuildMode.Check(new Int2(_home.X + 1, _home.Y), "wood_wall", ctx), Is.EqualTo(Verdict.Ok));
            Assert.That(PrototypeBuildMode.Check(_home, "wood_wall", ctx), Is.EqualTo(Verdict.Occupied), "you stand there");
            Assert.That(PrototypeBuildMode.Check(new Int2(_home.X + 30, _home.Y), "wood_wall", ctx), Is.Not.EqualTo(Verdict.Ok));
        }

        [Test]
        public void QueueSendsNoFasterThanTheHostAccepts()
        {
            var mode = new PrototypeBuildMode();
            mode.Toggle();
            PrototypeBuildMode.Context ctx = Ctx();
            mode.BeginDrag(new Int2(_home.X + 1, _home.Y));
            mode.UpdatePreview(new Int2(_home.X + 2, _home.Y), false, ctx);
            Assert.That(mode.EndDrag(), Is.EqualTo(2));

            Assert.That(mode.TryNext(0f, ctx, out PlayerIntent first), Is.True);
            Assert.That(first.Type, Is.EqualTo(IntentType.PlaceBlock));
            Assert.That(mode.TryNext(0.1f, ctx, out _), Is.False, "still cooling down");
            Assert.That(mode.TryNext(_settings.ActionCooldown + 0.05f, ctx, out _), Is.True);
            Assert.That(mode.Queue.Count, Is.EqualTo(0));
        }

        [Test]
        public void LeavingTheModeCancelsThePlan()
        {
            var mode = new PrototypeBuildMode();
            mode.Toggle();
            PrototypeBuildMode.Context ctx = Ctx();
            mode.BeginDrag(new Int2(_home.X + 1, _home.Y));
            mode.UpdatePreview(new Int2(_home.X + 3, _home.Y), false, ctx);
            mode.EndDrag();
            Assert.That(mode.Queue.Count, Is.GreaterThan(0));
            mode.Toggle();
            Assert.That(mode.Queue.Count, Is.EqualTo(0));
        }
    }
}
#endif
