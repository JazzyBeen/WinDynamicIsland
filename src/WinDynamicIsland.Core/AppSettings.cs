using System;
using System.IO;
using System.Web.Script.Serialization;

namespace WinDynamicIsland.Core
{
    public sealed class AppSettings
    {
        public string InterfaceDesign { get; set; }
        public string MaterialColorMode { get; set; }
        public string MaterialSeedColor { get; set; }
        public bool FocusEnabled { get; set; }
        public int FocusMinutes { get; set; }
        public int BreakMinutes { get; set; }
        public int FocusVolume { get; set; }
        public string FocusPlaylist { get; set; }
        public bool FocusBreakMusic { get; set; }
        public bool InterfaceAnimations
        {
            get; set;
        }
        public string NotificationMode
        {
            get; set;
        }
        public string[] NotificationApps
        {
            get; set;
        }
        public bool NotificationSelectionConfigured
        {
            get; set;
        }
        public bool GlassStyle
        {
            get; set;
        }
        public bool VolumeEnabled
        {
            get; set;
        }
        public bool ReplaceSystemVolumeFlyout
        {
            get; set;
        }
        public bool MicrophoneEnabled
        {
            get; set;
        }
        public bool F7MicrophoneIndicator
        {
            get; set;
        }
        public bool F7ControlsMicrophone
        {
            get; set;
        }
        public bool CapsLockEnabled
        {
            get; set;
        }
        public bool KeyboardLayoutEnabled
        {
            get; set;
        }
        public bool VpnStatusEnabled
        {
            get; set;
        }
        public string VpnAdapterId
        {
            get; set;
        }
        public int SchemaVersion
        {
            get; set;
        }
        public string Language
        {
            get; set;
        }
        public bool StartWithWindows
        {
            get; set;
        }
        public bool ShowStartupIsland
        {
            get; set;
        }
        public bool MediaEnabled
        {
            get; set;
        }
        public bool BrowserEnabled
        {
            get; set;
        }
        public bool MediaBackgroundOnly
        {
            get; set;
        }
        public int MediaCollapseSeconds
        {
            get; set;
        }
        public bool TelegramEnabled
        {
            get; set;
        }
        public bool CodexEnabled
        {
            get; set;
        }
        public bool CodexBackgroundOnly
        {
            get; set;
        }
        public bool SuppressWindowsBanners
        {
            get; set;
        }
        public bool BatteryEnabled
        {
            get; set;
        }
        public bool HeadphonesEnabled
        {
            get; set;
        }
        public bool ChargingEnabled
        {
            get; set;
        }
        public int BatteryThreshold
        {
            get; set;
        }
        public int TopGap
        {
            get; set;
        }
        public int MotionBlur
        {
            get; set;
        }
        public bool SettleAnimation
        {
            get; set;
        }
        public int MonitorIndex
        {
            get; set;
        }
        public int NotificationSeconds
        {
            get; set;
        }
        public bool VpnEnabled
        {
            get; set;
        }
        public string VpnProvider
        {
            get; set;
        }
        public string VpnName
        {
            get; set;
        }
        public string WindowsVpnProfile
        {
            get; set;
        }
        public string ConnectExecutable
        {
            get; set;
        }
        public string ConnectArguments
        {
            get; set;
        }
        public string DisconnectExecutable
        {
            get; set;
        }
        public string DisconnectArguments
        {
            get; set;
        }
        public string StatusExecutable
        {
            get; set;
        }
        public string StatusArguments
        {
            get; set;
        }
        public bool VpnHotkeyEnabled
        {
            get; set;
        }
        public AppSettings()
        {
            InterfaceDesign = "Classic";
            MaterialColorMode = "Default";
            MaterialSeedColor = "#6750A4";
            FocusEnabled = true;
            FocusMinutes = 50;
            BreakMinutes = 10;
            FocusVolume = 35;
            FocusPlaylist = "Ambient";
            SchemaVersion = 1;
            Language = "ru";
            StartWithWindows = true;
            ShowStartupIsland = true;
            MediaEnabled = BrowserEnabled = MediaBackgroundOnly = true;
            MediaCollapseSeconds = 5;
            TelegramEnabled = CodexEnabled = CodexBackgroundOnly = SuppressWindowsBanners = true;
            BatteryEnabled = HeadphonesEnabled = ChargingEnabled = true;
            BatteryThreshold = 20;
            TopGap = 8;
            MotionBlur = 28;
            SettleAnimation = true;
            MonitorIndex = 0;
            NotificationSeconds = 6;
            VpnProvider = "None";
            VpnName = "VPN";
            WindowsVpnProfile = "";
            ConnectExecutable = ConnectArguments = DisconnectExecutable = DisconnectArguments = StatusExecutable = StatusArguments = "";
            VpnHotkeyEnabled = true;
            NotificationMode = "Selected";
            NotificationApps = new string[0];
            GlassStyle = VolumeEnabled = MicrophoneEnabled = F7MicrophoneIndicator = true;
            InterfaceAnimations = true;
            ReplaceSystemVolumeFlyout = true;
            VpnAdapterId = "";
        }
        public void Validate()
        {
            InterfaceDesign = InterfaceDesign == "Material" ? "Material" : "Classic";
            MaterialColorMode = MaterialColorMode == "Custom" || MaterialColorMode == "Wallpaper" ? MaterialColorMode : "Default";
            int colorValue;
            if (MaterialSeedColor == null || MaterialSeedColor.Length != 7 || MaterialSeedColor[0] != '#' || !int.TryParse(MaterialSeedColor.Substring(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out colorValue)) MaterialSeedColor = "#6750A4";
            MaterialSeedColor = MaterialSeedColor.ToUpperInvariant();
            FocusMinutes = Clamp(FocusMinutes, 1, 120);
            BreakMinutes = Clamp(BreakMinutes, 1, 60);
            FocusVolume = Clamp(FocusVolume, 0, 100);
            FocusPlaylist = FocusPlaylist == "Lofi" ? "Lofi" : "Ambient";
            SchemaVersion = 1;
            Language = Language == "en" ? "en" : "ru";
            TopGap = Clamp(TopGap, 2, 48);
            MotionBlur = Clamp(MotionBlur, 0, 48);
            MonitorIndex = Math.Max(0, MonitorIndex);
            NotificationSeconds = Clamp(NotificationSeconds, 2, 20);
            MediaCollapseSeconds = Clamp(MediaCollapseSeconds, 2, 15);
            BatteryThreshold = Clamp(BatteryThreshold, 5, 50);
            if (VpnProvider != "Sota" && VpnProvider != "Windows" && VpnProvider != "Command" && VpnProvider != "Adapter")
                VpnProvider = "None";
            NotificationMode = NotificationMode == "All" ? "All" : "Selected";
            if (NotificationMode == "All")
                SuppressWindowsBanners = true;
            NotificationApps = NotificationApps ?? new string[0];
            VpnAdapterId = VpnAdapterId ?? "";
            VpnName = string.IsNullOrWhiteSpace(VpnName) ? "VPN" : VpnName.Trim();
        }
        static int Clamp(int value, int low, int high)
        {
            return Math.Max(low, Math.Min(high, value));
        }
        public AppSettings Clone()
        {
            var json = new JavaScriptSerializer();
            return json.Deserialize<AppSettings>(json.Serialize(this));
        }
    }
    public sealed class SettingsStore
    {
        readonly string path;
        public SettingsStore(string path)
        {
            this.path = path;
        }
        public AppSettings Load()
        {
            if (!File.Exists(path))
                return new AppSettings();
            try
            {
                var value = new JavaScriptSerializer().Deserialize<AppSettings>(File.ReadAllText(path));
                if (value == null)
                    throw new InvalidDataException();
                value.Validate();
                return value;
            }
            catch
            {
                try { File.Copy(path, path + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true); } catch { }
                try
                {
                    var backup = new JavaScriptSerializer().Deserialize<AppSettings>(File.ReadAllText(path + ".backup"));
                    if (backup != null) { backup.Validate(); return backup; }
                }
                catch { }
                return new AppSettings();
            }
        }
        public void Save(AppSettings settings)
        {
            settings.Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(settings));
            if (File.Exists(path))
                File.Replace(temporary, path, path + ".backup");
            else
                File.Move(temporary, path);
        }
    }
}
