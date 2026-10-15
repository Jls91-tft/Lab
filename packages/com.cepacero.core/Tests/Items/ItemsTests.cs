using System.Collections.Generic;
using System.Linq;
using CepaCero.Items;
using CepaCero.Map;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Items
{
    public class InventoryTests
    {
        private ItemCatalog _items;
        private ushort _wood;
        private ushort _bat;
        private ushort _medkit;

        [SetUp]
        public void SetUp()
        {
            _items = Fixtures.Items();
            _wood = _items.IndexOf("wood");
            _bat = _items.IndexOf("bat");
            _medkit = _items.IndexOf("medkit");
        }

        [Test]
        public void StacksFillUpBeforeOpeningNewOnes()
        {
            Inventory inv = Inventory.ForPlayer(_items);
            Assert.That(inv.Add(_wood, 30), Is.EqualTo(30));
            Assert.That(inv.Add(_wood, 30), Is.EqualTo(30));
            Assert.That(inv.Bag.Count, Is.EqualTo(2), "50 + 10");
            Assert.That(inv.Count(_wood), Is.EqualTo(60));
            Assert.That(inv.UsedSlots, Is.EqualTo(2));
        }

        [Test]
        public void BigItemsUseMoreSlotsAndTheBagFillsUp()
        {
            var inv = new Inventory(_items, bagSlots: 5);
            Assert.That(inv.Add(_medkit, 3), Is.EqualTo(3), "one stack of 3, size 2");
            Assert.That(inv.FreeSlots, Is.EqualTo(3));
            Assert.That(inv.Add(_bat, 3), Is.EqualTo(1), "a bat uses 2 slots: only one more fits");
            Assert.That(inv.FreeSlots, Is.EqualTo(1));
            Assert.That(inv.Add(_wood, 80), Is.EqualTo(50), "one slot left = one stack of 50");
        }

        [Test]
        public void ToolsKeepTheirOwnDurability()
        {
            Inventory inv = Inventory.ForPlayer(_items);
            inv.Add(_bat, 2);
            Assert.That(inv.Bag.Count, Is.EqualTo(2), "tools never stack");
            Assert.That(inv.Bag[0].Durability, Is.EqualTo(120));
            Assert.That(inv.MoveToHotbar(0, 0), Is.True);
            Assert.That(inv.EquippedDefinition.id, Is.EqualTo("bat"));
            for (int i = 0; i < 119; i++)
            {
                Assert.That(inv.WearEquipped(), Is.False);
            }
            Assert.That(inv.WearEquipped(), Is.True, "breaks on the 120th use");
            Assert.That(inv.Equipped.IsEmpty, Is.True);
            Assert.That(inv.Count(_bat), Is.EqualTo(1));
        }

        [Test]
        public void HotbarSwapsAndRemoveReachesIt()
        {
            Inventory inv = Inventory.ForPlayer(_items);
            inv.Add(_bat, 1);
            inv.Add(_wood, 10);
            Assert.That(inv.MoveToHotbar(0, 2), Is.True);
            inv.SelectHotbar(2);
            Assert.That(inv.EquippedDefinition.id, Is.EqualTo("bat"));
            Assert.That(inv.MoveToHotbar(0, 2), Is.True, "wood replaces the bat, which goes back to the bag");
            Assert.That(inv.EquippedDefinition.id, Is.EqualTo("wood"));
            Assert.That(inv.Count(_bat), Is.EqualTo(1));
            Assert.That(inv.Remove(_wood, 4), Is.EqualTo(4));
            Assert.That(inv.Equipped.Count, Is.EqualTo(6));
        }

        [Test]
        public void SerializesForTheOwnerAndForSaves()
        {
            Inventory inv = Inventory.ForPlayer(_items);
            inv.Add(_bat, 1);
            inv.Add(_wood, 42);
            inv.MoveToHotbar(0, 1);
            inv.SelectHotbar(1);
            inv.WearEquipped();
            var w = new ByteWriter();
            inv.Write(w);

            Inventory copy = Inventory.ForPlayer(_items);
            copy.Read(new ByteReader(w.ToArray()));
            Assert.That(copy.Count(_wood), Is.EqualTo(42));
            Assert.That(copy.Equipped.Durability, Is.EqualTo(119));
            Assert.That(copy.SelectedHotbar, Is.EqualTo(1));
            Assert.That(w.Length, Is.LessThan(64), "a typical inventory is a few dozen bytes");
        }

        [Test]
        public void VersionChangesOnlyWhenSomethingChanges()
        {
            Inventory inv = Inventory.ForPlayer(_items);
            int v = inv.Version;
            Assert.That(inv.Remove(_wood, 5), Is.EqualTo(0));
            Assert.That(inv.Version, Is.EqualTo(v));
            inv.Add(_wood, 1);
            Assert.That(inv.Version, Is.GreaterThan(v));
        }
    }

    public class RecipeBookTests
    {
        private ItemCatalog _items;
        private RecipeBook _book;

        [SetUp]
        public void SetUp()
        {
            _items = Fixtures.Items();
            _book = Fixtures.Recipes(_items);
        }

        private Inventory With(params (string id, int count)[] stuff)
        {
            Inventory inv = Inventory.ForPlayer(_items);
            foreach ((string id, int count) in stuff)
            {
                inv.Add(_items.IndexOf(id), count);
            }
            return inv;
        }

        [Test]
        public void EveryBuildRecipeTargetsARealBlock()
        {
            BlockRegistry registry = BlockRegistry.CreateDefault();
            foreach (RecipeDefinition r in _book.Recipes.Where(r => r.BuildsBlock))
            {
                Assert.That(registry.TryGet(r.result_block, out _), Is.True, $"{r.id} builds unknown block '{r.result_block}'");
            }
        }

        [Test]
        public void CraftingConsumesIngredients()
        {
            Inventory inv = With(("scrap_cloth", 5));
            Assert.That(_book.Craft(inv, _book.IndexOf("craft_bandage"), nearWorkbench: false), Is.EqualTo(CraftResult.Ok));
            Assert.That(inv.Count(_items.IndexOf("bandage")), Is.EqualTo(1));
            Assert.That(inv.Count(_items.IndexOf("scrap_cloth")), Is.EqualTo(3));
            Assert.That(_book.Craft(With(("scrap_cloth", 1)), _book.IndexOf("craft_bandage"), false), Is.EqualTo(CraftResult.MissingIngredients));
        }

        [Test]
        public void SomeRecipesNeedAWorkbench()
        {
            Inventory inv = With(("metal_scrap", 6));
            int plate = _book.IndexOf("craft_metal_plate");
            Assert.That(_book.Craft(inv, plate, nearWorkbench: false), Is.EqualTo(CraftResult.NeedsWorkbench));
            Assert.That(_book.Craft(inv, plate, nearWorkbench: true), Is.EqualTo(CraftResult.Ok));
        }

        [Test]
        public void SamplesUnlockRecipesForTheWholeTeam()
        {
            Inventory inv = With(("concrete_mix", 4));
            Assert.That(_book.TryPayForBlock(inv, "concrete_wall"), Is.False, "locked until La Mole's sample");
            Assert.That(_book.Unlock("sample_brute"), Is.True);
            Assert.That(_book.Unlock("sample_brute"), Is.False);
            Assert.That(_book.TryPayForBlock(inv, "concrete_wall"), Is.True);
            Assert.That(inv.Count(_items.IndexOf("concrete_mix")), Is.EqualTo(2));
        }

        [Test]
        public void BuildingCostsTheBuildRecipe()
        {
            Inventory inv = With(("wood", 5));
            Assert.That(_book.TryPayForBlock(inv, "wood_wall"), Is.True);
            Assert.That(inv.Count(_items.IndexOf("wood")), Is.EqualTo(1));
            Assert.That(_book.TryPayForBlock(inv, "wood_wall"), Is.False, "nothing is taken when you cannot afford it");
            Assert.That(inv.Count(_items.IndexOf("wood")), Is.EqualTo(1));
            Assert.That(_book.TryPayForBlock(inv, "building_wall"), Is.False, "map-only block");
        }

        [Test]
        public void NoRoomUndoesTheCraft()
        {
            var inv = new Inventory(_items, bagSlots: 2);
            inv.Add(_items.IndexOf("wood"), 3);
            inv.Add(_items.IndexOf("tape"), 1);
            // bat needs 2 slots; paying frees both slots, so it fits.
            Assert.That(_book.Craft(inv, _book.IndexOf("craft_bat"), false), Is.EqualTo(CraftResult.Ok));

            var full = new Inventory(_items, bagSlots: 3);
            full.Add(_items.IndexOf("wood"), 50);
            full.Add(_items.IndexOf("wood"), 3);
            full.Add(_items.IndexOf("tape"), 2);
            Assert.That(_book.Craft(full, _book.IndexOf("craft_bat"), false), Is.EqualTo(CraftResult.NoRoom));
            Assert.That(full.Count(_items.IndexOf("wood")), Is.EqualTo(53), "ingredients given back");
            Assert.That(full.Count(_items.IndexOf("tape")), Is.EqualTo(2));
        }
    }

    public class LootTests
    {
        private ItemCatalog _items;
        private LootRoller _loot;

        [SetUp]
        public void SetUp()
        {
            _items = Fixtures.Items();
            _loot = Fixtures.Loot(_items);
        }

        [Test]
        public void ContainersDependOnBuildingSize()
        {
            Assert.That(_loot.ContainersFor(10), Is.EqualTo(1));
            Assert.That(_loot.ContainersFor(240), Is.EqualTo(4));
            Assert.That(_loot.ContainersFor(5000), Is.EqualTo(6));
        }

        [Test]
        public void BuildingKindsGiveTheirOwnLoot()
        {
            int Total(string kind, string item)
            {
                var rng = new DeterministicRandom(9);
                int n = 0;
                for (int k = 0; k < 200; k++)
                {
                    var box = new Inventory(_items, ContainerStore.ContainerSlots);
                    _loot.FillContainer(kind, box, rng);
                    n += box.Count(_items.IndexOf(item));
                }
                return n;
            }
            Assert.That(Total("food", "food_can"), Is.GreaterThan(Total("hardware", "food_can") * 3));
            Assert.That(Total("hardware", "nails"), Is.GreaterThan(Total("food", "nails") * 3));
            Assert.That(Total("medical", "bandage"), Is.GreaterThan(0));
            Assert.That(Total("unknown_kind", "wood"), Is.GreaterThan(0), "falls back to generic");
        }

        [Test]
        public void ZombiesDropWhatTheBestiarySays()
        {
            var rng = new DeterministicRandom(1);
            var drop = new List<ItemStack>();
            CepaCero.AI.ZombieCatalog zombies = Fixtures.Catalog();
            _loot.RollDrop(zombies[zombies.IndexOf("brute")], drop, rng);
            Assert.That(drop.Any(s => s.Item == _items.IndexOf("sample_brute")), Is.True, "La Mole always drops its sample");
        }

        [Test]
        public void MapBuildingsGetContainersRolledOnFirstOpenOnly()
        {
            MapImportResult map = MapImporter.Import(Fixtures.LoadMap("synthetic.map.json"), BlockRegistry.CreateDefault());
            var store = new ContainerStore(_items);
            var rng = new DeterministicRandom(4);
            store.PlaceInBuildings(map.Buildings, map.Blocks, map.Ground, _loot, rng);
            Assert.That(store.Count, Is.InRange(9, 12), "1 + tiles/80 per building: 3 + 3 + 4, minus cells without room");
            foreach (Int2 cell in store.Positions)
            {
                Assert.That(map.Ground.Get(cell.X, cell.Y), Is.EqualTo(GroundType.Floor), "inside a building");
                Assert.That(map.Blocks.IsSolid(cell.X, cell.Y), Is.False);
            }

            Int2 first = store.Positions.First();
            Inventory box = store.Open(first, _loot, rng);
            var taken = new List<ItemStack>();
            box.TakeAll(taken);
            Assert.That(store.Open(first, _loot, rng).IsEmpty, Is.True, "no loot respawn by default");
            Assert.That(store.ResetEmpty(), Is.GreaterThanOrEqualTo(1), "the respawn option re-arms empty containers");
        }

        [Test]
        public void ContainersSurviveASave()
        {
            var store = new ContainerStore(_items);
            store.AddChest(new Int2(3, 4));
            store.Open(new Int2(3, 4), _loot, new DeterministicRandom(1)).Add(_items.IndexOf("nails"), 17);
            var w = new ByteWriter();
            store.Write(w);
            var copy = new ContainerStore(_items);
            copy.Read(new ByteReader(w.ToArray()), null);
            Assert.That(copy.Open(new Int2(3, 4), _loot, new DeterministicRandom(1)).Count(_items.IndexOf("nails")), Is.EqualTo(17));
        }

        [Test]
        public void PilesOnTheGroundCanBePickedUp()
        {
            var ground = new GroundItems();
            ItemPile pile = ground.Drop(new Float2(5, 5), new[] { new ItemStack(_items.IndexOf("wood"), 70), new ItemStack(_items.IndexOf("bat"), 1, 50) });
            Assert.That(ground.Nearest(new Float2(6, 5), 1.5f), Is.SameAs(pile));
            Assert.That(ground.Nearest(new Float2(9, 5), 1.5f), Is.Null);

            var small = new Inventory(_items, bagSlots: 3);
            ground.PickUp(pile, small);
            Assert.That(small.Count(_items.IndexOf("bat")), Is.EqualTo(1), "keeps its durability");
            Assert.That(small.Bag.First(s => s.Item == _items.IndexOf("bat")).Durability, Is.EqualTo(50));
            Assert.That(small.Count(_items.IndexOf("wood")), Is.EqualTo(50), "only one slot left for wood");
            Assert.That(ground.Piles.Count, Is.EqualTo(1), "the rest stays on the floor");
        }
    }
}
