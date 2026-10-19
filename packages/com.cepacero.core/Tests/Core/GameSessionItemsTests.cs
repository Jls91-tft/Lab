using System.Collections.Generic;
using System.Linq;
using CepaCero.AI;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Map;
using CepaCero.Net;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Core
{
    /// <summary>Items, combat and falling/dying inside a full match (D-012).</summary>
    public class GameSessionItemsTests
    {
        private GameData _data;
        private ItemCatalog _items;

        [SetUp]
        public void SetUp()
        {
            _data = Fixtures.GameData();
            _items = _data.Items;
        }

        private static GameSettings Calm() => new GameSettings
        {
            TickRate = 20, DaySeconds = 600, DuskSeconds = 10, NightSeconds = 60, DawnSeconds = 10, HordeBase = 0,
        };

        private static AsciiMap Open(int size)
        {
            var rows = new string[size];
            for (int r = 0; r < size; r++)
            {
                rows[r] = new string('.', size);
            }
            return new AsciiMap(rows);
        }

        private GameSession Session(AsciiMap map, GameSettings settings = null, PlayerSettings players = null) =>
            new GameSession(settings ?? Calm(), players ?? new PlayerSettings(), map.Blocks, map.Ground, _data);

        private ushort Item(string id) => _items.IndexOf(id);

        private static List<SessionEvent> Step(GameSession s, int ticks = 1)
        {
            var events = new List<SessionEvent>();
            for (int t = 0; t < ticks; t++)
            {
                s.Step();
                s.DrainEvents(events);
            }
            return events;
        }

        private static IntentResult Do(GameSession s, int player, PlayerIntent intent)
        {
            s.QueueIntent(player, intent);
            List<SessionEvent> events = Step(s, 10); // longer than any weapon cooldown (pistol 0.45 s = 9 ticks)
            return events.First(e => e.Type == SessionEventType.IntentResolved && e.Player == player).Result;
        }

        private void Equip(GameSession s, int player, string id, int count = 1)
        {
            Inventory inv = s.Inventories[player];
            inv.Add(Item(id), count);
            int index = inv.Bag.ToList().FindIndex(st => st.Item == Item(id));
            inv.MoveToHotbar(index, 0);
            inv.SelectHotbar(0);
        }

        // ---------------------------------------------------------- building

        [Test]
        public void BuildingCostsMaterials()
        {
            AsciiMap map = Open(20);
            GameSession s = Session(map);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            ushort wall = map.Registry.Get("wood_wall").Id;
            Assert.That(Do(s, p, PlayerIntent.Place(6, 7, wall)), Is.EqualTo(IntentResult.MissingIngredients));
            s.Inventories[p].Add(Item("wood"), 6);
            Assert.That(Do(s, p, PlayerIntent.Place(6, 7, wall)), Is.EqualTo(IntentResult.Ok));
            Assert.That(s.Inventories[p].Count(Item("wood")), Is.EqualTo(2));
        }

        [Test]
        public void ChoppingTreesGivesWoodAndWearsTheAxe()
        {
            var map = new AsciiMap(
                "......",
                "..PT..",
                "......");
            GameSession s = Session(map);
            int p = s.AddPlayer(new Float2(2.5f, 1.5f));
            Equip(s, p, "axe");
            for (int k = 0; k < 4; k++) // tree 150 hp, axe 40 per hit
            {
                Assert.That(Do(s, p, PlayerIntent.Hit(3, 1)), Is.EqualTo(IntentResult.Ok));
            }
            Assert.That(map.Blocks.GetBlock(3, 1).IsAir, Is.True);
            Assert.That(s.Inventories[p].Count(Item("wood")), Is.EqualTo(4));
            Assert.That(s.Inventories[p].Equipped.Durability, Is.EqualTo(146));
        }

        [Test]
        public void ChestsStoreItemsAndSpillWhenDestroyed()
        {
            AsciiMap map = Open(12);
            GameSession s = Session(map);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            s.Inventories[p].Add(Item("wood"), 8);
            s.Inventories[p].Add(Item("nails"), 30);
            Assert.That(Do(s, p, PlayerIntent.Place(6, 6, map.Registry.Get("chest").Id)), Is.EqualTo(IntentResult.Ok));
            int nails = s.Inventories[p].Bag.ToList().FindIndex(st => st.Item == Item("nails"));
            Assert.That(Do(s, p, PlayerIntent.Put(6, 6, nails)), Is.EqualTo(IntentResult.Ok));
            Assert.That(s.Inventories[p].Count(Item("nails")), Is.EqualTo(0));

            map.Blocks.Damage(6, 6, 1000); // zombies smash it
            Step(s);
            ItemPile pile = s.GroundItems.Nearest(new Float2(6.5f, 6.5f), 0.5f);
            Assert.That(pile, Is.Not.Null);
            Assert.That(pile.Stacks.Sum(st => st.Item == Item("nails") ? st.Count : 0), Is.EqualTo(30));
        }

        // ------------------------------------------------------------ combat

        [Test]
        public void WeaponsHitHarderThanFists()
        {
            AsciiMap map = Open(20);
            GameSession s = Session(map);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            int z = s.Zombies.Spawn(_data.Zombies.IndexOf("walker"), new Float2(6.5f, 5.5f), aware: false);
            Do(s, p, PlayerIntent.Melee(new Float2(1, 0)));
            Assert.That(s.Zombies.GetHealth(z), Is.EqualTo(60 - 8), "fists: 8");
            Equip(s, p, "bat");
            Do(s, p, PlayerIntent.Melee(new Float2(1, 0)));
            Assert.That(s.Zombies.GetHealth(z), Is.EqualTo(60 - 8 - 18), "bat: 18");
            Assert.That(s.Inventories[p].Equipped.Durability, Is.EqualTo(119));
        }

        [Test]
        public void AZombieCloseEnoughToBiteYouIsCloseEnoughToHit()
        {
            // D-031: a walker attacks from attack_range + both radii (about 1.55 tiles); a bat must reach it there, not only point blank.
            AsciiMap map = Open(20);
            GameSession s = Session(map);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            Equip(s, p, "bat");
            int z = s.Zombies.Spawn(_data.Zombies.IndexOf("walker"), new Float2(7.05f, 5.5f), aware: false);
            Assert.That(Do(s, p, PlayerIntent.Melee(new Float2(1, 0))), Is.EqualTo(IntentResult.Ok));
            Assert.That(s.Zombies.GetHealth(z), Is.LessThan(60));
        }

        [Test]
        public void ThePistolNeedsAmmoIsStoppedByWallsAndIsLoud()
        {
            var map = new AsciiMap(
                "..............................",
                "..............#...............",
                "..............................");
            GameSession s = Session(map);
            int p = s.AddPlayer(new Float2(2.5f, 0.5f));
            Equip(s, p, "pistol");
            int target = s.Zombies.Spawn(_data.Zombies.IndexOf("walker"), new Float2(10.5f, 0.5f), aware: false);
            int behindWall = s.Zombies.Spawn(_data.Zombies.IndexOf("walker"), new Float2(20.5f, 1.5f), aware: false);
            int far = s.Zombies.Spawn(_data.Zombies.IndexOf("walker"), new Float2(29.5f, 2.5f), aware: false);

            Assert.That(Do(s, p, PlayerIntent.Ranged(new Float2(1, 0))), Is.EqualTo(IntentResult.NoAmmo));
            s.Inventories[p].Add(Item("pistol_ammo"), 2);
            Assert.That(Do(s, p, PlayerIntent.Ranged(new Float2(1, 0))), Is.EqualTo(IntentResult.Ok));
            Assert.That(s.Zombies.GetHealth(target), Is.EqualTo(60 - 35));
            Assert.That(s.Inventories[p].Count(Item("pistol_ammo")), Is.EqualTo(1));
            Assert.That(s.Zombies.IsAware(far), Is.True, "a shot is heard 30 tiles away + hearing");

            Float2 aim = (s.Zombies.GetPosition(behindWall) - s.Players[p].Position).Normalized;
            Do(s, p, PlayerIntent.Ranged(aim));
            Assert.That(s.Zombies.GetHealth(behindWall), Is.EqualTo(60), "the wall took the bullet");
        }

        [Test]
        public void FriendlyFireIsAnOption()
        {
            int TeammateHealthAfterSwing(bool friendlyFire)
            {
                AsciiMap map = Open(20);
                GameSettings settings = Calm();
                settings.FriendlyFire = friendlyFire;
                GameSession s = Session(map, settings);
                int a = s.AddPlayer(new Float2(5.5f, 5.5f));
                int b = s.AddPlayer(new Float2(6.5f, 5.5f));
                Equip(s, a, "bat");
                Do(s, a, PlayerIntent.Melee(new Float2(1, 0)));
                return s.Players[b].Health;
            }
            Assert.That(TeammateHealthAfterSwing(false), Is.EqualTo(100));
            Assert.That(TeammateHealthAfterSwing(true), Is.EqualTo(100 - 18));
        }

        // ----------------------------------------------------- items & loot

        [Test]
        public void HealingAndAnalysingSamples()
        {
            AsciiMap map = Open(20);
            GameSession s = Session(map);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            Equip(s, p, "bandage", 2);
            Assert.That(Do(s, p, PlayerIntent.Use()), Is.EqualTo(IntentResult.NothingToUse), "full health");

            s.Zombies.Spawn(_data.Zombies.IndexOf("walker"), new Float2(6.5f, 5.5f));
            Step(s, 2);
            s.Zombies.Damage(0, 1000, "blunt");
            int before = s.Players[p].Health;
            Assert.That(before, Is.LessThan(100));
            Assert.That(Do(s, p, PlayerIntent.Use()), Is.EqualTo(IntentResult.Ok));
            Assert.That(s.Players[p].Health, Is.EqualTo(System.Math.Min(100, before + 20)));

            Equip(s, p, "sample_brute");
            Assert.That(s.Recipes.TryPayForBlock(Inventory.ForPlayer(_items), "concrete_wall"), Is.False);
            Assert.That(Do(s, p, PlayerIntent.Use()), Is.EqualTo(IntentResult.Ok));
            Assert.That(s.Recipes.UnlockedSamples, Does.Contain("sample_brute"));
        }

        [Test]
        public void CraftingThroughIntentsNeedsAWorkbenchNearby()
        {
            AsciiMap map = Open(20);
            GameSession s = Session(map);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            s.Inventories[p].Add(Item("metal_scrap"), 6);
            int plate = s.Recipes.IndexOf("craft_metal_plate");
            Assert.That(Do(s, p, PlayerIntent.CraftRecipe(plate)), Is.EqualTo(IntentResult.NeedsWorkbench));
            map.Blocks.SetBlock(7, 5, map.Registry.Get("workbench").Id);
            Assert.That(Do(s, p, PlayerIntent.CraftRecipe(plate)), Is.EqualTo(IntentResult.Ok));
            Assert.That(s.Inventories[p].Count(Item("metal_plate")), Is.EqualTo(1));
        }

        [Test]
        public void MapBuildingsHaveLootToTake()
        {
            MapImportResult map = MapImporter.Import(Fixtures.LoadMap("synthetic.map.json"), BlockRegistry.CreateDefault());
            var s = new GameSession(Calm(), new PlayerSettings(), map.Blocks, map.Ground, _data, map.Buildings);
            Assert.That(s.Containers.Count, Is.GreaterThan(0));
            // Open every container with a probe until one has something (loot is random).
            Int2 box = default;
            Inventory contents = null;
            foreach (Int2 cell in s.Containers.Positions)
            {
                var probe = PlayerState.Spawn(new Float2(cell.X + 0.5f, cell.Y + 0.5f), new PlayerSettings());
                contents = s.ItemRules.OpenInReach(probe, cell.X, cell.Y);
                box = cell;
                if (contents.Bag.Count > 0)
                {
                    break;
                }
            }
            Assume.That(contents.Bag.Count, Is.GreaterThan(0), "some container had loot");
            int p = s.AddPlayer(new Float2(box.X + 0.5f, box.Y + 0.5f));
            ushort first = contents.Bag[0].Item;
            Assert.That(Do(s, p, PlayerIntent.Take(box.X, box.Y, 0)), Is.EqualTo(IntentResult.Ok));
            Assert.That(s.Inventories[p].Count(first), Is.GreaterThan(0));
            var far = PlayerState.Spawn(new Float2(0.5f, 0.5f), new PlayerSettings());
            Assert.That(s.ItemRules.OpenInReach(far, box.X, box.Y), Is.Null, "out of reach");
        }

        [Test]
        public void KilledZombiesDropTheirLoot()
        {
            AsciiMap map = Open(20);
            GameSession s = Session(map);
            s.AddPlayer(new Float2(2.5f, 2.5f));
            int brute = s.Zombies.Spawn(_data.Zombies.IndexOf("brute"), new Float2(12.5f, 12.5f), aware: false);
            s.Zombies.Damage(brute, 100000, "fire");
            Step(s);
            ItemPile pile = s.GroundItems.Nearest(new Float2(12.5f, 12.5f), 0.5f);
            Assert.That(pile, Is.Not.Null);
            Assert.That(pile.Stacks.Any(st => st.Item == Item("sample_brute")), Is.True);
            Assert.That(s.ZombiesKilled, Is.EqualTo(1));
        }

        // -------------------------------------------------- falling & dying

        private static void HurtToZero(GameSession s, int player)
        {
            var zombieAt = s.Players[player].Position + new Float2(0.6f, 0f);
            int z = s.Zombies.Spawn(s.Catalog.IndexOf("brute"), zombieAt);
            for (int t = 0; t < 200 && !s.Players[player].IsDowned; t++)
            {
                s.Step();
            }
            s.Zombies.Damage(z, 100000, "fire");
            s.Step();
        }

        [Test]
        public void ATeammateCanHelpYouUp()
        {
            AsciiMap map = Open(30);
            GameSettings settings = Calm();
            settings.FriendlyFire = true; // the easiest way to get knocked down in a test
            GameSession s = Session(map, settings);
            int a = s.AddPlayer(new Float2(5.5f, 5.5f));
            int b = s.AddPlayer(new Float2(6.9f, 5.5f));
            Equip(s, b, "pistol");
            s.Inventories[b].Add(Item("pistol_ammo"), 3);
            for (int shot = 0; shot < 3; shot++)
            {
                Do(s, b, PlayerIntent.Ranged(new Float2(-1, 0)));
            }
            Assert.That(s.Players[a].IsDowned, Is.True);
            Assert.That(s.Players[a].IsDead, Is.False, "with a teammate around you go down, not die");

            for (int t = 0; t < 20 * 2; t++) // 2 s of help: not enough
            {
                s.QueueIntent(b, PlayerIntent.ReviveTeammate(a));
                s.Step();
            }
            s.Step(); // key released: progress is lost
            Assert.That(s.Players[a].ReviveProgress, Is.EqualTo(0f));

            var events = new List<SessionEvent>();
            for (int t = 0; t < 20 * 3 + 1; t++)
            {
                s.QueueIntent(b, PlayerIntent.ReviveTeammate(a));
                s.Step();
                s.DrainEvents(events);
            }
            Assert.That(events.Any(e => e.Type == SessionEventType.PlayerRevived), Is.True);
            Assert.That(s.Players[a].Health, Is.EqualTo(30));
        }

        [Test]
        public void BleedingOutDropsTheBackpackAndYouComeBackAtDawn()
        {
            AsciiMap map = Open(30);
            GameSettings settings = Calm();
            settings.DaySeconds = 40;
            GameSession s = Session(map, settings);
            int a = s.AddPlayer(new Float2(5.5f, 5.5f));
            s.AddPlayer(new Float2(25.5f, 25.5f));
            s.Inventories[a].Add(Item("wood"), 20);
            HurtToZero(s, a);
            Float2 fell = s.Players[a].Position;
            List<SessionEvent> events = Step(s, 20 * 31);
            Assert.That(events.Any(e => e.Type == SessionEventType.PlayerDied && e.Player == a), Is.True);
            Assert.That(s.Inventories[a].IsEmpty, Is.True);
            Assert.That(s.GroundItems.Nearest(fell, 0.5f).Stacks.Sum(st => st.Count), Is.EqualTo(20), "the backpack stays where they fell");

            while (s.Clock.Phase != DayPhase.Dawn)
            {
                s.Step();
            }
            Assert.That(s.Players[a].IsDead, Is.False);
            Assert.That(s.Players[a].Health, Is.EqualTo(100));
            Assert.That(s.Players[a].Position, Is.EqualTo(new Float2(5.5f, 5.5f)), "respawn at the fort");
        }

        [Test]
        public void AloneYouDieAtOnceAndTheMatchGoesOn()
        {
            AsciiMap map = Open(30);
            GameSession s = Session(map);
            int a = s.AddPlayer(new Float2(5.5f, 5.5f));
            HurtToZero(s, a);
            Assert.That(s.Players[a].IsDead, Is.True);
            Assert.That(s.IsGameOver, Is.False, "no PermaDeath by default");
        }
    }
}
