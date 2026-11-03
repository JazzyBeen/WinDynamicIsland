using System;

namespace WinDynamicIsland
{
    internal static class FocusMotionTests
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        static bool Near(double a, double b) { return Math.Abs(a - b) < .000001; }
        [STAThread] static void Main()
        {
            try
            {
                var clock = new FocusMotionClock();
                Check(Near(clock.Read(5), 5), "idle texture uses the normal slow clock");
                clock.Pulse(5, .28);
                Check(Near(clock.Read(5), 5) && Near(clock.Speed(5), 1), "opening starts without a phase or speed jump");
                Check(clock.Speed(5.04) > 1 && clock.Speed(5.04) < 18, "opening eases into the faster texture motion");
                Check(Near(clock.Speed(5.30), 18), "motion remains accelerated briefly after the expansion");
                Check(clock.Speed(5.5) > clock.Speed(5.7) && clock.Speed(5.7) > 1, "post-opening motion slows down gradually");
                Check(Near(clock.Speed(5.8), 1), "texture returns to its original slow speed");
                double end = clock.Read(5.8);
                Check(end > 10 && Near(clock.Read(6.8), end + 1), "easing retains the traveled distance instead of snapping back");
                var oneStep = new FocusMotionClock(); oneStep.Pulse(0, .28);
                var manySteps = new FocusMotionClock(); manySteps.Pulse(0, .28);
                for (int i = 1; i <= 200; i++) manySteps.Read(i / 100d);
                Check(Near(oneStep.Read(2), manySteps.Read(2)), "texture displacement is independent of rendering frame rate");
                clock = new FocusMotionClock(); clock.Pulse(0, .28);
                double at = clock.Read(.3), speed = clock.Speed(.3);
                clock.Pulse(.3, .28);
                Check(Near(clock.Read(.3), at) && Near(clock.Speed(.3), speed), "rapid repeated opening preserves phase and speed continuity");
                Check(clock.Speed(.4) <= 18, "repeated opening cannot stack an unbounded speed boost");
                at = clock.Read(3);
                Check(Near(clock.Read(2), at), "an older render timestamp never moves texture backwards");
                VerifyOpening("WinDynamicIsland.Focus.test", true);
                VerifyOpening("SyntheticPlayer", false);
            }
            catch (Exception ex) { Console.WriteLine(ex); Environment.ExitCode = 1; }
        }
        static void VerifyOpening(string id, bool expectBoost)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            using (var island = (Island)Activator.CreateInstance(typeof(Island), true))
            {
                ((System.Windows.Forms.Timer)typeof(Island).GetField("timer", flags).GetValue(island)).Stop();
                Action<string, object> set = (name, value) => typeof(Island).GetField(name, flags).SetValue(island, value);
                set("mediaData", new Core.MediaSnapshot { Id = id });
                set("mediaFromWidth", 124d); set("mediaFromHeight", 40d);
                set("mediaMorphAt", DateTime.UtcNow.AddSeconds(-2));
                typeof(Island).GetMethod("MorphMedia", flags).Invoke(island, new object[] { true });
                var motion = (FocusMotionClock)typeof(Island).GetField("focusMotion", flags).GetValue(island);
                var wall = (System.Diagnostics.Stopwatch)typeof(Island).GetField("focusTextureClock", flags).GetValue(island);
                double speed = motion.Speed(wall.Elapsed.TotalSeconds + .15);
                Check(expectBoost ? speed > 1 : Near(speed, 1), expectBoost ? "expanding the focus island triggers background acceleration" : "ordinary media opening retains its original behavior");
            }
        }
    }
}
