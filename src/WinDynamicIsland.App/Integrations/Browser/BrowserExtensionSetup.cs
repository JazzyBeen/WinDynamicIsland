using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
namespace WinDynamicIsland
{
    internal static class BrowserExtensionSetup
    {
        internal static string Prepare(bool open)
        {
            string template = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "browser-extension"), target = Path.Combine(Program.DataPath, "BrowserExtension");
            if (!Directory.Exists(template))
                throw new FileNotFoundException("Browser extension template is missing");
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(template))
            {
                if (Path.GetFileName(file) != "connection.js")
                    WriteIfChanged(Path.Combine(target, Path.GetFileName(file)), File.ReadAllText(file));
            }
            string secret = Path.Combine(Program.DataPath, "browser-loopback-token.txt");
            if (!File.Exists(secret))
            {
                var bytes = new byte[32];
                using (var random = RandomNumberGenerator.Create())
                    random.GetBytes(bytes);
                File.WriteAllText(secret, Convert.ToBase64String(bytes));
            }
            string token = File.ReadAllText(secret).Trim();
            var json = new JavaScriptSerializer();
            WriteIfChanged(Path.Combine(target, "connection.js"), "const ISLAND_ENDPOINT=\"http://127.0.0.1:48573\";\nconst ISLAND_TOKEN=" + json.Serialize(token) + ";\n");
            if (open)
                Process.Start("explorer.exe", target);
            return target;
        }
        static void WriteIfChanged(string path, string value)
        {
            if (File.Exists(path) && File.ReadAllText(path) == value) return;
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, value, new System.Text.UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}
