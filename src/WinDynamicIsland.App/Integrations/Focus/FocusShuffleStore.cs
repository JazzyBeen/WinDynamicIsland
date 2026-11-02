using System;
using System.Collections.Generic;
using System.IO;
using WinDynamicIsland.Core;

namespace WinDynamicIsland
{
    internal sealed class FocusShuffleStore
    {
        readonly string path;
        readonly Dictionary<string, ShufflePlaylist> playlists = new Dictionary<string, ShufflePlaylist>(StringComparer.OrdinalIgnoreCase);

        internal FocusShuffleStore(string path)
        {
            this.path = path;
            try
            {
                if (!File.Exists(path)) return;
                var states = JsonCodec.Create().Deserialize<Dictionary<string, ShufflePlaylistState>>(File.ReadAllText(path));
                if (states != null)
                    foreach (var state in states) playlists[state.Key] = new ShufflePlaylist(state.Value);
            }
            catch (Exception ex) { Program.Log("focus-shuffle-error.txt", "Load: " + ex.GetType().Name); }
        }

        internal ShufflePlaylist For(string playlist)
        {
            ShufflePlaylist value;
            if (!playlists.TryGetValue(playlist, out value)) playlists[playlist] = value = new ShufflePlaylist();
            return value;
        }

        internal void Save()
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var states = new Dictionary<string, ShufflePlaylistState>();
                foreach (var playlist in playlists) states[playlist.Key] = playlist.Value.Snapshot();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(temporary, JsonCodec.Serialize(states));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            catch (Exception ex)
            {
                // Shuffle persistence is independent of settings and must never stop playback.
                Program.Log("focus-shuffle-error.txt", "Save: " + ex.GetType().Name);
            }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } }
        }
    }
}
