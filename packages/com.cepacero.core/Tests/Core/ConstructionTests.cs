using System;
using System.Collections.Generic;
using System.Linq;
using CepaCero.Building;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Net;
using CepaCero.Net.Protocol;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Core
{
    /// <summary>The construction data (D-022): blocks from shape × material, costs, tools and the network/save contract.</summary>
    public class ConstructionTests
    {
        private GameData _data;
        private BlockRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _data = Fixtures.GameDataWithConstruction();
            _registry = BlockRegistry.CreateDefault(_data.Construction);
        }

        private static GameSettings Calm() => new GameSettings
        {
            TickRate = 20, DaySeconds = 600, DuskSeconds = 10, NightSeconds = 60, DawnSeconds = 10, HordeBase = 0,
        };

        private AsciiMap Open(int size) => new AsciiMap(_registry, Enumerable.Repeat(new string('.', size), size).ToArray());

        private GameSession Session(AsciiMap map) =>
            new GameSession(Calm(), new PlayerSettings(), map.Blocks, map.Ground, _data);

        private ushort Item(string id) => _data.Items.IndexOf(id);

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

        private void Equip(GameSession s, int player, string id)
        {
            Inventory inv = s.Inventories[player];
            inv.Add(Item(id), 1);
            inv.MoveToHotbar(inv.Bag.ToList().FindIndex(st => st.Item == Item(id)), 0);
            inv.SelectHotbar(0);
        }

        [Test]
        public void TheRegistryAppendsEveryBlockAfterTheLegacyOnes()
        {
            BlockRegistry legacy = BlockRegistry.CreateDefault();
            Assert.That(_registry.Count, Is.EqualTo(legacy.Count + _data.Construction.blocks.Length));
            for (ushort id = 0; id < legacy.Count; id++)
            {
                Assert.That(_registry[id].Key, Is.EqualTo(legacy[id].Key), "legacy ids never move");
            }
            Assert.That(_registry.Get("wall_brick").Id, Is.EqualTo((ushort)(legacy.Count + Array.FindIndex(_data.Construction.blocks, b => b.key == "wall_brick"))));
            Assert.That(_data.Construction.materials.Length, Is.GreaterThanOrEqualTo(14));
            Assert.That(_data.Construction.blocks.Length, Is.GreaterThan(150));
        }

        [Test]
        public void BlocksCarryTheirHealthSolidityAndCost()
        {
            BlockType brick = _registry.Get("wall_brick");
            Assert.That(brick.MaxHealth, Is.EqualTo(400));
            Assert.That(brick.IsSolid, Is.True);
            Assert.That(brick.Material, Is.EqualTo(BlockMaterial.Concrete));
            Assert.That(brick.Build.Tool, Is.EqualTo("pickaxe"));
            Assert.That(brick.Build.Cost.Single().Item, Is.EqualTo("brick"));
            Assert.That(_registry.Get("gate_wood").Build.Width, Is.EqualTo(2));
            Assert.That(_registry.Get("floor_wood").Build.Layer, Is.EqualTo(BuildLayer.Floor));
            Assert.That(_registry.Get("window_glass").Build.SeeThrough, Is.True);
            Assert.That(_registry.Get("earth").Build.Drops.Single().Item, Is.EqualTo("earth"));
            Assert.That(_registry.Get("wood_wall").Build, Is.Null, "legacy blocks keep their old recipes");
        }

        [Test]
        public void DoorsToggleBetweenClosedAndOpen()
        {
            AsciiMap map = Open(10);
            BlockType closed = _registry.Get("door_steel");
            BlockType open = _registry.Get("door_steel_open");
            Assert.That(closed.ToggleTo, Is.EqualTo(open.Id));
            Assert.That(open.ToggleTo, Is.EqualTo(closed.Id));
            Assert.That(open.IsSolid, Is.False);
            map.Blocks.SetBlock(4, 4, closed.Id);
            Assert.That(map.Blocks.Toggle(4, 4), Is.True);
            Assert.That(map.Blocks.GetTypeId(4, 4), Is.EqualTo(open.Id));
            Assert.That(map.Blocks.Toggle(4, 4), Is.True);
            Assert.That(map.Blocks.GetTypeId(4, 4), Is.EqualTo(closed.Id));
        }

        [Test]
        public void PlacingAPieceCostsItsMaterialsAndNothingIsTakenIfShort()
        {
            AsciiMap map = Open(20);
            GameSession s = Session(map);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            ushort wall = _registry.Get("wall_brick").Id;
            Assert.That(Do(s, p, PlayerIntent.Place(6, 7, wall)), Is.EqualTo(IntentResult.MissingIngredients));
            s.Inventories[p].Add(Item("brick"), 5);
            Assert.That(Do(s, p, PlayerIntent.Place(6, 7, wall)), Is.EqualTo(IntentResult.Ok));
            Assert.That(s.Inventories[p].Count(Item("brick")), Is.EqualTo(2));
            Assert.That(Do(s, p, PlayerIntent.Place(7, 6, wall)), Is.EqualTo(IntentResult.MissingIngredients));
            Assert.That(s.Inventories[p].Count(Item("brick")), Is.EqualTo(2), "a failed payment takes nothing");
            Assert.That(map.Blocks.GetBlock(6, 7).Key, Is.EqualTo("wall_brick"));
        }

        [Test]
        public void MaterialsCombineAndCostsRoundUp()
        {
            AsciiMap map = Open(20);
            GameSession s = Session(map);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            s.Inventories[p].Add(Item("wood"), 2);
            s.Inventories[p].Add(Item("nails"), 1);
            Assert.That(Do(s, p, PlayerIntent.Place(6, 7, _registry.Get("halfwall_wood").Id)), Is.EqualTo(IntentResult.Ok));      // half a wall: 1 wood + 1 nail
            Assert.That(s.Inventories[p].Count(Item("wood")), Is.EqualTo(1));
            Assert.That(s.Inventories[p].Count(Item("nails")), Is.EqualTo(0));
        }

        [Test]
        public void OpenVariantsCannotBePlacedDirectly()
        {
            AsciiMap map = Open(20);
            GameSession s = Session(map);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            s.Inventories[p].Add(Item("wood"), 20);
            s.Inventories[p].Add(Item("nails"), 10);
            Assert.That(Do(s, p, PlayerIntent.Place(6, 7, _registry.Get("door_wood_open").Id)), Is.Not.EqualTo(IntentResult.Ok));
        }

        [Test]
        public void TheRightToolBreaksTheWallFasterThanTheWrongOneOrBareHands()
        {
            AsciiMap map = Open(20);
            GameSession s = Session(map);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            ushort wood = _registry.Get("wall_wood").Id;
            map.Blocks.SetBlock(6, 6, wood);
            map.Blocks.SetBlock(6, 7, wood);
            map.Blocks.SetBlock(6, 5, wood);

            Do(s, p, PlayerIntent.Hit(6, 6));                                               // bare hands, a wall made for an axe
            int hands = map.Blocks.GetHealth(6, 6);
            Equip(s, p, "pickaxe");
            Do(s, p, PlayerIntent.Hit(6, 7));                                               // pickaxe: not the tool for wood
            int pickaxe = map.Blocks.GetHealth(6, 7);
            Equip(s, p, "axe");
            Do(s, p, PlayerIntent.Hit(6, 5));                                               // the axe
            int axe = map.Blocks.GetHealth(6, 5);

            Assert.That(100 - hands, Is.EqualTo(2), "8 damage × 0.25");
            Assert.That(100 - pickaxe, Is.EqualTo(10), "40 damage × 0.25");
            Assert.That(100 - axe, Is.EqualTo(40), "full damage");
        }

        [Test]
        public void ConstructionDataIsOptional()
        {
            var plain = new GameData(Fixtures.Catalog(), Fixtures.Items(), Fixtures.RecipesData(), Fixtures.LootData());
            Assert.That(plain.Construction, Is.Null);
            AsciiMap map = new AsciiMap("....", "....");
            var s = new GameSession(Calm(), new PlayerSettings(), map.Blocks, map.Ground, plain);
            Assert.That(s.Rules.WrongToolFactor, Is.EqualTo(0.25f));
        }

        [Test]
        public void SavesKeepConstructionBlocksAndNeedTheirRegistry()
        {
            AsciiMap map = Open(20);
            GameSession original = Session(map);
            original.AddPlayer(new Float2(5.5f, 5.5f));
            map.Blocks.SetBlock(8, 8, _registry.Get("wall_stone").Id);
            map.Blocks.SetBlock(9, 8, _registry.Get("door_glass").Id);
            var writer = new ByteWriter();
            SaveGame.Write(original, "open", writer);
            byte[] bytes = writer.ToArray();

            AsciiMap again = Open(20);
            GameSession loaded = Session(again);
            SaveGame.Load(bytes, loaded, "open");
            Assert.That(again.Blocks.GetBlock(8, 8).Key, Is.EqualTo("wall_stone"));
            Assert.That(again.Blocks.GetBlock(9, 8).Key, Is.EqualTo("door_glass"));

            var legacy = new AsciiMap(Enumerable.Repeat(new string('.', 20), 20).ToArray());          // a build without the construction data
            var legacySession = new GameSession(Calm(), new PlayerSettings(), legacy.Blocks, legacy.Ground, Fixtures.GameData());
            Assert.Throws<FormatException>(() => SaveGame.Load(bytes, legacySession, "open"));
        }

        [Test]
        public void HostAndClientMustAgreeOnTheRegistry()
        {
            AsciiMap hostMap = Open(20);
            GameSession host = Session(hostMap);
            host.AddPlayer(new Float2(4.5f, 4.5f));
            int remote = host.AddPlayer(new Float2(6.5f, 4.5f));
            hostMap.Blocks.SetBlock(10, 10, _registry.Get("wall_rebar").Id);
            var broadcaster = new HostBroadcaster(host, "open");
            var join = new ByteWriter();
            broadcaster.WriteJoin(remote, join);

            AsciiMap clientMap = Open(20);
            var client = new ClientMatchState("open", clientMap.Blocks, clientMap.Ground, _data);
            client.Receive(join.Segment, 0);
            Assert.That(clientMap.Blocks.GetBlock(10, 10).Key, Is.EqualTo("wall_rebar"));

            var legacy = new AsciiMap(Enumerable.Repeat(new string('.', 20), 20).ToArray());
            var stranger = new ClientMatchState("open", legacy.Blocks, legacy.Ground, _data);
            Assert.Throws<FormatException>(() => stranger.Receive(join.Segment, 0));
        }
    }
}
