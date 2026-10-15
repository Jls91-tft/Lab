using System.Collections.Generic;

namespace CepaCero.Rules
{
    /// <summary>How many of each zombie type a night brings. Ids match docs/bestiario.md.</summary>
    public sealed class HordePlan
    {
        private readonly List<KeyValuePair<string, int>> _entries = new List<KeyValuePair<string, int>>();

        public HordePlan(int night)
        {
            Night = night;
        }

        public int Night { get; }

        public IReadOnlyList<KeyValuePair<string, int>> Entries => _entries;

        public int Total
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<string, int> e in _entries)
                {
                    total += e.Value;
                }
                return total;
            }
        }

        public int CountOf(string zombieId)
        {
            foreach (KeyValuePair<string, int> e in _entries)
            {
                if (e.Key == zombieId)
                {
                    return e.Value;
                }
            }
            return 0;
        }

        internal void Add(string zombieId, int count)
        {
            if (count > 0)
            {
                _entries.Add(new KeyValuePair<string, int>(zombieId, count));
            }
        }
    }
}
