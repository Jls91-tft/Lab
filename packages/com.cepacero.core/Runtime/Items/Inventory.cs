using System;
using System.Collections.Generic;
using CepaCero.Util;

namespace CepaCero.Items
{
    /// <summary>
    /// Slot-based inventory (D-012): a bag measured in slots, where each stack
    /// uses its item's <c>size</c> in slots, plus an optional quick-access bar
    /// of single stacks. Players have 20 + 5; containers have a bag only.
    /// Host-authoritative: clients get a copy of their own inventory whenever
    /// <see cref="Version"/> changes.
    /// </summary>
    public sealed class Inventory
    {
        private readonly ItemCatalog _catalog;
        private readonly List<ItemStack> _bag = new List<ItemStack>();
        private readonly ItemStack[] _hotbar;

        public Inventory(ItemCatalog catalog, int bagSlots, int hotbarSlots = 0)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (bagSlots < 0 || hotbarSlots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bagSlots));
            }
            BagSlots = bagSlots;
            _hotbar = new ItemStack[hotbarSlots];
        }

        public static Inventory ForPlayer(ItemCatalog catalog) => new Inventory(catalog, 27, 9);

        public ItemCatalog Catalog => _catalog;

        public int BagSlots { get; }

        public int HotbarSlots => _hotbar.Length;

        /// <summary>Increments on every change: the host resends the inventory to its owner.</summary>
        public int Version { get; private set; }

        public IReadOnlyList<ItemStack> Bag => _bag;

        public ItemStack GetHotbar(int slot) => _hotbar[slot];

        public int SelectedHotbar { get; private set; }

        public ItemStack Equipped => _hotbar.Length == 0 ? default : _hotbar[SelectedHotbar];

        public ItemDefinition EquippedDefinition => Equipped.IsEmpty ? null : _catalog[Equipped.Item];

        public int UsedSlots
        {
            get
            {
                int used = 0;
                foreach (ItemStack s in _bag)
                {
                    used += _catalog[s.Item].size;
                }
                return used;
            }
        }

        public int FreeSlots => BagSlots - UsedSlots;

        public bool IsEmpty
        {
            get
            {
                if (_bag.Count > 0)
                {
                    return false;
                }
                foreach (ItemStack s in _hotbar)
                {
                    if (!s.IsEmpty)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public void SelectHotbar(int slot)
        {
            if (slot < 0 || slot >= _hotbar.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }
            if (SelectedHotbar != slot)
            {
                SelectedHotbar = slot;
                Version++;
            }
        }

        public int Count(ushort item)
        {
            int total = 0;
            foreach (ItemStack s in _bag)
            {
                total += s.Item == item ? s.Count : 0;
            }
            foreach (ItemStack s in _hotbar)
            {
                total += !s.IsEmpty && s.Item == item ? s.Count : 0;
            }
            return total;
        }

        /// <summary>
        /// Adds to the bag: tops up existing stacks, then opens new ones while there
        /// is room. Returns how many were added (the rest stays where it was).
        /// Items with durability always go in their own stack.
        /// </summary>
        public int Add(ushort item, int count, ushort durability = 0)
        {
            if (count <= 0)
            {
                return 0;
            }
            ItemDefinition def = _catalog[item];
            if (def.HasDurability && durability == 0)
            {
                durability = (ushort)Math.Min(ushort.MaxValue, def.durability);
            }
            int left = count;
            if (!def.HasDurability)
            {
                for (int i = 0; i < _bag.Count && left > 0; i++)
                {
                    ItemStack s = _bag[i];
                    if (s.Item != item || s.Count >= def.stack)
                    {
                        continue;
                    }
                    int put = Math.Min(left, def.stack - s.Count);
                    s.Count = (ushort)(s.Count + put);
                    _bag[i] = s;
                    left -= put;
                }
            }
            while (left > 0 && FreeSlots >= def.size)
            {
                int put = def.HasDurability ? 1 : Math.Min(left, def.stack);
                _bag.Add(new ItemStack(item, (ushort)put, durability));
                left -= put;
            }
            if (left != count)
            {
                Version++;
            }
            return count - left;
        }

        /// <summary>Removes up to <paramref name="count"/> (bag first, then the quick-access bar). Returns how many.</summary>
        public int Remove(ushort item, int count)
        {
            int left = count;
            for (int i = _bag.Count - 1; i >= 0 && left > 0; i--)
            {
                ItemStack s = _bag[i];
                if (s.Item != item)
                {
                    continue;
                }
                int take = Math.Min(left, s.Count);
                left -= take;
                s.Count = (ushort)(s.Count - take);
                if (s.Count == 0)
                {
                    _bag.RemoveAt(i);
                }
                else
                {
                    _bag[i] = s;
                }
            }
            for (int h = 0; h < _hotbar.Length && left > 0; h++)
            {
                if (_hotbar[h].IsEmpty || _hotbar[h].Item != item)
                {
                    continue;
                }
                int take = Math.Min(left, _hotbar[h].Count);
                left -= take;
                _hotbar[h].Count = (ushort)(_hotbar[h].Count - take);
                if (_hotbar[h].Count == 0)
                {
                    _hotbar[h] = default;
                }
            }
            if (left != count)
            {
                Version++;
            }
            return count - left;
        }

        /// <summary>Moves a bag stack to a quick-access slot, swapping with whatever was there.</summary>
        public bool MoveToHotbar(int bagIndex, int hotbarSlot)
        {
            if (bagIndex < 0 || bagIndex >= _bag.Count || hotbarSlot < 0 || hotbarSlot >= _hotbar.Length)
            {
                return false;
            }
            ItemStack incoming = _bag[bagIndex];
            ItemStack outgoing = _hotbar[hotbarSlot];
            _bag.RemoveAt(bagIndex);
            if (!outgoing.IsEmpty && FreeSlots < _catalog[outgoing.Item].size)
            {
                _bag.Insert(bagIndex, incoming); // no room to swap back
                return false;
            }
            _hotbar[hotbarSlot] = incoming;
            if (!outgoing.IsEmpty)
            {
                _bag.Add(outgoing);
            }
            Version++;
            return true;
        }

        public bool MoveToBag(int hotbarSlot)
        {
            if (hotbarSlot < 0 || hotbarSlot >= _hotbar.Length || _hotbar[hotbarSlot].IsEmpty)
            {
                return false;
            }
            ItemStack s = _hotbar[hotbarSlot];
            if (FreeSlots < _catalog[s.Item].size)
            {
                return false;
            }
            _bag.Add(s);
            _hotbar[hotbarSlot] = default;
            Version++;
            return true;
        }

        /// <summary>Takes a whole bag stack out (to move it to a container or drop it).</summary>
        public bool TakeBagStack(int bagIndex, out ItemStack stack)
        {
            if (bagIndex < 0 || bagIndex >= _bag.Count)
            {
                stack = default;
                return false;
            }
            stack = _bag[bagIndex];
            _bag.RemoveAt(bagIndex);
            Version++;
            return true;
        }

        /// <summary>Puts a whole stack (keeping its durability) if it fits; merges if possible.</summary>
        public bool PutStack(ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                return true;
            }
            ItemDefinition def = _catalog[stack.Item];
            if (def.HasDurability)
            {
                if (FreeSlots < def.size)
                {
                    return false;
                }
                _bag.Add(stack);
                Version++;
                return true;
            }
            int room = 0;
            foreach (ItemStack s in _bag)
            {
                room += s.Item == stack.Item ? def.stack - s.Count : 0;
            }
            room += FreeSlots / def.size * def.stack;
            if (room < stack.Count)
            {
                return false;
            }
            Add(stack.Item, stack.Count);
            return true;
        }

        /// <summary>Uses one durability point of the equipped item; it breaks at 0. Returns true if it broke.</summary>
        public bool WearEquipped()
        {
            if (_hotbar.Length == 0)
            {
                return false;
            }
            ref ItemStack s = ref _hotbar[SelectedHotbar];
            if (s.IsEmpty || s.Durability == 0)
            {
                return false;
            }
            s.Durability--;
            Version++;
            if (s.Durability > 0)
            {
                return false;
            }
            s = default;
            return true;
        }

        /// <summary>Everything, emptying the inventory (dropping the backpack on death).</summary>
        public void TakeAll(List<ItemStack> into)
        {
            into.AddRange(_bag);
            _bag.Clear();
            for (int h = 0; h < _hotbar.Length; h++)
            {
                if (!_hotbar[h].IsEmpty)
                {
                    into.Add(_hotbar[h]);
                    _hotbar[h] = default;
                }
            }
            Version++;
        }

        /// <summary>Owner sync and saves: selected, hotbar, bag (item index, count, durability).</summary>
        public void Write(ByteWriter writer)
        {
            writer.WriteByte((byte)SelectedHotbar);
            writer.WriteByte((byte)_hotbar.Length);
            foreach (ItemStack s in _hotbar)
            {
                WriteStack(writer, s);
            }
            writer.WriteByte((byte)_bag.Count);
            foreach (ItemStack s in _bag)
            {
                WriteStack(writer, s);
            }
        }

        /// <summary>Replaces the contents. <paramref name="remap"/> translates saved item indices (null = same).</summary>
        public void Read(ByteReader reader, ushort[] remap = null)
        {
            int selected = reader.ReadByte();
            int hotbar = reader.ReadByte();
            if (hotbar != _hotbar.Length)
            {
                throw new FormatException($"Inventory has {_hotbar.Length} quick slots, data has {hotbar}.");
            }
            for (int h = 0; h < hotbar; h++)
            {
                _hotbar[h] = ReadStack(reader, remap);
            }
            int bag = reader.ReadByte();
            _bag.Clear();
            for (int i = 0; i < bag; i++)
            {
                _bag.Add(ReadStack(reader, remap));
            }
            SelectedHotbar = hotbar == 0 ? 0 : Math.Min(selected, hotbar - 1);
            Version++;
        }

        private static void WriteStack(ByteWriter writer, ItemStack s)
        {
            writer.WriteUShort(s.Item);
            writer.WriteUShort(s.Count);
            writer.WriteUShort(s.Durability);
        }

        private ItemStack ReadStack(ByteReader reader, ushort[] remap)
        {
            var s = new ItemStack(reader.ReadUShort(), reader.ReadUShort(), reader.ReadUShort());
            if (s.IsEmpty)
            {
                return default;
            }
            if (remap != null)
            {
                s.Item = s.Item < remap.Length ? remap[s.Item] : ushort.MaxValue;
            }
            if (s.Item >= _catalog.Count)
            {
                throw new FormatException($"Unknown item index {s.Item} in inventory data.");
            }
            return s;
        }
    }
}
