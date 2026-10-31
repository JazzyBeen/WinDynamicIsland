using WinDynamicIsland.Core;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace WinDynamicIsland
{
    internal sealed class MediaSessionMonitor : IDisposable
    {
        sealed class Command
        {
            internal string Id, Name; internal double Position;
        }
        readonly ConcurrentQueue<Command> commands = new ConcurrentQueue<Command>();
        readonly Thread mediaWorker;
        readonly Action<MediaSnapshot> publish;
        readonly string sourceFilter;

        volatile bool stopping;
        string selectedId;
        internal MediaSessionMonitor(Action<MediaSnapshot> publish = null, bool monitorDevices = true, string sourceFilter = null)
        {
            this.publish = publish ?? Program.Presenter.Media;
            this.sourceFilter = sourceFilter;
            mediaWorker = new Thread(MediaLoop) { IsBackground = true, Name = "Island media sessions" };
            mediaWorker.Start();
        }
        void MediaLoop()
        {
            GlobalSystemMediaTransportControlsSessionManager manager = null;
            string cachedId = null, track = null, appName = "";
            Bitmap icon = null, artwork = null;
            DateTime nextMetadata = DateTime.MinValue;
            bool wasAvailable = false;
            string lastState = null;
            int failures = 0;
            var clock = new MediaTimelineClock();
            try
            {
                while (!stopping)
                {
                    try
                    {
                        var browser = sourceFilter == null && Program.Settings.BrowserEnabled ? BrowserMediaProvider.Read() : null;
                        if (browser != null && browser.Playing)
                        {
                            publish(browser);
                            wasAvailable = true;
                            selectedId = browser.Id;
                            string browserState = browser.Id + "; direct browser channel; playing=True; foreground=" + browser.Foreground;
                            if (browserState != lastState)
                            {
                                Program.Log("media-listener.txt", "running; " + browserState);
                                lastState = browserState;
                            }
                            failures = 0;
                            Sleep(300);
                            continue;
                        }
                        if (manager == null)
                            manager = WinRt.Wait(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
                        var sessions = manager.GetSessions().Where(s => sourceFilter == null || s.SourceAppUserModelId.IndexOf(sourceFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                        Command command;
                        while (commands.TryDequeue(out command))
                        {
                            var target = sessions.FirstOrDefault(s => s.SourceAppUserModelId == command.Id);
                            if (target == null)
                                continue;
                            bool success = false;
                            var playback = target.GetPlaybackInfo();
                            if (command.Name == "toggle")
                                success = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing ? WinRt.Wait(target.TryPauseAsync()) : WinRt.Wait(target.TryPlayAsync());
                            else if (command.Name == "seek" && playback.Controls.IsPlaybackPositionEnabled)
                            {
                                var timeline = target.GetTimelineProperties();
                                double low = timeline.MinSeekTime.TotalSeconds, high = timeline.MaxSeekTime.TotalSeconds;
                                if (high <= low)
                                {
                                    low = timeline.StartTime.TotalSeconds;
                                    high = timeline.EndTime.TotalSeconds;
                                }
                                if (high > low)
                                {
                                    double position = Math.Max(low, Math.Min(high, command.Position));
                                    success = WinRt.Wait(target.TryChangePlaybackPositionAsync(TimeSpan.FromSeconds(position).Ticks));
                                    if (success)
                                        clock.Seek(command.Id, position, DateTime.UtcNow);
                                }
                            }
                            Program.Log("media-command.txt", command.Name + "; accepted=" + success);
                            nextMetadata = DateTime.MinValue;
                        }
                        var current = manager.GetCurrentSession();
                        if (current != null && !sessions.Any(s => s.SourceAppUserModelId == current.SourceAppUserModelId))
                            current = null;
                        var active = current != null && current.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing ? current : sessions.FirstOrDefault(s => s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
                        if (active == null)
                            active = sessions.FirstOrDefault(s => s.SourceAppUserModelId == selectedId) ?? current;
                        if (browser != null && (active == null || active.GetPlaybackInfo().PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing))
                        {
                            publish(browser);
                            wasAvailable = true;
                            selectedId = browser.Id;
                            string browserState = browser.Id + "; direct browser channel; playing=False; foreground=" + browser.Foreground;
                            if (browserState != lastState)
                            {
                                Program.Log("media-listener.txt", "running; " + browserState);
                                lastState = browserState;
                            }
                        }
                        else if (active == null)
                        {
                            if (wasAvailable)
                            {
                                publish(null);
                                wasAvailable = false;
                            }
                            if (lastState != "none")
                            {
                                Program.Log("media-listener.txt", "running; sessions=0");
                                lastState = "none";
                            }
                        }
                        else
                        {
                            if (browser != null)
                                browser.Dispose();
                            string id = active.SourceAppUserModelId;
                            selectedId = id;
                            if (id != cachedId)
                            {
                                if (icon != null)
                                    icon.Dispose();
                                icon = AppIdentity.IconFor(id, out appName);
                                if (artwork != null)
                                {
                                    artwork.Dispose();
                                    artwork = null;
                                }
                                cachedId = id;
                                track = null;
                                nextMetadata = DateTime.MinValue;
                            }
                            string title = "", artist = "";
                            if (DateTime.UtcNow >= nextMetadata)
                            {
                                var properties = WinRt.Wait(active.TryGetMediaPropertiesAsync());
                                title = properties.Title ?? "";
                                artist = properties.Artist ?? "";
                                string newTrack = title + "\n" + artist;
                                if (newTrack != track)
                                {
                                    clock.Reset();
                                    if (artwork != null)
                                    {
                                        artwork.Dispose();
                                        artwork = null;
                                    }
                                    try
                                    {
                                        if (properties.Thumbnail != null)
                                            artwork = ReadImage(properties.Thumbnail);
                                    }
                                    catch { }
                                    track = newTrack;
                                }
                                nextMetadata = DateTime.UtcNow.AddSeconds(1);
                            }
                            else if (track != null)
                            {
                                int split = track.IndexOf('\n');
                                title = track.Substring(0, split);
                                artist = track.Substring(split + 1);
                            }
                            var playback = active.GetPlaybackInfo();
                            var timeline = active.GetTimelineProperties();
                            bool playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                            double rate = playback.PlaybackRate ?? 1;
                            double position = clock.Read(id, timeline.Position.TotalSeconds, timeline.LastUpdatedTime, playing, rate, DateTime.UtcNow);
                            double start = timeline.StartTime.TotalSeconds, end = timeline.EndTime.TotalSeconds;
                            var snapshot = new MediaSnapshot { Id = id, Title = string.IsNullOrWhiteSpace(title) ? appName : title, Artist = artist, AppName = appName, Playing = playing, Rate = rate, Foreground = AppIdentity.IsForeground(id), CanToggle = playback.Controls.IsPlayEnabled || playback.Controls.IsPauseEnabled || playback.Controls.IsPlayPauseToggleEnabled, CanSeek = playback.Controls.IsPlaybackPositionEnabled && end > start, Start = start, End = end, Position = Math.Max(start, end > start ? Math.Min(end, position) : position), Icon = icon == null ? null : new Bitmap(icon), Artwork = artwork == null ? null : new Bitmap(artwork), Command = (action, value) => commands.Enqueue(new Command { Id = id, Name = action, Position = value }) };
                            snapshot.Stopped = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped || playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed;
                            publish(snapshot);
                            wasAvailable = true;
                            string state = id + "; playing=" + playing + "; foreground=" + AppIdentity.IsForeground(id) + "; seek=" + snapshot.CanSeek;
                            if (state != lastState)
                            {
                                Program.Log("media-listener.txt", "running; " + state);
                                lastState = state;
                            }
                        }
                        failures = 0;
                    }
                    catch (Exception ex) { if (++failures == 3 && wasAvailable) { publish(null); wasAvailable = false; } Program.Log("media-listener.txt", "retrying; " + ex.GetType().Name); manager = null; }
                    Sleep(400);
                }
            }
            finally { if (icon != null) icon.Dispose(); if (artwork != null) artwork.Dispose(); }
        }
        static Bitmap ReadImage(IRandomAccessStreamReference reference)
        {
            using (var stream = WinRt.Wait(reference.OpenReadAsync()))
            {
                if (stream.Size == 0 || stream.Size > 3 * 1024 * 1024)
                    return null;
                using (var reader = new DataReader(stream.GetInputStreamAt(0)))
                {
                    uint count = WinRt.Wait(reader.LoadAsync((uint)stream.Size));
                    var bytes = new byte[count];
                    reader.ReadBytes(bytes);
                    using (var input = new MemoryStream(bytes))
                    using (var image = Image.FromStream(input))
                    {
                        if (image.Width > 4096 || image.Height > 4096)
                            return null;
                        return new Bitmap(image, 96, 96);
                    }
                }
            }
        }
        void Sleep(int ms)
        {
            for (int i = 0; i < ms / 50 && !stopping; i++)
                Thread.Sleep(50);
        }
        public void Dispose()
        {
            stopping = true;
            mediaWorker.Join(4000);
            publish(null);
        }
    }


}
