using System;
using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Net.Protocol;
using CepaCero.Player;
using CepaCero.Util;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Pooled sprites for players, zombies, piles on the floor and containers, with
    /// 4-facing walk animation (D-018). Draws either the host simulation directly or,
    /// in loopback mode, a client replica built only from network bytes.
    /// </summary>
    public sealed class PrototypeEntityView
    {
        private const float WalkFps = 8f;

        // Facing indices; right reuses the left frames mirrored.
        // Eight directions (D-025): the art has five views (down, up, left, down-left, up-left) and the rest are their mirrors.
        private const int Down = 0;
        private const int Up = 1;
        private const int Left = 2;
        private const int Right = 3;
        private const int DownLeft = 4;
        private const int UpLeft = 5;
        private const int DownRight = 6;
        private const int UpRight = 7;
        private static readonly string[] FacingNames = { "down", "up", "left", "dl", "ul" };

        /// <summary>The drawn view of a facing, and whether it is mirrored.</summary>
        private static int ArtView(int facing, out bool flip)
        {
            flip = facing == Right || facing == DownRight || facing == UpRight;
            switch (facing)                                  // index in FacingNames: down, up, left, dl, ul
            {
                case Right: return 2;
                case DownLeft:
                case DownRight: return 3;
                case UpLeft:
                case UpRight: return 4;
                default: return facing;
            }
        }

        /// <summary>
        /// The facing for a step on the grid. The grid is turned 45 degrees on screen, so a step along an axis looks diagonal:
        /// east goes up-right, north up-left, west down-left, south down-right; the grid diagonals are the straight views.
        /// </summary>
        private static int FacingOf(float dx, float dy)
        {
            int sector = (int)Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4.0));
            switch (((sector % 8) + 8) % 8)
            {
                case 0: return UpRight;       // east
                case 1: return Up;            // north-east
                case 2: return UpLeft;        // north
                case 3: return Left;          // north-west
                case 4: return DownLeft;      // west
                case 5: return Down;          // south-west
                case 6: return DownRight;     // south
                default: return Right;        // south-east
            }
        }

        // Everything is sorted by height on screen (lower = in front); the bias breaks ties.
        private const int SortBase = 20000;
        private const int ContainerBias = 0;
        private const int PileBias = 1;
        public const int DecorBias = 1;
        private const int ZombieBias = 2;
        private const int PlayerBias = 3;

        /// <summary>Lower on screen draws in front; shared with the decoration so a car can hide a zombie.</summary>
        public static int SortingOrder(float y, int bias) => SortBase - Mathf.RoundToInt(y * 4f) * 4 + bias;

        private readonly Transform _root;
        private readonly Transform _flat;                     // the cursor lies on the ground (D-025)
        private readonly int _ppu;
        private readonly ZombieCatalog _catalog;
        private readonly Sprite[][][] _zombieFrames;
        private readonly Sprite[][] _playerFrames;
        private readonly Sprite[][] _otherFrames;
        private readonly List<Sprite[][]> _walkerVariants = new List<Sprite[][]>();
        private readonly int _walkerType = -1;
        private readonly Sprite _downed;
        private readonly Sprite _pile;
        private readonly Sprite _container;
        private readonly Sprite _cursorSprite;
        private SpriteRenderer _cursor;
        private readonly Dictionary<int, Walk> _zombieWalk = new Dictionary<int, Walk>();
        private readonly Dictionary<int, Walk> _playerWalk = new Dictionary<int, Walk>();
        private readonly List<SpriteRenderer> _zombies = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _glows = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _players = new List<SpriteRenderer>();
        private SpriteRenderer _silhouette;                          // you, drawn faintly over everything: seen behind a building (D-029)
        private readonly List<SpriteRenderer> _piles = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _containers = new List<SpriteRenderer>();
        private int _used;

        private static readonly ItemPile[] NoPiles = new ItemPile[0];
        private static readonly Int2[] NoCells = new Int2[0];

        /// <summary>Level being shown (D-022): zombies and players of that level; piles and containers only exist on the street.</summary>
        public int ViewLevel { get; set; }

        /// <summary>The world, to hide what stands under a closed roof (D-024).</summary>
        public PrototypeWorldView Roofs { get; set; }

        /// <summary>Hit flashes and blood (D-024). Optional.</summary>
        public PrototypeEffects Effects { get; set; }

        /// <summary>Light on the map: mutants glow green where it is dark.</summary>
        public PrototypeLighting Lighting { get; set; }

        /// <summary>Growls of the zombies you can hear. Optional.</summary>
        public PrototypeAudio Audio { get; set; }

        /// <summary>Cells whose container is a piece of furniture already drawn by the decoration (fridges, wardrobes…): no chest on top of them.</summary>
        public HashSet<Int2> FurnitureCells { get; set; }

        /// <summary>The characters' pivot is at their feet; the entity position is the middle of the body, a little above.</summary>
        private const float FootOffset = -0.25f;

        private struct Walk
        {
            public Float2 Last;
            public int Facing;
            public float Phase;
        }

        public PrototypeEntityView(Transform parent, Transform flat, ZombieCatalog catalog, PrototypeArt art)
        {
            _flat = flat;
            var go = new GameObject("Entities (prototype)");
            go.transform.SetParent(parent, false);
            _root = go.transform;
            _ppu = art.PixelsPerUnit;
            _catalog = catalog;
            _zombieFrames = new Sprite[catalog.Count][][];
            for (byte t = 0; t < catalog.Count; t++)
            {
                _zombieFrames[t] = Frames(art, "zombie_" + catalog[t].id);
            }
            // Several looks for the commonest zombie, so the horde reads as the city's people (spritegen: zombie_walker_v1..v9).
            if (catalog.TryGetIndex("walker", out byte walker))
            {
                _walkerType = walker;
                _walkerVariants.Add(_zombieFrames[walker]);
                for (int v = 1; v <= 9 && art.TryGet($"zombie_walker_v{v}_down_0", out _); v++)
                {
                    _walkerVariants.Add(Frames(art, $"zombie_walker_v{v}"));
                }
            }
            _playerFrames = Frames(art, "player");
            _otherFrames = Frames(art, "player_other");
            _downed = art.Get("player_downed");
            _pile = art.Get("pile");
            _container = art.Get("block_chest");
            _cursorSprite = art.Get("ui_cursor");
        }

        /// <summary>Frame around the tile under the mouse: green if your hands reach it, red if not.</summary>
        public void SetCursor(bool visible, Int2 tile, bool inReach)
        {
            if (_cursor == null)
            {
                var go = new GameObject("Cursor");
                go.transform.SetParent(_flat, false);
                _cursor = go.AddComponent<SpriteRenderer>();
                _cursor.sprite = _cursorSprite;
                _cursor.sortingOrder = short.MaxValue;
                PrototypeMaterials.Apply(_cursor);
            }
            if (_cursor.gameObject.activeSelf != visible)
            {
                _cursor.gameObject.SetActive(visible);
            }
            if (visible)
            {
                _cursor.transform.localPosition = new Vector3(tile.X + 0.5f, tile.Y + 0.5f, 0f);
                _cursor.color = inReach ? PrototypePalette.InReach : PrototypePalette.OutOfReach;
            }
        }

        /// <summary>Host mode: reads the authoritative simulation. Returns how many zombies were drawn.</summary>
        public int Draw(GameSession session, int me, float ambient)
        {
            float light = 1f;                                    // the light overlay does the darkening (D-024)
            ZombieSimulation zombies = session.Zombies;
            _used = 0;
            for (int i = 0; i < zombies.Capacity; i++)
            {
                if (zombies.IsAlive(i) && zombies.GetLevel(i) == ViewLevel)
                {
                    DrawZombie(i, zombies.GetPosition(i), zombies.GetTypeIndex(i), zombies.GetState(i), light, zombies.GetHealth(i));
                }
            }
            int drawn = _used;
            Trim(_zombies, _used);
            Trim(_glows, _used);
            Effects?.EndFrame(session.Players.Count > me && me >= 0 ? session.Players[me].Position : default);
            DrawPlayers(session.Players, me, light);
            DrawPiles(ViewLevel == 0 ? session.GroundItems.Piles : NoPiles, light);
            DrawContainers(ViewLevel == 0 && session.Containers != null ? session.Containers.Positions : NoCells, light);
            return drawn;
        }

        /// <summary>Loopback/client mode: reads only what arrived through the protocol. Returns zombies drawn.</summary>
        public int Draw(ClientMatchState client, double renderTime, float ambient)
        {
            float light = 1f;                                    // the light overlay does the darkening (D-024)
            _used = 0;
            foreach (ushort id in client.Zombies.Ids)
            {
                if (client.Zombies.TrySample(id, renderTime, out ZombieSnapshotEntry z) && z.Type < _catalog.Count)
                {
                    DrawZombie(id, z.Position, z.Type, z.State, light, -1);
                }
            }
            int drawn = _used;
            Trim(_zombies, _used);
            Trim(_glows, _used);
            Effects?.EndFrame(client.Players.Count > client.MyPlayer && client.MyPlayer >= 0 ? client.Players[client.MyPlayer].Position : default);
            DrawPlayers(client.Players, client.MyPlayer, light);
            DrawPiles(ViewLevel == 0 ? client.Piles : NoPiles, light);
            DrawContainers(ViewLevel == 0 ? client.ContainerCells : NoCells, light);
            return drawn;
        }

        private void DrawZombie(int key, Float2 position, byte type, ZombieState state, float light, int health)
        {
            if (Roofs != null && Roofs.IsHiddenByRoof(position))
            {
                return;                                              // inside a closed house: you cannot see it
            }
            Effects?.Observe(key, position, health);
            Audio?.ZombieNear(key, position, state != ZombieState.Idle);       // you still hear them
            if (Lighting != null && !Lighting.CanSeeSpot(position))
            {
                return;                                              // out of your sight (D-031): behind a wall, around a corner
            }
            Animate(_zombieWalk, key, position, out int facing, out int frame);
            // Mutants glow at night (docs/lore.md): never darker than half their colour.
            Color c = Color.Lerp(Dim(Color.white, light), Color.white, 0.5f);
            if (state == ZombieState.Stunned)
            {
                c = new Color(0.6f, 0.8f, 1f, 1f);
            }
            else if (state == ZombieState.Screaming || state == ZombieState.Charging)
            {
                c = Color.Lerp(c, Color.red, Mathf.PingPong(Time.time * 6f, 1f));
            }
            Sprite[][] frames = type == _walkerType && _walkerVariants.Count > 1 ? _walkerVariants[Math.Abs(key) % _walkerVariants.Count] : _zombieFrames[type];
            Sprite sprite = frames[ArtView(facing, out bool flipZ)][frame];
            Place(_zombies, _used, position, sprite, flipZ, c, ZombieBias, FootOffset);

            // The same sprite again above the light: a green glow that grows with the darkness (the mutants shine, docs/lore.md), white when hit.
            float dark = Lighting != null && Lighting.Available ? 1f - Lighting.LightAt(position) : 0f;
            float flash = Effects != null ? Effects.Flash(key) : 0f;
            float alpha = Mathf.Max(Mathf.Clamp01(dark * 0.75f), flash * 0.85f);
            if (alpha > 0.02f)
            {
                Color glow = Color.Lerp(new Color(0.42f, 1f, 0.38f, 1f), Color.white, flash);
                glow.a = alpha;
                Place(_glows, _used, position, sprite, flipZ, glow, ZombieBias, FootOffset, short.MaxValue - 1);
            }
            else if (_glows.Count > _used && _glows[_used].gameObject.activeSelf)
            {
                _glows[_used].gameObject.SetActive(false);
            }
            _used++;
        }

        private void DrawPlayers(IReadOnlyList<PlayerState> players, int me, float light)
        {
            int used = 0;
            Color c = Dim(Color.white, Mathf.Max(light, 0.75f)); // players carry a torch: never too dark
            for (int p = 0; p < players.Count; p++)
            {
                PlayerState s = players[p];
                if (s.IsDead || s.Level != ViewLevel || (p != me && Roofs != null && Roofs.IsHiddenByRoof(s.Position)))
                {
                    continue;
                }
                Animate(_playerWalk, p, s.Position, out int facing, out int frame);
                int view = ArtView(facing, out bool flipP);
                Sprite sprite = s.IsDowned ? _downed : (p == me ? _playerFrames : _otherFrames)[view][frame];
                Place(_players, used++, s.Position, sprite, flipP && !s.IsDowned, c, PlayerBias, s.IsDowned ? 0f : FootOffset);
                if (p == me)
                {
                    Silhouette(s.Position, sprite, flipP && !s.IsDowned, s.IsDowned ? 0f : FootOffset);
                }
            }
            Trim(_players, used);
        }

        /// <summary>
        /// A faint copy of your character above walls and roofs: when a building stands between you and the camera you still see where you are,
        /// without the building opening up (no spoiling what is inside, D-029). Over your own sprite it barely shows.
        /// </summary>
        private void Silhouette(Float2 position, Sprite sprite, bool flip, float footOffset)
        {
            if (_silhouette == null)
            {
                var go = new GameObject("Your silhouette");
                go.transform.SetParent(_root, false);
                _silhouette = go.AddComponent<SpriteRenderer>();
                PrototypeMaterials.Apply(_silhouette);
                _silhouette.sortingOrder = short.MaxValue - 6;
            }
            Vector3 p = PrototypeIso.ToScreen(position);
            _silhouette.transform.localPosition = new Vector3(Snap(p.x), Snap(p.y), 0f);
            _silhouette.sprite = sprite;
            _silhouette.flipX = flip;
            _silhouette.color = new Color(0.75f, 0.9f, 1f, 0.38f);
        }

        private void DrawPiles(IReadOnlyList<ItemPile> piles, float light)
        {
            int used = 0;
            foreach (ItemPile pile in piles)
            {
                if (Roofs != null && Roofs.IsHiddenByRoof(pile.Position))
                {
                    continue;
                }
                Place(_piles, used++, pile.Position, _pile, false, Dim(Color.white, light), PileBias);
            }
            Trim(_piles, used);
        }

        private void DrawContainers(IEnumerable<Int2> cells, float light)
        {
            int used = 0;
            if (cells != null)
            {
                foreach (Int2 cell in cells)
                {
                    if (FurnitureCells != null && FurnitureCells.Contains(cell))
                    {
                        continue;
                    }
                    if (Roofs != null && Roofs.IsHiddenByRoof(new Float2(cell.X + 0.5f, cell.Y + 0.5f)))
                    {
                        continue;
                    }
                    Place(_containers, used++, new Float2(cell.X + 0.5f, cell.Y + 0.5f), _container, false, Dim(Color.white, light), ContainerBias);
                }
            }
            Trim(_containers, used);
        }

        private static void Animate(Dictionary<int, Walk> walks, int key, Float2 position, out int facing, out int frame)
        {
            if (!walks.TryGetValue(key, out Walk w))
            {
                w = new Walk { Last = position, Facing = Down };
            }
            float dx = position.X - w.Last.X;
            float dy = position.Y - w.Last.Y;
            bool moving = dx * dx + dy * dy > 1e-6f;
            if (moving)
            {
                w.Facing = FacingOf(dx, dy);
                w.Phase += Time.deltaTime * WalkFps;
            }
            else
            {
                w.Phase = 0f;
            }
            w.Last = position;
            walks[key] = w;
            facing = w.Facing;
            frame = moving ? (int)w.Phase % 4 : 0;
        }

        private void Place(List<SpriteRenderer> pool, int index, Float2 position, Sprite sprite, bool flip, Color color, int bias, float footOffset = 0f, int order = int.MinValue)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject("sprite");
                go.transform.SetParent(_root, false);
                SpriteRenderer r = go.AddComponent<SpriteRenderer>();
                r.spriteSortPoint = SpriteSortPoint.Pivot;               // their feet decide who is in front
                PrototypeMaterials.Apply(r);
                pool.Add(r);
            }
            SpriteRenderer sr = pool[index];
            if (!sr.gameObject.activeSelf)
            {
                sr.gameObject.SetActive(true);
            }
            // Projected to the screen (D-025), feet on the point, snapped to the pixel grid so sprites never shimmer.
            Vector3 p = PrototypeIso.ToScreen(position);
            sr.transform.localPosition = new Vector3(Snap(p.x), Snap(p.y), 0f);
            sr.sprite = sprite;
            sr.flipX = flip;
            sr.color = color;
            sr.sortingOrder = order != int.MinValue ? order : PrototypeIso.UprightOrder;   // sorted by screen height with the walls
        }

        private float Snap(float v) => Mathf.Round(v * _ppu) / _ppu;

        private static Sprite[][] Frames(PrototypeArt art, string prefix)
        {
            var frames = new Sprite[FacingNames.Length][];
            for (int f = 0; f < FacingNames.Length; f++)
            {
                frames[f] = new Sprite[4];
                for (int i = 0; i < 4; i++)
                {
                    // La Mole (brute) is drawn natively with four views: its diagonals use the side and the back.
                    string view = FacingNames[f];
                    if (!art.TryGet($"{prefix}_{view}_{i}", out Sprite sprite))
                    {
                        sprite = art.Get($"{prefix}_{(view == "dl" ? "left" : "up")}_{i}");
                    }
                    frames[f][i] = sprite;
                }
            }
            return frames;
        }

        // Darken without touching alpha (Color * float would make it transparent).
        private static Color Dim(Color c, float light) => new Color(c.r * light, c.g * light, c.b * light, 1f);

        private static void Trim(List<SpriteRenderer> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
            {
                if (pool[i].gameObject.activeSelf)
                {
                    pool[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
