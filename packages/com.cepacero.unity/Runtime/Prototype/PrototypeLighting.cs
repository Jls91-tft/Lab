using System;
using System.Collections.Generic;
using CepaCero.Lighting;
using CepaCero.Map;
using CepaCero.Util;
using CepaCero.World;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Real light for the view (D-024). Every frame it computes, for the window the camera shows, how much light reaches each tile
    /// (<see cref="LightGrid"/>: lamps, fires, houses with their lights on, the player's torch; walls cast shadows), turns it into
    /// a small texture and multiplies it over the world with <c>CepaCero/LightMultiply</c>. By day the ambient is white and the
    /// overlay changes nothing; at night only what the lights reach is seen, and underground there is only your own light.
    /// </summary>
    public sealed class PrototypeLighting
    {
        private const string ShaderResource = "Cepa/LightMultiply";
        private const int Margin = 2;

        // Ambient colours (linear): a cold dark blue at night, white by day, warmer at dusk and dawn; the caves are nearly black.
        private static readonly Vector3 NightAmbient = new Vector3(0.014f, 0.022f, 0.050f);
        private static readonly Vector3 CaveAmbient = new Vector3(0.006f, 0.007f, 0.012f);

        private struct StaticLight
        {
            public LightSource Source;
            public int Kind;                  // 0 lamp, 1 fire, 2 house
            public float Seed;
        }

        private readonly BlockWorld _blocks;
        private readonly LightGrid _grid = new LightGrid();
        private readonly List<StaticLight> _static = new List<StaticLight>();
        private readonly Transform _root;
        private SpriteRenderer _overlay;
        private Texture2D _texture;
        private Sprite _sprite;
        private byte[] _buffer = new byte[0];
        private Vector3 _ambient = Vector3.one;

        /// <summary>The torch in your hand (L): a cone toward the cursor. Off, you only keep the small glow around you.</summary>
        public bool FlashlightOn { get; set; } = true;

        /// <summary>False when the shader is missing (an old project): the game then runs without light instead of failing.</summary>
        public bool Available => _overlay != null;

        public PrototypeLighting(Transform parent, BlockWorld blocks)
        {
            _blocks = blocks;
            _root = new GameObject("Light (prototype)").transform;
            _root.SetParent(parent, false);
            Shader shader = Resources.Load<Shader>(ShaderResource);
            if (shader == null)
            {
                Debug.LogWarning("Cepa Cero: no encuentro el shader Resources/Cepa/LightMultiply; el juego corre sin iluminación.");
                return;
            }
            var go = new GameObject("Light overlay");
            go.transform.SetParent(_root, false);
            _overlay = go.AddComponent<SpriteRenderer>();
            _overlay.sharedMaterial = new Material(shader) { name = "CepaCero light multiply" };
            _overlay.sortingOrder = short.MaxValue - 2;                       // over the world, under the ghosts and the cursor
        }

        /// <summary>Debug bridge: set a path and the next frame also saves the light map there as a PNG.</summary>
        public static string DumpPath;

        public int StaticLightCount => _static.Count;

        /// <summary>Street lamps and fires among the decoration give light at night.</summary>
        public void AddFromDecor(IReadOnlyList<DecorItem> items)
        {
            foreach (DecorItem item in items)
            {
                if (item.Sprite == "decor_lamp")
                {
                    _static.Add(new StaticLight
                    {
                        Source = LightSource.Omni(item.X + 0.5f, item.Y + 0.5f, 6.0f, 1f, 0.86f, 0.58f, 0.8f),
                        Kind = 0,
                        Seed = item.X * 0.37f + item.Y * 0.11f,
                    });
                }
                else if (item.Sprite.StartsWith("decor_barrel_fire"))
                {
                    _static.Add(new StaticLight
                    {
                        Source = LightSource.Omni(item.X + 0.5f, item.Y + 0.5f, 5.0f, 1f, 0.52f, 0.20f, 0.8f),
                        Kind = 1,
                        Seed = item.X * 0.61f + item.Y * 0.29f,
                    });
                }
            }
        }

        /// <summary>Some houses have their lights on at night: a warm lamp in the middle of the interior of about two in five.</summary>
        public void AddHouses(IReadOnlyList<BuildingInfo> buildings, GroundLayer ground, int seed)
        {
            if (buildings == null)
            {
                return;
            }
            var rng = new System.Random(seed ^ 0x5EED);
            foreach (BuildingInfo b in buildings)
            {
                if (rng.NextDouble() > 0.4)
                {
                    continue;
                }
                int cx = (b.Min.X + b.Max.X) / 2;
                int cy = (b.Min.Y + b.Max.Y) / 2;
                if (!FindInside(ground, cx, cy, out Int2 at))
                {
                    continue;
                }
                _static.Add(new StaticLight
                {
                    Source = LightSource.Omni(at.X + 0.5f, at.Y + 0.5f, 6.0f, 1f, 0.88f, 0.60f, 0.7f),
                    Kind = 2,
                    Seed = at.X * 0.53f + at.Y * 0.17f,
                });
            }
        }

        /// <summary>
        /// Call every frame after the camera has been placed. <paramref name="clockAmbient"/> is the day/night curve (1 day, 0.08 night);
        /// <paramref name="aim"/> is the direction of the cursor from the player (unit, or zero).
        /// </summary>
        public void Update(Camera camera, int level, float clockAmbient, Float2 player, Float2 aim, float time)
        {
            if (_overlay == null)
            {
                return;
            }
            float halfH = camera.orthographicSize;
            float halfW = halfH * camera.aspect;
            Vector3 c = camera.transform.position;
            int x0 = (int)Math.Floor(c.x - halfW) - Margin;
            int y0 = (int)Math.Floor(c.y - halfH) - Margin;
            int w = (int)Math.Ceiling(halfW * 2f) + Margin * 2 + 1;
            int h = (int)Math.Ceiling(halfH * 2f) + Margin * 2 + 1;
            Resize(w, h);
            _grid.Begin(x0, y0, w, h);

            float dark = Mathf.Clamp01((1f - clockAmbient) * 1.25f);       // 0 by day, 1 at night
            if (level < 0)
            {
                dark = 1f;
            }
            _ambient = level < 0 ? CaveAmbient : AmbientFor(clockAmbient);

            if (level == 0 && dark > 0.02f)
            {
                AddStatic(x0, y0, w, h, dark, time);
            }
            AddPlayer(level, player, aim, dark);

            _grid.Smooth();
            _grid.Resolve(_ambient.x, _ambient.y, _ambient.z, _buffer);
            _texture.LoadRawTextureData(_buffer);
            _texture.Apply(false);
            _overlay.transform.position = new Vector3(x0, y0, 0f);
            if (DumpPath != null)
            {
                System.IO.File.WriteAllBytes(DumpPath, _texture.EncodeToPNG());
                DumpPath = null;
            }
        }

        /// <summary>The light on a spot, 0..1: for things that glow in the dark or hide in it.</summary>
        public float LightAt(Float2 position)
        {
            Int2 t = position.Tile;
            float lit = _grid.Brightness(t.X, t.Y);
            return Mathf.Clamp01(Math.Max(_ambient.x, Math.Max(_ambient.y, _ambient.z)) + lit);
        }

        private void AddStatic(int x0, int y0, int w, int h, float dark, float time)
        {
            foreach (StaticLight s in _static)
            {
                LightSource l = s.Source;
                if (l.X + l.Radius < x0 || l.X - l.Radius > x0 + w || l.Y + l.Radius < y0 || l.Y - l.Radius > y0 + h)
                {
                    continue;
                }
                float k = s.Kind == 1 ? 0.35f + 0.65f * dark : dark;       // fires burn all day, a little visible; lamps and houses come on at night
                if (s.Kind == 1)
                {
                    k *= 0.88f + 0.12f * (float)Math.Sin(time * 9f + s.Seed) * (float)Math.Sin(time * 5.3f + s.Seed * 1.7f);
                }
                l.Intensity *= k;
                _grid.Add(_blocks, 0, l);
            }
        }

        private void AddPlayer(int level, Float2 player, Float2 aim, float dark)
        {
            // A warm glow around you (a lamp on your belt), stronger the darker it is; underground it is all you have.
            var glow = LightSource.Omni(player.X, player.Y, 3.5f, 1f, 0.92f, 0.78f, 0.3f + 0.3f * dark);
            _grid.Add(_blocks, level, glow);
            if (FlashlightOn && dark > 0.05f && aim.LengthSquared > 0.01f)
            {
                Float2 d = aim.Normalized;
                _grid.Add(_blocks, level, LightSource.Cone(player.X, player.Y, 11f, d.X, d.Y, 0.86f, 1f, 0.97f, 0.88f, 0.95f * dark));
            }
        }

        /// <summary>Night blue → white, a touch of orange at dusk and dawn.</summary>
        private static Vector3 AmbientFor(float clockAmbient)
        {
            float t = Mathf.Clamp01((clockAmbient - 0.08f) / 0.92f);
            Vector3 a = NightAmbient + (Vector3.one - NightAmbient) * t;
            float warm = 4f * t * (1f - t);                                  // 0 at both ends, 1 half way
            return new Vector3(a.x * (1f + 0.22f * warm), a.y * (1f + 0.02f * warm), a.z * (1f - 0.28f * warm));
        }

        private void Resize(int w, int h)
        {
            if (_texture != null && _texture.width == w && _texture.height == h)
            {
                return;
            }
            if (_texture != null)
            {
                UnityEngine.Object.Destroy(_texture);
                UnityEngine.Object.Destroy(_sprite);
            }
            // Linear, bilinear: one texel per tile, softened by the GPU into a smooth light.
            _texture = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Light map",
            };
            _sprite = Sprite.Create(_texture, new Rect(0, 0, w, h), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
            _overlay.sprite = _sprite;
            _buffer = new byte[w * h * 4];
        }

        private bool FindInside(GroundLayer ground, int cx, int cy, out Int2 at)
        {
            for (int r = 0; r <= 6; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cx + dx;
                        int y = cy + dy;
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && _blocks.InBounds(x, y)
                            && ground.Get(x, y) == GroundType.Floor && _blocks.GetBlock(x, y).IsAir)
                        {
                            at = new Int2(x, y);
                            return true;
                        }
                    }
                }
            }
            at = default;
            return false;
        }
    }
}
