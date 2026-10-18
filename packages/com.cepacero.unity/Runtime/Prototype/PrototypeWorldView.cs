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
        private readonly Tilemap _roofLineMap;
        private readonly Tilemap _roofDetailMap;                     // chimneys, aerials, air conditioners on the roofs (D-027)                       // the outline of each building's roof
        private Tile[] _lineTiles;
        private int[] _owner;                                       // building index + 1 per cell, 0 = none
        private readonly Tilemap _belowFloorMap;
        private readonly Tilemap _belowBlockMap;
        // Each building's roof gets its own shade, so neighbours read as separate houses instead of one red sea.
        private static readonly Color[] RoofTints =
        {
            Color.white,
            new Color(0.60f, 0.66f, 0.80f),                  // slate
            new Color(1.00f, 0.86f, 0.52f),                  // ochre
            new Color(0.62f, 0.52f, 0.50f),                  // dark brown
            new Color(0.70f, 0.84f, 0.66f),                  // mossy green
            new Color(1.00f, 0.66f, 0.58f),                  // salmon
            new Color(0.50f, 0.56f, 0.68f),                  // blue grey
            new Color(0.84f, 0.80f, 0.76f),                  // pale clay
        };

        // The facade material of each building (D-025): plaster, ochre, brick, stone, concrete, so a street is not one brown wall.
        private static readonly string[] ResidentialFacades = { "drywall", "drywall", "drywall", "drywall", "adobe", "adobe", "adobe", "brick", "brick", "stone" };
        private static readonly string[] OtherFacades = { "concrete", "concrete", "concrete", "drywall", "drywall", "stone", "stone", "brick", "brick" };
        private string[] _facadeNames;
        private byte[] _facade;                                    // index into _facadeNames per cell, 0 = the map's own (brick)

        private byte[] _roofTint;
        private bool[] _tall;                                       // a building of three storeys or more is drawn two storeys tall (D-026)
        private readonly Tilemap _upperWallMap;
        private readonly Tilemap _decoMap;
        private readonly Tilemap _paperSouthMap;                    // the inside of the walls (D-027): paper, paint or tiles of the room in front
        private readonly Tilemap _paperWestMap;
        private readonly Dictionary<int, string> _paint = new Dictionary<int, string>();                          // pictures, clocks and curtains on the walls of the house you are in (D-027)
        private readonly Dictionary<int, string> _wallDeco = new Dictionary<int, string>();                     // the second storey of the walls of tall buildings
        private readonly Dictionary<int, List<int>> _byOwner = new Dictionary<int, List<int>>();     // building index + 1 -> its cells
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
            _belowBlockMap = CreateLayer(grid.transform, "Level below (blocks)", 0);
            _blockMap = CreateLayer(grid.transform, "Blocks", PrototypeDecorView.FlatOrder + 1); // flat decals go in between
            // D-025: walls and roofs stand on an isometric grid in screen space and sort with the people (Individual mode, by screen height).
            var isoGrid = new GameObject("Walls and roofs (isometric)");
            isoGrid.transform.SetParent(upright, false);
            Grid g = isoGrid.AddComponent<Grid>();
            g.cellLayout = GridLayout.CellLayout.Isometric;
            g.cellSize = new Vector3(2f, 1f, 1f);
            // Ground and floors are own diamonds on the same isometric grid (D-025), under everything that stands up and under the flat decals.
            _groundMap = CreateLayer(isoGrid.transform, "Ground", -2);
            _belowFloorMap = CreateLayer(isoGrid.transform, "Level below (floors)", -1);       // seen through holes in the floor (level cut)
            _floorMap = CreateLayer(isoGrid.transform, "Floors", 0);                           // you walk on it
            _wallMap = CreateLayer(isoGrid.transform, "Walls", PrototypeIso.UprightOrder);
            _upperWallMap = CreateLayer(isoGrid.transform, "Walls, second storey", PrototypeIso.UprightOrder);
            _paperSouthMap = CreateLayer(isoGrid.transform, "Inside of the walls (south faces)", PrototypeIso.UprightOrder);
            _paperWestMap = CreateLayer(isoGrid.transform, "Inside of the walls (west faces)", PrototypeIso.UprightOrder);
            foreach (Tilemap paper in new[] { _paperSouthMap, _paperWestMap })
            {
                paper.GetComponent<TilemapRenderer>().mode = TilemapRenderer.Mode.Individual;
                paper.tileAnchor = new Vector3(0.5f, 0.495f, 0f);              // over its own wall, under what hangs on it
            }
            _decoMap = CreateLayer(isoGrid.transform, "Wall decoration", PrototypeIso.UprightOrder);
            _decoMap.GetComponent<TilemapRenderer>().mode = TilemapRenderer.Mode.Individual;
            _decoMap.tileAnchor = new Vector3(0.5f, 0.49f, 0f);                // a hair in front of its wall
            _upperWallMap.GetComponent<TilemapRenderer>().mode = TilemapRenderer.Mode.Individual;
            _upperWallMap.transform.localPosition = new Vector3(0f, PrototypeIso.WallHeight, 0f);
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
            _roofDetailMap = CreateLayer(isoGrid.transform, "Roof details", PrototypeIso.UprightOrder);
            _roofDetailMap.GetComponent<TilemapRenderer>().mode = TilemapRenderer.Mode.Individual;
            _roofDetailMap.tileAnchor = new Vector3(0.46f, 0.46f, 0f);             // in front of its roof piece
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
            _tall = new bool[w * _blocks.Height];
            _facade = new byte[w * _blocks.Height];
            _facadeNames = new[] { null, "drywall", "adobe", "brick", "stone", "concrete" };
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
                                _tall[y * w + x] = b.Levels >= 3;
                                _facade[y * w + x] = FacadeIndex(b);
                            }
                        }
                    }
                    _roofTint[c.Y * w + c.X] = (byte)shade;
                    _tall[c.Y * w + c.X] = b.Levels >= 3;
                }
            }
            // Roofed cells the door flood did not reach (rooms shut in by walls, odd corners) join the building next to them, so a house is always
            // one whole under one roof (D-028).
            var spread = new Queue<int>();
            for (int i = 0; i < owner.Length; i++)
            {
                if (owner[i] != 0)
                {
                    spread.Enqueue(i);
                }
            }
            while (spread.Count > 0)
            {
                int i = spread.Dequeue();
                int ix = i % w;
                int iy = i / w;
                foreach (Int2 n in Neighbours)
                {
                    int nx = ix + n.X;
                    int ny = iy + n.Y;
                    if (_blocks.InBounds(nx, ny) && owner[ny * w + nx] == 0 && HasRoofOver(nx, ny))
                    {
                        owner[ny * w + nx] = owner[i];
                        _roofTint[ny * w + nx] = _roofTint[i];
                        _facade[ny * w + nx] = _facade[i];
                        _tall[ny * w + nx] = _tall[i];
                        spread.Enqueue(ny * w + nx);
                    }
                }
            }
            _owner = owner;
            _byOwner.Clear();
            for (int i = 0; i < owner.Length; i++)
            {
                if (owner[i] != 0)
                {
                    if (!_byOwner.TryGetValue(owner[i], out List<int> list))
                    {
                        _byOwner[owner[i]] = list = new List<int>();
                    }
                    list.Add(i);
                }
            }
            _wallDeco.Clear();
            _paint.Clear();
            foreach (BuildingInfo b in buildings)
            {
                foreach (KeyValuePair<Int2, string> kv in b.WallPaint)
                {
                    _paint[Key(kv.Key.X, kv.Key.Y)] = kv.Value;
                }
                foreach (Furniture f in b.Furniture)
                {
                    if (f.OnWall && _blocks.InBounds(f.Cell.X, f.Cell.Y))
                    {
                        _wallDeco[Key(f.Cell.X, f.Cell.Y)] = f.Sprite + "_" + f.Face;
                    }
                }
            }
            SpreadFacades();
            // Which cells are drawn two storeys tall comes from the roof itself (its level in the world), not from which building the door flood
            // gave the cell to: a cell given to the wrong neighbour had its roof drawn low inside tall walls and showed as a hole (D-029).
            for (int y = 0; y < _blocks.Height; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!TopRoof(x, y, out int roofLevel).IsAir)
                    {
                        _tall[y * w + x] = roofLevel >= 2;
                    }
                }
            }
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
                    // One roof is the whole connected run of roofed cells of the same height class (a row of houses shares its ridge), not one
                    // per building: per-building pitches left flat patches between pitched ones that looked like holes (D-028).
                    int self = _owner[y * w + x];
                    bool edge = false;
                    foreach (Int2 n in Neighbours)
                    {
                        int nx = x + n.X;
                        int ny = y + n.Y;
                        if (!_blocks.InBounds(nx, ny) || !HasRoofOver(nx, ny) || _tall[ny * w + nx] != _tall[y * w + x])
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
                    if (dist[j] == 0 && HasRoofOver(nx, ny) && _tall[j] == _tall[i])
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
                if (_blocks.InBounds(nx, ny) && HasRoofOver(nx, ny) && _tall[ny * _blocks.Width + nx] == _tall[y * _blocks.Width + x] && _roofLevel[ny * _blocks.Width + nx] > bestLevel)
                {
                    best = names[k];
                    bestLevel = _roofLevel[ny * _blocks.Width + nx];
                }
            }
            return best;
        }

        /// <summary>Wall cells that belong to no building's interior (the ends and stubs of walls) take the facade of the wall next to them.</summary>
        private void SpreadFacades()
        {
            int w = _blocks.Width;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int y = 0; y < _blocks.Height; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (_facade[y * w + x] != 0 || !_sprites.IsWallLike(x, y, 0))
                        {
                            continue;
                        }
                        for (int dy = -1; dy <= 1 && _facade[y * w + x] == 0; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (_blocks.InBounds(nx, ny) && _facade[ny * w + nx] != 0)
                                {
                                    _facade[y * w + x] = _facade[ny * w + nx];
                                    _tall[y * w + x] = _tall[ny * w + nx];
                                    _roofTint[y * w + x] = _roofTint[ny * w + nx];
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private byte FacadeIndex(BuildingInfo b)
        {
            string[] pool = b.Kind == "residential" ? ResidentialFacades : OtherFacades;
            string name = pool[(int)(((uint)(b.Id * 2246822519u + 7u)) >> 12) % pool.Length];
            return (byte)System.Array.IndexOf(_facadeNames, name);
        }

        private void Enqueue(BuildingInfo b, Int2 p, int[] owner, int id, Queue<Int2> queue, List<Int2> cells)
        {
            if (p.X < b.Min.X || p.X > b.Max.X || p.Y < b.Min.Y || p.Y > b.Max.Y || !_blocks.InBounds(p.X, p.Y))
            {
                return;
            }
            int i = p.Y * _blocks.Width + p.X;
            BlockType here = _blocks.GetBlock(p.X, p.Y);
            bool passable = here.IsAir || here.Key.StartsWith("wall_drywall") || here.Key.StartsWith("door_drywall");      // the rooms' own walls and doors (D-026)
            if (owner[i] != 0 || _ground.Get(p.X, p.Y) != GroundType.Floor || !passable)
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
            _roofDetailMap.gameObject.SetActive(level >= 0);
            _upperWallMap.gameObject.SetActive(level == 0);
            _decoMap.gameObject.SetActive(level == 0);
            _paperSouthMap.gameObject.SetActive(level == 0);
            _paperWestMap.gameObject.SetActive(level == 0);
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
                    groundTiles[y * w + x] = TileFor(_art.IsoFlat(_art.Ground(_ground.Get(x, y), x, y)));
                    blockTiles[y * w + x] = BlockTile(x, y);
                }
            }
            var area = new BoundsInt(0, 0, 0, w, h, 1);
            _groundMap.SetTilesBlock(area, groundTiles);
            _blockMap.SetTilesBlock(area, blockTiles);
            _wallMap.ClearAllTiles();
            _upperWallMap.ClearAllTiles();
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
            _roofDetailMap.ClearAllTiles();
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
            _belowFloorMap.SetTile(cell, TileFor(_art.IsoFlat(floor), _blocks.GetVariant(x, y, below, BuildLayer.Floor)));
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
        /// <summary>Debug bridge (`hide wall,roof,...`): layers listed here are switched off, to see what lies under what.</summary>
        public static string DebugHidden = "";

        private void ApplyDebugHidden()
        {
            bool Off(string n) => DebugHidden.Contains(n);
            _wallMap.gameObject.SetActive(!Off("wall"));
            _upperWallMap.gameObject.SetActive(!Off("upper"));
            _roofMap.gameObject.SetActive(!Off("roof"));
            _roofLineMap.gameObject.SetActive(!Off("line"));
            _roofDetailMap.gameObject.SetActive(!Off("detail"));
            _paperSouthMap.gameObject.SetActive(!Off("paper"));
            _paperWestMap.gameObject.SetActive(!Off("paper"));
            _decoMap.gameObject.SetActive(!Off("deco"));
            _floorMap.gameObject.SetActive(!Off("floor"));
        }

        public void UpdateRoofs(Int2 player, float deltaTime)
        {
            ApplyDebugHidden();
            if (_level < 0)
            {
                if (_house.Count > 0)
                {
                    ReleaseHouse();
                }
                if (player != _lastPlayer || _roofDirty)
                {
                    _lastPlayer = player;
                    _roofDirty = false;
                    UpdateCut(player, false);                          // underground too: the walls in front of you are drawn low (D-025)
                }
                return;
            }
            if (player != _lastPlayer || _roofDirty)
            {
                _lastPlayer = player;
                _roofDirty = false;
                bool found = FindHouse(player, _scratch);
                if (found)
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
            // The whole building opens at once (D-028): every cell of the same building, rooms and walls, not a flood that a wall can stop.
            int owner = OwnerAt(start.X, start.Y);
            if (owner != 0 && _byOwner.TryGetValue(owner, out List<int> cellsOfOwner))
            {
                foreach (int k in cellsOfOwner)
                {
                    into.Add(k);
                }
                return true;
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
                    // The outline is part of the house but does not lead into the next one; the walls inside it (rooms) do not stop it (D-026).
                    if (!_sprites.IsWallLike(n.X, n.Y, _level) || (_owner != null && _owner[Key(n.X, n.Y)] != 0 && _owner[Key(n.X, n.Y)] == _owner[Key(start.X, start.Y)] && HasInteriorSide(n)))
                    {
                        _queue.Enqueue(n);
                    }
                }
            }
            return true;
        }

        /// <summary>An inner wall: the cell next to it on both sides is under the same roof (an outer wall has the street on one side).</summary>
        private bool HasInteriorSide(Int2 w)
        {
            bool east = HasRoofOver(w.X + 1, w.Y) && !_sprites.IsWallLike(w.X + 1, w.Y, _level);
            bool west = HasRoofOver(w.X - 1, w.Y) && !_sprites.IsWallLike(w.X - 1, w.Y, _level);
            bool north = HasRoofOver(w.X, w.Y + 1) && !_sprites.IsWallLike(w.X, w.Y + 1, _level);
            bool south = HasRoofOver(w.X, w.Y - 1) && !_sprites.IsWallLike(w.X, w.Y - 1, _level);
            return (east && west) || (north && south);
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
                if (_sprites.IsWallLike(key % _blocks.Width, key / _blocks.Width, _level))
                {
                    PaintWall(key % _blocks.Width, key / _blocks.Width);
                }
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
            _decoMap.color = _tint;
            _paperSouthMap.color = _tint;
            _paperWestMap.color = _tint;
            _upperWallMap.color = _tint;
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
            string facade = _facade != null && _level == 0 ? _facadeNames[_facade[y * _blocks.Width + x]] : null;
            Sprite s = _sprites.IsWallLike(x, y, _level) ? _sprites.IsoWall(x, y, _level, _cut.Contains(Key(x, y)), facade) : null;
            _wallMap.SetTile(new Vector3Int(x, y, 0), TileFor(s));
            PaintUpperWall(x, y, s);
            int key = Key(x, y);
            // The inside of the walls only shows in the house you are in: elsewhere the roof hides it, and drawn there it showed through the
            // roofs as holes (paper sorts with its wall, in front of the roofs further back) (D-029).
            PaintInside(x, y, s != null && !_cut.Contains(key) && _houseOpen && _house.Contains(key));
            if (_wallDeco.TryGetValue(key, out string deco))
            {
                bool show = s != null && _level == 0 && _houseOpen && _house.Contains(key) && !_cut.Contains(key);
                _decoMap.SetTile(new Vector3Int(x, y, 0), show && _art.TryGet(deco, out Sprite d) ? TileFor(d) : null);
            }
        }

        /// <summary>The second storey of a tall building's wall: the same material, with a window on every other cell; gone while you are inside or the wall is in front of you.</summary>
        /// <summary>
        /// The faces of a wall turned to a room show that room's paper, paint or tiles instead of the facade (D-027): the south face when the
        /// room is south of the wall, the west face when it is west. The window or door of the wall stays open in the paper.
        /// </summary>
        private void PaintInside(int x, int y, bool standing)
        {
            var cell = new Vector3Int(x, y, 0);
            Sprite south = null, west = null;
            if (standing && _level == 0)
            {
                south = PaperFor(x, y, x, y - 1, 's');
                west = PaperFor(x, y, x - 1, y, 'w');
            }
            _paperSouthMap.SetTile(cell, TileFor(south));
            _paperWestMap.SetTile(cell, TileFor(west));
        }

        private Sprite PaperFor(int x, int y, int rx, int ry, char face)
        {
            if (!_blocks.InBounds(rx, ry) || !_paint.TryGetValue(Key(rx, ry), out string paper))
            {
                return null;
            }
            BlockType t = _blocks.GetBlock(x, y, _level, BuildLayer.Structure);
            string shape = t.Build != null ? t.Build.Shape : t.Key == "door" || t.Key == "door_open" ? "door" : "wall";
            string hole = "";
            if (shape == "door" || shape == "window" || shape == "bigwindow")
            {
                bool eastWest = _sprites.IsWallLike(x + 1, y, _level) || _sprites.IsWallLike(x - 1, y, _level);
                if ((eastWest ? 's' : 'w') == face)
                {
                    hole = shape == "door" ? "_door" : "_window";
                }
            }
            return _art.TryGet($"iso_paper_{paper}{hole}_{face}", out Sprite sprite) ? sprite : null;
        }

        private void PaintUpperWall(int x, int y, Sprite ground)
        {
            Sprite upper = null;
            int key = Key(x, y);
            if (ground != null && _level == 0 && _tall != null && _tall[key] && !_cut.Contains(key) && !(_houseOpen && _house.Contains(key)) && !_roofCut.Contains(key))
            {
                string facade = _facade != null ? _facadeNames[_facade[key]] : null;
                upper = _sprites.IsoUpperWall(x, y, _level, facade, ground);
            }
            _upperWallMap.SetTile(new Vector3Int(x, y, 0), TileFor(upper));
        }

        /// <summary>
        /// The cutaway (Project Zomboid): the walls in front of you are drawn low, those of the house you are in and any wall right in
        /// front of you outside, so a building never hides the player.
        /// </summary>
        /// <summary>How many rows (x + y) in front of the player a cell still hides them: a storey of wall is 5 rows tall on screen, two storeys 10 (D-028).</summary>
        private int Reach(int x, int y) => _tall != null && _tall[y * _blocks.Width + x] ? 11 : 6;

        private void UpdateCut(Int2 player, bool inside)
        {
            _cutNext.Clear();
            int pd = player.X + player.Y;
            {
                // The band of the screen between you and the camera: a wall two storeys tall hides you from up to 12 rows away (x + y), so
                // everything in that band drops low, your own house and the ones in front of it alike (D-028).
                for (int dy = -14; dy <= 2; dy++)
                {
                    for (int dx = -14; dx <= 2; dx++)
                    {
                        int x = player.X + dx;
                        int y = player.Y + dy;
                        if (dx + dy <= 1 && -(dx + dy) <= Reach(x, y) && Math.Abs(dx - dy) <= (inside ? 2 : 1) && _blocks.InBounds(x, y) && _sprites.IsWallLike(x, y, _level))
                        {
                            _cutNext.Add(Key(x, y));
                        }
                    }
                }
            }
            // Project Zomboid rules (D-028): from outside every building is whole and opaque, walls and roof. Only the building you are in
            // opens: its roof fades (UpdateRoofs) and its walls in front of you drop low (above). Nothing is cut away around you out in the street.
            _roofCutNext.Clear();
            {
                // The roofs of the buildings in front of you (the camera looks over them) go see-through so you can see
                // your own rooms; from the street they stay opaque.
                for (int dy = -14; dy <= 2; dy++)
                {
                    for (int dx = -14; dx <= 2; dx++)
                    {
                        int x = player.X + dx;
                        int y = player.Y + dy;
                        if (dx + dy <= 1 && -(dx + dy) <= Reach(x, y) + 1 && Math.Abs(dx - dy) <= (inside ? 7 : 2) && _blocks.InBounds(x, y) && HasRoofOver(x, y) && !_house.Contains(Key(x, y)))
                        {
                            _roofCutNext.Add(Key(x, y));
                        }
                    }
                }
            }
            if (_level < 0)
            {
                // Underground there is no roof to open; the earth in front of you drops low so you can see yourself.
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
                if (_sprites.IsWallLike(k % _blocks.Width, k / _blocks.Width, _level))
                {
                    PaintWall(k % _blocks.Width, k / _blocks.Width);
                }
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
                if (_sprites.IsWallLike(key % _blocks.Width, key / _blocks.Width, _level))
                {
                    PaintWall(key % _blocks.Width, key / _blocks.Width);
                }
            }
        }

        private void PaintExtraLayers(int x, int y)
        {
            var cell = new Vector3Int(x, y, 0);
            _floorMap.SetTile(cell, TileFor(_art.IsoFlat(_sprites.Floor(x, y, _level)), _blocks.GetVariant(x, y, _level, BuildLayer.Floor)));
            PaintObject(x, y);
            PaintRoofCell(x, y);
        }

        /// <summary>The roof of a cell goes to the fading map when the cell belongs to the house you are in.</summary>
        private static readonly string[] FlatRoofDetails = { "ac", "tank", "skylight", "antenna", "ac" };
        private static readonly string[] PitchedRoofDetails = { "chimney", "chimney", "antenna" };

        private void PaintRoofCell(int x, int y)
        {
            var cell = new Vector3Int(x, y, 0);
            BlockType type = TopRoof(x, y, out int level);
            TileBase tile = null;
            TileBase edge = null;
            TileBase detail = null;
            if (!type.IsAir && _level >= 0)
            {
                edge = EdgeTile(x, y);
                byte tint = _roofTint != null ? _roofTint[y * _blocks.Width + x] : (byte)0;
                int pitch = _roofLevel != null ? _roofLevel[y * _blocks.Width + x] : 0;
                string rising = Rising(x, y, pitch);
                uint h = unchecked((uint)(x * 73856093) ^ (uint)(y * 19349663));
                Sprite roofSprite = _sprites.IsoRoof(type, pitch, rising);
                if (rising == null && h % 4 == 0 && _art.TryGet(roofSprite.name + "_x", out Sprite aged))
                {
                    roofSprite = aged;                                                         // moss and patches here and there
                }
                bool tall = _tall != null && _tall[y * _blocks.Width + x];
                if (tall)
                {
                    roofSprite = _art.Lifted(roofSprite, PrototypeIso.WallHeight);              // on top of the second storey
                }
                if (rising == null && (h >> 8) % 61 == 0)
                {
                    bool flat = type.Build != null && type.Build.MaterialId == "flat";
                    string[] pool = flat ? FlatRoofDetails : PitchedRoofDetails;
                    if (_art.TryGet("iso_rdet_" + pool[(h >> 16) % (uint)pool.Length], out Sprite d))
                    {
                        detail = TileFor(_art.Lifted(d, PrototypeIso.WallHeight * (tall ? 2f : 1f) + pitch * 10f / 32f));
                    }
                }
                tile = TileFor(roofSprite, _blocks.GetVariant(x, y, level, BuildLayer.Roof), tint);
            }
            // One map per state: your house (fading), in front of you (see-through) or the rest.
            bool fading = _house.Contains(Key(x, y));
            bool cut = !fading && _roofCut.Contains(Key(x, y));
            _roofFadeMap.SetTile(cell, fading ? tile : null);
            _roofCutMap.SetTile(cell, cut ? tile : null);
            _roofMap.SetTile(cell, fading || cut ? null : tile);
            _roofDetailMap.SetTile(cell, fading || cut ? null : detail);
            _roofLineMap.SetTile(cell, fading || cut || tile == null ? null : LineTile(x, y, _tall != null && _tall[y * _blocks.Width + x]));
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
        private TileBase LineTile(int x, int y, bool tall)
        {
            int self = OwnerAt(x, y);
            int mask = (SameRoof(x, y + 1, self) ? 0 : 1) | (SameRoof(x + 1, y, self) ? 0 : 2) | (SameRoof(x, y - 1, self) ? 0 : 4) | (SameRoof(x - 1, y, self) ? 0 : 8);
            if (mask == 0)
            {
                return null;
            }
            if (_lineTiles == null)
            {
                _lineTiles = new Tile[32];
            }
            int slot = mask + (tall ? 16 : 0);
            if (_lineTiles[slot] == null)
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
                tile.sprite = Sprite.Create(tex, new Rect(0, 0, Wd, Hd), new Vector2(0.5f, (16f - PrototypeIso.WallHeight * 32f * (tall ? 2f : 1f)) / Hd), 32f);
                _lineTiles[slot] = tile;
            }
            return _lineTiles[slot];
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
