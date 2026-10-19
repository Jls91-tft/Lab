using System.Collections.Generic;
using System.Linq;
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
    /// <summary>Walking between levels, falling and digging (D-022 phase F).</summary>
    public class LevelsTests
    {
        private GameData _data;
        private BlockRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _data = Fixtures.GameDataWithConstruction();
            _registry = BlockRegistry.CreateDefault(_data.Construction);
        }

        private ushort Id(string key) => _registry.Get(key).Id;

        private ushort Item(string id) => _data.Items.IndexOf(id);

        private AsciiMap Open() => new AsciiMap(_registry, Enumerable.Repeat(new string('.', 24), 24).ToArray());

        private (GameSession s, AsciiMap map, int p) Match()
        {
            AsciiMap map = Open();
            var s = new GameSession(new GameSettings { TickRate = 20, HordeBase = 0 }, new PlayerSettings(), map.Blocks, map.Ground, _data);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            s.Inventories[p].Add(Item("wood"), 60);
            s.Inventories[p].Add(Item("nails"), 40);
            return (s, map, p);
        }

        private static IntentResult Do(GameSession s, int player, PlayerIntent intent, int ticks = 12)
        {
            s.QueueIntent(player, intent);
            var events = new List<SessionEvent>();
            for (int t = 0; t < ticks; t++)
            {
                s.Step();
                s.DrainEvents(events);
            }
            return events.First(e => e.Type == SessionEventType.IntentResolved && e.Player == player).Result;
        }

        private void Equip(GameSession s, int p, string id)
        {
            Inventory inv = s.Inventories[p];
            inv.Add(Item(id), 1);
            inv.MoveToHotbar(inv.Bag.ToList().FindIndex(st => st.Item == Item(id)), 0);
            inv.SelectHotbar(0);
        }

        private int Level(GameSession s, int p) => s.Players[p].Level;

        private void DigThrough(GameSession s, int p, int x, int y, int z)
        {
            for (int i = 0; i < 60; i++)
            {
                Do(s, p, PlayerIntent.Dig(x, y, z));
                if (!_isUndugAt(s, x, y, z))
                {
                    return;
                }
            }
            Assert.Fail("The cell never opened.");
        }

        private bool _isUndugAt(GameSession s, int x, int y, int z) => s.Blocks.IsUndug(x, y, z) || (z == 0 && s.Blocks.GetBlock(x, y, 0, BuildLayer.Structure).IsAir);

        /// <summary>A player standing in a pit at level -1.</summary>
        private (GameSession s, AsciiMap map, int p) Underground()
        {
            var (s, map, p) = Match();
            Equip(s, p, "shovel");
            for (int i = 0; i < 60 && s.Blocks.GetBlock(5, 5, 0, BuildLayer.Structure).Key != "pit"; i++)
            {
                Do(s, p, PlayerIntent.Dig(5, 5));
            }
            Assert.That(Do(s, p, PlayerIntent.Climb(false)), Is.EqualTo(IntentResult.Ok));
            return (s, map, p);
        }

        // ------------------------------------------------------------------ the ground below
        [Test]
        public void UndergroundIsSolidUntilDug()
        {
            var (_, map, _) = Match();
            BlockWorld w = map.Blocks;
            Assert.That(w.IsSolidAt(3, 3, -1), Is.True);
            Assert.That(w.IsUndug(3, 3, -1), Is.True);
            Assert.That(w.IsSolidAt(3, 3, 0), Is.False);
            Assert.That(w.IsSolidAt(3, 3, 1), Is.False, "the air above the street is open");
            w.SetBlock(3, 3, -1, BuildLayer.Floor, Id("cave"));
            Assert.That(w.IsSolidAt(3, 3, -1), Is.False);
            Assert.That(w.IsUndug(3, 3, -1), Is.False);
            Assert.That(w.IsSolidAt(-1, 3, -1), Is.True, "outside the map");
        }

        [Test]
        public void DiggingDownMakesAPitAndTheCellBelowOpen()
        {
            var (s, map, p) = Match();
            Equip(s, p, "shovel");
            int earth = s.Inventories[p].Count(Item("earth"));
            int digs = 0;
            while (map.Blocks.GetBlock(5, 6, 0, BuildLayer.Structure).Key != "pit" && digs++ < 60)
            {
                Assert.That(Do(s, p, PlayerIntent.Dig(5, 6)), Is.EqualTo(IntentResult.Ok));
            }
            Assert.That(digs, Is.GreaterThan(1), "it takes several swings");
            Assert.That(map.Blocks.GetBlock(5, 6, 0, BuildLayer.Structure).Key, Is.EqualTo("pit"));
            Assert.That(map.Blocks.GetBlock(5, 6, -1, BuildLayer.Floor).Key, Is.EqualTo("cave"));
            Assert.That(s.Inventories[p].Count(Item("earth")), Is.GreaterThan(earth), "the earth comes out");
            Assert.That(map.Blocks.IsSolidAt(5, 6, 0), Is.False, "a pit can be walked into");
        }

        [Test]
        public void TheWrongToolDigsSlower()
        {
            var (s, map, p) = Match();
            Equip(s, p, "pickaxe");
            var (s2, map2, p2) = Match();
            Equip(s2, p2, "shovel");
            int pick = 0, shovel = 0;
            while (map.Blocks.GetBlock(5, 6, 0, BuildLayer.Structure).Key != "pit" && pick < 200) { Do(s, p, PlayerIntent.Dig(5, 6)); pick++; }
            while (map2.Blocks.GetBlock(5, 6, 0, BuildLayer.Structure).Key != "pit" && shovel < 200) { Do(s2, p2, PlayerIntent.Dig(5, 6)); shovel++; }
            Assert.That(pick, Is.GreaterThan(shovel), "the pickaxe on earth is the wrong tool");
        }

        [Test]
        public void DiggingMakesNoiseAndNeedsReachAndAFreeCell()
        {
            var (s, map, p) = Match();
            Equip(s, p, "shovel");
            Assert.That(Do(s, p, PlayerIntent.Dig(20, 20)), Is.EqualTo(IntentResult.OutOfReach));
            map.Blocks.SetBlock(5, 6, Id("wall_wood"));
            Assert.That(Do(s, p, PlayerIntent.Dig(5, 6)), Is.EqualTo(IntentResult.InvalidTarget), "something is built there");
            Assert.That(Do(s, p, PlayerIntent.Dig(5, 7, 1)), Is.EqualTo(IntentResult.InvalidTarget), "wrong level in the intent");
            Do(s, p, PlayerIntent.Dig(6, 5));
            Assert.That(s.Rules.LastNoise, Is.GreaterThan(0f));
        }

        // ------------------------------------------------------------------ going down and up
        [Test]
        public void YouCanGoDownThePitAndComeBackUp()
        {
            var (s, map, p) = Underground();
            Assert.That(Level(s, p), Is.EqualTo(-1));
            Assert.That(s.Players[p].Position.Tile, Is.EqualTo(new Int2(5, 5)));
            Assert.That(Do(s, p, PlayerIntent.Climb(false)), Is.EqualTo(IntentResult.InvalidTarget), "nothing further down");
            Assert.That(Do(s, p, PlayerIntent.Climb(true)), Is.EqualTo(IntentResult.Ok));
            Assert.That(Level(s, p), Is.Zero);
        }

        [Test]
        public void ClimbingNeedsSomethingToClimbAndTheCooldownIsRespected()
        {
            var (s, _, p) = Match();
            Assert.That(Do(s, p, PlayerIntent.Climb(true)), Is.EqualTo(IntentResult.InvalidTarget));
            Assert.That(Do(s, p, PlayerIntent.Climb(false)), Is.EqualTo(IntentResult.InvalidTarget), "plain ground");
        }

        [Test]
        public void UndergroundYouAreStoppedByTheUndugWallsAndCanDigSideways()
        {
            var (s, map, p) = Underground();
            var move = PlayerInput.FromVector(new Float2(1f, 0f), false);
            for (int t = 0; t < 40; t++)
            {
                s.SetInput(p, move);
                s.Step();
            }
            Assert.That(s.Players[p].Position.X, Is.LessThan(6.7f), "solid ground blocks the way");
            Equip(s, p, "shovel");
            DigThrough(s, p, 6, 5, -1);
            Assert.That(map.Blocks.IsSolidAt(6, 5, -1), Is.False);
            for (int t = 0; t < 40; t++)
            {
                s.SetInput(p, move);
                s.Step();
            }
            Assert.That(s.Players[p].Position.X, Is.GreaterThan(6.2f), "the tunnel is open now");
            Assert.That(Level(s, p), Is.EqualTo(-1));
        }

        private (GameSession s, AsciiMap map, int p) ThirdLevel(string tool)
        {
            var (s, map, p) = Underground();
            map.Blocks.SetBlock(5, 5, -1, BuildLayer.Structure, Id("pit"));
            map.Blocks.SetBlock(5, 5, -2, BuildLayer.Floor, Id("cave"));
            map.Blocks.SetBlock(5, 5, -2, BuildLayer.Structure, Id("pit"));
            map.Blocks.SetBlock(5, 5, -3, BuildLayer.Floor, Id("cave"));
            Do(s, p, PlayerIntent.Climb(false));
            Do(s, p, PlayerIntent.Climb(false));
            Assert.That(Level(s, p), Is.EqualTo(-3));
            Equip(s, p, tool);
            return (s, map, p);
        }

        [Test]
        public void RockFromTheThirdLevelDownNeedsThePickaxe()
        {
            int Swings(string tool)
            {
                var (s, map, p) = ThirdLevel(tool);
                int n = 0;
                while (n < 400 && map.Blocks.IsUndug(5, 6, -3))
                {
                    Do(s, p, PlayerIntent.Dig(5, 6, -3));
                    n++;
                }
                Assert.That(map.Blocks.IsUndug(5, 6, -3), Is.False, tool);
                return n;
            }
            int pick = Swings("pickaxe");
            int shovel = Swings("shovel");
            Assert.That(pick, Is.LessThan(shovel), "the shovel is the wrong tool for rock");
        }

        [Test]
        public void ThereIsNothingBelowTheLastLevel()
        {
            var (s, map, p) = ThirdLevel("shovel");
            Assert.That(Do(s, p, PlayerIntent.Dig(5, 5, -3)), Is.EqualTo(IntentResult.InvalidTarget), "no pit at the bottom");
        }

        [Test]
        public void TheGroundCannotBeBrokenOrDismantledAndACaveFloorStaysWhenBuiltOver()
        {
            var (s, map, p) = Underground();
            Equip(s, p, "axe");
            Assert.That(Do(s, p, PlayerIntent.Hit(5, 5, -1)), Is.EqualTo(IntentResult.InvalidTarget), "the pit is not a block to break");
            Assert.That(Do(s, p, PlayerIntent.Dismantle(5, 5, 0, -1)), Is.EqualTo(IntentResult.InvalidTarget));
            // lay a wooden floor over the cave floor and take it away again: the cell must stay open
            map.Blocks.SetBlock(6, 5, -1, BuildLayer.Floor, Id("cave"));
            Assert.That(Do(s, p, PlayerIntent.Place(6, 5, Id("floor_wood"), 0, -1)), Is.EqualTo(IntentResult.Ok));
            Assert.That(map.Blocks.GetBlock(6, 5, -1, BuildLayer.Floor).Key, Is.EqualTo("floor_wood"));
            Assert.That(Do(s, p, PlayerIntent.Dismantle(6, 5, 0, -1)), Is.EqualTo(IntentResult.Ok));
            Assert.That(map.Blocks.GetBlock(6, 5, -1, BuildLayer.Floor).Key, Is.EqualTo("cave"));
            Assert.That(map.Blocks.IsSolidAt(6, 5, -1), Is.False);
        }

        [Test]
        public void UndergroundYouCannotPlaceInsideUndugGround()
        {
            var (s, map, p) = Underground();
            Assert.That(Do(s, p, PlayerIntent.Place(6, 5, Id("wall_wood"), 0, -1)), Is.EqualTo(IntentResult.InvalidTarget));
        }

        [Test]
        public void ActionsOnlyReachYourOwnLevel()
        {
            var (s, map, p) = Underground();
            map.Blocks.SetBlock(5, 6, 0, BuildLayer.Structure, Id("wall_wood"));
            Equip(s, p, "axe");
            Assert.That(Do(s, p, PlayerIntent.Hit(5, 6, 0)), Is.EqualTo(IntentResult.InvalidTarget), "that wall is on the street");
            Assert.That(Do(s, p, PlayerIntent.Place(7, 5, Id("wall_wood"), 0, 0)), Is.EqualTo(IntentResult.InvalidTarget));
        }

        // ------------------------------------------------------------------ up in the air
        [Test]
        public void StairsLeadUpAndYouFallOffTheStairwell()
        {
            var (s, map, p) = Match();
            Assert.That(Do(s, p, PlayerIntent.Place(5, 5, Id("stairs_wood"))), Is.EqualTo(IntentResult.Ok));
            Assert.That(Do(s, p, PlayerIntent.Climb(true)), Is.EqualTo(IntentResult.Ok));
            Assert.That(Level(s, p), Is.EqualTo(1));
            Assert.That(s.Players[p].Health, Is.EqualTo(new PlayerSettings().MaxHealth), "the stairwell holds you");
            var move = PlayerInput.FromVector(new Float2(1f, 0f), false);
            var events = new List<SessionEvent>();
            for (int t = 0; t < 30; t++)
            {
                s.SetInput(p, move);
                s.Step();
                s.DrainEvents(events);
            }
            Assert.That(Level(s, p), Is.Zero, "there was no floor beside the stairs");
            Assert.That(events.Any(e => e.Type == SessionEventType.PlayerFell), Is.True);
            Assert.That(s.Players[p].Health, Is.EqualTo(new PlayerSettings().MaxHealth - new PlayerSettings().FallDamagePerLevel));
        }

        [Test]
        public void UpperFloorsAreBuiltOutwardsFromWhatHoldsAndAreWalkable()
        {
            var (s, map, p) = Match();
            Do(s, p, PlayerIntent.Place(5, 5, Id("stairs_wood")));
            Do(s, p, PlayerIntent.Climb(true));
            Assert.That(Do(s, p, PlayerIntent.Place(9, 9, Id("floor_wood"), 0, 1)), Is.EqualTo(IntentResult.OutOfReach).Or.EqualTo(IntentResult.InvalidTarget));
            Assert.That(Do(s, p, PlayerIntent.Place(6, 5, Id("floor_wood"), 0, 1)), Is.EqualTo(IntentResult.Ok), "next to the stairwell");
            Assert.That(Do(s, p, PlayerIntent.Place(7, 5, Id("floor_wood"), 0, 1)), Is.EqualTo(IntentResult.Ok), "and onwards");
            Assert.That(Do(s, p, PlayerIntent.Place(5, 8, Id("floor_wood"), 0, 1)), Is.Not.EqualTo(IntentResult.Ok), "floating, and too far");
            var move = PlayerInput.FromVector(new Float2(1f, 0f), false);
            for (int t = 0; t < 12; t++)
            {
                s.SetInput(p, move);
                s.Step();
            }
            Assert.That(Level(s, p), Is.EqualTo(1), "you walk on the new floor");
            Assert.That(s.Players[p].Position.X, Is.GreaterThan(6.4f));
        }

        [Test]
        public void ARemovedFloorDropsWhoeverStandsOnIt()
        {
            var (s, map, p) = Match();
            Do(s, p, PlayerIntent.Place(5, 5, Id("stairs_wood")));
            Do(s, p, PlayerIntent.Climb(true));
            Do(s, p, PlayerIntent.Place(6, 5, Id("floor_wood"), 0, 1));
            var move = PlayerInput.FromVector(new Float2(1f, 0f), false);
            for (int t = 0; t < 6; t++)
            {
                s.SetInput(p, move);
                s.Step();
            }
            Int2 under = s.Players[p].Position.Tile;
            Assert.That(under.X, Is.EqualTo(6));
            Assert.That(Level(s, p), Is.EqualTo(1));
            map.Blocks.SetBlock(under.X, under.Y, 1, BuildLayer.Floor, BlockRegistry.AirId);
            s.SetInput(p, default);
            s.Step();
            Assert.That(Level(s, p), Is.Zero);
        }

        [Test]
        public void ATrapdoorTakesYouDownAndTheLandingMustBeOpen()
        {
            var (s, map, p) = Match();
            Do(s, p, PlayerIntent.Place(5, 5, Id("stairs_wood")));
            Do(s, p, PlayerIntent.Climb(true));
            map.Blocks.SetBlock(5, 5, 1, BuildLayer.Floor, Id("hatch_wood"));
            map.Blocks.SetBlock(5, 5, 0, BuildLayer.Structure, Id("wall_wood"));      // something solid below the landing
            Assert.That(Do(s, p, PlayerIntent.Climb(false)), Is.EqualTo(IntentResult.InvalidTarget));
            map.Blocks.SetBlock(5, 5, 0, BuildLayer.Structure, BlockRegistry.AirId);
            Assert.That(Do(s, p, PlayerIntent.Climb(false)), Is.EqualTo(IntentResult.Ok));
            Assert.That(Level(s, p), Is.Zero);
        }

        // ------------------------------------------------------------------ the rest of the match
        [Test]
        public void ZombiesFallDownAnOpenPitToReachSomeoneUnderground()
        {
            var (s, map, p) = Underground();
            int z = s.Zombies.Spawn(_data.Zombies.IndexOf("walker"), new Float2(5.5f, 7.5f));
            for (int t = 0; t < 400 && s.Zombies.GetLevel(z) == 0; t++)
            {
                s.Step();
            }
            Assert.That(s.Zombies.GetLevel(z), Is.EqualTo(-1), "it walked to the pit and fell in");
        }

        [Test]
        public void ZombiesDoNotSenseAPlayerInACaveWithNoWayIn()
        {
            var (s, map, p) = Match();
            map.Blocks.SetBlock(12, 12, -1, BuildLayer.Floor, Id("cave"));
            int z = s.Zombies.Spawn(_data.Zombies.IndexOf("walker"), new Float2(11.5f, 12.5f));
            int health = s.Players[p].Health;
            for (int t = 0; t < 100; t++)
            {
                s.Step();
            }
            Assert.That(s.Zombies.GetLevel(z), Is.Zero);
            Assert.That(s.Players[p].Health, Is.EqualTo(health));
        }

        [Test]
        public void DiggingUndergroundWakesTheZombiesAbove()
        {
            var (s, map, p) = Underground();
            Equip(s, p, "shovel");
            int z = s.Zombies.Spawn(_data.Zombies.IndexOf("walker"), new Float2(7.5f, 5.5f), aware: false);
            Assert.That(s.Zombies.GetState(z), Is.EqualTo(CepaCero.AI.ZombieState.Unaware));
            Do(s, p, PlayerIntent.Dig(6, 5, -1));
            Assert.That(s.Rules.LastNoise, Is.GreaterThan(0f));
            for (int t = 0; t < 4 && s.Zombies.GetState(z) == CepaCero.AI.ZombieState.Unaware; t++)
            {
                s.Step();
            }
            Assert.That(s.Zombies.GetState(z), Is.Not.EqualTo(CepaCero.AI.ZombieState.Unaware), "the noise carried through the ground");
        }

        [Test]
        public void TheLevelTravelsToClientsAndSurvivesASave()
        {
            var (s, map, p) = Underground();
            var broadcaster = new HostBroadcaster(s, "m");
            var join = new ByteWriter();
            broadcaster.WriteJoin(p, join);
            AsciiMap clientMap = Open();
            var client = new ClientMatchState("m", clientMap.Blocks, clientMap.Ground, _data);
            client.Receive(join.Segment, 0);
            s.Step();
            broadcaster.CollectTick();
            var down = new ByteWriter();
            broadcaster.WriteFor(p, down);
            client.Receive(down.Segment, 0.05);
            Assert.That(client.Players[p].Level, Is.EqualTo(-1));
            Assert.That(clientMap.Blocks.IsSolidAt(6, 5, -1), Is.True, "the same untouched ground on both sides");
            Assert.That(clientMap.Blocks.IsSolidAt(5, 5, -1), Is.False, "and the same hole");

            var save = new ByteWriter();
            SaveGame.Write(s, "m", save);
            AsciiMap again = Open();
            var loaded = new GameSession(new GameSettings { TickRate = 20, HordeBase = 0 }, new PlayerSettings(), again.Blocks, again.Ground, _data);
            SaveGame.Load(save.ToArray(), loaded, "m");
            Assert.That(loaded.Players[p].Level, Is.EqualTo(-1));
        }

        [Test]
        public void ThePlayerStatesMessageIsOneByteLongerPerPlayer()
        {
            var players = new List<PlayerState> { PlayerState.Spawn(new Float2(1, 1), new PlayerSettings()) };
            var w = new ByteWriter();
            Messages.WritePlayerStates(w, 5, players, 100f);
            var w0 = new ByteWriter();
            Messages.WritePlayerStates(w0, 5, new List<PlayerState>(), 100f);
            Assert.That(w.Length - w0.Length, Is.EqualTo(13));
        }
    }
}
