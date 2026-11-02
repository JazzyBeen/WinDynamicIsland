using System.Web.Script.Serialization;
namespace WinDynamicIsland.Core
{
    public static class JsonCodec
    {
        public static string Serialize(object value) { return Create().Serialize(value); }
        public static JavaScriptSerializer Create()
        {
            return new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
        }
    }
}
