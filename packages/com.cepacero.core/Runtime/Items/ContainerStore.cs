using System.Collections.Generic;
using CepaCero.Map;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Items
{
    /// <summary>
    /// Every lootable container of the match: the ones placed inside map
    /// buildings (filled from their kind's loot table the first time someone
    /// opens them) and the chests players build. Host only; clients get the
    /// contents of a container while they have it open.
    /// </summary>
    public sealed class ContainerStore
    {
        public const int ContainerSlots = 12;
        public const int ChestSlots = 30;

        private readonly ItemCatalog _items;
        private readonly Dictionary<Int2, Entry> _containers = new Dictionary<Int2, Entry>();

        private sealed class Entry
        {
            public string Kind;
            public bool Rolled;
            public Inventory Contents;
        }

        public ContainerStore(ItemCatalog items)
        {
            _items = items;
        }

        public int Count => _containers.Count;

        public IEnumerable<Int2> Positions => _containers.Keys;

        public bool Has(Int2 cell) => _containers.ContainsKey(cell);

        /// <summary>Building kind of a map container ("" for player chests, null if there is none).</summary>
        public string KindAt(Int2 cell) => _containers.TryGetValue(cell, out Entry e) ? e.Kind : null;

        /// <summary>
        /// Places containers inside each building, against an inner wall when possible
        /// (cupboards and shelves), with the match seed. Call once after importing the map.
        /// </summary>
        public void PlaceInBuildings(IReadOnlyList<BuildingInfo> buildings, BlockWorld blocks, GroundLayer ground,
            LootRoller loot, DeterministicRandom rng)
        {
            var candidates = new List<Int2>();
            foreach (BuildingInfo b in buildings)
            {
                candidates.Clear();
                for (int y = b.Min.Y + 1; y < b.Max.Y; y++)
                {
                    for (int x = b.Min.X + 1; x < b.Max.X; x++)
                    {
                        if (ground.Get(x, y) == GroundType.Floor && !blocks.IsSolid(x, y) && TouchesWall(blocks, x, y)
                            && !IsNextToDoor(blocks, x, y))
                        {
                            candidates.Add(new Int2(x, y));
                        }
                    }
                }
                int wanted = loot.ContainersFor(b.Tiles);
                for (int k = 0; k < wanted && candidates.Count > 0; k++)
                {
                    int pick = rng.Range(0, candidates.Count);
                    Int2 cell = candidates[pick];
                    candidates.RemoveAt(pick);
                    _containers[cell] = new Entry { Kind = b.Kind, Contents = new Inventory(_items, ContainerSlots) };
                }
            }
        }

        /// <summary>A chest a player built: starts empty and never rolls loot.</summary>
        public void AddChest(Int2 cell) =>
            _containers[cell] = new Entry { Kind = "", Rolled = true, Contents = new Inventory(_items, ChestSlots) };

        /// <summary>Removing a chest (destroyed): returns what was inside so it can drop to the ground.</summary>
        public void Remove(Int2 cell, List<ItemStack> spill)
        {
            if (_containers.TryGetValue(cell, out Entry e))
            {
                e.Contents.TakeAll(spill);
                _containers.Remove(cell);
            }
        }

        /// <summary>Contents of a container, rolling its loot the first time (no respawn by default, D-012).</summary>
        public Inventory Open(Int2 cell, LootRoller loot, DeterministicRandom rng)
        {
            if (!_containers.TryGetValue(cell, out Entry e))
            {
                return null;
            }
            if (!e.Rolled)
            {
                loot.FillContainer(e.Kind, e.Contents, rng);
                e.Rolled = true;
            }
            return e.Contents;
        }

        /// <summary>Loot respawn option: containers that are empty get rolled again next time they are opened.</summary>
        public int ResetEmpty()
        {
            int n = 0;
            foreach (Entry e in _containers.Values)
            {
                if (e.Rolled && e.Kind.Length > 0 && e.Contents.IsEmpty)
                {
                    e.Rolled = false;
                    n++;
                }
            }
            return n;
        }

        // Saves: kind, rolled flag and contents per container.
        public void Write(ByteWriter writer)
        {
            writer.WriteInt(_containers.Count);
            foreach (KeyValuePair<Int2, Entry> kv in _containers)
            {
                writer.WriteUShort((ushort)kv.Key.X);
                writer.WriteUShort((ushort)kv.Key.Y);
                writer.WriteString(kv.Value.Kind);
                writer.WriteByte(kv.Value.Rolled ? (byte)1 : (byte)0);
                writer.WriteByte((byte)kv.Value.Contents.BagSlots);
                kv.Value.Contents.Write(writer);
            }
        }

        public void Read(ByteReader reader, ushort[] itemRemap)
        {
            _containers.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var cell = new Int2(reader.ReadUShort(), reader.ReadUShort());
                var e = new Entry { Kind = reader.ReadString(), Rolled = reader.ReadByte() != 0 };
                e.Contents = new Inventory(_items, reader.ReadByte());
                e.Contents.Read(reader, itemRemap);
                _containers[cell] = e;
            }
        }

        private static bool TouchesWall(BlockWorld blocks, int x, int y) =>
            blocks.IsSolid(x + 1, y) || blocks.IsSolid(x - 1, y) || blocks.IsSolid(x, y + 1) || blocks.IsSolid(x, y - 1);

        private static bool IsNextToDoor(BlockWorld blocks, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (blocks.InBounds(x + dx, y + dy) && blocks.GetBlock(x + dx, y + dy).ToggleTo != 0)
                    {
                        return true; // keep doorways clear
                    }
                }
            }
            return false;
        }
    }
}
