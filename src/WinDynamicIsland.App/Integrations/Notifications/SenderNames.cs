using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
namespace WinDynamicIsland
{
    internal static class SenderNames
    {
        static readonly object gate = new object(); static Dictionary<string, string> names;
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)] static extern int SHLoadIndirectString(string source, StringBuilder output, uint length, IntPtr reserved);
        static object Call(object instance, string member, BindingFlags mode, params object[] args)
        {
            return instance.GetType().InvokeMember(member, mode, null, instance, args);
        }
        internal static void EnsureLoaded()
        {
            lock (gate)
            {
                if (names != null)
                    return;
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                object shell = null, folder = null, items = null;
                try
                {
                    shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                    folder = Call(shell, "NameSpace", BindingFlags.InvokeMethod, "shell:AppsFolder");
                    items = Call(folder, "Items", BindingFlags.InvokeMethod);
                    foreach (object item in (IEnumerable)items)
                        try
                        {
                            string id = Convert.ToString(Call(item, "Path", BindingFlags.GetProperty));
                            string name = Convert.ToString(Call(item, "Name", BindingFlags.GetProperty));
                            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                                result[id] = Resolve(name);
                        }
                        catch { }
                        finally { if (Marshal.IsComObject(item)) Marshal.ReleaseComObject(item); }
                }
                catch { }
                finally { if (items != null && Marshal.IsComObject(items)) Marshal.ReleaseComObject(items); if (folder != null && Marshal.IsComObject(folder)) Marshal.ReleaseComObject(folder); if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell); }
                names = result;
            }
        }
        internal static string Find(string id)
        {
            lock (gate)
            {
                string name;
                return names != null && names.TryGetValue(id, out name) ? name : null;
            }
        }
        internal static string Resolve(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("@"))
                return text;
            var output = new StringBuilder(1024);
            return SHLoadIndirectString(text, output, 1024, IntPtr.Zero) == 0 ? output.ToString() : null;
        }
    }
}
