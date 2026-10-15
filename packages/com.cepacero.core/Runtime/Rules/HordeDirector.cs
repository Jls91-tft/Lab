using System;
using System.Collections.Generic;
using CepaCero.Util;

namespace CepaCero.Rules
{
    /// <summary>
    /// Decides what each night brings and when each zombie enters. Host only.
    /// The horde grows every night; specials and the miniboss join later; the
    /// group's behaviour (noise) adds pressure — the "adaptive mutation" hook.
    /// </summary>
    public sealed class HordeDirector
    {
        public const string Walker = "walker";
        public const string Runner = "runner";
        public const string Screamer = "screamer";
        public const string Spitter = "spitter";
        public const string Brute = "brute";

        private readonly GameSettings _settings;
        private readonly List<string> _queue = new List<string>();
        private int _released;
        private int _queuedNight;

        public HordeDirector(GameSettings settings)
        {
            settings.Validate();
            _settings = settings;
        }

        /// <summary>Zombies still to enter tonight.</summary>
        public int Remaining => _queue.Count - _released;

        /// <summary>
        /// Plan for a night. <paramref name="noiseBonus"/> (0..1) is how loud the
        /// group has been: up to +50 % zombies.
        /// </summary>
        public HordePlan PlanNight(int night, float noiseBonus = 0f)
        {
            if (night < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(night));
            }
            noiseBonus = Math.Clamp(noiseBonus, 0f, 1f);
            double raw = _settings.HordeBase * Math.Pow(_settings.HordeGrowth, night - 1) * (1.0 + 0.5 * noiseBonus);
            int total = (int)Math.Min(_settings.HordeMax, Math.Round(raw, MidpointRounding.AwayFromZero));

            var plan = new HordePlan(night);
            bool boss = _settings.BossEveryNights > 0 && night % _settings.BossEveryNights == 0;
            if (boss)
            {
                plan.Add(Brute, 1);
                total = Math.Max(0, total - 1);
            }

            int specials = night >= _settings.SpecialsFromNight ? Math.Max(1, (int)Math.Round(total * _settings.SpecialShare, MidpointRounding.AwayFromZero)) : 0;
            specials = Math.Min(specials, total);
            int screamers = (specials + 1) / 2;
            int spitters = specials - screamers;

            // Runners: none on night 1, then a growing share up to 30 %.
            float runnerShare = Math.Min(0.3f, 0.06f * (night - 1));
            int commons = total - specials;
            int runners = (int)Math.Round(commons * runnerShare, MidpointRounding.AwayFromZero);

            plan.Add(Walker, commons - runners);
            plan.Add(Runner, runners);
            plan.Add(Screamer, screamers);
            plan.Add(Spitter, spitters);
            return plan;
        }

        /// <summary>Prepares tonight's arrival order (shuffled with the match seed).</summary>
        public void BeginNight(HordePlan plan)
        {
            _queue.Clear();
            _released = 0;
            _queuedNight = plan.Night;
            foreach (KeyValuePair<string, int> e in plan.Entries)
            {
                if (e.Key == Brute)
                {
                    continue; // the miniboss enters last, on its own
                }
                for (int i = 0; i < e.Value; i++)
                {
                    _queue.Add(e.Key);
                }
            }
            var rng = new DeterministicRandom(_settings.Seed * 7919UL + (ulong)plan.Night);
            for (int i = _queue.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (_queue[i], _queue[j]) = (_queue[j], _queue[i]);
            }
            if (plan.CountOf(Brute) > 0)
            {
                _queue.Add(Brute);
            }
        }

        /// <summary>
        /// Zombies that should enter now. Arrivals are spread evenly over the
        /// spawn window of the night and held back while too many are alive.
        /// </summary>
        public void TakeDue(float nightProgress, int alive, List<string> into)
        {
            if (_queue.Count == 0)
            {
                return;
            }
            float t = Math.Clamp(nightProgress / _settings.SpawnWindow, 0f, 1f);
            int due = (int)Math.Ceiling(t * _queue.Count);
            int room = _settings.MaxAliveZombies - alive;
            while (_released < due && room > 0)
            {
                into.Add(_queue[_released++]);
                room--;
            }
        }

        public int QueuedNight => _queuedNight;
    }
}
