using System;
using System.Collections.Generic;
using CepaCero.Util;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Sound for the prototype (D-024), made entirely in code: every clip is synthesised once at start-up from noise and simple
    /// waves (no audio files, no licences), and replaced later by real recordings if that is decided. Sounds are positional by hand:
    /// volume falls with the distance to the player and the stereo pan follows the side they come from. Fear is half sound:
    /// growls you hear before you see them, wind that swells at night, steps that quicken when you run.
    /// </summary>
    public sealed class PrototypeAudio
    {
        private const int Rate = 22050;
        private const int Voices = 14;
        private const float HearDistance = 16f;

        private readonly AudioClip[] _steps = new AudioClip[3];
        private readonly AudioClip[] _growls = new AudioClip[4];
        private readonly AudioClip _swing;
        private readonly AudioClip _hit;
        private readonly AudioClip _thud;
        private readonly AudioClip _splat;
        private readonly AudioClip _shot;
        private readonly AudioClip _hurt;
        private readonly AudioClip _horn;
        private readonly AudioSource[] _voices = new AudioSource[Voices];
        private readonly AudioSource _wind;
        private readonly Dictionary<int, float> _nextGrowl = new Dictionary<int, float>();
        private readonly System.Random _rng = new System.Random(77);
        private int _nextVoice;
        private float _nextStep;
        private Float2 _listener;

        /// <summary>Master volume 0..1.</summary>
        public float Volume { get; set; } = 0.8f;

        public PrototypeAudio(Transform parent)
        {
            var go = new GameObject("Audio (prototype)");
            go.transform.SetParent(parent, false);
            for (int i = 0; i < Voices; i++)
            {
                AudioSource v = go.AddComponent<AudioSource>();
                v.playOnAwake = false;
                v.spatialBlend = 0f;
                _voices[i] = v;
            }
            _wind = go.AddComponent<AudioSource>();
            _wind.playOnAwake = false;
            _wind.loop = true;
            _wind.spatialBlend = 0f;

            var seed = new System.Random(5);
            for (int i = 0; i < _steps.Length; i++)
            {
                _steps[i] = Clip("step" + i, Step(seed, 0.2f + 0.05f * i, 70f + 12f * i));
            }
            for (int i = 0; i < _growls.Length; i++)
            {
                _growls[i] = Clip("growl" + i, Growl(seed, 62f + 14f * i, 0.55f + 0.12f * i));
            }
            _swing = Clip("swing", Whoosh(seed));
            _hit = Clip("hit", Thump(seed, 0.16f, 150f, 55f, 0.5f));
            _thud = Clip("thud", Thump(seed, 0.2f, 95f, 45f, 0.25f));
            _splat = Clip("splat", Thump(seed, 0.3f, 80f, 35f, 0.8f));
            _shot = Clip("shot", Shot(seed));
            _hurt = Clip("hurt", Thump(seed, 0.22f, 120f, 60f, 0.35f));
            _horn = Clip("horn", Horn());
            _wind.clip = Clip("wind", Wind(seed));
        }

        // ---------------------------------------------------------------- play

        /// <summary>Call every frame: keeps the listener, the steps and the wind (louder at night, silent underground).</summary>
        public void Update(Float2 player, int level, bool isMoving, bool isRunning, float dark)
        {
            _listener = player;
            if (isMoving && Time.time >= _nextStep)
            {
                _nextStep = Time.time + (isRunning ? 0.27f : 0.4f);
                Play(_steps[_rng.Next(_steps.Length)], player, 0.35f, 0.92f + (float)_rng.NextDouble() * 0.16f);
            }
            float wantWind = level < 0 ? 0.04f : 0.07f + 0.16f * dark;
            _wind.volume = Mathf.MoveTowards(_wind.volume, wantWind * Volume, Time.deltaTime * 0.1f);
            if (!_wind.isPlaying)
            {
                _wind.Play();
            }
        }

        public void Swing(Float2 at) => Play(_swing, at, 0.4f, 0.95f + (float)_rng.NextDouble() * 0.1f);

        public void Shot(Float2 at) => Play(_shot, at, 0.8f, 1f);

        /// <summary>A blow on a wall, a door or the ground.</summary>
        public void Thud(Float2 at) => Play(_thud, at, 0.6f, 0.9f + (float)_rng.NextDouble() * 0.2f);

        public void Hit(Float2 at) => Play(_hit, at, 0.7f, 0.9f + (float)_rng.NextDouble() * 0.2f);

        public void Splat(Float2 at) => Play(_splat, at, 0.8f, 0.9f + (float)_rng.NextDouble() * 0.2f);

        public void Hurt(Float2 at) => Play(_hurt, at, 0.9f, 1f);

        public void NightHorn() => Play(_horn, _listener, 0.7f, 1f);

        /// <summary>For every zombie drawn: now and then it growls, louder when close (you hear them before you see them).</summary>
        public void ZombieNear(int key, Float2 position, bool isAwake)
        {
            float dx = position.X - _listener.X;
            float dy = position.Y - _listener.Y;
            if (dx * dx + dy * dy > HearDistance * HearDistance)
            {
                return;
            }
            if (!_nextGrowl.TryGetValue(key, out float at))
            {
                at = Time.time + 1f + (float)_rng.NextDouble() * 6f;
            }
            if (Time.time >= at)
            {
                Play(_growls[Math.Abs(key) % _growls.Length], position, 0.55f, 0.9f + (float)_rng.NextDouble() * 0.2f);
                at = Time.time + (isAwake ? 2.5f : 6f) + (float)_rng.NextDouble() * 5f;
            }
            _nextGrowl[key] = at;
            if (_nextGrowl.Count > 400)
            {
                _nextGrowl.Clear();                         // forget the dead; the living pick up new timers
            }
        }

        private void Play(AudioClip clip, Float2 at, float gain, float pitch)
        {
            float dx = at.X - _listener.X;
            float dy = at.Y - _listener.Y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d > HearDistance)
            {
                return;
            }
            float near = 1f - d / HearDistance;
            AudioSource v = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % Voices;
            v.pitch = pitch;
            v.panStereo = Mathf.Clamp(dx / 8f, -0.8f, 0.8f);
            v.volume = gain * near * near * Volume + (d < 0.5f ? gain * (1f - near) * 0.3f * Volume : 0f);
            v.clip = clip;
            v.Play();
        }

        // ------------------------------------------------------------ synthesis

        private static AudioClip Clip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        /// <summary>A soft step: low-passed noise with a quick decay and a thump under it.</summary>
        private static float[] Step(System.Random r, float cutoff, float thumpHz)
        {
            int n = (int)(Rate * 0.11f);
            var s = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = (float)Math.Exp(-t * 7f);
                lp += (Noise(r) - lp) * cutoff;
                s[i] = (lp * 1.6f + (float)Math.Sin(i * 2 * Math.PI * thumpHz / Rate) * 0.5f) * env * 0.7f;
            }
            return s;
        }

        /// <summary>A swish of air: noise whose brightness rises and falls.</summary>
        private static float[] Whoosh(System.Random r)
        {
            int n = (int)(Rate * 0.2f);
            var s = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = (float)Math.Sin(Math.PI * t);
                lp += (Noise(r) - lp) * (0.08f + 0.35f * env);
                s[i] = lp * env * 0.9f;
            }
            return s;
        }

        /// <summary>An impact: a falling sine plus a puff of noise (more noise = wetter).</summary>
        private static float[] Thump(System.Random r, float seconds, float fromHz, float toHz, float wet)
        {
            int n = (int)(Rate * seconds);
            var s = new float[n];
            double phase = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                double hz = toHz + (fromHz - toHz) * Math.Exp(-t * 6.0);
                phase += 2 * Math.PI * hz / Rate;
                float env = (float)Math.Exp(-t * 5f);
                lp += (Noise(r) - lp) * 0.18f;
                s[i] = ((float)Math.Sin(phase) * (1.2f - wet) + lp * wet * 2f) * env;
            }
            return s;
        }

        private static float[] Shot(System.Random r)
        {
            int n = (int)(Rate * 0.32f);
            var s = new float[n];
            double phase = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                phase += 2 * Math.PI * (45.0 + 220.0 * Math.Exp(-t * 18.0)) / Rate;
                lp += (Noise(r) - lp) * (0.6f * (1f - t) + 0.05f);
                s[i] = (lp * (float)Math.Exp(-t * 9f) * 1.3f + (float)Math.Sin(phase) * (float)Math.Exp(-t * 7f)) * 0.9f;
            }
            return s;
        }

        /// <summary>A zombie's groan: a rough low buzz that wavers, with breath over it.</summary>
        private static float[] Growl(System.Random r, float baseHz, float seconds)
        {
            int n = (int)(Rate * seconds);
            var s = new float[n];
            double phase = 0;
            float lp = 0f;
            float breath = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                double vibrato = 1.0 + 0.06 * Math.Sin(i * 2 * Math.PI * 5.5 / Rate);
                double hz = baseHz * (1.15 - 0.3 * t) * vibrato;
                phase += 2 * Math.PI * hz / Rate;
                double saw = (phase / (2 * Math.PI)) % 1.0 * 2.0 - 1.0;
                lp += ((float)saw - lp) * 0.12f;                              // rounds the sawtooth into a growl
                breath += (Noise(r) - breath) * 0.1f;
                float rough = 0.7f + 0.3f * (float)Math.Sin(i * 2 * Math.PI * 27.0 / Rate);
                float env = (float)Math.Sin(Math.PI * Math.Pow(t, 0.6)) ;
                s[i] = (lp * 1.5f * rough + breath * 0.9f) * env * 0.8f;
            }
            return s;
        }

        /// <summary>The night falling: two low notes that swell and fade.</summary>
        private static float[] Horn()
        {
            int n = (int)(Rate * 2.6f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = (float)(Math.Sin(Math.PI * t) * Math.Exp(-t * 0.8));
                double a = Math.Sin(i * 2 * Math.PI * 82.0 / Rate) + 0.5 * Math.Sin(i * 2 * Math.PI * 123.0 / Rate) + 0.25 * Math.Sin(i * 2 * Math.PI * 164.5 / Rate);
                s[i] = (float)a * env * 0.45f;
            }
            return s;
        }

        /// <summary>Wind: low-passed noise swelling slowly; whole waves fit the loop and the ends fade so it does not click.</summary>
        private static float[] Wind(System.Random r)
        {
            int n = Rate * 6;
            var s = new float[n];
            float lp = 0f;
            float lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                lp += (Noise(r) - lp) * 0.035f;
                lp2 += (lp - lp2) * 0.2f;
                float swell = 0.6f + 0.4f * (float)Math.Sin(t * 2 * Math.PI * 2.0 + 1.0) * (float)Math.Sin(t * 2 * Math.PI * 3.0);
                float edge = Math.Min(1f, Math.Min(t, 1f - t) * 30f);
                s[i] = lp2 * 6f * swell * edge;
            }
            return s;
        }
    }
}
