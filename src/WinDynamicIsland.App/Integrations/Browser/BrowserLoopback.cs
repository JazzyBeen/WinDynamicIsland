using WinDynamicIsland.Core;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

// Local authenticated IPC for the browser extension. Only loopback is bound.
namespace WinDynamicIsland
{
    internal sealed class BrowserLoopback : IDisposable
    {
        internal const int Port = 48573;
        readonly TcpListener listener;
        readonly string dataPath;
        internal int BoundPort
        {
            get
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
        }
        readonly Thread worker; readonly string token;
        readonly object stateGate = new object(), commandGate = new object();
        readonly SemaphoreSlim clients = new SemaphoreSlim(4);
        volatile bool stopping;
        internal BrowserLoopback(int port = Port, string directory = null)
        {
            dataPath = directory ?? Program.DataPath;
            Directory.CreateDirectory(dataPath);
            listener = new TcpListener(IPAddress.Loopback, port);
            string path = Path.Combine(dataPath, "browser-loopback-token.txt");
            if (!File.Exists(path))
            {
                var bytes = new byte[32];
                using (var random = RandomNumberGenerator.Create())
                    random.GetBytes(bytes);
                File.WriteAllText(path, Convert.ToBase64String(bytes));
            }
            token = File.ReadAllText(path).Trim();
            listener.Start();
            worker = new Thread(Listen) { IsBackground = true, Name = "Island browser local channel" };
            worker.Start();
            Program.Log("browser-loopback.txt", "Ready; loopback only; authenticated requests required");
        }
        void Listen()
        {
            while (!stopping)
            {
                try
                {
                    if (!listener.Pending())
                    {
                        Thread.Sleep(30);
                        continue;
                    }
                    var client = listener.AcceptTcpClient();
                    if (!clients.Wait(0))
                    {
                        client.Close();
                        continue;
                    }
                    ThreadPool.QueueUserWorkItem(_ => { try { Handle(client); } catch { } finally { client.Close(); clients.Release(); } });
                }
                catch { if (!stopping) Thread.Sleep(100); }
            }
        }
        void Handle(TcpClient client)
        {
            client.ReceiveTimeout = 1500;
            client.SendTimeout = 1500;
            using (var stream = client.GetStream())
            {
                var header = new StringBuilder();
                int previous = 0;
                while (header.Length < 16384)
                {
                    int b = stream.ReadByte();
                    if (b < 0)
                        return;
                    header.Append((char)b);
                    previous = (previous << 8) | b;
                    if (previous == 0x0d0a0d0a)
                        break;
                }
                string[] lines = header.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None);
                string[] request = lines[0].Split(' ');
                if (request.Length < 2)
                    return;
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length; i++)
                {
                    int split = lines[i].IndexOf(':');
                    if (split > 0)
                        headers[lines[i].Substring(0, split)] = lines[i].Substring(split + 1).Trim();
                }
                if (request[0] == "OPTIONS")
                {
                    Respond(stream, 200, "{}");
                    return;
                }
                string authorization;
                headers.TryGetValue("Authorization", out authorization);
                if (authorization != "Bearer " + token)
                {
                    Respond(stream, 401, "{}");
                    return;
                }
                var json = new JavaScriptSerializer { MaxJsonLength = 65536 };
                if (request[0] == "GET" && request[1] == "/ping")
                {
                    Respond(stream, 200, "{\"connected\":true}");
                    return;
                }
                if (request[0] == "POST" && request[1] == "/state")
                {
                    string rawLength;
                    int length;
                    headers.TryGetValue("Content-Length", out rawLength);
                    if (!int.TryParse(rawLength, out length) || length <= 0 || length > 65536 || headers.ContainsKey("Transfer-Encoding"))
                    {
                        Respond(stream, 400, "{}");
                        return;
                    }
                    var bytes = new byte[length];
                    int count = 0;
                    while (count < length)
                    {
                        int got = stream.Read(bytes, count, length - count);
                        if (got == 0)
                            return;
                        count += got;
                    }
                    var state = json.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(bytes));
                    if (!state.ContainsKey("sourceKey") || !state.ContainsKey("playing") || !(state["playing"] is bool))
                    {
                        Respond(stream, 400, "{}");
                        return;
                    }
                    state["receivedTicks"] = DateTime.UtcNow.Ticks;
                    state["owner"] = "loopback";
                    string kind = state.ContainsKey("browser") ? Convert.ToString(state["browser"]) : "";
                    if (kind != "Chrome" && kind != "Edge" && kind != "Yandex")
                        kind = "state";
                    string path = Path.Combine(dataPath, "browser-media-" + kind + ".json");
                    lock (stateGate)
                    {
                        string temporary = path + ".loopback.tmp";
                        File.WriteAllText(temporary, json.Serialize(state), new UTF8Encoding(false));
                        if (File.Exists(path))
                            File.Replace(temporary, path, null);
                        else
                            File.Move(temporary, path);
                    }
                    Respond(stream, 200, "{}");
                    return;
                }
                if (request[0] == "GET" && (request[1] == "/commands" || request[1] == "/commands/Chrome" || request[1] == "/commands/Edge" || request[1] == "/commands/Yandex"))
                {
                    string kind = request[1] == "/commands" ? "Yandex" : request[1].Substring("/commands/".Length);
                    var commands = new List<object>();
                    lock (commandGate)
                    {
                        var files = Directory.GetFiles(dataPath, "browser-command-*.json");
                        Array.Sort(files, StringComparer.Ordinal);
                        foreach (var file in files)
                        {
                            try
                            {
                                if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(file)).TotalSeconds >= 5)
                                {
                                    File.Delete(file);
                                    continue;
                                }
                                var packet = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(file));
                                object value;
                                string key = packet.TryGetValue("sourceKey", out value) ? Convert.ToString(value) : "";
                                bool legacy = key.Length > 0 && char.IsDigit(key[0]);
                                if (!key.StartsWith(kind + ":", StringComparison.Ordinal) && !(kind == "Yandex" && legacy))
                                    continue;
                                commands.Add(packet);
                                File.Delete(file);
                            }
                            catch { }
                        }
                    }
                    Respond(stream, 200, json.Serialize(new
                    {
                        commands = commands
                    }));
                    return;
                }
                Respond(stream, 404, "{}");
            }
        }
        static void Respond(Stream stream, int status, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            string header = "HTTP/1.1 " + status + " OK\r\nContent-Type: application/json\r\nContent-Length: " + bytes.Length + "\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Authorization, Content-Type\r\nAccess-Control-Allow-Private-Network: true\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
            var head = Encoding.ASCII.GetBytes(header);
            stream.Write(head, 0, head.Length);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }
        public void Dispose()
        {
            stopping = true;
            listener.Stop();
            worker.Join(500);
        }
    }

}
