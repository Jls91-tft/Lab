using System.Collections.Generic;

namespace CepaCero.Map
{
    /// <summary>The furniture the interior planner places (D-026): sprite, footprint and the loot table of the containers.</summary>
    public static class FurnitureKinds
    {
        public readonly struct Info
        {
            public readonly string Sprite;
            public readonly int Width;
            public readonly int Height;
            public readonly string Loot;

            public Info(string sprite, int width, int height, string loot)
            {
                Sprite = sprite;
                Width = width;
                Height = height;
                Loot = loot;
            }
        }

        private static readonly Dictionary<string, Info> Table = new Dictionary<string, Info>(System.StringComparer.Ordinal)
        {
            // Kitchen: food and utensils.
            { "fridge", new Info("decor_fridge", 1, 1, "kitchen") },
            { "cupboard", new Info("decor_counter_0", 1, 1, "kitchen") },
            { "stove", new Info("decor_stove", 1, 1, null) },
            { "dining_table", new Info("decor_table_round", 1, 1, null) },
            { "chair", new Info("decor_chair_0", 1, 1, null) },
            // Bedroom.
            { "bed", new Info("decor_bed_0", 2, 2, null) },
            { "wardrobe", new Info("decor_wardrobe", 1, 1, "bedroom") },
            { "nightstand", new Info("decor_shelf_0", 1, 1, "bedroom") },
            // Bathroom.
            { "toilet", new Info("decor_toilet", 1, 1, null) },
            { "bathroom_cabinet", new Info("decor_counter_1", 1, 1, "bathroom") },
            // Living room.
            { "sofa", new Info("decor_armchair", 1, 1, null) },
            { "bookshelf", new Info("decor_shelf_1", 1, 1, "living") },
            { "coffee_table", new Info("decor_table_oval", 1, 1, null) },
            // Shops, offices and the rest: the building's own table.
            { "shelf", new Info("decor_shelf_0", 1, 1, "") },
            { "counter", new Info("decor_counter_2", 1, 1, "") },
            { "desk", new Info("decor_table_long", 1, 1, "") },
            // Storage.
            { "boxes", new Info("decor_boxes_0", 1, 1, "storage") },
            { "crate", new Info("decor_boxes_1", 1, 1, "storage") },
        };

        private static readonly Info Unknown = new Info("decor_boxes_0", 1, 1, null);

        public static Info Of(string kind) => kind != null && Table.TryGetValue(kind, out Info i) ? i : Unknown;

        public static bool Exists(string kind) => kind != null && Table.ContainsKey(kind);
    }
}
