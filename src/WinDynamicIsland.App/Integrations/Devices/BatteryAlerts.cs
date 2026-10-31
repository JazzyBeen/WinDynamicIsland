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
    internal static class BatteryAlerts
    {
        [StructLayout(LayoutKind.Sequential)]
        struct PowerStatus
        {
            internal byte AC, Flags, Percent, Reserved; internal uint Life, FullLife;
        }
        [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out PowerStatus status);
        static int lastAC = -1; static int warnedBand = 100; static string lastState = "";
        static readonly object Gate = new object(); static int lastWarningPercent = -1; static DateTime lastWarning;
        internal static int CurrentPercent
        {
            get
            {
                PowerStatus p;
                return GetSystemPowerStatus(out p) && p.Percent <= 100 ? p.Percent : -1;
            }
        }
        internal static void Poll()
        {
            PowerStatus p;
            if (!GetSystemPowerStatus(out p) || p.Percent > 100 || (p.Flags & 128) != 0)
                return;
            if (lastAC >= 0 && lastAC != p.AC && Program.Settings.ChargingEnabled)
                Program.Presenter.Compact(CompactNoticeKind.Charging, L.T(p.AC == 1 ? "ChargingOn" : "ChargingOff"), p.Percent);
            lastAC = p.AC;
            if (!Program.Settings.BatteryEnabled)
                return;
            string state = "running; percent=" + p.Percent + "; onAC=" + (p.AC == 1);
            if (state != lastState)
            {
                Program.Log("battery-listener.txt", state);
                lastState = state;
            }
            if (p.AC == 1 || p.Percent > Program.Settings.BatteryThreshold + 5)
            {
                warnedBand = 100;
                return;
            }
            int band = p.Percent <= 2 ? 2 : p.Percent <= 5 ? 5 : p.Percent <= 10 ? 10 : p.Percent <= Program.Settings.BatteryThreshold ? Program.Settings.BatteryThreshold : 100;
            if (band < warnedBand)
            {
                warnedBand = band;
                ShowWarning(p.Percent);
            }
        }
        internal static void ShowWarning(int percent)
        {
            lock (Gate)
            {
                if (percent == lastWarningPercent && (DateTime.UtcNow - lastWarning).TotalMinutes < 5)
                    return;
                lastWarningPercent = percent;
                lastWarning = DateTime.UtcNow;
            }
            Program.Presenter.Compact(CompactNoticeKind.Battery, L.T("LowBattery"), percent);
        }
    }

}
