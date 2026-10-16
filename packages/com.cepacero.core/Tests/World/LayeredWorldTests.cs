using System;
using System.Collections.Generic;
using System.Linq;
using CepaCero.Building;
using CepaCero.Core;
using CepaCero.Net;
using CepaCero.Net.Protocol;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Tests.TestSupport;
using CepaCero.Util;
using CepaCero.World;
using NUnit.Framework;

namespace CepaCero.Tests.World
{
    /// <summary>Levels, layers and the colour/rotation byte of a cell (D-022 phase B).</summary>
    public class LayeredWorldTests
    {
        private ConstructionData _construction;
        private BlockRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _construction = Fixtures.Construction();
            _registry = BlockRegistry.CreateDefault(_construction);
        }

        private ushort Id(string key) => _registry.Get(key).Id;

        private BlockWorld NewWorld(int size = 64) => new BlockWorld(size, size, _registry);

        [Test]
        public void LayersOfTheSameCellAreIndependent()
        {
            BlockWorld w = NewWorld();
            w.SetBlock(5, 5, 0, BuildLayer.Floor, Id("floor_wood"));
            w.SetBlock(5, 5, 0, BuildLayer.Structure, Id("wall_wood"));
            w.SetBlock(5, 5, 0, BuildLayer.Roof, Id("roof_tile"));
            Assert.That(w.GetBlock(5, 5, 0, BuildLayer.Floor).Key, Is.EqualTo("floor_wood"));
            Assert.That(w.GetBlock(5, 5).Key, Is.EqualTo("wall_wood"), "the 2D view is the ground-level structure");
            Assert.That(w.GetBlock(5, 5, 0, BuildLayer.Roof).Key, Is.EqualTo("roof_tile"));
            Assert.That(w.GetBlock(5, 5, 0, BuildLayer.Object).IsAir, Is.True);

            Assert.That(w.Damage(5, 5, 1000), Is.EqualTo(DamageResult.Destroyed));
            Assert.That(w.GetBlock(5, 5).IsAir, Is.True);
            Assert.That(w.GetBlock(5, 5, 0, BuildLayer.Floor).Key, Is.EqualTo("floor_wood"), "breaking a wall leaves the floor");
        }

        [Test]
        public void TheTwoDimensionalCallsRouteByTheLayerOfTheBlock()
        {
            BlockWorld w = NewWorld();
            w.SetBlock(3, 3, Id("floor_stone"));                      // the old call, but the block says "floor"
            Assert.That(w.GetBlock(3, 3).IsAir, Is.True);
            Assert.That(w.GetBlock(3, 3, 0, BuildLayer.Floor).Key, Is.EqualTo("floor_stone"));
            Assert.That(w.IsSolid(3, 3), Is.False);
        }

