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
        private const int Down = 0;
        private const int Up = 1;
        private const int Left = 2;
        private const int Right = 3;
        private static readonly string[] FacingNames = { "down", "up", "left" };

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
        private readonly int _ppu;
        private readonly ZombieCatalog _catalog;
        private readonly Sprite[][][] _zombieFrames;
        private readonly Sprite[][] _playerFrames;
        private readonly Sprite[][] _otherFrames;
        private readonly Sprite _downed;
        private readonly Sprite _pile;
        private readonly Sprite _container;
        private readonly Sprite _cursorSprite;
        private SpriteRenderer _cursor;
        private readonly Dictionary<int, Walk> _zombieWalk = new Dictionary<int, Walk>();
        private readonly Dictionary<int, Walk> _playerWalk = new Dictionary<int, Walk>();
        private readonly List<SpriteRenderer> _zombies = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _players = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _piles = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _containers = new List<SpriteRenderer>();
        private int _used;

        private static readonly ItemPile[] NoPiles = new ItemPile[0];
        private static readonly Int2[] NoCells = new Int2[0];

        /// <summary>Level being shown (D-022): the street shows zombies, piles and containers; other levels only the players on them.</summary>
        public int ViewLevel { get; set; }

        private struct Walk
        {
            public Float2 Last;
            public int Facing;
            public float Phase;
        }

        public PrototypeEntityView(Transform parent, ZombieCatalog catalog, PrototypeArt art)
        {
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
                go.transform.SetParent(_root, false);
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
            float light = Mathf.Lerp(0.45f, 1f, ambient);
            ZombieSimulation zombies = session.Zombies;
            _used = 0;
            for (int i = 0; ViewLevel == 0 && i < zombies.Capacity; i++)
            {
                if (zombies.IsAlive(i))
                {
                    DrawZombie(i, zombies.GetPosition(i), zombies.GetTypeIndex(i), zombies.GetState(i), light);
                }
            }
            int drawn = _used;
            Trim(_zombies, _used);
            DrawPlayers(session.Players, me, light);
            DrawPiles(ViewLevel == 0 ? session.GroundItems.Piles : NoPiles, light);
            DrawContainers(ViewLevel == 0 && session.Containers != null ? session.Containers.Positions : NoCells, light);
            return drawn;
        }

        /// <summary>Loopback/client mode: reads only what arrived through the protocol. Returns zombies drawn.</summary>
        public int Draw(ClientMatchState client, double renderTime, float ambient)
        {
            float light = Mathf.Lerp(0.45f, 1f, ambient);
            _used = 0;
            foreach (ushort id in ViewLevel == 0 ? client.Zombies.Ids : new ushort[0])
            {
                if (client.Zombies.TrySample(id, renderTime, out ZombieSnapshotEntry z) && z.Type < _catalog.Count)
                {
                    DrawZombie(id, z.Position, z.Type, z.State, light);
                }
            }
            int drawn = _used;
            Trim(_zombies, _used);
            DrawPlayers(client.Players, client.MyPlayer, light);
            DrawPiles(ViewLevel == 0 ? client.Piles : NoPiles, light);
            DrawContainers(ViewLevel == 0 ? client.ContainerCells : NoCells, light);
            return drawn;
        }

        private void DrawZombie(int key, Float2 position, byte type, ZombieState state, float light)
        {
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
            Sprite[][] frames = _zombieFrames[type];
            Place(_zombies, _used++, position, frames[Math.Min(facing, Left)][frame], facing == Right, c, ZombieBias);
        }

        private void DrawPlayers(IReadOnlyList<PlayerState> players, int me, float light)
        {
            int used = 0;
            Color c = Dim(Color.white, Mathf.Max(light, 0.75f)); // players carry a torch: never too dark
            for (int p = 0; p < players.Count; p++)
            {
                PlayerState s = players[p];
                if (s.IsDead || s.Level != ViewLevel)
                {
                    continue;
                }
                Animate(_playerWalk, p, s.Position, out int facing, out int frame);
                Sprite sprite = s.IsDowned ? _downed : (p == me ? _playerFrames : _otherFrames)[Math.Min(facing, Left)][frame];
                Place(_players, used++, s.Position, sprite, facing == Right && !s.IsDowned, c, PlayerBias);
            }
            Trim(_players, used);
        }

        private void DrawPiles(IReadOnlyList<ItemPile> piles, float light)
        {
            int used = 0;
            foreach (ItemPile pile in piles)
            {
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
                w.Facing = Math.Abs(dx) > Math.Abs(dy) ? (dx < 0f ? Left : Right) : (dy > 0f ? Up : Down);
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

        private void Place(List<SpriteRenderer> pool, int index, Float2 position, Sprite sprite, bool flip, Color color, int bias)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject("sprite");
                go.transform.SetParent(_root, false);
                SpriteRenderer r = go.AddComponent<SpriteRenderer>();
                PrototypeMaterials.Apply(r);
                pool.Add(r);
            }
            SpriteRenderer sr = pool[index];
            if (!sr.gameObject.activeSelf)
            {
                sr.gameObject.SetActive(true);
            }
            // Snap to the pixel grid so sprites never shimmer between screen pixels.
            sr.transform.localPosition = new Vector3(Snap(position.X), Snap(position.Y), 0f);
            sr.sprite = sprite;
            sr.flipX = flip;
            sr.color = color;
            sr.sortingOrder = SortingOrder(position.Y, bias);
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
                    frames[f][i] = art.Get($"{prefix}_{FacingNames[f]}_{i}");
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
