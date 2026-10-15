using System;

namespace CepaCero.Rules
{
    /// <summary>
    /// Match clock. Everything (day number, phase, light) is derived from one
    /// tick counter, so the network only needs to send <see cref="Tick"/>.
    /// Day 1 starts at tick 0 in the morning; its night is night 1.
    /// </summary>
    public sealed class DayNightCycle
    {
        private readonly long _dayTicks;
        private readonly long _duskTicks;
        private readonly long _nightTicks;
        private readonly long _dawnTicks;
        private readonly long _cycleTicks;

        public DayNightCycle(GameSettings settings)
        {
            settings.Validate();
            TickRate = settings.TickRate;
            _dayTicks = (long)settings.DaySeconds * TickRate;
            _duskTicks = (long)settings.DuskSeconds * TickRate;
            _nightTicks = (long)settings.NightSeconds * TickRate;
            _dawnTicks = (long)settings.DawnSeconds * TickRate;
            _cycleTicks = _dayTicks + _duskTicks + _nightTicks + _dawnTicks;
        }

        public int TickRate { get; }

        public long Tick { get; private set; }

        /// <summary>1-based day number. Night n belongs to day n.</summary>
        public int Day => (int)(Tick / _cycleTicks) + 1;

        public DayPhase Phase => Locate(out _, out _);

        /// <summary>0 at the start of the current phase, approaching 1 at its end.</summary>
        public float PhaseProgress
        {
            get
            {
                Locate(out long intoPhase, out long phaseLength);
                return phaseLength == 0 ? 0f : (float)intoPhase / phaseLength;
            }
        }

        /// <summary>Ticks since the current phase began.</summary>
        public long TicksIntoPhase
        {
            get
            {
                Locate(out long intoPhase, out _);
                return intoPhase;
            }
        }

        public long PhaseLengthTicks
        {
            get
            {
                Locate(out _, out long length);
                return length;
            }
        }

        /// <summary>
        /// Ambient light for the view, 1 = full day, 0.08 = deep night
        /// (flashlights and fire do the rest). Smooth across dusk and dawn.
        /// </summary>
        public float AmbientLight
        {
            get
            {
                const float night = 0.08f;
                switch (Phase)
                {
                    case DayPhase.Day:
                        return 1f;
                    case DayPhase.Dusk:
                        return Lerp(1f, night, Smooth(PhaseProgress));
                    case DayPhase.Night:
                        return night;
                    default:
                        return Lerp(night, 1f, Smooth(PhaseProgress));
                }
            }
        }

        /// <summary>Advances one tick. Returns true if the phase changed.</summary>
        public bool Step()
        {
            DayPhase before = Phase;
            Tick++;
            return Phase != before;
        }

        /// <summary>Client side: jump to the host's tick.</summary>
        public void SetTick(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }
            Tick = tick;
        }

        private DayPhase Locate(out long intoPhase, out long phaseLength)
        {
            long t = Tick % _cycleTicks;
            if (t < _dayTicks)
            {
                intoPhase = t;
                phaseLength = _dayTicks;
                return DayPhase.Day;
            }
            t -= _dayTicks;
            if (t < _duskTicks)
            {
                intoPhase = t;
                phaseLength = _duskTicks;
                return DayPhase.Dusk;
            }
            t -= _duskTicks;
            if (t < _nightTicks)
            {
                intoPhase = t;
                phaseLength = _nightTicks;
                return DayPhase.Night;
            }
            intoPhase = t - _nightTicks;
            phaseLength = _dawnTicks;
            return DayPhase.Dawn;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
