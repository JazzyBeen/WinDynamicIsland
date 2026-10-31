using WinDynamicIsland.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

namespace WinDynamicIsland {
internal static class CaptureTests
{
    static void Require(bool value,string message) { if(!value) throw new Exception(message); Console.WriteLine("PASS: "+message); }
    static void Main() {
        string id;
        Require(CaptureProxy.StartParameters("{\"id\":1,\"method\":\"stop\",\"params\":{}}",out id)==null,"stop is never saved as a start profile");
        Require(CaptureProxy.StartParameters("{\"id\":1,\"method\":\"start\",\"params\":{}}",out id)==null,"incomplete profile is rejected");
        Run(false,false); Run(true,false); Run(false,true);
        byte[] plain=Encoding.UTF8.GetBytes("synthetic-config-only");
        byte[] protectedBytes=ProtectedData.Protect(plain,null,DataProtectionScope.CurrentUser);
        Require(Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes,null,DataProtectionScope.CurrentUser))=="synthetic-config-only","Windows user encryption round trip");
    }
    static void Run(bool reject,bool replaceEndpoint) {
        string path=Path.Combine(Path.GetTempPath(),"SotaCapture-test-"+Guid.NewGuid().ToString("N")+".json");
        var server=new TcpListener(IPAddress.Loopback,0); server.Start();
        int realPort=((IPEndPoint)server.LocalEndpoint).Port;
        string original=ApiProfile.Json().Serialize(new {port=realPort,token="test-only"});
        File.WriteAllText(path,original);
        var saved=new ManualResetEvent(false); int count=0;
        var service=new Thread(delegate() {
            try {
                using(var c=server.AcceptTcpClient()) {
                    var r=new StreamReader(c.GetStream()); var w=new StreamWriter(c.GetStream(),new UTF8Encoding(false)){AutoFlush=true};
                    string line;
                    while((line=r.ReadLine())!=null) {
                        var frame=ApiProfile.Json().Deserialize<Dictionary<string,object>>(line);
                        Interlocked.Increment(ref count);
                        if(object.Equals(frame["method"],"start") && reject) w.WriteLine(ApiProfile.Json().Serialize(new {id=frame["id"],error=new {message="synthetic rejection"}}));
                        else w.WriteLine(ApiProfile.Json().Serialize(new {id=frame["id"],result=new {ok=true}}));
                    }
                }
            } catch {}
        }){IsBackground=true}; service.Start();
        try {
            using(var proxy=new CaptureProxy(delegate(string s){},path,delegate(Dictionary<string,object> p){saved.Set();})) {
                proxy.Start();
                int port=Convert.ToInt32(ApiProfile.Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(path))["port"]);
                Require(port!=realPort,"proxy uses a separate loopback port");
                using(var c=new TcpClient("127.0.0.1",port)) {
                    c.GetStream().ReadTimeout=3000;
                    var r=new StreamReader(c.GetStream()); var w=new StreamWriter(c.GetStream(),new UTF8Encoding(false)){AutoFlush=true};
                    w.WriteLine("{\"id\":1,\"method\":\"hello\",\"params\":{\"token\":\"test-only\"}}");
                    Require(r.ReadLine().Contains("result"),"hello reply is forwarded unchanged");
                    if(replaceEndpoint) File.WriteAllText(path,"{\"port\":12345,\"token\":\"new-service-test-only\"}");
                    w.WriteLine(ApiProfile.Json().Serialize(new {id=2,method="start",@params=new {configuration="{}",localOptionsJson="{}"}}));
                    string reply=r.ReadLine();
                    if(reject) { Require(reply.Contains("error") && !saved.WaitOne(200),"rejected start is not saved"); }
                    else Require(reply.Contains("result") && saved.WaitOne(3000),"accepted exact start parameters are saved");
                }
            }
            Require(count==2,"only client commands reach the fake service");
            Require(replaceEndpoint ? File.ReadAllText(path).Contains("new-service-test-only") : File.ReadAllText(path)==original,
                replaceEndpoint ? "a new service endpoint is never overwritten" : "original endpoint restored after capture or cancellation");
        } finally { server.Stop(); saved.Dispose(); File.Delete(path); }
    }
}

}