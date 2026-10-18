using System;
using System.Collections.Generic;
using CepaCero.World;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// The game's art (D-024): the world at 32 px from tools/spritegen (Resources/Cepa/Art32) and the
    /// icons, interface and cursor at 16 px from tools/artgen (Resources/Cepa/Art). Each is a texture
    /// plus a JSON with every sprite's rectangle. Names are looked up in the world atlas first.
    /// </summary>
    public sealed class PrototypeArt
    {
        public const string WorldResource = "Cepa/Art32/atlas";
        public const string InterfaceResource = "Cepa/Art/atlas";
        public const int GroundVariants = 3;

        /// <summary>Base resolution height of the world (D-021): it is scaled by whole numbers from 960×540.</summary>
        public const int BaseHeight = 540;

        /// <summary>Base height of the interface: it keeps its 16 px pixel grid and its own scale (D-018).</summary>
        public const int UiBaseHeight = 360;

        /// <summary>
        /// Screen pixels per art pixel for a screen height: a whole number, rounded up unless the screen is only a little
        /// taller than a multiple of the base (so a small editor window still gets ×1 or ×2 instead of ×1 for everything).
        /// </summary>
        public static int ZoomFor(int screenHeight, int baseHeight)
        {
            if (screenHeight <= 0)
            {
                return 1; // batch mode
            }
            return Math.Max(1, (int)Math.Ceiling(screenHeight / (double)baseHeight - 0.25));
        }

        /// <summary>
        /// World zoom (base 960×540): the whole number closest to the screen height over 540, never below 1
        /// (500 → 1, 720 → 1, 900 → 2, 1080 → 2, 1440 → 3, 2160 → 4). With 32 px tiles that is about 17 tiles of height at 1080 p.
        /// </summary>
        public static int Zoom(int screenHeight) =>
            screenHeight <= 0 ? 1 : Math.Max(1, (int)Math.Round(screenHeight / (double)BaseHeight, MidpointRounding.AwayFromZero));

        /// <summary>Interface zoom (base 640×360).</summary>
        public static int UiZoom(int screenHeight) => ZoomFor(screenHeight, UiBaseHeight);

        private sealed class Atlas
        {
            public Texture2D Texture;
            public int PixelsPerUnit;
            public readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            public readonly Dictionary<string, Rect> Uv = new Dictionary<string, Rect>(StringComparer.Ordinal);
            public readonly Dictionary<string, Vector2> Size = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        }

        private readonly Atlas _world;
        private readonly Atlas _interface;
        private readonly Sprite[][] _ground;

        /// <summary>The interface atlas (16 px): what the HUD draws with OnGUI.</summary>
        public Texture2D Texture => _interface.Texture;

        /// <summary>Pixels per unit of the world: one tile is one unit, so this is the pixels of a tile (32).</summary>
        public int PixelsPerUnit => _world.PixelsPerUnit;

        private PrototypeArt(Atlas world, Atlas ui)
        {
            _world = world;
            _interface = ui;
            var types = (GroundType[])Enum.GetValues(typeof(GroundType));
            _ground = new Sprite[types.Length][];
            foreach (GroundType t in types)
            {
                _ground[(int)t] = new Sprite[GroundVariants];
                for (int v = 0; v < GroundVariants; v++)
                {
                    _ground[(int)t][v] = Get($"ground_{t.ToString().ToLowerInvariant()}_{v}");
                }
            }
        }

        /// <summary>Loads both atlases from Resources; throws with a hint when one is missing.</summary>
        public static PrototypeArt Load()
        {
            Atlas world = Read(WorldResource, "cd tools/spritegen && python -m spritegen");
            Atlas ui = Read(InterfaceResource, "cd tools/artgen && python -m artgen");
            return new PrototypeArt(world, ui);
        }

        private static Atlas Read(string resource, string howToMake)
        {
            var texture = Resources.Load<Texture2D>(resource);
            var json = Resources.Load<TextAsset>(resource);
            if (texture == null || json == null)
            {
                throw new InvalidOperationException($"Cepa Cero: no encuentro el atlas Resources/{resource}. Genéralo con: {howToMake}");
            }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            AtlasData data = JsonUtility.FromJson<AtlasData>(json.text);
            var atlas = new Atlas { Texture = texture, PixelsPerUnit = data.ppu };
            foreach (AtlasSprite s in data.sprites)
            {
                var rect = new Rect(s.x, s.y, s.w, s.h);
                // Characters carry a pivot (fractions of the sprite) so their feet sit on the entity's position.
                var pivot = s.pivot_x > 0f ? new Vector2(s.pivot_x, s.pivot_y) : new Vector2(0.5f, 0.5f);
                Sprite created = Sprite.Create(texture, rect, pivot, data.ppu, 0, SpriteMeshType.FullRect);
                created.name = s.name;
                atlas.Sprites[s.name] = created;
                atlas.Uv[s.name] = new Rect((float)s.x / data.width, (float)s.y / data.height, (float)s.w / data.width, (float)s.h / data.height);
                atlas.Size[s.name] = new Vector2(s.w, s.h);
            }
            return atlas;
        }

        public Sprite Get(string name)
        {
            if (!TryGet(name, out Sprite sprite))
            {
                throw new KeyNotFoundException($"Cepa Cero: ningún atlas tiene el sprite '{name}' (¿falta regenerar con spritegen o artgen?)");
            }
            return sprite;
        }

        public bool TryGet(string name, out Sprite sprite) =>
            _world.Sprites.TryGetValue(name, out sprite) || _interface.Sprites.TryGetValue(name, out sprite);

        /// <summary>True when the world atlas (not just the interface) has this sprite.</summary>
        public bool HasWorld(string name) => _world.Sprites.ContainsKey(name);

        /// <summary>Normalised texture coordinates in the interface atlas, for drawing straight from it in OnGUI.</summary>
        public Rect Uv(string name) => _interface.Uv[name];

        /// <summary>Size in atlas pixels (interface atlas).</summary>
        public Vector2 Size(string name) => _interface.Size[name];

        /// <summary>Size in world units of a sprite of the world atlas (pixels / pixels per unit).</summary>
        public Vector2 WorldSize(string name) => _world.Size[name] / _world.PixelsPerUnit;

        /// <summary>Size in world units of any sprite, from whichever atlas has it.</summary>
        public Vector2 UnitSize(string name) =>
            _world.Size.TryGetValue(name, out Vector2 w) ? w / _world.PixelsPerUnit : _interface.Size[name] / _interface.PixelsPerUnit;

        /// <summary>Ground tile; the variant changes per cell so large areas do not look tiled.</summary>
        public Sprite Ground(GroundType type, int x, int y)
        {
            uint h = unchecked((uint)(x * 73856093) ^ (uint)(y * 19349663));
            return _ground[(int)type][h % GroundVariants];
        }

        private readonly Dictionary<(Sprite, int), Sprite> _lifted = new Dictionary<(Sprite, int), Sprite>();

        /// <summary>The same sprite drawn <paramref name="units"/> higher on screen (its pivot moved down): roofs of taller buildings (D-026).</summary>
        public Sprite Lifted(Sprite sprite, float units)
        {
            if (sprite == null || units <= 0f)
            {
                return sprite;
            }
            int key = (int)(units * 100f);
            if (!_lifted.TryGetValue((sprite, key), out Sprite lifted))
            {
                Rect rect = sprite.rect;
                Vector2 pivot = sprite.pivot;
                var normalised = new Vector2(pivot.x / rect.width, (pivot.y - units * sprite.pixelsPerUnit) / rect.height);
                lifted = Sprite.Create(sprite.texture, rect, normalised, sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                lifted.name = sprite.name + "+" + key;
                _lifted[(sprite, key)] = lifted;
            }
            return lifted;
        }

        /// <summary>The isometric diamond of a flat tile (D-025: ground and floors drawn as 64x32 diamonds), or the sprite itself when the atlas has none.</summary>
        public Sprite IsoFlat(Sprite flat)
        {
            return flat != null && TryGet("iso_" + flat.name, out Sprite iso) ? iso : flat;
        }

        /// <summary>Legacy block sprite (the map's own blocks: building_wall, tree, door…), or null for air.</summary>
        public Sprite Block(string key) => key == "air" ? null : Get("block_" + key);

        [Serializable]
        public sealed class AtlasData
        {
            public int ppu;
            public int width;
            public int height;
            public AtlasSprite[] sprites;
        }

        [Serializable]
        public sealed class AtlasSprite
        {
            public string name;
            public int x;
            public int y;
            public int w;
            public int h;
            public float pivot_x;
            public float pivot_y;
        }
    }
}
