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
    internal sealed class DeviceMonitor : IDisposable
    {
        readonly Thread worker; volatile bool stopping;
        internal DeviceMonitor()
        {
            worker = new Thread(DeviceLoop) { IsBackground = true, Name = "Island devices" };
            worker.Start();
        }
        void DeviceLoop()
        {
            var connected = new HashSet<ulong>();
            bool initialized = false;
            DateTime nextBluetooth = DateTime.MinValue;
            while (!stopping)
            {

                try
                {
                    BatteryAlerts.Poll();
                    if (Program.Settings.BatteryEnabled && Program.Settings.SuppressWindowsBanners && BatteryAlerts.CurrentPercent >= 0)
                        BatteryBannerPolicy.Ensure();
                    else
                        BatteryBannerPolicy.Restore();
                }
                catch (Exception ex) { try { BatteryBannerPolicy.Restore(); } catch { } Program.Log("battery-listener.txt", "retrying; " + ex.GetType().Name); }
                if (Program.Settings.HeadphonesEnabled && DateTime.UtcNow >= nextBluetooth)
                {
                    try
                    {
                        var next = new HashSet<ulong>();
                        var devices = WinRt.Wait(DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true)));
                        foreach (var info in devices)
                        {
                            using (var device = WinRt.Wait(BluetoothDevice.FromIdAsync(info.Id)))
                            {
                                if (device == null || device.ClassOfDevice.MajorClass != BluetoothMajorClass.AudioVideo || device.ConnectionStatus != BluetoothConnectionStatus.Connected)
                                    continue;
                                next.Add(device.BluetoothAddress);
                                if (initialized && !connected.Contains(device.BluetoothAddress))
                                {
                                    Program.Presenter.Compact(CompactNoticeKind.Headphones, device.Name, -1);
                                    Program.Log("bluetooth-listener.txt", "running; audio device connected; compact notice queued");
                                }
                            }
                        }
                        connected = next;
                        initialized = true;
                        if (!File.Exists(Path.Combine(Program.DataPath, "bluetooth-listener.txt")))
                            Program.Log("bluetooth-listener.txt", "running; connected audio devices=" + next.Count);
                    }
                    catch (Exception ex) { Program.Log("bluetooth-listener.txt", "retrying; " + ex.GetType().Name); }
                    nextBluetooth = DateTime.UtcNow.AddSeconds(3);
                }
                Sleep(800);
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
            worker.Join(4000);
            BatteryBannerPolicy.Restore();
        }
    }

}
