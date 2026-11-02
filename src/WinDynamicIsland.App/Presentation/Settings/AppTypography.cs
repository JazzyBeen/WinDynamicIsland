using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
namespace WinDynamicIsland
{
    internal static class AppTypography
    {
        static readonly PrivateFontCollection fonts = new PrivateFontCollection();
        static readonly List<IntPtr> buffers = new List<IntPtr>();
        static readonly FontFamily family;
        [DllImport("gdi32.dll")] static extern IntPtr AddFontMemResourceEx(IntPtr data, uint length, IntPtr reserved, ref uint count);
        static AppTypography()
        {
            try
            {
                foreach (string name in new[] { "InterRegular", "InterBold" })
                {
                    using (var stream = typeof(AppTypography).Assembly.GetManifestResourceStream(name))
                    {
                        if (stream == null) continue;
                        var bytes = new byte[(int)stream.Length];
                        int offset = 0, read;
                        while (offset < bytes.Length && (read = stream.Read(bytes, offset, bytes.Length - offset)) > 0) offset += read;
                        var memory = Marshal.AllocHGlobal(bytes.Length); Marshal.Copy(bytes, 0, memory, bytes.Length);
                        // Keep the buffer alive for GDI+ and register privately for TextRenderer/GDI.
                        buffers.Add(memory); fonts.AddMemoryFont(memory, bytes.Length);
                        uint count = 0; AddFontMemResourceEx(memory, (uint)bytes.Length, IntPtr.Zero, ref count);
                    }
                }
                if (fonts.Families.Length > 0) family = fonts.Families[0];
            }
            catch { }
        }
        internal static Font Create(float size, FontStyle style = FontStyle.Regular)
        {
            return family == null ? new Font("Segoe UI", size, style) : new Font(family, size, style, GraphicsUnit.Point);
        }
    }
}