        [Test]
        public void LevelsAreIndependentAndRangeChecked()
        {
            BlockWorld w = NewWorld();
            w.SetBlock(7, 7, -2, BuildLayer.Structure, Id("earth"));
            w.SetBlock(7, 7, 3, BuildLayer.Structure, Id("wall_brick"));
            Assert.That(w.GetBlock(7, 7, -2, BuildLayer.Structure).Key, Is.EqualTo("earth"));
            Assert.That(w.GetBlock(7, 7, 3, BuildLayer.Structure).Key, Is.EqualTo("wall_brick"));
            Assert.That(w.GetBlock(7, 7).IsAir, Is.True, "the ground level is untouched");
            Assert.Throws<ArgumentOutOfRangeException>(() => w.SetBlock(7, 7, 4, BuildLayer.Structure, Id("wall_brick")));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.GetTypeId(7, 7, -4, BuildLayer.Structure));
            Assert.That(new BlockWorld(8, 8, _registry, -1, 1).LevelCount, Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlockWorld(8, 8, _registry, 1, 2));
        }

        [Test]
        public void ABlockOnTheWrongLayerIsRejected()
        {
            BlockWorld w = NewWorld();
            Assert.Throws<ArgumentException>(() => w.SetBlock(1, 1, 0, BuildLayer.Structure, Id("floor_wood")));
            Assert.Throws<ArgumentException>(() => w.SetBlock(1, 1, 0, BuildLayer.Roof, Id("wall_wood")));
        }

        [Test]
        public void TheVariantStoresColourAndRotationAndAirForgetsIt()
        {
            BlockWorld w = NewWorld();
            byte v = BlockVariant.Make(5, 3);
            Assert.That(BlockVariant.Color(v), Is.EqualTo(5));
            Assert.That(BlockVariant.Rotation(v), Is.EqualTo(3));
            w.SetBlock(2, 2, 0, BuildLayer.Structure, Id("wall_wood"), v);
            Assert.That(w.GetVariant(2, 2, 0, BuildLayer.Structure), Is.EqualTo(v));
            Assert.That(w.GetVariant(3, 2, 0, BuildLayer.Structure), Is.Zero);
            w.Damage(2, 2, 10);
            Assert.That(w.GetVariant(2, 2, 0, BuildLayer.Structure), Is.EqualTo(v), "damage keeps the paint");
            w.Damage(2, 2, 1000);
            Assert.That(w.GetVariant(2, 2, 0, BuildLayer.Structure), Is.Zero, "air has no variant");
        }

        [Test]
        public void OnlyPaintablePiecesTakePaintAndColoursStayInRange()
        {
            BlockWorld w = NewWorld();
            byte painted = BlockVariant.Make(4, 2);
            Assert.That(w.TryPlace(1, 1, 0, Id("wall_wood"), painted), Is.EqualTo(PlaceResult.Placed));
            Assert.That(w.TryPlace(2, 1, 0, Id("wall_brick"), painted), Is.EqualTo(PlaceResult.Placed));
            Assert.That(w.TryPlace(3, 1, 0, Id("wall_wood"), BlockVariant.Make(31, 1)), Is.EqualTo(PlaceResult.Placed));
            Assert.That(BlockVariant.Color(w.GetVariant(1, 1, 0, BuildLayer.Structure)), Is.EqualTo(4), "wood is paintable");
            Assert.That(BlockVariant.Color(w.GetVariant(2, 1, 0, BuildLayer.Structure)), Is.Zero, "brick is not");
            Assert.That(BlockVariant.Rotation(w.GetVariant(2, 1, 0, BuildLayer.Structure)), Is.EqualTo(2), "rotation always counts");
            Assert.That(BlockVariant.Color(w.GetVariant(3, 1, 0, BuildLayer.Structure)), Is.Zero, "colour 31 does not exist");
        }

        [Test]
        public void TryPlaceChecksTheLayerOfTheBlock()
        {
            BlockWorld w = NewWorld();
            Assert.That(w.TryPlace(4, 4, 0, Id("floor_wood"), 0), Is.EqualTo(PlaceResult.Placed));
            Assert.That(w.TryPlace(4, 4, 0, Id("floor_stone"), 0), Is.EqualTo(PlaceResult.Occupied), "the floor is taken");
            Assert.That(w.TryPlace(4, 4, 0, Id("wall_wood"), 0), Is.EqualTo(PlaceResult.Placed), "the wall goes on top of the floor");
            Assert.That(w.TryPlace(4, 4, 0, Id("roof_tile"), 0), Is.EqualTo(PlaceResult.Placed));
            Assert.That(w.TryPlace(4, 4, 9, Id("wall_wood"), 0), Is.EqualTo(PlaceResult.OutOfBounds));
            Assert.That(w.TryPlace(4, 4, -1, Id("earth"), 0), Is.EqualTo(PlaceResult.Placed), "underground does not need buildable ground");
        }

        [Test]
        public void OnlyGroundStructuresRaiseTypeChangedAndStructureRevision()
        {
            BlockWorld w = NewWorld();
            var typeChanged = new List<(int, int)>();
            var cellChanged = new List<(int, int, int, BuildLayer)>();
            w.TypeChanged += (x, y, a, b) => typeChanged.Add((x, y));
            w.CellChanged += (x, y, z, l, a, b) => cellChanged.Add((x, y, z, l));
            int revision = w.StructureRevision;

            w.SetBlock(1, 1, 0, BuildLayer.Floor, Id("floor_wood"));
            w.SetBlock(1, 1, 2, BuildLayer.Structure, Id("wall_wood"));
            Assert.That(typeChanged, Is.Empty);
            Assert.That(w.StructureRevision, Is.EqualTo(revision), "paths on the ground are not affected by a floor or an upper wall");

            w.SetBlock(1, 1, Id("wall_wood"));
            Assert.That(typeChanged, Is.EqualTo(new[] { (1, 1) }));
            Assert.That(w.StructureRevision, Is.EqualTo(revision + 1));
            Assert.That(cellChanged.Count, Is.EqualTo(3));
            Assert.That(cellChanged[1], Is.EqualTo((1, 1, 2, BuildLayer.Structure)));
        }

        [Test]
        public void ChangesCarryLevelLayerAndVariantAndAReplicaConverges()
        {
            BlockWorld host = NewWorld();
            host.SetBlock(9, 9, 0, BuildLayer.Floor, Id("floor_wood"), BlockVariant.Make(2, 0));
            host.SetBlock(9, 9, -1, BuildLayer.Structure, Id("rock"));
            host.SetBlock(9, 9, 2, BuildLayer.Roof, Id("roof_sheet"), BlockVariant.Make(0, 1));
            host.Toggle(9, 9, 0, BuildLayer.Floor);                                              // not toggleable: no change
            var changes = new List<BlockChange>();
            host.DrainChanges(changes);
            Assert.That(changes.Count, Is.EqualTo(3));
            Assert.That(changes[2].Z, Is.EqualTo((sbyte)2));
            Assert.That(changes[2].Layer, Is.EqualTo(BuildLayer.Roof));

            var writer = new ByteWriter();
            BlockChange.WriteBatch(changes, writer);
            Assert.That(writer.Length, Is.EqualTo(2 + 3 * BlockChange.Bytes));
            var received = new List<BlockChange>();
            BlockChange.ReadBatch(new ByteReader(writer.Segment), received);
            BlockWorld replica = NewWorld();
            foreach (BlockChange c in received)
            {
                replica.ApplyChange(c);
            }
            AssertSame(host, replica);
        }

        [Test]
        public void ChunkPlanesTravelOneMessageEachAndTheFirstResetsTheChunk()
        {
            BlockWorld host = NewWorld();
            host.SetBlock(40, 40, 0, BuildLayer.Floor, Id("floor_wood"), BlockVariant.Make(7, 0));
            host.SetBlock(40, 40, 0, BuildLayer.Structure, Id("wall_brick"));
            host.SetBlock(41, 40, -2, BuildLayer.Structure, Id("earth"));
            host.SetBlock(42, 41, 3, BuildLayer.Roof, Id("roof_tile"), BlockVariant.Make(0, 2));
            var planes = new List<KeyValuePair<int, BuildLayer>>();
            host.GetChunkPlanes(1, 1, planes);
            Assert.That(planes.Count, Is.EqualTo(4), "ground structure, ground floor, one underground, one roof: no empty planes");
            Assert.That(planes[0], Is.EqualTo(new KeyValuePair<int, BuildLayer>(0, BuildLayer.Structure)));

            BlockWorld replica = NewWorld();
            replica.SetBlock(45, 45, 1, BuildLayer.Structure, Id("wall_wood"));                  // stale content that the reset must wipe
            replica.SetBlock(45, 45, Id("door_wood"));
            for (int i = 0; i < planes.Count; i++)
            {
                var w = new ByteWriter();
                host.WriteChunkPlane(1, 1, planes[i].Key, planes[i].Value, i == 0, w);
                replica.ReadChunkPlane(new ByteReader(w.Segment));
            }
            AssertSame(host, replica);
            Assert.That(replica.GetBlock(45, 45).IsAir, Is.True);
            Assert.That(replica.GetBlock(45, 45, 1, BuildLayer.Structure).IsAir, Is.True);
        }

        [Test]
        public void AWholeChunkRoundTripsForSaves()
        {
            BlockWorld host = NewWorld();
            host.SetBlock(33, 33, 0, BuildLayer.Floor, Id("floor_stone"));
            host.SetBlock(34, 33, 1, BuildLayer.Structure, Id("stairs_wood"), BlockVariant.Make(0, 3));
            host.SetBlock(35, 35, -3, BuildLayer.Structure, Id("rock"));
            var writer = new ByteWriter();
            host.WriteChunk(1, 1, writer);
            BlockWorld loaded = NewWorld();
            loaded.ReadChunk(new ByteReader(writer.Segment));
            AssertSame(host, loaded);
        }

        [Test]
        public void EmptyLevelsAreNotSent()
        {
            BlockWorld w = NewWorld();
            var planes = new List<KeyValuePair<int, BuildLayer>>();
            w.GetChunkPlanes(0, 0, planes);
            Assert.That(planes.Count, Is.EqualTo(1), "just the ground structure, the plane a snapshot resets");
            var writer = new ByteWriter();
            w.WriteChunk(0, 0, writer);
            Assert.That(writer.Length, Is.EqualTo(4 + 1 + 2 + 7));
        }

        [Test]
        public void ChunksOfOlderSavesStillLoad()
        {
            var legacy = new ByteWriter();                       // the v4 layout: chunkX, chunkY, runs of [count, type, health]
            legacy.WriteUShort(0);
            legacy.WriteUShort(0);
            legacy.WriteUShort(10);
            legacy.WriteUShort(0);
            legacy.WriteUShort(0);
            legacy.WriteUShort(1);
            legacy.WriteUShort(Id("wall_wood"));
            legacy.WriteUShort(100);
            legacy.WriteUShort(Chunk.CellCount - 11);
            legacy.WriteUShort(0);
            legacy.WriteUShort(0);
            BlockWorld w = NewWorld();
            w.SetBlock(5, 5, 1, BuildLayer.Structure, Id("wall_brick"));                          // must be cleared by the load
            w.ReadChunkLegacy(new ByteReader(legacy.Segment));
            Assert.That(w.GetBlock(10, 0).Key, Is.EqualTo("wall_wood"));
            Assert.That(w.GetHealth(10, 0), Is.EqualTo(100));
            Assert.That(w.GetBlock(11, 0).IsAir, Is.True);
            Assert.That(w.GetBlock(5, 5, 1, BuildLayer.Structure).IsAir, Is.True);
        }

        [Test]
        public void ACorruptPlaneIsRefused()
        {
            var w = new ByteWriter();
            w.WriteUShort(0); w.WriteUShort(0); w.WriteByte(1);
            w.WriteByte(100);                                   // level 100: not in this world
            w.WriteByte(0);
            BlockWorld world = NewWorld();
            Assert.Throws<FormatException>(() => world.ReadChunkPlane(new ByteReader(w.Segment)));
        }

        // ------------------------------------------------------------------ through the match
        private GameData Data() => Fixtures.GameDataWithConstruction();

        private static GameSettings Calm() => new GameSettings
        {
            TickRate = 20, DaySeconds = 600, DuskSeconds = 10, NightSeconds = 60, DawnSeconds = 10, HordeBase = 0,
        };

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

        [Test]
        public void PlacingByIntentUsesTheLayerAndKeepsPaintAndRotation()
        {
            GameData data = Data();
            var map = new AsciiMap(_registry, Enumerable.Repeat(new string('.', 20), 20).ToArray());
            var s = new GameSession(Calm(), new PlayerSettings(), map.Blocks, map.Ground, data);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            s.Inventories[p].Add(data.Items.IndexOf("wood"), 20);
            s.Inventories[p].Add(data.Items.IndexOf("nails"), 10);
            byte variant = BlockVariant.Make(6, 1);

            Assert.That(Do(s, p, PlayerIntent.Place(6, 6, Id("floor_wood"))), Is.EqualTo(IntentResult.Ok));
            Assert.That(Do(s, p, PlayerIntent.Place(6, 6, Id("wall_wood"), variant)), Is.EqualTo(IntentResult.Ok), "wall and floor share the cell");
            Assert.That(map.Blocks.GetBlock(6, 6, 0, BuildLayer.Floor).Key, Is.EqualTo("floor_wood"));
            Assert.That(map.Blocks.GetBlock(6, 6).Key, Is.EqualTo("wall_wood"));
            Assert.That(map.Blocks.GetVariant(6, 6, 0, BuildLayer.Structure), Is.EqualTo(variant));
            Assert.That(Do(s, p, PlayerIntent.Place(6, 6, Id("wall_wood"), variant)), Is.EqualTo(IntentResult.Occupied), "the same piece again");
            Assert.That(Do(s, p, PlayerIntent.Place(6, 7, Id("wall_wood"), 0, z: 1)), Is.EqualTo(IntentResult.InvalidTarget),
                "players only act on their own level until they can climb");
        }

        [Test]
        public void AFloorCanBeLaidUnderAPlayer()
        {
            GameData data = Data();
            var map = new AsciiMap(_registry, Enumerable.Repeat(new string('.', 20), 20).ToArray());
            var s = new GameSession(Calm(), new PlayerSettings(), map.Blocks, map.Ground, data);
            int p = s.AddPlayer(new Float2(5.5f, 5.5f));
            s.Inventories[p].Add(data.Items.IndexOf("wood"), 20);
            s.Inventories[p].Add(data.Items.IndexOf("nails"), 10);
            Assert.That(Do(s, p, PlayerIntent.Place(5, 5, Id("floor_wood"))), Is.EqualTo(IntentResult.Ok), "a floor does not trap anyone");
            Assert.That(Do(s, p, PlayerIntent.Place(5, 5, Id("wall_wood"))), Is.EqualTo(IntentResult.Occupied), "a wall would");
        }

        [Test]
        public void TheClientReplicaGetsEveryLevelLayerAndVariantOnJoinAndInDeltas()
        {
            GameData data = Data();
            var hostMap = new AsciiMap(_registry, Enumerable.Repeat(new string('.', 40), 40).ToArray());
            var host = new GameSession(Calm(), new PlayerSettings(), hostMap.Blocks, hostMap.Ground, data);
            host.AddPlayer(new Float2(4.5f, 4.5f));
            int remote = host.AddPlayer(new Float2(6.5f, 4.5f));
            hostMap.Blocks.SetBlock(20, 20, 0, BuildLayer.Floor, Id("floor_wood"), BlockVariant.Make(3, 0));
            hostMap.Blocks.SetBlock(20, 20, -1, BuildLayer.Structure, Id("rock"));
            hostMap.Blocks.SetBlock(21, 20, 2, BuildLayer.Roof, Id("roof_tile"), BlockVariant.Make(0, 3));
            var broadcaster = new HostBroadcaster(host, "m");
            var join = new ByteWriter();
            broadcaster.WriteJoin(remote, join);

            var clientMap = new AsciiMap(_registry, Enumerable.Repeat(new string('.', 40), 40).ToArray());
            var client = new ClientMatchState("m", clientMap.Blocks, clientMap.Ground, data);
            client.Receive(join.Segment, 0);
            AssertSame(hostMap.Blocks, clientMap.Blocks);

            hostMap.Blocks.SetBlock(22, 22, 1, BuildLayer.Structure, Id("wall_stone"), BlockVariant.Make(0, 2));
            host.Step();
            broadcaster.CollectTick();
            var down = new ByteWriter();
            broadcaster.WriteFor(remote, down);
            client.Receive(down.Segment, 0.05);
            AssertSame(hostMap.Blocks, clientMap.Blocks);
            Assert.That(clientMap.Blocks.GetBlock(22, 22, 1, BuildLayer.Structure).Key, Is.EqualTo("wall_stone"));
        }

        [Test]
        public void SavesKeepEveryLevelAndLayer()
        {
            GameData data = Data();
            var map = new AsciiMap(_registry, Enumerable.Repeat(new string('.', 40), 40).ToArray());
            var original = new GameSession(Calm(), new PlayerSettings(), map.Blocks, map.Ground, data);
            original.AddPlayer(new Float2(4.5f, 4.5f));
            map.Blocks.SetBlock(20, 20, 0, BuildLayer.Floor, Id("floor_stone"), BlockVariant.Make(2, 1));
            map.Blocks.SetBlock(20, 20, -2, BuildLayer.Structure, Id("earth"));
            map.Blocks.SetBlock(21, 20, 3, BuildLayer.Structure, Id("pillar_brick"));
            var writer = new ByteWriter();
            SaveGame.Write(original, "m", writer);

            var again = new AsciiMap(_registry, Enumerable.Repeat(new string('.', 40), 40).ToArray());
            var loaded = new GameSession(Calm(), new PlayerSettings(), again.Blocks, again.Ground, data);
            SaveGame.Load(writer.ToArray(), loaded, "m");
            AssertSame(map.Blocks, again.Blocks);
            Assert.That(SaveGame.Version, Is.EqualTo((ushort)5));
        }

        [Test]
        public void IntentsCarryLevelAndVariantOnTheWire()
        {
            PlayerIntent i = PlayerIntent.Place(12, 34, 567, BlockVariant.Make(9, 2), z: -1);
            var w = new ByteWriter();
            i.Write(w);
            Assert.That(w.Length, Is.EqualTo(PlayerIntent.Bytes));
            PlayerIntent back = PlayerIntent.Read(new ByteReader(w.Segment));
            Assert.That(back.X, Is.EqualTo(12));
            Assert.That(back.Y, Is.EqualTo(34));
            Assert.That(back.Arg, Is.EqualTo(567));
            Assert.That(back.Z, Is.EqualTo((sbyte)-1));
            Assert.That(back.Variant, Is.EqualTo(BlockVariant.Make(9, 2)));
        }

        private static void AssertSame(BlockWorld a, BlockWorld b)
        {
            Assert.That(b.MinLevel, Is.EqualTo(a.MinLevel));
            for (int z = a.MinLevel; z <= a.MaxLevel; z++)
            {
                for (int l = 0; l < Chunk.Layers; l++)
                {
                    var layer = (BuildLayer)l;
                    for (int y = 0; y < a.Height; y++)
                    {
                        for (int x = 0; x < a.Width; x++)
                        {
                            if (a.GetTypeId(x, y, z, layer) != b.GetTypeId(x, y, z, layer) || a.GetHealth(x, y, z, layer) != b.GetHealth(x, y, z, layer)
                                || a.GetVariant(x, y, z, layer) != b.GetVariant(x, y, z, layer))
                            {
                                Assert.Fail($"Worlds differ at ({x},{y}) level {z} layer {layer}.");
                            }
                        }
                    }
                }
            }
        }
    }
}
