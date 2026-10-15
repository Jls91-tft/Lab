using System;
using System.Collections.Generic;
using CepaCero.World;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// The generated atlas (tools/artgen, D-018): one texture plus a JSON with every
    /// sprite's rectangle. Lives in Resources/Cepa/Art, so a build carries it too.
    /// </summary>
    public sealed class PrototypeArt
    {
        public const string ResourcePath = "Cepa/Art/atlas";
        public const int GroundVariants = 3;

        /// <summary>Base resolution height (D-018): the view is scaled by whole numbers from 640×360.</summary>
        public const int BaseHeight = 360;

        /// <summary>
        /// Screen pixels per art pixel for a screen height: a whole number, rounded up
        /// unless the screen is only a little taller than a multiple of 360 (500 px → 2,
        /// 720 → 2, 900 → 3, 1080 → 3, 1440 → 4). Rounding to nearest made small editor
        /// windows show everything at ×1, tiny.
        /// </summary>
        public static int Zoom(int screenHeight)
        {
            if (screenHeight <= 0)
            {
                return 1; // batch mode
            }
            return Math.Max(1, (int)Math.Ceiling(screenHeight / (double)BaseHeight - 0.25));
        }

        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, Rect> _uv = new Dictionary<string, Rect>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> _size = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly Sprite[][] _ground;

        public Texture2D Texture { get; }

        public int PixelsPerUnit { get; }

        private PrototypeArt(Texture2D texture, AtlasData data)
        {
            Texture = texture;
            PixelsPerUnit = data.ppu;
            foreach (AtlasSprite s in data.sprites)
            {
                var rect = new Rect(s.x, s.y, s.w, s.h);
                _sprites[s.name] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), data.ppu, 0, SpriteMeshType.FullRect);
                _uv[s.name] = new Rect((float)s.x / data.width, (float)s.y / data.height, (float)s.w / data.width, (float)s.h / data.height);
                _size[s.name] = new Vector2(s.w, s.h);
            }

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

        /// <summary>Loads the atlas from Resources; throws with a hint when it is missing.</summary>
        public static PrototypeArt Load()
        {
            var texture = Resources.Load<Texture2D>(ResourcePath);
            var json = Resources.Load<TextAsset>(ResourcePath);
            if (texture == null || json == null)
            {
                throw new InvalidOperationException(
                    "Cepa Cero: no encuentro el atlas en Assets/Resources/Cepa/Art. Genéralo con: cd tools/artgen && python -m artgen");
            }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return new PrototypeArt(texture, JsonUtility.FromJson<AtlasData>(json.text));
        }

        public Sprite Get(string name)
        {
            if (!_sprites.TryGetValue(name, out Sprite sprite))
            {
                throw new KeyNotFoundException($"Cepa Cero: el atlas no tiene el sprite '{name}' (¿falta regenerar con artgen?)");
            }
            return sprite;
        }

        public bool TryGet(string name, out Sprite sprite) => _sprites.TryGetValue(name, out sprite);

        /// <summary>Normalised texture coordinates, for drawing straight from the atlas in OnGUI.</summary>
        public Rect Uv(string name) => _uv[name];

        /// <summary>Size in atlas pixels.</summary>
        public Vector2 Size(string name) => _size[name];

        /// <summary>Ground tile; the variant changes per cell so large areas do not look tiled.</summary>
        public Sprite Ground(GroundType type, int x, int y)
        {
            uint h = unchecked((uint)(x * 73856093) ^ (uint)(y * 19349663));
            return _ground[(int)type][h % GroundVariants];
        }

        /// <summary>Block sprite, or null for air.</summary>
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
        }
    }
}
