using System;
using System.Diagnostics;
using System.IO;
internal static class LegacyVpnLauncher {
    static void Main(string[] args){
        string action=args.Length==0 || args[0]=="--toggle"?"--toggle-vpn":args[0]=="--listen"?"--background":args[0];
        if(action!="--toggle-vpn" && action!="--background" && action!="--check-api" && action!="--check" && action!="--settings"){Environment.ExitCode=2;return;}
        string executable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","WinDynamicIsland","WinDynamicIsland.exe");
        if(File.Exists(executable))Process.Start(new ProcessStartInfo(executable,action){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
    }
}
