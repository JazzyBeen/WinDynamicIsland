using System;
using System.Windows.Forms;
using Microsoft.Win32;
namespace WinDynamicIsland
{
    internal static class StartupRegistration
    {
        const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal static bool IsEnabled
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(Run))
                    return key != null && string.Equals(key.GetValue("WinDynamicIsland") as string, Command, StringComparison.Ordinal);
            }
        }
        static string Command
        {
            get
            {
                return "\"" + Application.ExecutablePath + "\" --background";
            }
        }
        internal static void SetEnabled(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(Run))
            {
                if (enabled)
                    key.SetValue("WinDynamicIsland", Command, RegistryValueKind.String);
                else
                    key.DeleteValue("WinDynamicIsland", false);
            }
        }
    }
}
