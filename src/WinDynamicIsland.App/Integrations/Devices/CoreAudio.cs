using System;
using System.Runtime.InteropServices;
namespace WinDynamicIsland
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumerator
    {
    }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int flow, int mask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr callback); int UnregisterEndpointNotificationCallback(IntPtr callback);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid id, int context, IntPtr activation, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        int OpenPropertyStore(int mode, out IntPtr properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }
    [ComVisible(true), Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(IntPtr notification);
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
        int GetChannelCount(out uint count); int SetMasterVolumeLevel(float level, ref Guid context); [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        int GetMasterVolumeLevel(out float level); [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel, float level, ref Guid context); int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        int GetChannelVolumeLevel(uint channel, out float level); int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
        int GetVolumeStepInfo(out uint step, out uint count); int VolumeStepUp(ref Guid context); int VolumeStepDown(ref Guid context); int QueryHardwareSupport(out uint mask); int GetVolumeRange(out float low, out float high, out float step);
    }
    [StructLayout(LayoutKind.Sequential)]
    struct AudioNotification
    {
        internal Guid Context; [MarshalAs(UnmanagedType.Bool)] internal bool Muted; internal float Volume; internal uint Channels;
    }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    internal sealed class VolumeCallback : IAudioEndpointVolumeCallback
    {
        readonly Action<float, bool> changed;
        internal VolumeCallback(Action<float, bool> changed)
        {
            this.changed = changed;
        }
        public int OnNotify(IntPtr pointer)
        {
            try
            {
                var info = (AudioNotification)Marshal.PtrToStructure(pointer, typeof(AudioNotification));
                changed(info.Volume, info.Muted);
            }
            catch { }
            return 0;
        }
    }
    internal sealed class AudioEndpoint : IDisposable
    {
        readonly IMMDeviceEnumerator enumerator;
        readonly IMMDevice device;
        readonly IAudioEndpointVolume volume;
        readonly VolumeCallback callback;
        internal string Id
        {
            get; private set;
        }
        internal AudioEndpoint(int flow, Action<float, bool> changed)
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            try
            {
                IMMDevice found;
                Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(flow, flow == 1 ? 2 : 0, out found));
                device = found;
                string id;
                Marshal.ThrowExceptionForHR(device.GetId(out id));
                Id = id;
                Guid iid = typeof(IAudioEndpointVolume).GUID;
                object instance;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, IntPtr.Zero, out instance));
                volume = (IAudioEndpointVolume)instance;
                if (changed != null)
                {
                    callback = new VolumeCallback(changed);
                    Marshal.ThrowExceptionForHR(volume.RegisterControlChangeNotify(callback));
                }
            }
            catch { Dispose(); throw; }
        }
        internal void Read(out float level, out bool muted)
        {
            Marshal.ThrowExceptionForHR(volume.GetMasterVolumeLevelScalar(out level));
            Marshal.ThrowExceptionForHR(volume.GetMute(out muted));
        }
        internal void ToggleMute()
        {
            bool muted;
            float level;
            Read(out level, out muted);
            Guid context = Guid.Empty;
            Marshal.ThrowExceptionForHR(volume.SetMute(!muted, ref context));
        }
        internal void Step(int direction)
        {
            float level;
            bool muted;
            Read(out level, out muted);
            Guid context = Guid.Empty;
            if (muted)
                Marshal.ThrowExceptionForHR(volume.SetMute(false, ref context));
            Marshal.ThrowExceptionForHR(volume.SetMasterVolumeLevelScalar(Math.Max(0, Math.Min(1, level + direction * .02f)), ref context));
        }
        internal bool IsDefault(int flow)
        {
            IMMDevice current = null;
            try
            {
                if (enumerator.GetDefaultAudioEndpoint(flow, flow == 1 ? 2 : 0, out current) < 0)
                    return false;
                string id;
                current.GetId(out id);
                return id == Id;
            }
            catch { return false; }
            finally { if (current != null) Marshal.ReleaseComObject(current); }
        }
        public void Dispose()
        {
            if (volume != null)
            {
                if (callback != null)
                    try
                    {
                        volume.UnregisterControlChangeNotify(callback);
                    }
                    catch { }
                Marshal.ReleaseComObject(volume);
            }
            if (device != null)
                Marshal.ReleaseComObject(device);
            if (enumerator != null)
                Marshal.ReleaseComObject(enumerator);
        }
    }
}
