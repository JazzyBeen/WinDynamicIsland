using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace WinDynamicIsland
{
    // An animation timer that ticks once per display refresh. A WinForms timer
    // with a 16 ms interval is rounded up to two 15.6 ms system ticks and runs at
    // about 40 Hz with 45 ms gaps; this one follows the compositor instead.
    // Ticks never queue up: the next one is posted only after the previous ran.
    // It stays a WinForms Timer, so existing callers keep Start/Stop/Tick.
    internal sealed class FrameTimer : System.Windows.Forms.Timer
    {
        SynchronizationContext context;
        readonly SendOrPostCallback deliver;
        bool enabled, disposed, fallback;
        int pending;

        internal FrameTimer()
        {
            // Used only where no compositor clock is reachable: one 15.6 ms tick.
            Interval = 15;
            deliver = Deliver;
        }

        public override bool Enabled
        {
            get { return enabled; }
            set
            {
                if (value == enabled || value && disposed)
                    return;
                enabled = value;
                if (!value)
                {
                    FramePump.Remove(this);
                    if (fallback)
                    {
                        fallback = false;
                        base.Enabled = false;
                    }
                    return;
                }
                var current = SynchronizationContext.Current;
                if (current != null)
                    context = current;
                if (context == null || !FramePump.Add(this))
                {
                    fallback = true;
                    base.Enabled = true;
                }
            }
        }

        // Called on the pump thread at each vertical blank.
        internal void Frame()
        {
            if (Interlocked.Exchange(ref pending, 1) == 1)
                return;
            context.Post(deliver, null);
        }

        void Deliver(object state)
        {
            Interlocked.Exchange(ref pending, 0);
            if (enabled && !disposed)
                OnTick(EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Enabled = false;
                disposed = true;
            }
            base.Dispose(disposing);
        }
    }

    internal static class FramePump
    {
        static readonly object Sync = new object();
        static readonly List<FrameTimer> Timers = new List<FrameTimer>();
        static Thread thread;

        internal static bool Add(FrameTimer timer)
        {
            lock (Sync)
            {
                if (!Timers.Contains(timer))
                    Timers.Add(timer);
                if (thread == null)
                {
                    thread = new Thread(Run) { IsBackground = true, Name = "Frame pump", Priority = ThreadPriority.AboveNormal };
                    thread.Start();
                }
                Monitor.PulseAll(Sync);
                return true;
            }
        }

        internal static void Remove(FrameTimer timer)
        {
            lock (Sync)
                Timers.Remove(timer);
        }

        static void Run()
        {
            var clock = Stopwatch.StartNew();
            double last = 0;
            while (true)
            {
                FrameTimer[] active;
                lock (Sync)
                {
                    while (Timers.Count == 0)
                        Monitor.Wait(Sync);
                    active = Timers.ToArray();
                }
                last = WaitForFrame(clock, last);
                foreach (var timer in active)
                {
                    try { timer.Frame(); }
                    // The owner's thread or window is gone; it can no longer tick.
                    catch (Exception) { Remove(timer); }
                }
            }
        }

        static double WaitForFrame(Stopwatch clock, double last)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (!Flush())
                    break;
                double now = clock.Elapsed.TotalMilliseconds;
                // DwmFlush can return at once right after a present; wait for the next one.
                if (now - last >= 4)
                    return now;
            }
            double elapsed = clock.Elapsed.TotalMilliseconds - last;
            Thread.Sleep((int)Math.Max(1, Math.Min(16, 16.6 - elapsed)));
            return clock.Elapsed.TotalMilliseconds;
        }

        static bool flushBroken;
        static bool Flush()
        {
            if (flushBroken)
                return false;
            try { return DwmFlush() >= 0; }
            catch (DllNotFoundException) { flushBroken = true; }
            catch (EntryPointNotFoundException) { flushBroken = true; }
            return false;
        }

        [DllImport("dwmapi.dll")] static extern int DwmFlush();
    }

    // DateTime.UtcNow on .NET Framework follows the system tick, which can be as
    // coarse as 15.6 ms; motion sampled with it stutters even at 60 frames per
    // second. This reads the same wall clock with sub-microsecond resolution.
    internal static class PreciseClock
    {
        static bool unavailable;
        internal static DateTime UtcNow
        {
            get
            {
                if (!unavailable)
                {
                    try
                    {
                        long time;
                        GetSystemTimePreciseAsFileTime(out time);
                        return DateTime.FromFileTimeUtc(time);
                    }
                    catch (EntryPointNotFoundException) { unavailable = true; }
                }
                return DateTime.UtcNow;
            }
        }
        [DllImport("kernel32.dll")] static extern void GetSystemTimePreciseAsFileTime(out long time);
    }
}
