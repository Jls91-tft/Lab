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
        private readonly Tilemap _floorMap;
        private readonly Tilemap _objectMap;
        private readonly Tilemap _roofMap;
        private readonly PrototypePlaceholders _placeholders;
        private readonly Dictionary<(Sprite, int), Tile> _tiles = new Dictionary<(Sprite, int), Tile>();
        private readonly List<BlockChange> _changes = new List<BlockChange>();

        public PrototypeWorldView(BlockWorld blocks, GroundLayer ground, PrototypeArt art, Transform parent)
        {
            _blocks = blocks;
            _art = art;
            _placeholders = new PrototypePlaceholders(art.PixelsPerUnit);

            var grid = new GameObject("World (prototype)");
            grid.transform.SetParent(parent, false);
            grid.AddComponent<Grid>();
            _groundMap = CreateLayer(grid.transform, "Ground", 0);
            _floorMap = CreateLayer(grid.transform, "Floors", PrototypeDecorView.FlatOrder);        // under the flat decals' neighbours: you walk on it
            _blockMap = CreateLayer(grid.transform, "Blocks", PrototypeDecorView.FlatOrder + 1); // flat decals go in between
            _objectMap = CreateLayer(grid.transform, "Objects", PrototypeDecorView.FlatOrder + 1);
            _roofMap = CreateLayer(grid.transform, "Roofs", PrototypeDecorView.FlatOrder + 2);   // translucent: see the house through it

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
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    PaintExtraLayers(x, y);
                }
            }
        }

        /// <summary>Repaints the blocks that changed since the last call (drains the world's change queue).</summary>
        public void Refresh()
        {
            _changes.Clear();
            _blocks.DrainChanges(_changes);
            foreach (BlockChange c in _changes)
            {
                if (c.Z != 0)
                {
                    continue;                                   // only the ground level is drawn until the levels view exists (phase F)
                }
                if (c.Layer == BuildLayer.Structure)
                {
                    _blockMap.SetTile(new Vector3Int(c.X, c.Y, 0), BlockTile(c.X, c.Y));
                    // The wall above may switch between its top and its brick front.
                    if (c.Y + 1 < _blocks.Height)
                    {
                        _blockMap.SetTile(new Vector3Int(c.X, c.Y + 1, 0), BlockTile(c.X, c.Y + 1));
                    }
                }
                else
                {
                    PaintExtraLayers(c.X, c.Y);
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
            _floorMap.color = tint;
            _objectMap.color = tint;
            _roofMap.color = new Color(tint.r, tint.g, tint.b, 0.6f);
        }

        /// <summary>
        /// Seen from above at an angle (D-019): a wall shows its brick front when nothing
        /// wall-like stands just south of it, and its top when it continues southwards.
        /// </summary>
        private TileBase BlockTile(int x, int y)
        {
            BlockType type = _blocks.GetBlock(x, y);
            if (type.Key == "building_wall" && !IsWallLike(x, y - 1))
            {
                return TileFor(_art.Get("block_building_wall_face"));
            }
            return TileFor(SpriteOf(type), _blocks.GetVariant(x, y, 0, BuildLayer.Structure));
        }

        /// <summary>The atlas sprite of a block or, for pieces the atlas does not have yet, a flat stand-in.</summary>
        public Sprite SpriteOf(BlockType type)
        {
            if (type.IsAir)
            {
                return null;
            }
            return _art.TryGet("block_" + type.Key, out Sprite sprite) ? sprite : _placeholders.Get(type);
        }

        private void PaintExtraLayers(int x, int y)
        {
            Paint(_floorMap, x, y, BuildLayer.Floor);
            Paint(_objectMap, x, y, BuildLayer.Object);
            Paint(_roofMap, x, y, BuildLayer.Roof);
        }

        private void Paint(Tilemap map, int x, int y, BuildLayer layer)
        {
            BlockType type = _blocks.GetBlock(x, y, 0, layer);
            map.SetTile(new Vector3Int(x, y, 0), type.IsAir ? null : TileFor(SpriteOf(type), _blocks.GetVariant(x, y, 0, layer)));
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

        /// <summary>One tile per sprite and paint colour (the colour of <see cref="BlockVariant"/> tints the tile).</summary>
        private Tile TileFor(Sprite sprite, byte variant = 0)
        {
            if (sprite == null)
            {
                return null;
            }
            int paint = BlockVariant.Color(variant);
            if (!_tiles.TryGetValue((sprite, paint), out Tile tile))
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = paint == 0 ? Color.white : (Color)PrototypePlaceholders.Paints[Mathf.Clamp(paint, 0, PrototypePlaceholders.Paints.Length - 1)];
                _tiles.Add((sprite, paint), tile);
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
