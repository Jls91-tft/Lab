using System.Collections.Generic;
using CepaCero.Building;
using CepaCero.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// The map as two Tilemaps (ground below, blocks above) with the generated
    /// 16 px art (D-018). One world unit = one tile and tile (0,0) sits at the
    /// origin. Only changed cells are repainted; changing level repaints it all.
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

            _ground = ground;
            RepaintAll();
        }

        private readonly GroundLayer _ground;
        private int _level;
        private float _ambient = 1f;

        /// <summary>Shows another level (D-022): the street, a floor of a building (the street dimmed below it) or the ground underneath.</summary>
        public void SetLevel(int level)
        {
            if (level == _level)
            {
                return;
            }
            _level = level;
            _groundMap.gameObject.SetActive(level >= 0);
            RepaintAll();
            SetAmbient(_ambient);
        }

        private void RepaintAll()
        {
            int w = _blocks.Width;
            int h = _blocks.Height;
            var groundTiles = new TileBase[w * h];
            var blockTiles = new TileBase[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    groundTiles[y * w + x] = TileFor(_art.Ground(_ground.Get(x, y), x, y));
                    blockTiles[y * w + x] = BlockTile(x, y);
                }
            }
            var area = new BoundsInt(0, 0, 0, w, h, 1);
            _groundMap.SetTilesBlock(area, groundTiles);
            _blockMap.SetTilesBlock(area, blockTiles);
            _floorMap.ClearAllTiles();
            _objectMap.ClearAllTiles();
            _roofMap.ClearAllTiles();
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
                if (c.Z != _level)
                {
                    continue;                                   // only the level being shown is drawn
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
                else if (c.Layer == BuildLayer.Roof)
                {
                    for (int dy = -3; dy <= 3; dy++)                  // a roof's shape depends on the roof around it
                    {
                        for (int dx = -3; dx <= 3; dx++)
                        {
                            if (_blocks.InBounds(c.X + dx, c.Y + dy))
                            {
                                Paint(_roofMap, c.X + dx, c.Y + dy, BuildLayer.Roof);
                            }
                        }
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
            _ambient = light;
            float v = Mathf.Lerp(0.3f, 1f, light) * (_level != 0 ? 0.75f : 1f);          // other levels read a little darker
            var tint = new Color(v, v, Mathf.Min(1f, v * 1.1f), 1f);
            _groundMap.color = _level > 0 ? new Color(tint.r * 0.6f, tint.g * 0.6f, tint.b * 0.6f, 1f) : tint;
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
            if (_level != 0)
            {
                BlockType here = _blocks.GetBlock(x, y, _level, BuildLayer.Structure);
                if (!here.IsAir)
                {
                    return TileFor(SpriteOf(here), _blocks.GetVariant(x, y, _level, BuildLayer.Structure));
                }
                if (_blocks.IsUndug(x, y, _level) && _blocks.Registry.TryGet(_level <= -3 ? "rock" : "earth", out BlockType soil))
                {
                    return TileFor(SpriteOf(soil));                 // untouched ground, solid until dug
                }
                return null;
            }
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
            BlockType type = _blocks.GetBlock(x, y, _level, layer);
            Sprite sprite = type.IsAir ? null : layer == BuildLayer.Roof ? RoofSprite(type, x, y) : SpriteOf(type);
            map.SetTile(new Vector3Int(x, y, 0), type.IsAir ? null : TileFor(sprite, _blocks.GetVariant(x, y, _level, layer)));
        }

        /// <summary>The roof sprite for its place in the roof (slope, corner, ridge, flat) when the atlas has it, else the flat stand-in.</summary>
        private Sprite RoofSprite(BlockType type, int x, int y)
        {
            RoofShape shape = RoofShapes.At((px, py) => _blocks.InBounds(px, py) && !_blocks.GetBlock(px, py, _level, BuildLayer.Roof).IsAir, x, y);
            string name = $"roof_{type.Build.MaterialId}_{RoofShapes.SpriteSuffix(shape)}";
            return _art.TryGet(name, out Sprite sprite) ? sprite : SpriteOf(type);
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
