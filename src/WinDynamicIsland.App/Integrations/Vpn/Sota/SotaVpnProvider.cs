using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed class SotaVpnProvider : IVpnProvider
    {
        public string Name
        {
            get
            {
                return "Sota VPN";
            }
        }
        public VpnState GetState()
        {
            int state = Convert.ToInt32(Call("status", null)["state"]);
            return state == 2 ? VpnState.Connected : state == 0 ? VpnState.Disconnected : VpnState.Unknown;
        }
        public void Connect()
        {
            var parameters = ApiProfile.Load();
            if (GetState() != VpnState.Disconnected)
                throw new InvalidOperationException(L.T("Unavailable"));
            Call("start", parameters);
            Wait(VpnState.Connected, 90);
        }
        public void Disconnect()
        {
            if (GetState() != VpnState.Connected)
                throw new InvalidOperationException(L.T("Unavailable"));
            Call("stop", null);
            Wait(VpnState.Disconnected, 40);
        }
        void Wait(VpnState wanted, int attempts)
        {
            for (int i = 0; i < attempts; i++)
            {
                Thread.Sleep(350);
                if (GetState() == wanted)
                    return;
            }
            throw new TimeoutException(L.T("Unavailable"));
        }
        static Dictionary<string, object> Call(string method, object parameters)
        {
            var json = ApiProfile.Json();
            var endpoint = json.Deserialize<Dictionary<string, object>>(ApiProfile.ReadShared(ApiProfile.Endpoint));
            using (var client = new TcpClient())
            {
                var pending = client.BeginConnect("127.0.0.1", Convert.ToInt32(endpoint["port"]), null, null);
                using (pending.AsyncWaitHandle)
                {
                    if (!pending.AsyncWaitHandle.WaitOne(3000))
                        throw new TimeoutException(L.T("Unavailable"));
                    client.EndConnect(pending);
                }
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = method == "start" ? 30000 : 3000;
                    stream.WriteTimeout = 3000;
                    var reader = new StreamReader(stream);
                    var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                    writer.WriteLine(json.Serialize(new
                    {
                        id = 1,
                        method = "hello",
                        @params = new
                        {
                            protocolVersion = 1,
                            token = endpoint["token"]
                        }
                    }));
                    Read(reader, 1);
                    writer.WriteLine(json.Serialize(new
                    {
                        id = 2,
                        method = method,
                        @params = parameters ?? new
                        {
                        }
                    }));
                    return Read(reader, 2);
                }
            }
        }
        static Dictionary<string, object> Read(StreamReader reader, int id)
        {
            for (int i = 0; i < 50; i++)
            {
                string line = reader.ReadLine();
                if (line == null)
                    break;
                var reply = ApiProfile.Json().Deserialize<Dictionary<string, object>>(line);
                object value;
                if (!reply.TryGetValue("id", out value) || Convert.ToInt32(value) != id)
                    continue;
                if (reply.TryGetValue("error", out value) && value != null)
                    throw new IOException(L.T("Error"));
                if (reply.TryGetValue("result", out value) && value is Dictionary<string, object>)
                    return (Dictionary<string, object>)value;
                return new Dictionary<string, object>();
            }
            throw new IOException(L.T("Unavailable"));
        }
    }
}
