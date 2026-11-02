using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed class FocusService : IDisposable
    {
        static readonly object Gate = new object();
        static FocusService instance;
        readonly FocusTimer clock = new FocusTimer();
        readonly Thread worker;
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        volatile bool stopping;
        bool resetAudio;
        readonly Stopwatch elapsed = Stopwatch.StartNew();
        readonly MciMusicPlayer player = new MciMusicPlayer();
        string playlist, track = "";
        string sessionId = "";
        readonly FocusShuffleStore shuffle;
        readonly string musicDirectory;
        bool audioFailed;
        double lastTime;
        DateTime retryAudioAt;
        internal static string MusicPath { get { return Path.Combine(Program.DataPath, "Music"); } }
        internal FocusService(string musicDirectory = null, string shufflePath = null)
        {
            this.musicDirectory = musicDirectory ?? MusicPath;
            shuffle = new FocusShuffleStore(shufflePath ?? Path.Combine(Program.DataPath, "focus-shuffle.json"));
            lock (Gate) instance = this;
            worker = new Thread(delegate()
            {
                try
                {
                    while (!stopping) { Tick(null); System.Windows.Forms.Application.DoEvents(); wake.WaitOne(250); }
                }
                finally { lock (Gate) player.Close(); }
            }) { IsBackground = true, Name = "Island focus audio" };
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }
        internal static void Toggle()
        {
            lock (Gate)
            {
                if (instance == null) return;
                var self = instance;
                if (self.clock.Active) { self.clock.Stop(); self.resetAudio = true; Program.Presenter.Status("Pomodoro", L.T("FocusStopped"), true, false); }
                else
                {
                    self.clock.Start(Program.Settings.FocusMinutes, Program.Settings.BreakMinutes);
                    self.sessionId = Guid.NewGuid().ToString("N");
                    self.lastTime = self.elapsed.Elapsed.TotalSeconds;
                    self.playlist = null; self.track = ""; self.audioFailed = false;
                    self.retryAudioAt = DateTime.MinValue;
                    self.resetAudio = true;
                }
                self.wake.Set();
            }
        }
        void Tick(object state)
        {
            lock (Gate)
            {
                if (instance != this) return;
                double now = elapsed.Elapsed.TotalSeconds, delta = now - lastTime;
                lastTime = now;
                try
                {
                    if (resetAudio) { player.Close(); resetAudio = false; }
                    if (!clock.Active) return;
                    if (clock.Advance(delta))
                    {
                        player.Close(); track = "";
                        // A new phase id expands the existing island smoothly; no second popup.
                    }
                    if (clock.Paused || clock.Resting && !Program.Settings.FocusBreakMusic) { player.Pause(); return; }
                    string selected = Program.Settings.FocusPlaylist;
                    if (playlist != selected) { player.Close(); playlist = selected; track = ""; retryAudioAt = DateTime.MinValue; }
                    player.Volume(Program.Settings.FocusVolume);
                    if (player.IsPaused) player.Resume();
                    if (player.IsPlaying) return;
                    if (DateTime.UtcNow < retryAudioAt) return;
                    string folder = Path.Combine(musicDirectory, selected);
                    string[] files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.mp3").Select(Path.GetFileName).ToArray() : new string[0];
                    if (files.Length == 0) { track = L.T("FocusSilent"); return; }
                    // Try each track once: one damaged file must not stop the timer or the playlist.
                    bool played = false;
                    var bag = shuffle.For(selected);
                    var attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < files.Length * 2 && attempted.Count < files.Length; i++)
                    {
                        string file = bag.Next(files);
                        if (!attempted.Add(file)) continue;
                        if (player.Open(Path.Combine(folder, file), Program.Settings.FocusVolume))
                        {
                            bag.MarkPlayed(file);
                            track = Path.GetFileNameWithoutExtension(file).Replace('-', ' '); played = true; break;
                        }
                    }
                    shuffle.Save();
                    audioFailed = !played;
                    if (!played) retryAudioAt = DateTime.UtcNow.AddSeconds(10);
                }
                catch (Exception ex) { audioFailed = true; Program.Log("focus-error.txt", ex.GetType().Name); }
            }
        }
        internal static MediaSnapshot Read()
        {
            lock (Gate)
            {
                var self = instance;
                if (self == null || !self.clock.Active) return null;
                return new MediaSnapshot
                {
                    Id = "WinDynamicIsland.Focus." + self.sessionId + (self.clock.Resting ? "rest" : "work") + self.clock.Cycles,
                    Title = L.T(self.clock.Resting ? "FocusRest" : "FocusWork"),
                    Artist = (self.audioFailed ? L.T("FocusAudioError") : self.track) + " · " + (int)(self.clock.Duration / 60) + (L.Language == "en" ? " min" : " мин"),
                    AppName = "Pomodoro", Playing = !self.clock.Paused, CanToggle = true, CanSeek = true,
                    Position = self.clock.Position, End = self.clock.Duration,
                    Command = delegate(string name, double value)
                    {
                        lock (Gate)
                        {
                            if (instance != self) return;
                            if (name == "toggle") self.clock.TogglePause();
                            else if (name == "seek") self.clock.Seek(value);
                            self.wake.Set();
                        }
                    }
                };
            }
        }
        public void Dispose()
        {
            lock (Gate) { if (instance == this) instance = null; clock.Stop(); stopping = true; }
            wake.Set();
            if (worker.Join(4000)) wake.Dispose();
        }
    }
    internal sealed class MciMusicPlayer
    {
        readonly string alias = "island" + Guid.NewGuid().ToString("N");
        bool opened;
        internal string LastError { get; private set; }
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] static extern int mciSendString(string command, StringBuilder result, int size, IntPtr callback);
        string Query(string item)
        {
            if (!opened) return "";
            var result = new StringBuilder(256);
            return mciSendString("status " + alias + " " + item, result, result.Capacity, IntPtr.Zero) == 0 ? result.ToString() : "";
        }
        int Send(string text) { return mciSendString(text + " " + alias, null, 0, IntPtr.Zero); }
        internal bool IsPlaying { get { return Query("mode") == "playing"; } }
        internal bool IsPaused { get { return Query("mode") == "paused"; } }
        internal bool Open(string file, int volume)
        {
            Close();
            if (file.IndexOf('"') >= 0) return false;
            int code = mciSendString("open \"" + Path.GetFullPath(file) + "\" type mpegvideo alias " + alias, null, 0, IntPtr.Zero);
            LastError = "open:" + code;
            opened = code == 0;
            if (!opened) return false;
            Volume(volume);
            code = Send("play"); LastError = "play:" + code;
            if (code == 0) return true;
            Close(); return false;
        }
        internal void Volume(int percent) { if (opened) mciSendString("setaudio " + alias + " volume to " + Math.Max(0, Math.Min(100, percent)) * 10, null, 0, IntPtr.Zero); }
        internal void Pause() { if (IsPlaying) Send("pause"); }
        internal void Resume() { if (IsPaused) Send("resume"); }
        internal void Close() { if (opened) Send("close"); opened = false; }
    }
}
