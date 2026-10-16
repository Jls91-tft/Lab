using System;
using System.Collections.Generic;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.World
{
    public class BlockWorldTests
    {
        private BlockRegistry _registry;
        private ushort _wood;
        private ushort _door;

        [SetUp]
        public void SetUp()
        {
            _registry = BlockRegistry.CreateDefault();
            _wood = _registry.Get("wood_wall").Id;
            _door = _registry.Get("door").Id;
        }

        [Test]
        public void NewWorldIsAirAndSplitIntoChunks()
        {
            var world = new BlockWorld(70, 40, _registry);
            Assert.That(world.ChunksX, Is.EqualTo(3));
            Assert.That(world.ChunksY, Is.EqualTo(2));
            Assert.That(world.GetBlock(69, 39).IsAir, Is.True);
            Assert.That(world.IsSolid(-1, 0), Is.True, "outside the map counts as solid");
        }

        [Test]
        public void PlaceValidatesBoundsOccupancyAndGround()
        {
            var world = new BlockWorld(10, 10, _registry);
            var ground = new GroundLayer(10, 10);
            ground.Set(5, 5, GroundType.Water);

            Assert.That(world.TryPlace(1, 1, _wood, ground), Is.EqualTo(PlaceResult.Placed));
            Assert.That(world.TryPlace(1, 1, _wood, ground), Is.EqualTo(PlaceResult.Occupied));
            Assert.That(world.TryPlace(10, 1, _wood, ground), Is.EqualTo(PlaceResult.OutOfBounds));
            Assert.That(world.TryPlace(2, 2, BlockRegistry.AirId, ground), Is.EqualTo(PlaceResult.InvalidType));
            Assert.That(world.TryPlace(5, 5, _wood, ground), Is.EqualTo(PlaceResult.BlockedGround));
            Assert.That(world.GetHealth(1, 1), Is.EqualTo(100));
        }

        [Test]
        public void DamageReducesHealthAndDestroysAtZero()
        {
            var world = new BlockWorld(10, 10, _registry);
            world.SetBlock(3, 3, _wood);

            Assert.That(world.Damage(3, 3, 40), Is.EqualTo(DamageResult.Damaged));
            Assert.That(world.GetHealth(3, 3), Is.EqualTo(60));
            Assert.That(world.Damage(3, 3, 60), Is.EqualTo(DamageResult.Destroyed));
            Assert.That(world.GetBlock(3, 3).IsAir, Is.True);
            Assert.That(world.Damage(3, 3, 10), Is.EqualTo(DamageResult.None));
        }

        [Test]
        public void DoorsToggleKeepingHealth()
        {
            var world = new BlockWorld(10, 10, _registry);
            world.SetBlock(4, 4, _door);
            world.Damage(4, 4, 30);

            Assert.That(world.Toggle(4, 4), Is.True);
            Assert.That(world.GetBlock(4, 4).Key, Is.EqualTo("door_open"));
            Assert.That(world.IsSolid(4, 4), Is.False);
            Assert.That(world.GetHealth(4, 4), Is.EqualTo(70));
            Assert.That(world.Toggle(4, 4), Is.True);
            Assert.That(world.IsSolid(4, 4), Is.True);
            Assert.That(world.Toggle(0, 0), Is.False, "air is not toggleable");
        }

        [Test]
        public void ChangesAreRecordedForTheNetworkAndReplicaConverges()
        {
            var host = new BlockWorld(64, 64, _registry);
            var client = new BlockWorld(64, 64, _registry);
            host.SetBlock(1, 1, _wood);
            host.SetBlock(40, 33, _door);
            host.Damage(1, 1, 25);
            host.SetBlock(40, 33, _door); // no-op: identical state is not recorded again

            var changes = new List<BlockChange>();
            host.DrainChanges(changes);
            Assert.That(changes.Count, Is.EqualTo(3));
            Assert.That(host.PendingChangeCount, Is.EqualTo(0));

            var writer = new ByteWriter();
            BlockChange.WriteBatch(changes, writer);
            Assert.That(writer.Length, Is.EqualTo(2 + 3 * BlockChange.Bytes), "11 bytes per change: position, type, health, level, layer, variant");

            var received = new List<BlockChange>();
            BlockChange.ReadBatch(new ByteReader(writer.ToArray()), received);
            foreach (BlockChange c in received)
            {
                client.ApplyChange(c);
            }
            AssertSameWorld(host, client);
        }

        [Test]
        public void ChunkSnapshotRoundTripsAndIsCompact()
        {
            var host = new BlockWorld(64, 64, _registry);
            for (int x = 32; x < 64; x++)
            {
                host.SetBlock(x, 40, _wood);
            }
            host.Damage(35, 40, 10);

            var writer = new ByteWriter();
            host.WriteChunk(0, 0, writer);
            Assert.That(writer.Length, Is.EqualTo(4 + 1 + 2 + 7), "an all-air chunk is a header, one plane and a single run");

            writer.Clear();
            host.WriteChunk(1, 1, writer);
            var client = new BlockWorld(64, 64, _registry);
            client.ReadChunk(new ByteReader(writer.ToArray()));

            Assert.That(client.GetBlock(40, 40).Key, Is.EqualTo("wood_wall"));
            Assert.That(client.GetHealth(35, 40), Is.EqualTo(90));
            Assert.That(client.GetChunk(1, 1).Version, Is.GreaterThan(0));
            AssertSameWorld(host, client);
        }

        [Test]
        public void CorruptChunkDataIsRejected()
        {
            var world = new BlockWorld(32, 32, _registry);
            var writer = new ByteWriter();
            writer.WriteUShort(0);
            writer.WriteUShort(0);
            writer.WriteUShort(2000); // run longer than the chunk
            writer.WriteUShort(0);
            writer.WriteUShort(0);
            Assert.Throws<FormatException>(() => world.ReadChunk(new ByteReader(writer.ToArray())));
            Assert.Throws<FormatException>(() => world.ReadChunk(new ByteReader(new byte[] { 0, 0 })));
        }

        [Test]
        public void RevisionTracksAnyChangeButStructureOnlyTypeChanges()
        {
            var world = new BlockWorld(10, 10, _registry);
            world.SetBlock(2, 2, _wood);
            int revision = world.Revision;
            int structure = world.StructureRevision;

            world.Damage(2, 2, 10);
            Assert.That(world.Revision, Is.GreaterThan(revision));
            Assert.That(world.StructureRevision, Is.EqualTo(structure), "a dent does not change the paths");

            world.Damage(2, 2, 1000);
            Assert.That(world.StructureRevision, Is.GreaterThan(structure), "a destroyed block does");
        }

        private static void AssertSameWorld(BlockWorld a, BlockWorld b)
        {
            for (int y = 0; y < a.Height; y++)
            {
                for (int x = 0; x < a.Width; x++)
                {
                    Assert.That(b.GetTypeId(x, y), Is.EqualTo(a.GetTypeId(x, y)), $"type at ({x},{y})");
                    Assert.That(b.GetHealth(x, y), Is.EqualTo(a.GetHealth(x, y)), $"health at ({x},{y})");
                }
            }
        }
    }
}
