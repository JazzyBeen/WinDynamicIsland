using System;
using System.Diagnostics;
using System.IO;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class SettingsForm
    {
        void FocusSettings()
        {
            Help("FocusHelp");
            Check("FocusEnabled", draft.FocusEnabled, v => draft.FocusEnabled = v);
            string[] names = { "50 / 10", "25 / 5", "90 / 20", L.T("Custom") };
            int index = draft.FocusMinutes == 50 && draft.BreakMinutes == 10 ? 0 : draft.FocusMinutes == 25 && draft.BreakMinutes == 5 ? 1 : draft.FocusMinutes == 90 && draft.BreakMinutes == 20 ? 2 : 3;
            Choice("FocusPreset", names, index, i => { if (i == 0) { draft.FocusMinutes = 50; draft.BreakMinutes = 10; } else if (i == 1) { draft.FocusMinutes = 25; draft.BreakMinutes = 5; } else if (i == 2) { draft.FocusMinutes = 90; draft.BreakMinutes = 20; } }, false);
            Number("FocusMinutes", draft.FocusMinutes, 1, 120, v => draft.FocusMinutes = v);
            Number("BreakMinutes", draft.BreakMinutes, 1, 60, v => draft.BreakMinutes = v);
            Choice("FocusPlaylist", new[] { "Ambient · 12", "Lo-fi · 12" }, draft.FocusPlaylist == "Lofi" ? 1 : 0, i => draft.FocusPlaylist = i == 1 ? "Lofi" : "Ambient", false);
            Number("FocusVolume", draft.FocusVolume, 0, 100, v => draft.FocusVolume = v);
            Check("FocusBreakMusic", draft.FocusBreakMusic, v => draft.FocusBreakMusic = v);
            Help("FocusMusicHelp");
            body.Controls.Add(Button(L.T("FocusToggle"), () => { if (PersistDraft()) FocusService.Toggle(); }));
            body.Controls.Add(Button(L.T("MusicFolder"), () => { Directory.CreateDirectory(FocusService.MusicPath); Process.Start("explorer.exe", FocusService.MusicPath); }));
        }
    }
}
