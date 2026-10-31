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
    internal static class WinRt
    {
        internal static T Wait<T>(IAsyncOperation<T> operation, int timeout = 3000)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                while (operation.Status == AsyncStatus.Started)
                {
                    if (watch.ElapsedMilliseconds > timeout)
                    {
                        operation.Cancel();
                        throw new TimeoutException("Windows operation timed out");
                    }
                    Thread.Sleep(10);
                }
                return operation.GetResults();
            }
            finally { try { operation.Close(); } catch { } }
        }
    }


}
