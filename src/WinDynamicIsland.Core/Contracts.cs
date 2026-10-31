using System;
using System.Collections.Generic;
using System.Drawing;

namespace WinDynamicIsland.Core
{
    public enum NotificationApp
    {
        Telegram, Codex, System, Other
    }
    public enum CompactNoticeKind
    {
        None, Headphones, Battery, Charging, Volume, Microphone, CapsLock, KeyboardLayout
    }
    public sealed class MediaSnapshot : IDisposable
    {
        public string Id, Title, Artist, AppName;
        public bool Playing, Stopped, Foreground, CanToggle, CanSeek;
        public double Position, Start, End, Rate = 1;
        public Bitmap Icon, Artwork;
        public Action<string, double> Command;
        public void Dispose()
        {
            if (Icon != null)
                Icon.Dispose();
            if (Artwork != null)
                Artwork.Dispose();
        }
    }
    public interface IIslandPresenter
    {
        void Status(string title, string message, bool complete, bool error);
        void Notify(string title, string message, NotificationApp app, Bitmap avatar, Action presented);
        void Compact(CompactNoticeKind kind, string message, int value);
        void Media(MediaSnapshot snapshot);
    }
    public interface IIslandModule : IDisposable
    {
        string Id
        {
            get;
        }
        void Start();
    }
    public sealed class ModuleHost : IDisposable
    {
        readonly List<IIslandModule> modules = new List<IIslandModule>();
        readonly Action<string, Exception> report;
        public ModuleHost(Action<string, Exception> report)
        {
            this.report = report;
        }
        public void Add(IIslandModule module)
        {
            try
            {
                module.Start();
                modules.Add(module);
            }
            catch (Exception ex) { try { module.Dispose(); } catch { } report(module.Id, ex); }
        }
        public void Dispose()
        {
            for (int i = modules.Count - 1; i >= 0; i--)
                try
                {
                    modules[i].Dispose();
                }
                catch (Exception ex) { report(modules[i].Id, ex); }
            modules.Clear();
        }
    }
    public enum VpnState
    {
        Disconnected, Connected, Unknown
    }
    public interface IVpnProvider
    {
        string Name
        {
            get;
        }
        VpnState GetState(); void Connect(); void Disconnect();
    }
}
