using WinDynamicIsland.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// Read-only access, restricted by the caller to explicitly supported app IDs.
namespace WinDynamicIsland
{
    internal static class NotificationStore
    {
        internal static readonly string PathName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Notifications", "wpndatabase.db");
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] path, out IntPtr db, int flags, IntPtr vfs);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_busy_timeout(IntPtr db, int ms);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int length, out IntPtr statement, IntPtr tail);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr statement);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr statement);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_column_count(IntPtr statement);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_name(IntPtr statement, int index);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr statement, int index);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_column_bytes(IntPtr statement, int index);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_column_type(IntPtr statement, int index);
        static string Utf8(IntPtr pointer, int length = -1)
        {
            if (pointer == IntPtr.Zero)
                return "";
            if (length < 0)
            {
                length = 0;
                while (Marshal.ReadByte(pointer, length) != 0)
                    length++;
            }
            byte[] bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
        internal static List<Dictionary<string, string>> Query(string sql)
        {
            IntPtr db = IntPtr.Zero, statement = IntPtr.Zero;
            try
            {
                if (sqlite3_open_v2(Encoding.UTF8.GetBytes(PathName + "\0"), out db, 1, IntPtr.Zero) != 0)
                    throw new IOException("Notification store unavailable");
                sqlite3_busy_timeout(db, 100);
                byte[] query = Encoding.UTF8.GetBytes(sql + "\0");
                if (sqlite3_prepare_v2(db, query, query.Length, out statement, IntPtr.Zero) != 0)
                    throw new IOException("Unsupported notification schema");
                var result = new List<Dictionary<string, string>>();
                int code;
                while ((code = sqlite3_step(statement)) == 100)
                {
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < sqlite3_column_count(statement); i++)
                        row[Utf8(sqlite3_column_name(statement, i))] = Utf8(sqlite3_column_text(statement, i), sqlite3_column_bytes(statement, i));
                    result.Add(row);
                }
                if (code != 101)
                    throw new IOException("Notification store busy");
                return result;
            }
            finally { if (statement != IntPtr.Zero) sqlite3_finalize(statement); if (db != IntPtr.Zero) sqlite3_close(db); }
        }
    }

}
