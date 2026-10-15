using System;
using System.Collections.Generic;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.Core
{
    public class SaveGameTests
    {
        private const int Size = 70; // 3x3 chunks, the last ones partial

        private static GameSettings Settings() => new GameSettings
        {
            TickRate = 20, DaySeconds = 30, DuskSeconds = 5, NightSeconds = 30, DawnSeconds = 5,
        };

        private static GameSession NewSession(BlockRegistry registry = null)
        {
            registry ??= BlockRegistry.CreateDefault();
            var ground = new GroundLayer(Size, Size);
            var blocks = new BlockWorld(Size, Size, registry);
            return new GameSession(Settings(), new PlayerSettings(), blocks, ground, Fixtures.Catalog());
        }

        private static byte[] Save(GameSession session, string map = "test-map")
        {
            var writer = new ByteWriter();
            SaveGame.Write(session, map, writer);
            return writer.ToArray();
        }

        private static GameSession PlayedSession()
        {
            GameSession s = NewSession();
            ushort wood = s.Blocks.Registry.Get("wood_wall").Id;
            ushort door = s.Blocks.Registry.Get("door").Id;
            for (int x = 10; x < 60; x++)
            {
                s.Blocks.SetBlock(x, 40, wood);
            }
            s.Blocks.SetBlock(33, 40, door);
            s.Blocks.Damage(12, 40, 30);
            s.Blocks.SetBlock(69, 69, s.Blocks.Registry.Get("metal_wall").Id);
            s.AddPlayer(new Float2(20.5f, 20.5f));
            s.AddPlayer(new Float2(25.5f, 21.5f));
            s.SetInput(0, PlayerInput.FromVector(new Float2(1, 0), sprint: true));
            for (int t = 0; t < 20 * 10; t++)
            {
                s.Step();
            }
            return s;
        }

        [Test]
        public void RoundTripRestoresWorldClockAndPlayers()
        {
            GameSession original = PlayedSession();
            byte[] data = Save(original);
            TestContext.WriteLine($"save size: {data.Length} bytes");

            GameSession loaded = NewSession();
            SaveGame.Load(data, loaded, "test-map");

            Assert.That(loaded.Clock.Tick, Is.EqualTo(original.Clock.Tick));
            Assert.That(loaded.Clock.Phase, Is.EqualTo(original.Clock.Phase));
            Assert.That(loaded.Players.Count, Is.EqualTo(2));
            Assert.That(loaded.Players[0].Position, Is.EqualTo(original.Players[0].Position));
            Assert.That(loaded.Players[0].Stamina, Is.EqualTo(original.Players[0].Stamina));
            Assert.That(loaded.Players[1].Health, Is.EqualTo(original.Players[1].Health));
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Assert.That(loaded.Blocks.GetTypeId(x, y), Is.EqualTo(original.Blocks.GetTypeId(x, y)));
                    Assert.That(loaded.Blocks.GetHealth(x, y), Is.EqualTo(original.Blocks.GetHealth(x, y)));
                }
            }
            Assert.That(loaded.Blocks.PendingChangeCount, Is.EqualTo(0), "loading is not a network delta");
            Assert.That(data.Length, Is.LessThan(1024), "RLE keeps a mostly empty map tiny");
        }

        [Test]
        public void OldSavesLoadAfterBlockTypesAreAddedOrReordered()
        {
            byte[] data = Save(PlayedSession());

            // A future version of the game: new block types, different order.
            var future = new BlockRegistry();
            string[] keys = { "air", "sandbags", "metal_wall", "door", "door_open", "wood_wall", "fence", "tree", "building_wall", "concrete_wall" };
            for (ushort id = 0; id < keys.Length; id++)
            {
                future.Add(new BlockType(id, keys[id], 100, BlockMaterial.Wood, isSolid: id != 0));
            }
            GameSession loaded = NewSession(future);
            SaveGame.Load(data, loaded, "test-map");
            Assert.That(loaded.Blocks.GetBlock(20, 40).Key, Is.EqualTo("wood_wall"));
            Assert.That(loaded.Blocks.GetBlock(33, 40).Key, Is.EqualTo("door"));
            Assert.That(loaded.Blocks.GetBlock(69, 69).Key, Is.EqualTo("metal_wall"));
        }

        [Test]
        public void CorruptOrMismatchedSavesAreRejected()
        {
            byte[] data = Save(PlayedSession());

            byte[] flipped = (byte[])data.Clone();
            flipped[flipped.Length / 2] ^= 0x40;
            Assert.Throws<FormatException>(() => SaveGame.Load(flipped, NewSession(), "test-map"), "checksum");

            Assert.Throws<FormatException>(() => SaveGame.Load(data, NewSession(), "other-map"));
            Assert.Throws<FormatException>(() => SaveGame.Load(new byte[3], NewSession(), "test-map"));

            var missing = new BlockRegistry();
            missing.Add(new BlockType(0, "air", 0, BlockMaterial.None, isSolid: false));
            var ex = Assert.Throws<FormatException>(() => SaveGame.Load(data, NewSession(missing), "test-map"));
            StringAssert.Contains("wood_wall", ex.Message, "names the blocks this version does not know");
        }

        [Test]
        public void LoadingDuringTheNightRestartsTheHorde()
        {
            GameSession original = NewSession();
            original.AddPlayer(new Float2(35.5f, 35.5f));
            while (original.Clock.Phase != DayPhase.Night)
            {
                original.Step();
            }
            for (int t = 0; t < 20 * 5; t++)
            {
                original.Step();
            }
            byte[] data = Save(original);
            GameSession loaded = NewSession();
            SaveGame.Load(data, loaded, "test-map");
            Assert.That(loaded.Clock.Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(loaded.CurrentPlan, Is.Not.Null);
            Assert.That(loaded.Zombies.SightMultiplier, Is.EqualTo(0.5f));
            for (int t = 0; t < 20 * 3; t++)
            {
                loaded.Step();
            }
            Assert.That(loaded.Zombies.Count, Is.GreaterThan(0));
        }
    
        [Test]
        public void ItemsSurviveASave()
        {
            GameData data = Fixtures.GameData();
            ItemCatalog items = data.Items;
            GameSession NewFull() => new GameSession(Settings(), new PlayerSettings(), new BlockWorld(Size, Size, BlockRegistry.CreateDefault()),
                new GroundLayer(Size, Size), data);

            GameSession original = NewFull();
            int a = original.AddPlayer(new Float2(10.5f, 10.5f));
            original.AddPlayer(new Float2(12.5f, 10.5f));
            original.RespawnPoint = new Float2(33.5f, 33.5f);
            original.Inventories[a].Add(items.IndexOf("wood"), 37);
            original.Inventories[a].Add(items.IndexOf("axe"), 1);
            original.Inventories[a].MoveToHotbar(1, 3);
            original.Inventories[a].SelectHotbar(3);
            original.Inventories[a].WearEquipped();
            original.Recipes.Unlock("sample_brute");
            original.Containers.AddChest(new Int2(20, 20));
            original.Containers.Open(new Int2(20, 20), data.Loot, new DeterministicRandom(1)).Add(items.IndexOf("nails"), 12);
            original.GroundItems.Drop(new Float2(40.5f, 40.5f), new[] { new ItemStack(items.IndexOf("bandage"), 2) });
            original.Step();

            var writer = new ByteWriter();
            SaveGame.Write(original, "test-map", writer);
            GameSession loaded = NewFull();
            SaveGame.Load(writer.ToArray(), loaded, "test-map");

            Inventory inv = loaded.Inventories[a];
            Assert.That(inv.Count(items.IndexOf("wood")), Is.EqualTo(37));
            Assert.That(inv.EquippedDefinition.id, Is.EqualTo("axe"));
            Assert.That(inv.Equipped.Durability, Is.EqualTo(149));
            Assert.That(loaded.Recipes.UnlockedSamples, Does.Contain("sample_brute"));
            Assert.That(loaded.Containers.Open(new Int2(20, 20), data.Loot, new DeterministicRandom(1)).Count(items.IndexOf("nails")), Is.EqualTo(12));
            Assert.That(loaded.GroundItems.Piles.Count, Is.EqualTo(1));
            Assert.That(loaded.RespawnPoint, Is.EqualTo(new Float2(33.5f, 33.5f)));
            Assert.That(loaded.Players.Count, Is.EqualTo(2));
        }
    }
}
