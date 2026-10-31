using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using WinDynamicIsland.Core;
namespace WinDynamicIsland {
    internal static class SettingsTests {
        static int checks;
        static void Check(bool condition,string label){if(!condition)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
        sealed class FakeModule:IIslandModule {public string Id{get{return "fake";}}internal bool Fail,Started,Disposed;public void Start(){Started=true;if(Fail)throw new InvalidOperationException();}public void Dispose(){Disposed=true;}}
        static void Main(string[] args){
            if(args.Length==3 && args[0]=="--fixture"){if(args[2]=="connect")File.WriteAllText(args[1],"on");else if(args[2]=="disconnect" && File.Exists(args[1]))File.Delete(args[1]);Environment.ExitCode=args[2]=="status"?(File.Exists(args[1])?0:1):0;return;}
            string folder=Path.Combine(Path.GetTempPath(),"WinDynamicIsland-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try{
                var store=new SettingsStore(Path.Combine(folder,"settings.json"));var defaults=store.Load();
                Check(!defaults.VpnEnabled && defaults.VpnProvider=="None","a new laptop has no Sota dependency");
                defaults.Language="en";defaults.TopGap=12;defaults.MediaCollapseSeconds=4;defaults.VpnProvider="Windows";defaults.WindowsVpnProfile="Work VPN";store.Save(defaults);
                var restored=store.Load();Check(restored.Language=="en" && restored.TopGap==12 && restored.WindowsVpnProfile=="Work VPN","settings round-trip includes language, animation and VPN");
                restored.TopGap=900;restored.MotionBlur=-5;restored.Language="unknown";restored.VpnProvider="anything";restored.Validate();Check(restored.TopGap==48 && restored.MotionBlur==0 && restored.Language=="ru" && restored.VpnProvider=="None","invalid settings are bounded safely");
                File.WriteAllText(Path.Combine(folder,"settings.json"),"{broken");Check(store.Load().Language=="ru" && Directory.GetFiles(folder,"*.invalid-*").Length==1,"corrupt settings are preserved and recover to defaults");
                L.Language="ru";string ru=L.T("Startup");L.Language="en";Check(L.T("Startup")!=ru && L.T("LowBattery")=="Low battery","Russian and English switch without restart");L.Language="ru";
                var fault=new FakeModule{Fail=true};var good=new FakeModule();int failures=0;using(var host=new ModuleHost((id,error)=>failures++)){host.Add(fault);host.Add(good);Check(failures==1 && fault.Disposed && good.Started,"a broken module does not stop other islands");}Check(good.Disposed,"module host disposes active features");
                var config=new AppSettings{VpnProvider="Command",VpnName="Test VPN",ConnectExecutable=Process.GetCurrentProcess().MainModule.FileName,DisconnectExecutable=Process.GetCurrentProcess().MainModule.FileName,StatusExecutable=Process.GetCurrentProcess().MainModule.FileName};
                string state=Path.Combine(folder,"vpn-state");config.ConnectArguments="--fixture \""+state+"\" connect";config.DisconnectArguments="--fixture \""+state+"\" disconnect";config.StatusArguments="--fixture \""+state+"\" status";
                var vpn=VpnProviderFactory.Create(config);Check(vpn.GetState()==VpnState.Disconnected,"custom CLI reads a disconnected fixture");vpn.Connect();Check(vpn.GetState()==VpnState.Connected,"custom CLI connects and confirms fixture status");vpn.Disconnect();Check(vpn.GetState()==VpnState.Disconnected,"custom CLI disconnects the isolated fixture");
                Check(new WindowsVpnProvider("WinDynamicIsland-absent-profile","Test").GetState()==VpnState.Disconnected,"Windows RAS profiles can be inspected without changing connections");
                Console.WriteLine(checks+" settings and architecture checks passed");
            }catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}
            finally{foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
        }
    }
}
