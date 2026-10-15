using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Player;
using CepaCero.Util;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Pooled coloured squares for players, zombies, piles on the floor and map
    /// containers. Reads the host simulation directly (the prototype is the host).
    /// </summary>
    public sealed class PrototypeEntityView
    {
        private readonly Transform _root;
        private readonly Sprite _square;
        private readonly List<SpriteRenderer> _zombies = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _players = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _piles = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _containers = new List<SpriteRenderer>();
        private Color[] _zombieColors;

        public PrototypeEntityView(Transform parent)
        {
            var go = new GameObject("Entities (prototype)");
            go.transform.SetParent(parent, false);
            _root = go.transform;
            Texture2D white = Texture2D.whiteTexture;
            _square = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
        }

        public void Draw(GameSession session, float ambient)
        {
            float light = Mathf.Lerp(0.45f, 1f, ambient);
            if (_zombieColors == null)
            {
                _zombieColors = new Color[session.Catalog.Count];
                for (byte t = 0; t < session.Catalog.Count; t++)
                {
                    _zombieColors[t] = PrototypePalette.Zombie(session.Catalog[t].id);
                }
            }

            int used = 0;
            ZombieSimulation zombies = session.Zombies;
            for (int i = 0; i < zombies.Capacity; i++)
            {
                if (!zombies.IsAlive(i))
                {
                    continue;
                }
                byte type = zombies.GetTypeIndex(i);
                float size = Mathf.Max(0.4f, session.Catalog[type].size_tiles * 0.8f);
                Color c = _zombieColors[type];
                if (zombies.GetState(i) == ZombieState.Stunned)
                {
                    c = Color.white;
                }
                else if (zombies.GetState(i) == ZombieState.Screaming || zombies.GetState(i) == ZombieState.Charging)
                {
                    c = Color.Lerp(c, Color.red, Mathf.PingPong(Time.time * 6f, 1f));
                }
                // Mutants glow at night (docs/lore.md): never darker than half their colour.
                Place(_zombies, used++, zombies.GetPosition(i), size, Color.Lerp(Dim(c, light), c, 0.5f), 20);
            }
            Trim(_zombies, used);

            used = 0;
            for (int p = 0; p < session.Players.Count; p++)
            {
                PlayerState s = session.Players[p];
                if (s.IsDead)
                {
                    continue;
                }
                Color c = s.IsDowned ? PrototypePalette.Downed : p == 0 ? PrototypePalette.Player : PrototypePalette.OtherPlayer;
                Place(_players, used++, s.Position, 0.7f, c, 30);
            }
            Trim(_players, used);

            used = 0;
            foreach (ItemPile pile in session.GroundItems.Piles)
            {
                Place(_piles, used++, pile.Position, 0.35f, Dim(PrototypePalette.Pile, light), 10);
            }
            Trim(_piles, used);

            used = 0;
            if (session.Containers != null)
            {
                foreach (Int2 cell in session.Containers.Positions)
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
