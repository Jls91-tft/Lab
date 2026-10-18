using System;
using System.Collections.Generic;
using CepaCero.Building;
using CepaCero.Map;
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
        private readonly Tilemap _edgeMap;                         // dark outline and eave of each building, over its roof
        private readonly Tilemap _edgeFadeMap;
        private Tile[] _edgeTiles;

        // D-025: walls stand up as isometric blocks on their own Tilemap (an isometric grid in screen space), sorted with the people.
        private readonly Tilemap _wallMap;
        private readonly HashSet<int> _cut = new HashSet<int>();      // walls drawn low so you can see yourself (in front of you, PZ)
        private readonly HashSet<int> _cutNext = new HashSet<int>();
        private readonly List<int> _cutChanged = new List<int>();
        private readonly HashSet<int> _roofCut = new HashSet<int>();  // roofs in front of you, see-through (PZ)
        private readonly HashSet<int> _roofCutNext = new HashSet<int>();
        private readonly Tilemap _roofCutMap;
        private readonly Tilemap _roofLineMap;                       // the outline of each building's roof
        private Tile[] _lineTiles;
        private int[] _owner;                                       // building index + 1 per cell, 0 = none
        private readonly Tilemap _belowFloorMap;
        private readonly Tilemap _belowBlockMap;
        // Each building's roof gets its own shade, so neighbours read as separate houses instead of one red sea.
        private static readonly Color[] RoofTints =
        {
            Color.white,
            new Color(0.80f, 0.86f, 1.00f),
            new Color(1.00f, 0.90f, 0.70f),
            new Color(0.74f, 0.68f, 0.66f),
            new Color(0.86f, 0.98f, 0.84f),
            new Color(1.00f, 0.76f, 0.68f),
            new Color(0.66f, 0.74f, 0.88f),
        };

        private byte[] _roofTint;
        private byte[] _roofLevel;                                  // pitch: 0 at the eaves .. 3 at the ridge of a wide building (D-025)
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

        public PrototypeWorldView(BlockWorld blocks, GroundLayer ground, PrototypeArt art, Transform parent, Transform upright)
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
            // D-025: walls and roofs stand on an isometric grid in screen space and sort with the people (Individual mode, by screen height).
            var isoGrid = new GameObject("Walls and roofs (isometric)");
            isoGrid.transform.SetParent(upright, false);
            Grid g = isoGrid.AddComponent<Grid>();
            g.cellLayout = GridLayout.CellLayout.Isometric;
            g.cellSize = new Vector3(2f, 1f, 1f);
            _wallMap = CreateLayer(isoGrid.transform, "Walls", PrototypeIso.UprightOrder);
            _wallMap.GetComponent<TilemapRenderer>().mode = TilemapRenderer.Mode.Individual;
            // A roof piece is drawn on top of the walls (its sprite pivot lifts it) but sorts at the ground of its cell, a hair in front of the
            // wall of the same cell: whoever is behind the house is covered, whoever is in front is not.
            _roofMap = CreateLayer(isoGrid.transform, "Roofs", PrototypeIso.UprightOrder);
            _roofFadeMap = CreateLayer(isoGrid.transform, "Roof of your house (fading)", PrototypeIso.UprightOrder);
            _roofCutMap = CreateLayer(isoGrid.transform, "Roofs in front of you (see-through)", PrototypeIso.UprightOrder);
            _roofCutMap.color = new Color(1f, 1f, 1f, 0.22f);
            // Furniture and other objects stand up (D-025): their sprite rests on the cell centre; hidden under a closed roof.
            _objectMap = CreateLayer(isoGrid.transform, "Objects", PrototypeIso.UprightOrder);
            _objectMap.GetComponent<TilemapRenderer>().mode = TilemapRenderer.Mode.Individual;
            _objectMap.tileAnchor = new Vector3(0.5f, 0.97f, 0f);
            _roofLineMap = CreateLayer(isoGrid.transform, "Roof outlines", PrototypeIso.UprightOrder);
            foreach (Tilemap roof in new[] { _roofMap, _roofFadeMap, _roofCutMap, _roofLineMap })
            {
                roof.GetComponent<TilemapRenderer>().mode = TilemapRenderer.Mode.Individual;
                roof.tileAnchor = new Vector3(0.48f, 0.48f, 0f);
            }
            _roofLineMap.tileAnchor = new Vector3(0.47f, 0.47f, 0f);            // just in front of its roof piece
            // The outlines of the flat roofs are not used in the isometric view (the walls draw the edges); kept empty and hidden.
            _edgeMap = CreateLayer(grid.transform, "Roof edges", PrototypeDecorView.FlatOrder + 3);
            _edgeFadeMap = CreateLayer(grid.transform, "Roof edges of your house (fading)", PrototypeDecorView.FlatOrder + 3);
            _edgeMap.gameObject.SetActive(false);
            _edgeFadeMap.gameObject.SetActive(false);

            RepaintAll();
        }

        /// <summary>
        /// Gives every building's roof its own shade: finds each building's interior (floor connected to its doors) and the walls around it,
        /// and a neighbour never takes the shade of the one next to it.
        /// </summary>
        public void SetBuildings(IReadOnlyList<BuildingInfo> buildings)
        {
            if (buildings == null)
            {
                return;
            }
            int w = _blocks.Width;
            _roofTint = new byte[w * _blocks.Height];
            var owner = new int[w * _blocks.Height];                       // building index + 1, 0 = none
            var queue = new Queue<Int2>();
            var cells = new List<Int2>();
            for (int bi = 0; bi < buildings.Count; bi++)
            {
                BuildingInfo b = buildings[bi];
                cells.Clear();
                foreach (Int2 d in b.Doors)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        Enqueue(b, new Int2(d.X + Neighbours[k].X, d.Y + Neighbours[k].Y), owner, bi + 1, queue, cells);
                    }
                }
                while (queue.Count > 0)
                {
                    Int2 c = queue.Dequeue();
                    for (int k = 0; k < 4; k++)
                    {
                        Enqueue(b, new Int2(c.X + Neighbours[k].X, c.Y + Neighbours[k].Y), owner, bi + 1, queue, cells);
                    }
                }
                int shade = 1 + (int)(((uint)(b.Id * 2654435761u)) >> 16) % (RoofTints.Length - 1);
                int count = cells.Count;
                for (int i = 0; i < count; i++)
                {
                    Int2 c = cells[i];
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int x = c.X + dx;
                            int y = c.Y + dy;
                            if (_blocks.InBounds(x, y) && owner[y * w + x] == 0 && x >= b.Min.X && x <= b.Max.X && y >= b.Min.Y && y <= b.Max.Y)
                            {
                                owner[y * w + x] = bi + 1;                 // its walls
                                _roofTint[y * w + x] = (byte)shade;
                            }
                        }
                    }
                    _roofTint[c.Y * w + c.X] = (byte)shade;
                }
            }
            _owner = owner;
            ComputePitch();
            RepaintAll();
        }

        /// <summary>
        /// The pitch of every roof (D-025): a cell's height is how far it is from the edge of its building's roof (a hip roof), up to three steps,
        /// so a house rises to a ridge and a wide building ends in a flat top.
        /// </summary>
        private void ComputePitch()
        {
            int w = _blocks.Width;
            int h = _blocks.Height;
            _roofLevel = new byte[w * h];
            var dist = new short[w * h];
            var queue = new Queue<int>();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!HasRoofOver(x, y))
                    {
                        continue;
                    }
                    int self = _owner[y * w + x];
                    bool edge = false;
                    foreach (Int2 n in Neighbours)
                    {
                        int nx = x + n.X;
                        int ny = y + n.Y;
                        if (!_blocks.InBounds(nx, ny) || !HasRoofOver(nx, ny) || _owner[ny * w + nx] != self)
                        {
                            edge = true;
                            break;
                        }
                    }
                    if (edge || self == 0)
                    {
                        dist[y * w + x] = 1;
                        queue.Enqueue(y * w + x);
                    }
                }
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w;
                int y = i / w;
                foreach (Int2 n in Neighbours)
                {
                    int nx = x + n.X;
                    int ny = y + n.Y;
                    if (!_blocks.InBounds(nx, ny))
                    {
                        continue;
                    }
                    int j = ny * w + nx;
                    if (dist[j] == 0 && HasRoofOver(nx, ny) && _owner[j] == _owner[i])
                    {
                        dist[j] = (short)(dist[i] + 1);
                        queue.Enqueue(j);
                    }
                }
            }
            for (int i = 0; i < dist.Length; i++)
            {
                _roofLevel[i] = (byte)Mathf.Clamp(dist[i] - 1, 0, 3);
            }
        }

        /// <summary>Which side of this roof cell climbs (n, e, s, w) toward a higher neighbour of the same building, or null when it is flat.</summary>
        private string Rising(int x, int y, int level)
        {
            if (_roofLevel == null || level >= 3)
            {
                return null;
            }
            int self = OwnerAt(x, y);
            string best = null;
            int bestLevel = level;
            string[] names = { "n", "e", "s", "w" };
            Int2[] dirs = { new Int2(0, 1), new Int2(1, 0), new Int2(0, -1), new Int2(-1, 0) };
            for (int k = 0; k < 4; k++)
            {
                int nx = x + dirs[k].X;
                int ny = y + dirs[k].Y;
                if (_blocks.InBounds(nx, ny) && OwnerAt(nx, ny) == self && _roofLevel[ny * _blocks.Width + nx] > bestLevel)
                {
                    best = names[k];
                    bestLevel = _roofLevel[ny * _blocks.Width + nx];
                }
            }
            return best;
        }

        private void Enqueue(BuildingInfo b, Int2 p, int[] owner, int id, Queue<Int2> queue, List<Int2> cells)
        {
            if (p.X < b.Min.X || p.X > b.Max.X || p.Y < b.Min.Y || p.Y > b.Max.Y || !_blocks.InBounds(p.X, p.Y))
            {
                return;
            }
            int i = p.Y * _blocks.Width + p.X;
            if (owner[i] != 0 || _ground.Get(p.X, p.Y) != GroundType.Floor || !_blocks.GetBlock(p.X, p.Y).IsAir)
            {
                return;
            }
            owner[i] = id;
            queue.Enqueue(p);
            cells.Add(p);
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
            _wallMap.ClearAllTiles();
            _cut.Clear();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (_sprites.IsWallLike(x, y, _level))
                    {
                        PaintWall(x, y);
                    }
                }
            }
            _floorMap.ClearAllTiles();
            _objectMap.ClearAllTiles();
            _roofMap.ClearAllTiles();
            _roofFadeMap.ClearAllTiles();
            _roofCutMap.ClearAllTiles();
            _roofLineMap.ClearAllTiles();
            _roofCut.Clear();
            _edgeMap.ClearAllTiles();
            _edgeFadeMap.ClearAllTiles();
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
                            PaintRoofCell(key % _blocks.Width, key / _blocks.Width);
                        }
                        _fade = 1f;
                    }
                    _fadeTarget = 0f;
                }
                else if (_house.Count > 0)
                {
                    _fadeTarget = 1f;
                }
                UpdateCut(player, _fadeTarget <= 0f && _house.Count > 0);
            }
            if (_house.Count == 0)
            {
                return;
            }
            _fade = Mathf.MoveTowards(_fade, _fadeTarget, deltaTime * FadeSpeed);
            _roofFadeMap.color = new Color(_tint.r, _tint.g, _tint.b, _fade);
            _edgeFadeMap.color = _roofFadeMap.color;
            bool open = _fade < 0.5f;
            if (open != _houseOpen)
            {
                _houseOpen = open;
                PaintHouseObjects();
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
            var cells = new List<int>(_house);
            _house.Clear();
            _houseOpen = false;
            foreach (int key in cells)
            {
                PaintRoofCell(key % _blocks.Width, key / _blocks.Width);
                PaintObject(key % _blocks.Width, key / _blocks.Width);
            }
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
                _edgeFadeMap.SetTile(cell, _edgeMap.GetTile<TileBase>(cell));
                _edgeMap.SetTile(cell, null);
            }
            else
            {
                _roofMap.SetTile(cell, _roofFadeMap.GetTile<TileBase>(cell));
                _roofFadeMap.SetTile(cell, null);
                _edgeMap.SetTile(cell, _edgeFadeMap.GetTile<TileBase>(cell));
                _edgeFadeMap.SetTile(cell, null);
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
                            PaintWall(x, y);
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
            _wallMap.color = _tint;
            _floorMap.color = _tint;
            _objectMap.color = _tint;
            _roofMap.color = _tint;
            _edgeMap.color = _tint;
            _roofFadeMap.color = new Color(_tint.r, _tint.g, _tint.b, _fade);
            _edgeFadeMap.color = _roofFadeMap.color;
            var dim = new Color(_tint.r * 0.45f, _tint.g * 0.45f, _tint.b * 0.45f, 1f);
            _belowFloorMap.color = dim;
            _belowBlockMap.color = dim;
        }

        // ------------------------------------------------------------------ painting

        private TileBase BlockTile(int x, int y)
        {
            if (_sprites.IsWallLike(x, y, _level))
            {
                return null;                                     // drawn standing up on the wall map
            }
            return TileFor(_sprites.Structure(x, y, _level), _blocks.GetVariant(x, y, _level, BuildLayer.Structure));
        }

        private void PaintWall(int x, int y)
        {
            Sprite s = _sprites.IsWallLike(x, y, _level) ? _sprites.IsoWall(x, y, _level, _cut.Contains(Key(x, y))) : null;
            _wallMap.SetTile(new Vector3Int(x, y, 0), TileFor(s));
        }

        /// <summary>
        /// The cutaway (Project Zomboid): the walls in front of you are drawn low, those of the house you are in and any wall right in
        /// front of you outside, so a building never hides the player.
        /// </summary>
        private void UpdateCut(Int2 player, bool inside)
        {
            _cutNext.Clear();
            int pd = player.X + player.Y;
            if (inside)
            {
                foreach (int key in _house)
                {
                    int x = key % _blocks.Width;
                    int y = key / _blocks.Width;
                    if (x + y <= pd + 1 && _sprites.IsWallLike(x, y, _level))
                    {
                        _cutNext.Add(key);
                    }
                }
            }
            for (int dy = -3; dy <= 1; dy++)
            {
                for (int dx = -3; dx <= 1; dx++)
                {
                    int x = player.X + dx;
                    int y = player.Y + dy;
                    if (dx + dy < 0 && _blocks.InBounds(x, y) && _sprites.IsWallLike(x, y, _level))
                    {
                        _cutNext.Add(Key(x, y));
                    }
                }
            }
            // Roofs that would cover you on screen: in front of you and close, drawn see-through.
            _roofCutNext.Clear();
            for (int dy = -7; dy <= 2; dy++)
            {
                for (int dx = -7; dx <= 2; dx++)
                {
                    int x = player.X + dx;
                    int y = player.Y + dy;
                    if (dx + dy <= 1 && Math.Abs(dx - dy) <= 6 && _blocks.InBounds(x, y) && HasRoofOver(x, y))
                    {
                        _roofCutNext.Add(Key(x, y));
                    }
                }
            }
            var roofChanged = new List<int>();
            foreach (int k in _roofCut)
            {
                if (!_roofCutNext.Contains(k))
                {
                    roofChanged.Add(k);
                }
            }
            foreach (int k in _roofCutNext)
            {
                if (!_roofCut.Contains(k))
                {
                    roofChanged.Add(k);
                }
            }
            _roofCut.Clear();
            _roofCut.UnionWith(_roofCutNext);
            foreach (int k in roofChanged)
            {
                PaintRoofCell(k % _blocks.Width, k / _blocks.Width);
            }

            _cutChanged.Clear();
            foreach (int k in _cut)
            {
                if (!_cutNext.Contains(k))
                {
                    _cutChanged.Add(k);
                }
            }
            foreach (int k in _cutNext)
            {
                if (!_cut.Contains(k))
                {
                    _cutChanged.Add(k);
                }
            }
            _cut.Clear();
            _cut.UnionWith(_cutNext);
            foreach (int k in _cutChanged)
            {
                PaintWall(k % _blocks.Width, k / _blocks.Width);
            }
        }

        /// <summary>An object is seen unless it is under a closed roof: inside a house you only see its things once you are in.</summary>
        private void PaintObject(int x, int y)
        {
            bool hidden = _level >= 0 && HasRoofOver(x, y) && !(_houseOpen && _house.Contains(Key(x, y)));
            _objectMap.SetTile(new Vector3Int(x, y, 0), hidden ? null : TileFor(_sprites.Object(x, y, _level), _blocks.GetVariant(x, y, _level, BuildLayer.Object)));
        }

        private void PaintHouseObjects()
        {
            foreach (int key in _house)
            {
                PaintObject(key % _blocks.Width, key / _blocks.Width);
            }
        }

        private void PaintExtraLayers(int x, int y)
        {
            var cell = new Vector3Int(x, y, 0);
            _floorMap.SetTile(cell, TileFor(_sprites.Floor(x, y, _level), _blocks.GetVariant(x, y, _level, BuildLayer.Floor)));
            PaintObject(x, y);
            PaintRoofCell(x, y);
        }

        /// <summary>The roof of a cell goes to the fading map when the cell belongs to the house you are in.</summary>
        private void PaintRoofCell(int x, int y)
        {
            var cell = new Vector3Int(x, y, 0);
            BlockType type = TopRoof(x, y, out int level);
            TileBase tile = null;
            TileBase edge = null;
            if (!type.IsAir && _level >= 0)
            {
                edge = EdgeTile(x, y);
                byte tint = _roofTint != null ? _roofTint[y * _blocks.Width + x] : (byte)0;
                int pitch = _roofLevel != null ? _roofLevel[y * _blocks.Width + x] : 0;
                tile = TileFor(_sprites.IsoRoof(type, pitch, Rising(x, y, pitch)), _blocks.GetVariant(x, y, level, BuildLayer.Roof), tint);
            }
            // One map per state: your house (fading), in front of you (see-through) or the rest.
            bool fading = _house.Contains(Key(x, y));
            bool cut = !fading && _roofCut.Contains(Key(x, y));
            _roofFadeMap.SetTile(cell, fading ? tile : null);
            _roofCutMap.SetTile(cell, cut ? tile : null);
            _roofMap.SetTile(cell, fading || cut ? null : tile);
            _roofLineMap.SetTile(cell, fading || cut || tile == null ? null : LineTile(x, y));
            (fading ? _edgeFadeMap : _edgeMap).SetTile(cell, edge);
            (fading ? _edgeMap : _edgeFadeMap).SetTile(cell, null);
        }

        /// <summary>One tile per sprite and paint colour (the colour of <see cref="BlockVariant"/> tints the tile).</summary>
        /// <summary>The outline of a roof piece: dark where the next cell is outside the building (or another building), with a deeper eave to the south.</summary>
        private TileBase EdgeTile(int x, int y)
        {
            int self = OwnerAt(x, y);
            int mask = 0;
            if (!SameRoof(x, y + 1, self)) { mask |= 1; }
            if (!SameRoof(x + 1, y, self)) { mask |= 2; }
            if (!SameRoof(x, y - 1, self)) { mask |= 4; }
            if (!SameRoof(x - 1, y, self)) { mask |= 8; }
            if (mask == 0)
            {
                return null;
            }
            if (_edgeTiles == null)
            {
                _edgeTiles = new Tile[16];
            }
            if (_edgeTiles[mask] == null)
            {
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = MakeEdgeSprite(mask);
                _edgeTiles[mask] = tile;
            }
            return _edgeTiles[mask];
        }

        /// <summary>The lifted roof of this cell would hide the door right above it (a door on a north, east or west wall): leave a gap so the way in shows.</summary>
        private bool CoversADoor(int x, int y)
        {
            if (!_blocks.InBounds(x, y + 1))
            {
                return false;
            }
            BlockType t = _blocks.GetBlock(x, y + 1, 0, BuildLayer.Structure);
            return !t.IsAir && (t.Key == "door" || t.Key == "door_open" || (t.Build != null && t.Build.Shape == "door"));
        }

        /// <summary>Dark lines along the sides of a roof piece where the building ends (iso diamond: N up-left, E up-right, S down-right, W down-left).</summary>
        private TileBase LineTile(int x, int y)
        {
            int self = OwnerAt(x, y);
            int mask = (SameRoof(x, y + 1, self) ? 0 : 1) | (SameRoof(x + 1, y, self) ? 0 : 2) | (SameRoof(x, y - 1, self) ? 0 : 4) | (SameRoof(x - 1, y, self) ? 0 : 8);
            if (mask == 0)
            {
                return null;
            }
            if (_lineTiles == null)
            {
                _lineTiles = new Tile[16];
            }
            if (_lineTiles[mask] == null)
            {
                const int Wd = 64, Hd = 32;
                var tex = new Texture2D(Wd, Hd, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var clear = new Color(0f, 0f, 0f, 0f);
                var line = new Color(0.07f, 0.04f, 0.04f, 0.9f);
                var light = new Color(1f, 0.92f, 0.8f, 0.35f);
                for (int py = 0; py < Hd; py++)
                {
                    for (int px = 0; px < Wd; px++)
                    {
                        // Distance (in px) to each edge of the diamond; py counts from the bottom.
                        float fx = (px + 0.5f - 32f) / 32f;
                        float fy = (py + 0.5f - 16f) / 16f;
                        bool inside = Mathf.Abs(fx) + Mathf.Abs(fy) <= 1f;
                        Color c = clear;
                        if (inside)
                        {
                            float d = (1f - Mathf.Abs(fx) - Mathf.Abs(fy)) * 16f;
                            bool up = fy > 0f, right = fx > 0f;
                            int edge = up ? (right ? 2 : 1) : (right ? 4 : 8);
                            if ((mask & edge) != 0 && d < 1.6f)
                            {
                                c = (edge == 4 || edge == 8) ? line : light;     // the eave toward you is dark, the far edge catches the light
                            }
                        }
                        tex.SetPixel(px, py, c);
                    }
                }
                tex.Apply(false);
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = Sprite.Create(tex, new Rect(0, 0, Wd, Hd), new Vector2(0.5f, (16f - PrototypeIso.WallHeight * 32f) / Hd), 32f);
                _lineTiles[mask] = tile;
            }
            return _lineTiles[mask];
        }

        private int OwnerAt(int x, int y) => _owner != null && _blocks.InBounds(x, y) ? _owner[y * _blocks.Width + x] : 0;

        private bool SameRoof(int x, int y, int owner) => HasRoofOver(x, y) && OwnerAt(x, y) == owner;

        // Mask: N = 1, E = 2, S = 4, W = 8 (a side where the roof ends). 2 px of dark line; the south side adds a soft shadow like an eave.
        private Sprite MakeEdgeSprite(int mask)
        {
            const int S = 32;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int py = 0; py < S; py++)
            {
                for (int px = 0; px < S; px++)
                {
                    float a = 0f;
                    if ((mask & 1) != 0 && py >= S - 2) { a = Mathf.Max(a, 0.9f); }
                    if ((mask & 2) != 0 && px >= S - 2) { a = Mathf.Max(a, 0.9f); }
                    if ((mask & 8) != 0 && px < 2) { a = Mathf.Max(a, 0.9f); }
                    if ((mask & 4) != 0)
                    {
                        if (py < 3) { a = Mathf.Max(a, 0.95f); }
                        else if (py < 8) { a = Mathf.Max(a, 0.35f * (8 - py) / 5f); }
                    }
                    tex.SetPixel(px, py, a <= 0f ? clear : new Color(0.06f, 0.03f, 0.03f, a));
                }
            }
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), _art.PixelsPerUnit);
        }

        private Tile TileFor(Sprite sprite, byte variant = 0, int tint = 0)
        {
            if (sprite == null)
            {
                return null;
            }
            int paint = BlockVariant.Color(variant);
            int key = paint + 100 * tint;
            if (!_tiles.TryGetValue((sprite, key), out Tile tile))
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                Color baseColor = paint == 0 ? Color.white : (Color)PrototypePlaceholders.Paints[Mathf.Clamp(paint, 0, PrototypePlaceholders.Paints.Length - 1)];
                Color shade = RoofTints[tint % RoofTints.Length];
                tile.color = new Color(baseColor.r * shade.r, baseColor.g * shade.g, baseColor.b * shade.b, 1f);
                _tiles.Add((sprite, key), tile);
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
