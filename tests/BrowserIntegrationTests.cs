using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
namespace WinDynamicIsland {
    internal static class BrowserIntegrationTests {
        static int checks;
        static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);checks++;}
        static string Request(string root,string path,string token,string body=null){
            var request=(HttpWebRequest)WebRequest.Create(root+path);request.Proxy=null;request.Timeout=3000;request.Method=body==null?"GET":"POST";if(token!=null)request.Headers["Authorization"]="Bearer "+token;
            if(body!=null){var bytes=Encoding.UTF8.GetBytes(body);request.ContentType="application/json";request.ContentLength=bytes.Length;using(var stream=request.GetRequestStream())stream.Write(bytes,0,bytes.Length);}
            using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))return reader.ReadToEnd();
        }
        static void Main(){string directory=Path.Combine(Path.GetTempPath(),"Island-browser-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try{using(var bridge=new BrowserLoopback(0,directory)){
                string endpoint="http://127.0.0.1:"+bridge.BoundPort,token=File.ReadAllText(Path.Combine(directory,"browser-loopback-token.txt"));bool denied=false;
                try{Request(endpoint,"/ping",null);}catch(WebException ex){using(var response=(HttpWebResponse)ex.Response)denied=response.StatusCode==HttpStatusCode.Unauthorized;}
                Check(denied,"local bridge rejects unauthenticated requests");Check(Request(endpoint,"/ping",token).Contains("true"),"local bridge accepts its machine-specific token");
                Request(endpoint,"/state",token,"{\"sourceKey\":\"Chrome:1:0\",\"browser\":\"Chrome\",\"playing\":true,\"position\":10,\"duration\":90,\"title\":\"Test\",\"activatedAt\":100}");
                Request(endpoint,"/state",token,"{\"sourceKey\":\"Edge:2:0\",\"browser\":\"Edge\",\"playing\":false,\"position\":0,\"duration\":50,\"activatedAt\":200}");
                using(var media=BrowserMediaProvider.Read(directory)){Check(media!=null && media.Id.StartsWith("Chrome.") && media.Playing,"a paused browser cannot replace another browser's playing media");media.Command("seek",40);}
                Check(Request(endpoint,"/commands/Edge",token).Contains("[]"),"commands are not consumed by the wrong browser");
                string reply=Request(endpoint,"/commands/Chrome",token);Check(reply.Contains("seek") && reply.Contains("40"),"browser seek reaches the matching extension");
                Check(Request(endpoint,"/commands/Chrome",token).Contains("[]"),"media commands are delivered once");
                string state=Path.Combine(directory,"browser-media-Chrome.json");File.SetLastWriteTimeUtc(state,DateTime.UtcNow.AddMinutes(-1));var packet=WinDynamicIsland.Core.JsonCodec.Create().Deserialize<Dictionary<string,object>>(File.ReadAllText(state));packet["receivedTicks"]=DateTime.UtcNow.AddMinutes(-1).Ticks;File.WriteAllText(state,WinDynamicIsland.Core.JsonCodec.Create().Serialize(packet));
                using(var media=BrowserMediaProvider.Read(directory))Check(media!=null && media.Id.StartsWith("Edge."),"stale browser sessions expire");
            }Console.WriteLine(checks+" browser integration checks passed");}catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}
            finally{Thread.Sleep(100);foreach(string file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);}
        }
    }
}
