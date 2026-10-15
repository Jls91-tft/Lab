using System.Collections.Generic;
using CepaCero.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// The map as two Tilemaps (ground below, blocks above) with the generated
    /// 16 px art (D-018). One world unit = one tile and tile (0,0) sits at the
    /// origin. Only changed cells are repainted.
    /// </summary>
    public sealed class PrototypeWorldView
    {
        private readonly BlockWorld _blocks;
        private readonly PrototypeArt _art;
        private readonly Tilemap _groundMap;
        private readonly Tilemap _blockMap;
        private readonly Dictionary<Sprite, Tile> _tiles = new Dictionary<Sprite, Tile>();
        private readonly List<BlockChange> _changes = new List<BlockChange>();

        public PrototypeWorldView(BlockWorld blocks, GroundLayer ground, PrototypeArt art, Transform parent)
        {
            _blocks = blocks;
            _art = art;

            var grid = new GameObject("World (prototype)");
            grid.transform.SetParent(parent, false);
            grid.AddComponent<Grid>();
            _groundMap = CreateLayer(grid.transform, "Ground", 0);
            _blockMap = CreateLayer(grid.transform, "Blocks", PrototypeDecorView.FlatOrder + 1); // flat decals go in between

            int w = blocks.Width;
            int h = blocks.Height;
            var groundTiles = new TileBase[w * h];
            var blockTiles = new TileBase[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    groundTiles[y * w + x] = TileFor(art.Ground(ground.Get(x, y), x, y));
                    blockTiles[y * w + x] = BlockTile(x, y);
                }
            }
            var area = new BoundsInt(0, 0, 0, w, h, 1);
            _groundMap.SetTilesBlock(area, groundTiles);
            _blockMap.SetTilesBlock(area, blockTiles);
        }

        /// <summary>Repaints the blocks that changed since the last call (drains the world's change queue).</summary>
        public void Refresh()
        {
            _changes.Clear();
            _blocks.DrainChanges(_changes);
            foreach (BlockChange c in _changes)
            {
                _blockMap.SetTile(new Vector3Int(c.X, c.Y, 0), BlockTile(c.X, c.Y));
                // The wall above may switch between its top and its brick front.
                if (c.Y + 1 < _blocks.Height)
                {
                    _blockMap.SetTile(new Vector3Int(c.X, c.Y + 1, 0), BlockTile(c.X, c.Y + 1));
                }
            }
        }

        /// <summary>Day/night: darkens everything with the ambient light (kept visible for debugging).</summary>
        public void SetAmbient(float light)
        {
            float v = Mathf.Lerp(0.3f, 1f, light);
            var tint = new Color(v, v, Mathf.Min(1f, v * 1.1f), 1f);
            _groundMap.color = tint;
            _blockMap.color = tint;
        }

        /// <summary>
        /// Seen from above at an angle (D-019): a wall shows its brick front when nothing
        /// wall-like stands just south of it, and its top when it continues southwards.
        /// </summary>
        private TileBase BlockTile(int x, int y)
        {
            string key = _blocks.GetBlock(x, y).Key;
            if (key == "building_wall" && !IsWallLike(x, y - 1))
            {
                return TileFor(_art.Get("block_building_wall_face"));
            }
            return TileFor(_art.Block(key));
        }

        private bool IsWallLike(int x, int y)
        {
            if (!_blocks.InBounds(x, y))
            {
                return false;
            }
            string key = _blocks.GetBlock(x, y).Key;
            return key == "building_wall" || key == "door" || key == "door_open";
        }

        private Tile TileFor(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }
            if (!_tiles.TryGetValue(sprite, out Tile tile))
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                _tiles.Add(sprite, tile);
            }
            return tile;
        }

        private static Tilemap CreateLayer(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Tilemap map = go.AddComponent<Tilemap>();
            TilemapRenderer renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            Material unlit = PrototypeMaterials.Unlit;
            if (unlit != null)
            {
                renderer.sharedMaterial = unlit;
            }
            return map;
        }
    }
}
