using System.Collections.Generic;
using CepaCero.World;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// The whole map as one texture, one pixel per tile, on a sprite where one
    /// world unit = one tile and tile (0,0) sits at the origin. Only changed
    /// cells are repainted. Debug view: the real game will use Tilemaps.
    /// </summary>
    public sealed class PrototypeWorldView
    {
        private readonly BlockWorld _blocks;
        private readonly GroundLayer _ground;
        private readonly Texture2D _texture;
        private readonly SpriteRenderer _renderer;
        private readonly List<BlockChange> _changes = new List<BlockChange>();
        private bool _dirty;

        public PrototypeWorldView(BlockWorld blocks, GroundLayer ground, Transform parent)
        {
            _blocks = blocks;
            _ground = ground;
            _texture = new Texture2D(blocks.Width, blocks.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PrototypeWorld",
            };
            var pixels = new Color32[blocks.Width * blocks.Height];
            for (int y = 0; y < blocks.Height; y++)
            {
                for (int x = 0; x < blocks.Width; x++)
                {
                    pixels[y * blocks.Width + x] = ColorAt(x, y);
                }
            }
            _texture.SetPixels32(pixels);
            _texture.Apply(false);

            var go = new GameObject("World (prototype)");
            go.transform.SetParent(parent, false);
            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sprite = Sprite.Create(_texture, new Rect(0, 0, blocks.Width, blocks.Height), Vector2.zero, 1f);
            PrototypeMaterials.Apply(_renderer);
            _renderer.sortingOrder = 0;
        }

        /// <summary>Repaints the blocks that changed since the last call (drains the world's change queue).</summary>
        public void Refresh()
        {
            _changes.Clear();
            _blocks.DrainChanges(_changes);
            foreach (BlockChange c in _changes)
            {
                _texture.SetPixel(c.X, c.Y, ColorAt(c.X, c.Y));
                _dirty = true;
            }
            if (_dirty)
            {
                _texture.Apply(false);
                _dirty = false;
            }
        }

        /// <summary>Day/night: darkens everything with the ambient light (kept visible for debugging).</summary>
        public void SetAmbient(float light)
        {
            float v = Mathf.Lerp(0.3f, 1f, light);
            _renderer.color = new Color(v, v, v * 1.05f, 1f);
        }

        private Color32 ColorAt(int x, int y)
        {
            Color32 block = PrototypePalette.Block(_blocks.GetBlock(x, y).Key);
            if (block.a > 0)
            {
                return block;
            }
            return PrototypePalette.Ground(_ground.Get(x, y));
        }
    }
}
