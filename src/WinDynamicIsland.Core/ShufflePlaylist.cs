using System;
using System.Collections.Generic;
using System.Linq;

namespace WinDynamicIsland.Core
{
    public sealed class ShufflePlaylistState
    {
        public string LastPlayed { get; set; }
        public string[] Remaining { get; set; }
        public string[] KnownTracks { get; set; }
    }

    // A shuffled bag survives short sessions: restarting never resets it to track one.
    public sealed class ShufflePlaylist
    {
        readonly Random random;
        readonly List<string> remaining;
        HashSet<string> known;
        string lastPlayed;
        static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

        public ShufflePlaylist(ShufflePlaylistState state = null, Random random = null)
        {
            this.random = random ?? new Random(Guid.NewGuid().GetHashCode());
            remaining = new List<string>((state == null ? null : state.Remaining) ?? new string[0]);
            known = new HashSet<string>((state == null ? null : state.KnownTracks) ?? new string[0], Comparer);
            lastPlayed = state == null ? null : state.LastPlayed;
        }

        public string Next(IEnumerable<string> tracks)
        {
            var available = new HashSet<string>(tracks.Where(t => !string.IsNullOrWhiteSpace(t)), Comparer);
            // Reconcile a user's edited music folder without replaying the consumed bag.
            var queued = new HashSet<string>(Comparer);
            remaining.RemoveAll(t => !available.Contains(t) || !queued.Add(t));
            foreach (string added in available.Where(t => !known.Contains(t) && !queued.Contains(t)))
                remaining.Insert(random.Next(remaining.Count + 1), added);
            known = available;
            if (available.Count == 0) return null;
            if (remaining.Count == 0)
            {
                remaining.AddRange(available.OrderBy(t => t, Comparer));
                for (int i = remaining.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    string value = remaining[i]; remaining[i] = remaining[j]; remaining[j] = value;
                }
            }
            if (remaining.Count > 1 && Comparer.Equals(remaining[0], lastPlayed))
            {
                int j = random.Next(1, remaining.Count);
                string value = remaining[0]; remaining[0] = remaining[j]; remaining[j] = value;
            }
            string next = remaining[0]; remaining.RemoveAt(0);
            return next;
        }

        public void MarkPlayed(string track) { lastPlayed = track; }
        public ShufflePlaylistState Snapshot()
        {
            return new ShufflePlaylistState { LastPlayed = lastPlayed, Remaining = remaining.ToArray(), KnownTracks = known.ToArray() };
        }
    }
}
