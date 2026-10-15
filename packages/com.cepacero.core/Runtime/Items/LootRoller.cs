using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Util;

namespace CepaCero.Items
{
    /// <summary>Rolls loot tables (buildings and zombies) with the match's deterministic RNG.</summary>
    public sealed class LootRoller
    {
        public const string Format = "cepacero.loot";

        private readonly ItemCatalog _items;
        private readonly Dictionary<string, LootTable> _byKind = new Dictionary<string, LootTable>(System.StringComparer.Ordinal);

        public LootRoller(LootData data, ItemCatalog items)
        {
            if (data == null || data.format != Format || data.version != 1 || data.tables == null)
            {
                throw new System.FormatException($"Not a {Format} v1 document.");
            }
            _items = items;
            TilesPerContainer = System.Math.Max(1, data.tiles_per_container);
            MaxContainers = System.Math.Max(1, data.max_containers);
            foreach (LootTable t in data.tables)
            {
                _byKind[t.kind] = t;
            }
        }

        public int TilesPerContainer { get; }

        public int MaxContainers { get; }

        /// <summary>How many containers a building of this many floor tiles gets.</summary>
        public int ContainersFor(int buildingTiles) => System.Math.Min(MaxContainers, 1 + buildingTiles / TilesPerContainer);

        /// <summary>Fills a container for a building kind (unknown kinds use "generic"). Returns stacks that did not fit.</summary>
        public int FillContainer(string kind, Inventory into, DeterministicRandom rng)
        {
            if (!_byKind.TryGetValue(kind ?? "", out LootTable table) && !_byKind.TryGetValue("generic", out table))
            {
                return 0;
            }
            int lost = 0;
            for (int r = 0; r < table.rolls; r++)
            {
                lost += Roll(table.loot, into, rng);
            }
            return lost;
        }

        /// <summary>A zombie's drop (bestiary loot), as stacks for a pile on the ground.</summary>
        public void RollDrop(ZombieDefinition zombie, List<ItemStack> into, DeterministicRandom rng)
        {
            if (zombie.loot == null)
            {
                return;
            }
            foreach (LootEntry e in zombie.loot)
            {
                if (!rng.Chance(e.chance) || !_items.TryGetIndex(e.item, out ushort item))
                {
                    continue;
                }
                int count = rng.Range(e.min, e.max + 1);
                if (count > 0)
                {
                    ItemDefinition def = _items[item];
                    into.Add(new ItemStack(item, (ushort)count, (ushort)System.Math.Max(0, def.durability)));
                }
            }
        }

        private int Roll(LootEntry[] entries, Inventory into, DeterministicRandom rng)
        {
            int lost = 0;
            foreach (LootEntry e in entries)
            {
                if (!rng.Chance(e.chance) || !_items.TryGetIndex(e.item, out ushort item))
                {
                    continue;
                }
                int count = rng.Range(e.min, e.max + 1);
                lost += count - into.Add(item, count);
            }
            return lost;
        }
    }
}
