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
    /// Pooled coloured squares for players, zombies, piles on the floor and
    /// containers. Draws either the host simulation directly or, in loopback
    /// mode, a client replica built only from network bytes.
    /// </summary>
    public sealed class PrototypeEntityView
    {
        private readonly Transform _root;
        private readonly Sprite _square;
        private readonly ZombieCatalog _catalog;
        private readonly Color[] _zombieColors;
        private readonly List<SpriteRenderer> _zombies = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _players = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _piles = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _containers = new List<SpriteRenderer>();
        private int _used;

        public PrototypeEntityView(Transform parent, ZombieCatalog catalog)
        {
            var go = new GameObject("Entities (prototype)");
            go.transform.SetParent(parent, false);
            _root = go.transform;
            Texture2D white = Texture2D.whiteTexture;
            _square = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
            _catalog = catalog;
            _zombieColors = new Color[catalog.Count];
            for (byte t = 0; t < catalog.Count; t++)
            {
                _zombieColors[t] = PrototypePalette.Zombie(catalog[t].id);
            }
        }

        /// <summary>Host mode: reads the authoritative simulation. Returns how many zombies were drawn.</summary>
        public int Draw(GameSession session, int me, float ambient)
        {
            float light = Mathf.Lerp(0.45f, 1f, ambient);
            ZombieSimulation zombies = session.Zombies;
            _used = 0;
            for (int i = 0; i < zombies.Capacity; i++)
            {
                if (zombies.IsAlive(i))
                {
                    DrawZombie(zombies.GetPosition(i), zombies.GetTypeIndex(i), zombies.GetState(i), light);
                }
            }
            int drawn = _used;
            Trim(_zombies, _used);
            DrawPlayers(session.Players, me);
            DrawPiles(session.GroundItems.Piles, light);
            DrawContainers(session.Containers != null ? session.Containers.Positions : null, light);
            return drawn;
        }

        /// <summary>Loopback/client mode: reads only what arrived through the protocol. Returns zombies drawn.</summary>
        public int Draw(ClientMatchState client, double renderTime, float ambient)
        {
            float light = Mathf.Lerp(0.45f, 1f, ambient);
            _used = 0;
            foreach (ushort id in client.Zombies.Ids)
            {
                if (client.Zombies.TrySample(id, renderTime, out ZombieSnapshotEntry z) && z.Type < _catalog.Count)
                {
                    DrawZombie(z.Position, z.Type, z.State, light);
                }
            }
            int drawn = _used;
            Trim(_zombies, _used);
            DrawPlayers(client.Players, client.MyPlayer);
            DrawPiles(client.Piles, light);
            DrawContainers(client.ContainerCells, light);
            return drawn;
        }

        private void DrawZombie(Float2 position, byte type, ZombieState state, float light)
        {
            float size = Mathf.Max(0.4f, _catalog[type].size_tiles * 0.8f);
            Color c = _zombieColors[type];
            if (state == ZombieState.Stunned)
            {
                c = Color.white;
            }
            else if (state == ZombieState.Screaming || state == ZombieState.Charging)
            {
                c = Color.Lerp(c, Color.red, Mathf.PingPong(Time.time * 6f, 1f));
            }
            // Mutants glow at night (docs/lore.md): never darker than half their colour.
            Place(_zombies, _used++, position, size, Color.Lerp(Dim(c, light), c, 0.5f), 20);
        }

        private void DrawPlayers(IReadOnlyList<PlayerState> players, int me)
        {
            int used = 0;
            for (int p = 0; p < players.Count; p++)
            {
                PlayerState s = players[p];
                if (s.IsDead)
                {
                    continue;
                }
                Color c = s.IsDowned ? PrototypePalette.Downed : p == me ? PrototypePalette.Player : PrototypePalette.OtherPlayer;
                Place(_players, used++, s.Position, 0.7f, c, 30);
            }
            Trim(_players, used);
        }

        private void DrawPiles(IReadOnlyList<ItemPile> piles, float light)
        {
            int used = 0;
            foreach (ItemPile pile in piles)
            {
                Place(_piles, used++, pile.Position, 0.35f, Dim(PrototypePalette.Pile, light), 10);
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
                    Place(_containers, used++, new Float2(cell.X + 0.5f, cell.Y + 0.5f), 0.6f, Dim(PrototypePalette.Container, light), 5);
                }
            }
            Trim(_containers, used);
        }

        private void Place(List<SpriteRenderer> pool, int index, Float2 position, float size, Color color, int order)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject("square");
                go.transform.SetParent(_root, false);
                SpriteRenderer r = go.AddComponent<SpriteRenderer>();
                r.sprite = _square;
                PrototypeMaterials.Apply(r);
                pool.Add(r);
            }
            SpriteRenderer sr = pool[index];
            if (!sr.gameObject.activeSelf)
            {
                sr.gameObject.SetActive(true);
            }
            sr.transform.localPosition = new Vector3(position.X, position.Y, 0f);
            sr.transform.localScale = new Vector3(size, size, 1f);
            sr.color = color;
            sr.sortingOrder = order;
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
