using System;
using System.Threading;
using System.Collections.Concurrent;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed class AudioMonitor : IDisposable
    {
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        readonly AutoResetEvent f7 = new AutoResetEvent(false);
        readonly AutoResetEvent volumeKey = new AutoResetEvent(false);
        readonly ConcurrentQueue<int> volumeCommands = new ConcurrentQueue<int>();
        readonly Thread worker;
        readonly VolumeGesture gesture = new VolumeGesture();
        readonly object gate = new object();
        float lastVolume = -1; bool lastMute, lastMic; bool micKnown;
        DateTime lastKeyAt;
        internal volatile bool CanControlVolume;
        internal static AudioMonitor Current;
        internal AudioMonitor()
        {
            Current = this;
            worker = new Thread(Run) { IsBackground = true, Name = "Island audio endpoints" };
            worker.Start();
        }
        internal void F7()
        {
            f7.Set();
        }
        internal void VolumeKey(int key, bool control)
        {
            lock (gate)
            {
                lastKeyAt = DateTime.UtcNow;
                gesture.Change(lastKeyAt);
            }
            if (control)
                volumeCommands.Enqueue(key);
            volumeKey.Set();
        }
        void VolumeChanged(float level, bool muted)
        {
            lock (gate)
            {
                if (Math.Abs(lastVolume - level) < 0.001 && lastMute == muted)
                    return;
                bool initialized = lastVolume >= 0;
                lastVolume = level;
                lastMute = muted;
                if (initialized && Program.Settings.VolumeEnabled)
                {
                    bool wide = (DateTime.UtcNow - lastKeyAt).TotalMilliseconds < 400 ? gesture.Wide : gesture.Change(DateTime.UtcNow);
                    Island.ShowHud(CompactNoticeKind.Volume, L.T("Volume"), (int)Math.Round(level * 100), muted, wide);
                }
            }
        }
        void MicChanged(float level, bool muted)
        {
            lock (gate)
            {
                bool changed = micKnown && lastMic != muted;
                lastMic = muted;
                micKnown = true;
                if (changed && Program.Settings.MicrophoneEnabled)
                    Island.ShowHud(CompactNoticeKind.Microphone, L.T(muted ? "MicOff" : "MicOn"), 0, muted, false);
            }
        }
        void Run()
        {
            AudioEndpoint output = null, microphone = null;
            try
            {
                while (true)
                {
                    int wake = WaitHandle.WaitAny(new WaitHandle[] { stop, f7, volumeKey }, 700);
                    if (wake == 0)
                        break;
                    try
                    {
                        if (output == null || !output.IsDefault(0))
                        {
                            CanControlVolume = false;
                            if (output != null)
                                output.Dispose();
                            output = null;
                            lastVolume = -1;
                            output = new AudioEndpoint(0, VolumeChanged);
                            float level;
                            bool mute;
                            output.Read(out level, out mute);
                            VolumeChanged(level, mute);
                            CanControlVolume = true;
                        }
                        int key;
                        while (volumeCommands.TryDequeue(out key))
                        {
                            if (key == 0xAD)
                                output.ToggleMute();
                            else
                                output.Step(key == 0xAF ? 1 : -1);
                        }
                        if (wake == 2 && Program.Settings.VolumeEnabled)
                        {
                            stop.WaitOne(25);
                            float level;
                            bool mute;
                            output.Read(out level, out mute);
                            VolumeChanged(level, mute);
                            lock (gate)
                                Island.ShowHud(CompactNoticeKind.Volume, L.T("Volume"), (int)Math.Round(level * 100), mute, gesture.Wide);
                        }
                        if (microphone == null || !microphone.IsDefault(1))
                            try
                            {
                                if (microphone != null)
                                    microphone.Dispose();
                                microphone = null;
                                micKnown = false;
                                microphone = new AudioEndpoint(1, MicChanged);
                                float level;
                                bool mute;
                                microphone.Read(out level, out mute);
                                MicChanged(level, mute);
                            }
                            catch { microphone = null; }
                        if (wake == 1 && microphone != null && Program.Settings.F7MicrophoneIndicator)
                        {
                            if (Program.Settings.F7ControlsMicrophone)
                                microphone.ToggleMute();
                            stop.WaitOne(140);
                            float level;
                            bool muted;
                            microphone.Read(out level, out muted);
                            MicChanged(level, muted);
                            if (Program.Settings.MicrophoneEnabled)
                                Island.ShowHud(CompactNoticeKind.Microphone, L.T(muted ? "MicOff" : "MicOn"), 0, muted, false);
                        }
                    }
                    catch (Exception ex) { Program.Log("audio.txt", "Retrying: " + ex.GetType().Name); stop.WaitOne(2000); }
                }
            }
            finally { CanControlVolume = false; if (output != null) output.Dispose(); if (microphone != null) microphone.Dispose(); }
        }
        public void Dispose()
        {
            Current = null;
            stop.Set();
            worker.Join(4000);
        }
    }
}
