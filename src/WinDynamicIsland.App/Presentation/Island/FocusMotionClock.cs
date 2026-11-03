using System;

namespace WinDynamicIsland
{
    // Integrate the speed envelope instead of multiplying wall time: speeding up
    // and easing back down must never jump the gradient or particle positions.
    internal sealed class FocusMotionClock
    {
        const double Rise = .08, Tail = .42, PeakExtra = 17;
        double lastTime, position, pulseStart, hold, initialExtra;
        bool pulsing;

        internal double Read(double time)
        {
            if (double.IsNaN(time) || double.IsInfinity(time)) return position;
            time = Math.Max(lastTime, time);
            position += time - lastTime;
            if (pulsing) position += Integral(time - pulseStart) - Integral(lastTime - pulseStart);
            lastTime = time;
            return position;
        }
        internal void Pulse(double time, double openingSeconds)
        {
            Read(time);
            double currentExtra = Extra(lastTime - pulseStart);
            pulseStart = lastTime;
            hold = Math.Max(Rise, Math.Min(1, openingSeconds)) + .08;
            initialExtra = currentExtra;
            pulsing = true;
        }
        internal double Speed(double time) { return 1 + Extra(Math.Max(lastTime, time) - pulseStart); }
        double Extra(double age)
        {
            if (!pulsing || age < 0) return 0;
            if (age < Rise) return initialExtra + (PeakExtra - initialExtra) * Smooth(age / Rise);
            if (age < hold) return PeakExtra;
            return PeakExtra * (1 - Smooth((age - hold) / Tail));
        }
        double Integral(double age)
        {
            if (age <= 0) return 0;
            double u = Math.Min(1, age / Rise);
            double value = initialExtra * Math.Min(age, Rise) + (PeakExtra - initialExtra) * Rise * (u * u * u - .5 * u * u * u * u);
            value += PeakExtra * Math.Max(0, Math.Min(age, hold) - Rise);
            if (age > hold)
            {
                u = Math.Min(1, (age - hold) / Tail);
                value += PeakExtra * Tail * (u - u * u * u + .5 * u * u * u * u);
            }
            return value;
        }
        static double Smooth(double t) { t = Math.Max(0, Math.Min(1, t)); return t * t * (3 - 2 * t); }
    }
}
