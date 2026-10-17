using System;
using System.Collections.Generic;
using CepaCero.Util;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Game feel (D-024), all in the view: a white flash when a zombie is hit, blood that sprays and stays on the ground for a
    /// while, and camera shake when you are hurt. It only watches what the simulation shows (a zombie losing health, a
    /// zombie that is gone), so it works the same for the host and for a client that only has snapshots.
    /// </summary>
    public sealed class PrototypeEffects
    {
        private const int MaxParticles = 256;
        private const int MaxStains = 96;
        private const float FlashSeconds = 0.12f;
        private const float StainSeconds = 90f;

        private struct Particle
        {
            public Float2 Position;
            public Float2 Velocity;
            public float Age;
            public float Life;
            public Color Color;
        }

        private struct Seen
        {
            public Float2 Position;
            public int Health;
            public int Frame;
        }

        private readonly Transform _root;
        private readonly Sprite _dot;
        private readonly Sprite[] _blood = new Sprite[3];
        private readonly Particle[] _particles = new Particle[MaxParticles];
        private readonly SpriteRenderer[] _renderers = new SpriteRenderer[MaxParticles];
        private int _count;
        private readonly SpriteRenderer[] _stains = new SpriteRenderer[MaxStains];
        private readonly float[] _stainBorn = new float[MaxStains];
        private int _nextStain;
        private readonly Dictionary<int, Seen> _seen = new Dictionary<int, Seen>();
        private readonly Dictionary<int, float> _flash = new Dictionary<int, float>();
        private readonly List<int> _gone = new List<int>();
        private readonly System.Random _rng = new System.Random(1234);
        private int _frame;
        private float _shake;
        private readonly int _ppu;

        public PrototypeEffects(Transform parent, PrototypeArt art)
        {
            var go = new GameObject("Effects (prototype)");
            go.transform.SetParent(parent, false);
            _root = go.transform;
            _ppu = art.PixelsPerUnit;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false);
            _dot = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            for (int i = 0; i < _blood.Length; i++)
            {
                art.TryGet("decal_blood_" + i, out _blood[i]);
            }
        }

        /// <summary>Call for every zombie drawn this frame. <paramref name="health"/> is -1 when unknown (a client has no health).</summary>
        public void Observe(int key, Float2 position, int health)
        {
            if (_seen.TryGetValue(key, out Seen s) && health >= 0 && s.Health >= 0 && health < s.Health)
            {
                _flash[key] = Time.time + FlashSeconds;
                Burst(position, 5 + (s.Health - health) / 4, 2.6f, new Color(0.55f, 0.04f, 0.05f, 1f));
                if (_rng.NextDouble() < 0.5)
                {
                    Stain(position, 0.6f);
                }
            }
            _seen[key] = new Seen { Position = position, Health = health, Frame = _frame };
        }

        /// <summary>0..1: how much of the white hit flash a zombie still shows.</summary>
        public float Flash(int key) => _flash.TryGetValue(key, out float until) && until > Time.time ? (until - Time.time) / FlashSeconds : 0f;

        /// <summary>After all zombies of the frame were observed: those missing died (or left the view; far ones are ignored).</summary>
        public void EndFrame(Float2 viewCentre)
        {
            _gone.Clear();
            foreach (KeyValuePair<int, Seen> kv in _seen)
            {
                if (kv.Value.Frame != _frame)
                {
                    _gone.Add(kv.Key);
                }
            }
            foreach (int key in _gone)
            {
                Seen s = _seen[key];
                _seen.Remove(key);
                _flash.Remove(key);
                float dx = s.Position.X - viewCentre.X;
                float dy = s.Position.Y - viewCentre.Y;
                if (dx * dx + dy * dy < 14f * 14f)
                {
                    Burst(s.Position, 14, 3.4f, new Color(0.5f, 0.03f, 0.04f, 1f));
                    Stain(s.Position, 1f);
                }
            }
            _frame++;
        }

        /// <summary>Shakes the camera (decays by itself): 0.1 is a tap, 0.3 a heavy blow.</summary>
        public void Shake(float amount) => _shake = Math.Max(_shake, amount);

        /// <summary>Camera offset for this frame, in world units, snapped to the art pixel grid.</summary>
        public Vector3 ShakeOffset()
        {
            if (_shake < 0.003f)
            {
                return new Vector3(0f, 0f, 0f);
            }
            float x = ((float)_rng.NextDouble() - 0.5f) * 2f * _shake;
            float y = ((float)_rng.NextDouble() - 0.5f) * 2f * _shake;
            return new Vector3(Mathf.Round(x * _ppu) / _ppu, Mathf.Round(y * _ppu) / _ppu, 0f);
        }

        public void Update(float dt)
        {
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 0.9f);
            for (int i = _count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    _renderers[i].gameObject.SetActive(false);
                    SwapOut(i);
                    continue;
                }
                float drag = Mathf.Max(0f, 1f - 5f * dt);
                p.Velocity = new Float2(p.Velocity.X * drag, p.Velocity.Y * drag);
                p.Position = new Float2(p.Position.X + p.Velocity.X * dt, p.Position.Y + p.Velocity.Y * dt);
                _particles[i] = p;
                SpriteRenderer r = _renderers[i];
                r.transform.localPosition = new Vector3(Mathf.Round(p.Position.X * _ppu) / _ppu, Mathf.Round(p.Position.Y * _ppu) / _ppu, 0f);
                Color c = p.Color;
                c.a = 1f - p.Age / p.Life;
                r.color = c;
            }
            float now = Time.time;
            for (int i = 0; i < MaxStains; i++)
            {
                SpriteRenderer s = _stains[i];
                if (s != null && s.gameObject.activeSelf)
                {
                    float age = now - _stainBorn[i];
                    if (age > StainSeconds)
                    {
                        s.gameObject.SetActive(false);
                    }
                    else if (age > StainSeconds - 20f)
                    {
                        s.color = new Color(1f, 1f, 1f, (StainSeconds - age) / 20f * 0.85f);
                    }
                }
            }
        }

        private void SwapOut(int i)
        {
            _count--;
            if (i != _count)
            {
                _particles[i] = _particles[_count];
                SpriteRenderer t = _renderers[i];
                _renderers[i] = _renderers[_count];
                _renderers[_count] = t;
            }
        }

        private void Burst(Float2 at, int n, float speed, Color color)
        {
            for (int k = 0; k < n && _count < MaxParticles; k++)
            {
                double a = _rng.NextDouble() * Math.PI * 2.0;
                float v = speed * (0.3f + (float)_rng.NextDouble() * 0.7f);
                SpriteRenderer r = _renderers[_count];
                if (r == null)
                {
                    var go = new GameObject("particle");
                    go.transform.SetParent(_root, false);
                    r = go.AddComponent<SpriteRenderer>();
                    r.sprite = _dot;
                    PrototypeMaterials.Apply(r);
                    _renderers[_count] = r;
                }
                float size = 1f / 16f * (1f + (float)_rng.NextDouble() * 1.5f);
                r.gameObject.SetActive(true);
                r.transform.localScale = new Vector3(size, size, 1f);
                r.sortingOrder = short.MaxValue - 3;
                _particles[_count] = new Particle
                {
                    Position = at,
                    Velocity = new Float2((float)Math.Cos(a) * v, (float)Math.Sin(a) * v),
                    Life = 0.35f + (float)_rng.NextDouble() * 0.35f,
                    Color = color,
                };
                _count++;
            }
        }

        private void Stain(Float2 at, float scale)
        {
            Sprite sprite = _blood[_rng.Next(_blood.Length)];
            if (sprite == null)
            {
                return;
            }
            SpriteRenderer s = _stains[_nextStain];
            if (s == null)
            {
                var go = new GameObject("blood");
                go.transform.SetParent(_root, false);
                s = go.AddComponent<SpriteRenderer>();
                PrototypeMaterials.Apply(s);
                s.sortingOrder = PrototypeDecorView.FlatOrder;
                _stains[_nextStain] = s;
            }
            s.gameObject.SetActive(true);
            s.sprite = sprite;
            s.color = new Color(1f, 1f, 1f, 0.85f);
            s.flipX = _rng.Next(2) == 0;
            s.transform.localScale = new Vector3(scale, scale, 1f);
            s.transform.localPosition = new Vector3(Mathf.Round(at.X * _ppu) / _ppu, Mathf.Round(at.Y * _ppu) / _ppu, 0f);
            _stainBorn[_nextStain] = Time.time;
            _nextStain = (_nextStain + 1) % MaxStains;
        }
    }
}
