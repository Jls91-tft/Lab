namespace CepaCero.Util
{
    /// <summary>
    /// Small seeded PRNG (xorshift64*). Same sequence on every platform and
    /// runtime, unlike System.Random, so games and tests are reproducible.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed)
        {
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }
            ulong span = (ulong)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextULong() % span);
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextULong() >> 40) / (float)(1UL << 24);

        public bool Chance(float probability) => NextFloat() < probability;
    }
}
