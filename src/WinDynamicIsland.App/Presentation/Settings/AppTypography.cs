using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
namespace WinDynamicIsland
{
    internal static class AppTypography
    {
        static readonly Dictionary<string, string> paths = ExtractFonts();
        static readonly ThreadLocal<FontSet> sets = new ThreadLocal<FontSet>(() => new FontSet());
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern int AddFontResourceEx(string path, uint flags, IntPtr reserved);
        static Dictionary<string, string> ExtractFonts()
        {
            var result = new Dictionary<string, string>();
            string folder = Path.Combine(Program.DataPath, "font-cache");
            Directory.CreateDirectory(folder);
            foreach (string name in new[] { "InterRegular", "InterBold", "GoogleSansRegular", "GoogleSansBold", "MaterialIcons" })
            {
                using (var stream = typeof(AppTypography).Assembly.GetManifestResourceStream(name))
                {
                    if (stream == null) continue;
                    var bytes = new byte[(int)stream.Length];
                    int offset = 0, read;
                    while (offset < bytes.Length && (read = stream.Read(bytes, offset, bytes.Length - offset)) > 0) offset += read;
                    string hash;
                    using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes),0,8).Replace("-", "");
                    string file = Path.Combine(folder, name + "-" + hash + ".ttf");
                    if (!File.Exists(file))
                    {
                        string temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        File.WriteAllBytes(temporary, bytes);
                        try { File.Move(temporary,file); } catch (IOException) { if (!File.Exists(file)) throw; File.Delete(temporary); }
                    }
                    // FR_PRIVATE: available to GDI/TextRenderer in this process only.
                    AddFontResourceEx(file,0x10,IntPtr.Zero);
                    result[name] = file;
                }
            }
            return result;
        }
        // GDI+ font-family handles must never be shared by the settings and island
        // rendering threads. File-backed collections also survive DPI cloning.
        sealed class FontSet
        {
            readonly PrivateFontCollection classic = new PrivateFontCollection(), material = new PrivateFontCollection(), symbols = new PrivateFontCollection();
            internal readonly FontFamily Classic, Material, Symbols;
            internal FontSet()
            {
                classic.AddFontFile(paths["InterRegular"]); classic.AddFontFile(paths["InterBold"]);
                material.AddFontFile(paths["GoogleSansRegular"]); material.AddFontFile(paths["GoogleSansBold"]);
                symbols.AddFontFile(paths["MaterialIcons"]);
                Classic = classic.Families[0]; Material = material.Families[0]; Symbols = symbols.Families[0];
            }
        }
        internal static Font Create(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(AppDesign.Material ? sets.Value.Material : sets.Value.Classic, size, style, GraphicsUnit.Point);
        }
        internal static Font Island(float size, FontStyle style = FontStyle.Regular)
        {
            return AppDesign.Material ? Create(size, style) : new Font("Segoe UI", size, style);
        }
        internal static Font Symbol(float size)
        {
            return new Font(sets.Value.Symbols, size, FontStyle.Regular, GraphicsUnit.Pixel);
        }
    }
}
