using WinDynamicIsland.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace WinDynamicIsland
{
    internal static class ApiProfile
    {
        internal static readonly string PathName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinDynamicIsland", "api-profile.bin");
        internal static readonly string Preferences = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Interhive OÜ", "Sota Connect", "shared_preferences.json");
        internal static readonly string Endpoint = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Sota Connect", "endpoint.json");
        internal static JavaScriptSerializer Json()
        {
            return new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
        }
        internal static string ReadShared(string path)
        {
            using (var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var r = new StreamReader(f))
                return r.ReadToEnd();
        }
        internal static string Fingerprint()
        {
            var p = Json().Deserialize<Dictionary<string, object>>(ReadShared(Preferences));
            var selected = new SortedDictionary<string, object>();
            foreach (var pair in p)
            {
                string k = pair.Key;
                if (k.StartsWith("flutter.kitten.") || k.StartsWith("flutter.SELECTED_") || k.StartsWith("flutter.SPLIT_")
                    || k == "flutter.VPN_MODE" || k == "flutter.ENCRYPTED_ACCESS_KEY" || k == "flutter.rule_sets_paths"
                    || k.IndexOf("DNS", StringComparison.OrdinalIgnoreCase) >= 0 || k.IndexOf("IPV6", StringComparison.OrdinalIgnoreCase) >= 0
                    || k.IndexOf("NETWORK_STACK", StringComparison.OrdinalIgnoreCase) >= 0 || k.IndexOf("KILL_SWITCH", StringComparison.OrdinalIgnoreCase) >= 0
                    || k.IndexOf("BYPASS_LAN", StringComparison.OrdinalIgnoreCase) >= 0 || k.IndexOf("LOCAL_DOMAINS", StringComparison.OrdinalIgnoreCase) >= 0)
                    selected[k] = pair.Value;
            }
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(Json().Serialize(selected))));
        }
        internal static void Validate(Dictionary<string, object> p)
        {
            foreach (string name in new[] { "configuration", "localOptionsJson" })
            {
                object v;
                if (!p.TryGetValue(name, out v) || !(v is string) || ((string)v).Length == 0)
                    throw new InvalidDataException(L.T("ProfileInvalid"));
                if (!(Json().DeserializeObject((string)v) is Dictionary<string, object>))
                    throw new InvalidDataException(L.T("ProfileInvalid"));
            }
        }
        internal static string AppVersion()
        {
            return System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SotaConnect", "SotaVPN.exe")).FileVersion;
        }
        internal static void Save(Dictionary<string, object> parameters)
        {
            Validate(parameters);
            var payload = new Dictionary<string, object> { { "parameters", parameters }, { "preferences", Fingerprint() }, { "version", AppVersion() }, { "capturedUtc", DateTime.UtcNow.ToString("o") } };
            byte[] plain = Encoding.UTF8.GetBytes(Json().Serialize(payload));
            try
            {
                byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                string temp = PathName + ".tmp";
                File.WriteAllBytes(temp, encrypted);
                if (File.Exists(PathName))
                    File.Replace(temp, PathName, null);
                else
                    File.Move(temp, PathName);
            }
            finally { Array.Clear(plain, 0, plain.Length); }
        }
        internal static Dictionary<string, object> Load()
        {
            if (!File.Exists(PathName))
                throw new InvalidOperationException(L.T("ProfileMissing"));
            byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(PathName), null, DataProtectionScope.CurrentUser);
            try
            {
                var p = Json().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(plain));
                if ((string)p["preferences"] != Fingerprint() || (string)p["version"] != AppVersion())
                    throw new InvalidOperationException(L.T("ProfileStale"));
                var parameters = (Dictionary<string, object>)p["parameters"];
                Validate(parameters);
                return parameters;
            }
            finally { Array.Clear(plain, 0, plain.Length); }
        }
    }

}
