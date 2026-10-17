using System.Collections.Generic;
using CepaCero.Building;
using CepaCero.Util;
using CepaCero.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// The map as Tilemaps (ground, floors, blocks, objects, roofs) with the 32 px art of tools/spritegen (D-021).
    /// One world unit = one tile and tile (0,0) sits at the origin. Only changed cells are repainted; changing
    /// level repaints it all. Walls join their neighbours (<see cref="PrototypeBlockSprites"/>).
    ///
    /// Roofs (D-024): every building has one, opaque from outside, so no interior is seen from the street. When
    /// you are under a roof, only that house's roof fades (its flood-filled interior and its outline), not the
    /// neighbours' (Project Zomboid). Things standing under a roof that is not fading are hidden too
    /// (<see cref="IsHiddenByRoof"/>), so furniture and zombies do not show through.
    /// </summary>
    public sealed class PrototypeWorldView
    {
        private const float FadeSpeed = 3.5f;
        private const int MaxHouseCells = 6000;
        private static readonly Int2[] Neighbours = { new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1) };

        private readonly BlockWorld _blocks;
        private readonly GroundLayer _ground;
        private readonly PrototypeArt _art;
        private readonly PrototypeBlockSprites _sprites;
        private readonly Tilemap _groundMap;
        private readonly Tilemap _blockMap;
        private readonly Tilemap _floorMap;
        private readonly Tilemap _objectMap;
        private readonly Tilemap _roofMap;
        private readonly Tilemap _roofFadeMap;
        private readonly Tilemap _belowFloorMap;
        private readonly Tilemap _belowBlockMap;
        private readonly Dictionary<(Sprite, int), Tile> _tiles = new Dictionary<(Sprite, int), Tile>();
        private readonly List<BlockChange> _changes = new List<BlockChange>();

        private int _level;
        private Color _tint = Color.white;
        private float _ambient = 1f;

        // The roof of the house you are in: its cells live in _roofFadeMap while it fades.
        private readonly HashSet<int> _house = new HashSet<int>();
        private readonly HashSet<int> _scratch = new HashSet<int>();
        private readonly Queue<Int2> _queue = new Queue<Int2>();
        private float _fade = 1f;
        private float _fadeTarget = 1f;
        private Int2 _lastPlayer = new Int2(-1, -1);
        private bool _roofDirty = true;
        private bool _houseOpen;

        /// <summary>Changes whenever something under a roof may have become visible or hidden (decoration and entities re-check).</summary>
        public int RoofVersion { get; private set; }

        public PrototypeWorldView(BlockWorld blocks, GroundLayer ground, PrototypeArt art, Transform parent)
        {
            _blocks = blocks;
            _ground = ground;
            _art = art;
            _sprites = new PrototypeBlockSprites(blocks, art);

            var grid = new GameObject("World (prototype)");
            grid.transform.SetParent(parent, false);
            grid.AddComponent<Grid>();
            _groundMap = CreateLayer(grid.transform, "Ground", -2);
            _belowFloorMap = CreateLayer(grid.transform, "Level below (floors)", -1);       // seen through holes in the floor (level cut)
            _belowBlockMap = CreateLayer(grid.transform, "Level below (blocks)", 0);
            _floorMap = CreateLayer(grid.transform, "Floors", PrototypeDecorView.FlatOrder);        // under the flat decals' neighbours: you walk on it
            _blockMap = CreateLayer(grid.transform, "Blocks", PrototypeDecorView.FlatOrder + 1); // flat decals go in between
            _objectMap = CreateLayer(grid.transform, "Objects", PrototypeDecorView.FlatOrder + 1);
            _roofMap = CreateLayer(grid.transform, "Roofs", PrototypeDecorView.FlatOrder + 2);
            _roofFadeMap = CreateLayer(grid.transform, "Roof of your house (fading)", PrototypeDecorView.FlatOrder + 2);

            RepaintAll();
        }

        /// <summary>Shows another level (D-022): the street, a floor of a building (the street dimmed below it) or the ground underneath.</summary>
        public void SetLevel(int level)
        {
            if (level == _level)
            {
                return;
            }
            _level = level;
            _groundMap.gameObject.SetActive(level >= 0);
            _roofMap.gameObject.SetActive(level >= 0);                  // underground you do not see the roofs above
            _roofFadeMap.gameObject.SetActive(level >= 0);
            RepaintAll();
            SetAmbient(_ambient);
        }

        /// <summary>The sprite a block would have on its own (ghosts, the held piece).</summary>
        public Sprite SpriteOf(BlockType type) => _sprites.Representative(type);

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
            _roofFadeMap.ClearAllTiles();
            _belowFloorMap.ClearAllTiles();
            _belowBlockMap.ClearAllTiles();
            _house.Clear();
            _fade = _fadeTarget = 1f;
            _lastPlayer = new Int2(-1, -1);
            _roofDirty = true;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    PaintExtraLayers(x, y);
                    PaintBelow(x, y);
                }
            }
            RoofVersion++;
        }

        /// <summary>
        /// The level cut: from an upper floor you see the level below, darker, wherever your own level has no floor (a stairwell, a hole, the
        /// edge of the house), so the street is still there under a balcony and you can see who is down the stairs.
        /// </summary>
        private void PaintBelow(int x, int y)
        {
            var cell = new Vector3Int(x, y, 0);
            int below = _level - 1;
            bool open = _level > 0 && _blocks.InLevels(below) && _blocks.GetTypeId(x, y, _level, BuildLayer.Floor) == BlockRegistry.AirId
                && _blocks.GetTypeId(x, y, _level, BuildLayer.Structure) == BlockRegistry.AirId;
            if (!open)
            {
                _belowFloorMap.SetTile(cell, null);
                _belowBlockMap.SetTile(cell, null);
                return;
            }
            Sprite floor = _sprites.Floor(x, y, below);
            Sprite wall = _sprites.Structure(x, y, below);
            _belowFloorMap.SetTile(cell, TileFor(floor, _blocks.GetVariant(x, y, below, BuildLayer.Floor)));
            _belowBlockMap.SetTile(cell, TileFor(wall, _blocks.GetVariant(x, y, below, BuildLayer.Structure)));
        }

        // ------------------------------------------------------------------ roofs

        /// <summary>The topmost roof over a cell at or above the level being shown, or air.</summary>
        private BlockType TopRoof(int x, int y, out int level)
        {
            for (int z = _blocks.MaxLevel; z >= _level; z--)
            {
                BlockType roof = _blocks.GetBlock(x, y, z, BuildLayer.Roof);
                if (!roof.IsAir)
                {
                    level = z;
                    return roof;
                }
            }
            level = _level;
            return _blocks.GetBlock(x, y, _level, BuildLayer.Roof);
        }

        public bool HasRoofOver(int x, int y) => _level >= 0 && _blocks.InBounds(x, y) && !TopRoof(x, y, out _).IsAir;

        /// <summary>True when something at this position is under a roof that is not open: you must not see it from outside.</summary>
        public bool IsHiddenByRoof(Float2 position)
        {
            Int2 t = position.Tile;
            if (!HasRoofOver(t.X, t.Y))
            {
                return false;
            }
            return !(_houseOpen && _house.Contains(Key(t.X, t.Y)));
        }

        /// <summary>
        /// Call every frame with the player's tile: finds the house you are in (the roof cells connected over the interior, bounded
        /// by walls) and fades only that roof. Leaving it fades the roof back in.
        /// </summary>
        public void UpdateRoofs(Int2 player, float deltaTime)
        {
            if (_level < 0)
            {
                if (_house.Count > 0)
                {
                    ReleaseHouse();
                }
                return;
            }
            if (player != _lastPlayer || _roofDirty)
            {
                _lastPlayer = player;
                _roofDirty = false;
                if (FindHouse(player, _scratch))
                {
                    if (!_house.SetEquals(_scratch))
                    {
                        ReleaseHouse();
                        foreach (int key in _scratch)
                        {
                            _house.Add(key);
                            MoveRoofCell(key % _blocks.Width, key / _blocks.Width, toFade: true);
                        }
                        _fade = 1f;
                    }
                    _fadeTarget = 0f;
                }
                else if (_house.Count > 0)
                {
                    _fadeTarget = 1f;
                }
            }
            if (_house.Count == 0)
            {
                return;
            }
            _fade = Mathf.MoveTowards(_fade, _fadeTarget, deltaTime * FadeSpeed);
            _roofFadeMap.color = new Color(_tint.r, _tint.g, _tint.b, _fade);
            bool open = _fade < 0.5f;
            if (open != _houseOpen)
            {
                _houseOpen = open;
                RoofVersion++;
            }
            if (_fadeTarget >= 1f && _fade >= 0.999f)
            {
                ReleaseHouse();
            }
        }

        /// <summary>The roof cells of the house over this cell: flood fill through roofed cells, stopping at walls (which fade with it).</summary>
        private bool FindHouse(Int2 start, HashSet<int> into)
        {
            into.Clear();
            if (!HasRoofOver(start.X, start.Y))
            {
                return false;
            }
            _queue.Clear();
            _queue.Enqueue(start);
            into.Add(Key(start.X, start.Y));
            while (_queue.Count > 0 && into.Count < MaxHouseCells)
            {
                Int2 c = _queue.Dequeue();
                foreach (Int2 d in Neighbours)
                {
                    var n = new Int2(c.X + d.X, c.Y + d.Y);
                    if (!_blocks.InBounds(n.X, n.Y) || !HasRoofOver(n.X, n.Y) || !into.Add(Key(n.X, n.Y)))
                    {
                        continue;
                    }
                    if (!_sprites.IsWallLike(n.X, n.Y, _level))
                    {
                        _queue.Enqueue(n);                       // the outline is part of the house but does not lead into the next one
                    }
                }
            }
            return true;
        }

        private void ReleaseHouse()
        {
            foreach (int key in _house)
            {
                MoveRoofCell(key % _blocks.Width, key / _blocks.Width, toFade: false);
            }
            _house.Clear();
            _fade = _fadeTarget = 1f;
            _roofFadeMap.ClearAllTiles();
            if (_houseOpen)
            {
                _houseOpen = false;
            }
            RoofVersion++;
        }

        private void MoveRoofCell(int x, int y, bool toFade)
        {
            var cell = new Vector3Int(x, y, 0);
            if (toFade)
            {
                _roofFadeMap.SetTile(cell, _roofMap.GetTile<TileBase>(cell));
                _roofMap.SetTile(cell, null);
            }
            else
            {
                _roofMap.SetTile(cell, _roofFadeMap.GetTile<TileBase>(cell));
                _roofFadeMap.SetTile(cell, null);
            }
        }

        private int Key(int x, int y) => y * _blocks.Width + x;

        // ------------------------------------------------------------------ refresh

        /// <summary>Repaints the blocks that changed since the last call (drains the world's change queue).</summary>
        public void Refresh()
        {
            _changes.Clear();
            _blocks.DrainChanges(_changes);
            foreach (BlockChange c in _changes)
            {
                if (c.Layer == BuildLayer.Roof && c.Z >= _level)
                {
                    for (int dy = -3; dy <= 3; dy++)                  // a roof's shape depends on the roof around it
                    {
                        for (int dx = -3; dx <= 3; dx++)
                        {
                            if (_blocks.InBounds(c.X + dx, c.Y + dy))
                            {
                                PaintRoofCell(c.X + dx, c.Y + dy);
                            }
                        }
                    }
                    _roofDirty = true;
                    continue;
                }
                if (c.Z == _level - 1 || c.Z == _level)
                {
                    PaintBelow(c.X, c.Y);
                }
                if (c.Z != _level)
                {
                    continue;                                   // only the level being shown is drawn
                }
                if (c.Layer == BuildLayer.Structure || c.Layer == BuildLayer.Floor)
                {
                    // Walls join their neighbours, and the cut face of dug-out ground depends on what is dug around it.
                    foreach (Int2 d in new[] { new Int2(0, 0), new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1) })
                    {
                        int x = c.X + d.X;
                        int y = c.Y + d.Y;
                        if (_blocks.InBounds(x, y))
                        {
                            _blockMap.SetTile(new Vector3Int(x, y, 0), BlockTile(x, y));
                        }
                    }
                }
                if (c.Layer != BuildLayer.Structure)
                {
                    PaintExtraLayers(c.X, c.Y);
                }
            }
        }

        /// <summary>Day/night: darkens everything with the ambient light (kept visible for debugging).</summary>
        public void SetAmbient(float light)
        {
            _ambient = light;
            // The night is done by the light overlay (PrototypeLighting); here other levels only read a little darker.
            float v = _level != 0 ? 0.85f : 1f;
            _tint = new Color(v, v, v, 1f);
            _groundMap.color = _level > 0 ? new Color(_tint.r * 0.6f, _tint.g * 0.6f, _tint.b * 0.6f, 1f) : _tint;
            _blockMap.color = _tint;
            _floorMap.color = _tint;
            _objectMap.color = _tint;
            _roofMap.color = _tint;
            _roofFadeMap.color = new Color(_tint.r, _tint.g, _tint.b, _fade);
            var dim = new Color(_tint.r * 0.45f, _tint.g * 0.45f, _tint.b * 0.45f, 1f);
            _belowFloorMap.color = dim;
            _belowBlockMap.color = dim;
        }

        // ------------------------------------------------------------------ painting

        private TileBase BlockTile(int x, int y) =>
            TileFor(_sprites.Structure(x, y, _level), _blocks.GetVariant(x, y, _level, BuildLayer.Structure));

        private void PaintExtraLayers(int x, int y)
        {
            var cell = new Vector3Int(x, y, 0);
            _floorMap.SetTile(cell, TileFor(_sprites.Floor(x, y, _level), _blocks.GetVariant(x, y, _level, BuildLayer.Floor)));
            _objectMap.SetTile(cell, TileFor(_sprites.Object(x, y, _level), _blocks.GetVariant(x, y, _level, BuildLayer.Object)));
            PaintRoofCell(x, y);
        }

        /// <summary>The roof of a cell goes to the fading map when the cell belongs to the house you are in.</summary>
        private void PaintRoofCell(int x, int y)
        {
            var cell = new Vector3Int(x, y, 0);
            BlockType type = TopRoof(x, y, out int level);
            TileBase tile = null;
            if (!type.IsAir && _level >= 0)
            {
                RoofShape shape = RoofShapes.At((px, py) => HasRoofOver(px, py), x, y);
                tile = TileFor(_sprites.Roof(type, shape), _blocks.GetVariant(x, y, level, BuildLayer.Roof));
            }
            bool fading = _house.Contains(Key(x, y));
            (fading ? _roofFadeMap : _roofMap).SetTile(cell, tile);
            (fading ? _roofMap : _roofFadeMap).SetTile(cell, null);
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
