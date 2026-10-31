using WinDynamicIsland.Core;
using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace WinDynamicIsland
{
    internal static class BrowserMediaProvider
    {
        static Bitmap icon; static string lastBrowser; static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 65536 };
        static string Text(Dictionary<string, object> state, string key, int limit = 200)
        {
            object value;
            if (!state.TryGetValue(key, out value))
                return "";
            string text = Convert.ToString(value);
            return text.Length > limit ? text.Substring(0, limit) : text;
        }
        static double Number(Dictionary<string, object> state, string key, double fallback = 0)
        {
            object value;
            double result;
            return state.TryGetValue(key, out value) && double.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result) && !double.IsNaN(result) && !double.IsInfinity(result) ? result : fallback;
        }
        internal static MediaSnapshot Read(string directory = null)
        {
            try
            {
                string folder = directory ?? Program.DataPath;
                var available = new List<Dictionary<string, object>>();
                foreach (string file in Directory.GetFiles(folder, "browser-media-*.json"))
                    try
                    {
                        var candidate = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(file));
                        double elapsed = (DateTime.UtcNow - new DateTime((long)Number(candidate, "receivedTicks"), DateTimeKind.Utc)).TotalSeconds;
                        if (elapsed >= 0 && elapsed <= 15)
                            available.Add(candidate);
                    }
                    catch { }
                var state = available.OrderByDescending(s => s.ContainsKey("playing") && Convert.ToBoolean(s["playing"])).ThenByDescending(s => Number(s, "activatedAt")).FirstOrDefault();
                if (state == null)
                    return null;
                double age = (DateTime.UtcNow - new DateTime((long)Number(state, "receivedTicks"), DateTimeKind.Utc)).TotalSeconds;
                string key = Text(state, "sourceKey", 80);
                if (age < 0 || age > 15 || key.Length == 0)
                    return null;
                bool playing = state.ContainsKey("playing") && Convert.ToBoolean(state["playing"]);
                double duration = Number(state, "duration"), position = Number(state, "position"), rate = Number(state, "rate", 1);
                if (rate <= 0 || rate > 16)
                    rate = 1;
                if (playing)
                    position += age * rate;
                string kind = Text(state, "browser");
                if (kind != "Chrome" && kind != "Edge")
                    kind = "Yandex";
                string id = kind + ".Island." + key, appName;
                if (icon == null || lastBrowser != kind)
                {
                    if (icon != null)
                        icon.Dispose();
                    icon = AppIdentity.IconFor(kind + ".Island", out appName);
                    lastBrowser = kind;
                }
                return new MediaSnapshot { Id = id, Title = Text(state, "title"), Artist = Text(state, "artist"), AppName = kind == "Yandex" ? (L.Language == "ru" ? "Яндекс Браузер" : "Yandex Browser") : kind, Playing = playing, Foreground = AppIdentity.IsForeground(id), CanToggle = true, CanSeek = duration > 0, Rate = rate, Start = 0, End = duration, Position = Math.Max(0, duration > 0 ? Math.Min(duration, position) : position), Icon = icon == null ? null : new Bitmap(icon), Command = (action, value) => Command(folder, key, action, value) };
            }
            catch { return null; }
        }
        static void Command(string folder, string key, string action, double value)
        {
            if (action != "toggle" && action != "seek")
                return;
            string path = Path.Combine(folder, "browser-command-" + DateTime.UtcNow.Ticks + "-" + Guid.NewGuid().ToString("N") + ".json");
            // Rename a completed packet so the host never reads a partial write.
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(new
            {
                type = "command",
                sourceKey = key,
                action = action,
                value = value
            }));
            File.Move(temporary, path);
        }
    }

}
