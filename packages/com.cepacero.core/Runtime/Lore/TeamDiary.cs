using System;
using System.Collections.Generic;

namespace CepaCero.Lore
{
    /// <summary>
    /// The group's shared diary (docs/lore.md): entries unlock for everyone when
    /// their trigger fires ("match_start", "sample:sample_brute"...). Order of
    /// discovery is kept for the diary screen.
    /// </summary>
    public sealed class TeamDiary
    {
        public const string Format = "cepacero.diary";

        private readonly DiaryEntry[] _entries;
        private readonly Dictionary<string, int> _indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<int> _unlocked = new List<int>();
        private readonly HashSet<string> _fired = new HashSet<string>(StringComparer.Ordinal);

        public TeamDiary(DiaryData data)
        {
            if (data == null || data.format != Format || data.version != 1 || data.entries == null)
            {
                throw new FormatException($"Not a {Format} v1 document.");
            }
            _entries = data.entries;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_indexById.ContainsKey(_entries[i].id))
                {
                    throw new FormatException($"Duplicate diary entry '{_entries[i].id}'.");
                }
                _indexById.Add(_entries[i].id, i);
            }
        }

        public IReadOnlyList<DiaryEntry> Entries => _entries;

        /// <summary>Indices into <see cref="Entries"/>, in the order the group found them.</summary>
        public IReadOnlyList<int> Unlocked => _unlocked;

        public bool IsUnlocked(string id) => _indexById.TryGetValue(id, out int i) && _unlocked.Contains(i);

        /// <summary>Fires a trigger once; appends the indices of entries it unlocked to <paramref name="newlyUnlocked"/>.</summary>
        public void Fire(string trigger, List<int> newlyUnlocked)
        {
            if (!_fired.Add(trigger))
            {
                return;
            }
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].trigger == trigger && !_unlocked.Contains(i))
                {
                    _unlocked.Add(i);
                    newlyUnlocked?.Add(i);
                }
            }
        }

        public bool HasFired(string trigger) => _fired.Contains(trigger);

        /// <summary>Restores an entry from a save or a host message (unknown ids are ignored).</summary>
        public void Unlock(string id)
        {
            if (_indexById.TryGetValue(id, out int i) && !_unlocked.Contains(i))
            {
                _unlocked.Add(i);
                _fired.Add(_entries[i].trigger);
            }
        }
    }
}
