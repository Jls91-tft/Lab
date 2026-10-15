using System;
using System.Collections.Generic;

namespace CepaCero.Items
{
    /// <summary>
    /// Item types of a match. Inventories store the item index (ushort);
    /// saves store the ids so they survive new items being added.
    /// </summary>
    public sealed class ItemCatalog
    {
        public const string Format = "cepacero.items";

        private readonly List<ItemDefinition> _byIndex = new List<ItemDefinition>();
        private readonly Dictionary<string, ushort> _indexById = new Dictionary<string, ushort>(StringComparer.Ordinal);

        public ItemCatalog(ItemsData data)
        {
            if (data == null || data.format != Format || data.version != 1)
            {
                throw new FormatException($"Not a {Format} v1 document.");
            }
            if (data.items == null || data.items.Length == 0 || data.items.Length >= ushort.MaxValue)
            {
                throw new FormatException("An item catalog needs at least one item.");
            }
            foreach (ItemDefinition item in data.items)
            {
                if (string.IsNullOrEmpty(item.id) || item.stack < 1 || item.size < 1 || _indexById.ContainsKey(item.id))
                {
                    throw new FormatException($"Invalid or duplicate item '{item.id}'.");
                }
                _indexById.Add(item.id, (ushort)_byIndex.Count);
                _byIndex.Add(item);
            }
        }

        public int Count => _byIndex.Count;

        public ItemDefinition this[ushort index] => _byIndex[index];

        public bool TryGetIndex(string id, out ushort index) => _indexById.TryGetValue(id, out index);

        public ushort IndexOf(string id)
        {
            if (!_indexById.TryGetValue(id, out ushort index))
            {
                throw new KeyNotFoundException($"Unknown item '{id}'.");
            }
            return index;
        }
    }
}
