using System.Collections.Generic;

namespace CepaCero.Items
{
    /// <summary>
    /// What a player gets for breaking a block (harvesting: wood comes from trees).
    /// Blocks broken by zombies give nothing. Small enough to live in code for the
    /// prototype; move to docs/ + datagen if it grows.
    /// </summary>
    public static class BlockDrops
    {
        private static readonly Dictionary<string, KeyValuePair<string, int>> Drops =
            new Dictionary<string, KeyValuePair<string, int>>(System.StringComparer.Ordinal)
            {
                { "tree", new KeyValuePair<string, int>("wood", 4) },
                { "fence", new KeyValuePair<string, int>("wood", 1) },
                { "wood_wall", new KeyValuePair<string, int>("wood", 2) },
                { "door", new KeyValuePair<string, int>("wood", 3) },
                { "door_open", new KeyValuePair<string, int>("wood", 3) },
                { "workbench", new KeyValuePair<string, int>("wood", 5) },
                { "chest", new KeyValuePair<string, int>("wood", 4) },
                { "spike_trap", new KeyValuePair<string, int>("nails", 3) },
                { "metal_wall", new KeyValuePair<string, int>("metal_scrap", 2) },
                { "building_wall", new KeyValuePair<string, int>("metal_scrap", 1) },
            };

        public static bool TryGet(string blockKey, out string item, out int count)
        {
            if (Drops.TryGetValue(blockKey, out KeyValuePair<string, int> drop))
            {
                item = drop.Key;
                count = drop.Value;
                return true;
            }
            item = null;
            count = 0;
            return false;
        }
    }
}
