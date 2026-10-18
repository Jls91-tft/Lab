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

            /// <summary>Lies on the floor (a rug): drawn flat, under people and furniture.</summary>
            public readonly bool Flat;

            /// <summary>Hangs on a wall (a picture, a curtain): its cell is the wall, and it shows on the face toward the room.</summary>
            public readonly bool OnWall;

            public Info(string sprite, int width, int height, string loot, bool flat = false, bool onWall = false)
            {
                Sprite = sprite;
                Width = width;
                Height = height;
                Loot = loot;
                Flat = flat;
                OnWall = onWall;
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
            // Floor and walls (D-027): rugs lie flat; pictures, a clock and curtains hang on the wall facing the room. The view adds _s or _w
            // to the sprite: the south face of a wall north of the room, or the west face of a wall east of it.
            { "rug", new Info("decal_rug_0", 3, 1, null, flat: true) },
            { "rug_small", new Info("decal_rug_1", 3, 1, null, flat: true) },
            { "picture", new Info("iso_deco_picture", 1, 1, null, onWall: true) },
            { "picture_tall", new Info("iso_deco_picture2", 1, 1, null, onWall: true) },
            { "clock", new Info("iso_deco_clock", 1, 1, null, onWall: true) },
            { "wall_shelf", new Info("iso_deco_shelf", 1, 1, null, onWall: true) },
            { "curtain", new Info("iso_deco_curtain", 1, 1, null, onWall: true) },
            // Little things that make a home (D-027): no loot, they are there to look lived in.
            { "sink", new Info("decor_counter_1", 1, 1, "kitchen") },
            { "washer", new Info("decor_counter_3", 1, 1, null) },
            { "tv", new Info("decor_shelf_1", 1, 1, null) },
            { "plant", new Info("decor_plant_0", 1, 1, null) },
            { "lamp", new Info("decor_lamp", 1, 1, null) },
            // Storage.
            { "boxes", new Info("decor_boxes_0", 1, 1, "storage") },
            { "crate", new Info("decor_boxes_1", 1, 1, "storage") },
        };

        private static readonly Info Unknown = new Info("decor_boxes_0", 1, 1, null);

        public static Info Of(string kind) => kind != null && Table.TryGetValue(kind, out Info i) ? i : Unknown;

        public static bool Exists(string kind) => kind != null && Table.ContainsKey(kind);
    }
}
