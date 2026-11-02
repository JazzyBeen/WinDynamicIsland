using System;
using System.IO;
using System.Threading;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal static class FocusTests
    {
        static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS: " + label); }
        [STAThread] static void Main()
        {
            try
            {
                var clock = new FocusTimer(); clock.Start(50, 10);
                Check(clock.Active && clock.Duration == 3000, "50/10 starts with a 50-minute focus interval");
                Check(!clock.Advance(2999) && clock.Position == 2999, "focus does not finish early");
                Check(clock.Advance(1) && clock.Resting && clock.Duration == 600 && clock.Cycles == 1, "focus transitions exactly into a ten-minute break");
                clock.TogglePause(); clock.Advance(100); Check(clock.Position == 0, "paused timer does not consume break time");
                clock.TogglePause(); Check(clock.Advance(600) && !clock.Resting && clock.Duration == 3000, "break resumes the next focus interval");
                clock.Seek(9000); Check(clock.Position < clock.Duration, "seeking cannot skip beyond the phase");
                clock.Seek(double.NaN); Check(!double.IsNaN(clock.Position), "invalid media seek cannot poison the timer");
                clock.Start(25, 5); clock.Advance(3700); Check(clock.Cycles == 2 && !clock.Resting && clock.Position == 100, "delayed ticks preserve phase boundaries and remaining time");
                clock.Stop(); clock.Advance(500); Check(!clock.Active, "Alt+B stop ends the session");
                var old = Program.Settings; Program.Settings = new AppSettings { FocusVolume = 0 };
                try
                {
                    using (var service = new FocusService())
                    {
                        FocusService.Toggle();
                        using (var snapshot = FocusService.Read()) { Check(snapshot != null && snapshot.CanToggle && snapshot.CanSeek, "focus exposes the same media control contract"); snapshot.Command("toggle", 0); }
                        using (var paused = FocusService.Read()) Check(!paused.Playing && paused.Title.Length > 0, "pause is reflected in the island snapshot");
                        FocusService.Toggle(); Check(FocusService.Read() == null, "stopping releases focus priority to other media");
                    }
                    Check(FocusService.Read() == null, "disposing focus removes the session");
                }
                finally { Program.Settings = old; }
                if (Environment.GetEnvironmentVariable("ISLAND_AUDIO_TEST") == "1")
                {
                    var player = new MciMusicPlayer();
                    try
                    {
                        var track = Directory.GetFiles(Path.Combine(FocusService.MusicPath, "Ambient"), "*.mp3")[0];
                        bool opened = player.Open(track, 0); Console.WriteLine("Audio decoder: " + player.LastError);
                        Check(opened, "downloaded MP3 opens at zero volume"); Thread.Sleep(300);
                        Check(player.IsPlaying, "Windows decoder plays the downloaded MP3"); player.Pause(); Check(player.IsPaused, "music pause works"); player.Resume(); Check(player.IsPlaying, "music resume works");
                    }
                    finally { player.Close(); }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex); Environment.ExitCode = 1; }
        }
    }
}
