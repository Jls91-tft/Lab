using System.Collections.Generic;
using CepaCero.Util;

namespace CepaCero.AI
{
    /// <summary>
    /// Client-side view of the zombies. Keeps the last two snapshots of each
    /// zombie and interpolates between them, rendering slightly in the past
    /// (one snapshot interval) so movement looks smooth at 10 Hz.
    /// </summary>
    public sealed class ZombieReplica
    {
        private readonly Dictionary<ushort, Track> _tracks = new Dictionary<ushort, Track>();
        private readonly List<ushort> _gone = new List<ushort>();
        private readonly List<ZombieSnapshotEntry> _scratch = new List<ZombieSnapshotEntry>();

        public int Count => _tracks.Count;

        public IEnumerable<ushort> Ids => _tracks.Keys;

        /// <summary>Applies a snapshot received at <paramref name="time"/> (seconds, client clock).</summary>
        public void Apply(ByteReader reader, double time)
        {
            _scratch.Clear();
            ZombieSnapshotCodec.Read(reader, _scratch);
            Apply(_scratch, time);
        }

        public void Apply(IReadOnlyList<ZombieSnapshotEntry> entries, double time)
        {
            foreach (KeyValuePair<ushort, Track> kv in _tracks)
            {
                kv.Value.Seen = false;
            }
            foreach (ZombieSnapshotEntry e in entries)
            {
                if (_tracks.TryGetValue(e.NetId, out Track track))
                {
                    track.Previous = track.Current;
                    track.PreviousTime = track.CurrentTime;
                }
                else
                {
                    track = new Track { Previous = e, PreviousTime = time };
                    _tracks.Add(e.NetId, track);
                }
                track.Current = e;
                track.CurrentTime = time;
                track.Seen = true;
            }
            // Not in this snapshot: died or out of range. The view despawns it.
            _gone.Clear();
            foreach (KeyValuePair<ushort, Track> kv in _tracks)
            {
                if (!kv.Value.Seen)
                {
                    _gone.Add(kv.Key);
                }
            }
            foreach (ushort id in _gone)
            {
                _tracks.Remove(id);
            }
        }

        /// <summary>
        /// Interpolated state at <paramref name="renderTime"/>. Use
        /// renderTime = now − snapshotInterval for smooth motion.
        /// </summary>
        public bool TrySample(ushort netId, double renderTime, out ZombieSnapshotEntry sample)
        {
            if (!_tracks.TryGetValue(netId, out Track t))
            {
                sample = default;
                return false;
            }
            sample = t.Current;
            double span = t.CurrentTime - t.PreviousTime;
            if (span > 1e-6)
            {
                float a = (float)System.Math.Clamp((renderTime - t.PreviousTime) / span, 0.0, 1.0);
                sample.Position = t.Previous.Position + (t.Current.Position - t.Previous.Position) * a;
            }
            return true;
        }

        private sealed class Track
        {
            public ZombieSnapshotEntry Previous;
            public ZombieSnapshotEntry Current;
            public double PreviousTime;
            public double CurrentTime;
            public bool Seen;
        }
    }
}
