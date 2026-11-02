using System;
namespace WinDynamicIsland.Core
{
    // Clock-independent state machine: deterministic tests, no audio or UI dependency.
    public sealed class FocusTimer
    {
        public bool Active { get; private set; }
        public bool Paused { get; private set; }
        public bool Resting { get; private set; }
        public int Cycles { get; private set; }
        public double Duration { get; private set; }
        public double Position { get; private set; }
        double work, rest;
        public void Start(int minutes, int breakMinutes)
        {
            work = Math.Max(1, minutes) * 60d;
            rest = Math.Max(1, breakMinutes) * 60d;
            Active = true; Paused = Resting = false; Cycles = 0;
            Duration = work; Position = 0;
        }
        public void Stop() { Active = Paused = false; }
        public void TogglePause() { if (Active) Paused = !Paused; }
        public void Seek(double seconds) { if (Active && !double.IsNaN(seconds) && !double.IsInfinity(seconds)) Position = Math.Max(0, Math.Min(Duration - .01, seconds)); }
        public bool Advance(double seconds)
        {
            if (!Active || Paused || seconds <= 0) return false;
            Position += seconds;
            bool changed = false;
            while (Position >= Duration)
            {
                Position -= Duration; Resting = !Resting;
                if (Resting) Cycles++;
                Duration = Resting ? rest : work;
                changed = true;
            }
            return changed;
        }
    }
}
