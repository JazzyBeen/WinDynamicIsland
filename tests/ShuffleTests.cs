using System;
using System.IO;
using System.Linq;
using WinDynamicIsland.Core;

namespace WinDynamicIsland
{
    internal static class ShuffleTests
    {
        static void Check(bool condition, string label)
        {
            if (!condition) throw new Exception(label);
            Console.WriteLine("PASS: " + label);
        }
        static string Play(ShufflePlaylist playlist, string[] files)
        {
            string file = playlist.Next(files); playlist.MarkPlayed(file); return file;
        }
        static void Main()
        {
            string directory = Path.Combine(Path.GetTempPath(), "island-shuffle-" + Guid.NewGuid().ToString("N"));
            try
            {
                var files = Enumerable.Range(1, 12).Select(n => "track-" + n + ".mp3").ToArray();
                var playlist = new ShufflePlaylist(null, new Random(13));
                string previous = null;
                for (int round = 0; round < 20; round++)
                {
                    var cycle = Enumerable.Range(0, 12).Select(_ => Play(playlist, files)).ToArray();
                    Check(cycle.Distinct().Count() == 12 && cycle.All(files.Contains), "all 12 tracks play once in cycle " + round);
                    Check(previous == null || cycle[0] != previous, "cycle boundary never repeats the previous track");
                    previous = cycle[11];
                }
                playlist = new ShufflePlaylist(null, new Random(3));
                var first = Enumerable.Range(0, 4).Select(_ => Play(playlist, files)).ToArray();
                var snapshot = JsonCodec.Create().Deserialize<ShufflePlaylistState>(JsonCodec.Serialize(playlist.Snapshot()));
                var restored = new ShufflePlaylist(snapshot, new Random(5));
                var next = Enumerable.Range(0, 8).Select(_ => Play(restored, files)).ToArray();
                Check(first.Concat(next).Distinct().Count() == 12, "a new session resumes the unplayed bag after JSON round trip");
                var oneLeft = new ShufflePlaylist(new ShufflePlaylistState { Remaining = new string[0], KnownTracks = new[] { "a", "b" }, LastPlayed = "a" }, new Random(1));
                Check(Play(oneLeft, new[] { "a", "b" }) == "b", "a restarted session avoids the previous track when refilling");
                Check(Play(oneLeft, new[] { "only" }) == "only" && Play(oneLeft, new[] { "only" }) == "only", "single-track folders remain playable");
                Check(oneLeft.Next(new string[0]) == null, "empty folder has no selection");
                var changed = new ShufflePlaylist(new ShufflePlaylistState { Remaining = new[] { "removed", "keep" }, KnownTracks = new[] { "removed", "keep" } }, new Random(2));
                var selections = new[] { Play(changed, new[] { "keep", "new" }), Play(changed, new[] { "keep", "new" }) };
                Check(selections.Distinct().Count() == 2 && !selections.Contains("removed") && selections.Contains("new"), "folder edits drop removed tracks and include added tracks once");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "shuffle.json");
                var store = new FocusShuffleStore(path);
                var before = Play(store.For("Ambient"), files); store.Save();
                var reloaded = new FocusShuffleStore(path);
                var after = Play(reloaded.For("Ambient"), files);
                Check(before != after && reloaded.For("Ambient").Snapshot().Remaining.Length == 10, "application restart restores the actual persisted queue");
                Check(reloaded.For("Lofi").Snapshot().Remaining.Length == 0, "Ambient and Lo-fi maintain independent queues");
                File.WriteAllText(path, "invalid");
                Check(new FocusShuffleStore(path).For("Ambient").Next(files) != null, "corrupt shuffle state does not stop music");
            }
            catch (Exception ex) { Console.WriteLine(ex); Environment.ExitCode = 1; }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
