using WinDynamicIsland.Core;
using System;
namespace WinDynamicIsland {
internal static class TelegramNotificationTests
{
    static void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS: "+name); }
    static void Main() {
        string title,body;
        Check(DesktopNotifications.Parse("<toast><visual><binding template='ToastGeneric'><text>Чат</text><text>Привет &amp; спасибо</text><text>Ещё строка</text></binding></visual></toast>",out title,out body) && title=="Чат" && body=="Привет & спасибо\nЕщё строка","Telegram title, text and XML entities");
        Check(!DesktopNotifications.Parse("<!DOCTYPE toast [<!ENTITY x SYSTEM 'file:///C:/test.txt'>]><toast><visual><binding><text>&x;</text></binding></visual></toast>",out title,out body),"external XML entities are rejected");
        Check(!DesktopNotifications.Parse("<badge value='5'/>",out title,out body),"non-message payloads are ignored");
        Check(!DesktopNotifications.Parse("not xml",out title,out body),"malformed payload is ignored");
        string avatar;
        Check(DesktopNotifications.Parse("<toast><visual><binding><image placement='appLogoOverride' src='file:///C:/Users/test/avatar.png'/><text>Test</text><text>Message</text></binding></visual></toast>",out title,out body,out avatar) && avatar=="file:///C:/Users/test/avatar.png","sender avatar comes from appLogoOverride");
        Check(DesktopNotifications.LoadAvatar("https://example.invalid/private.jpg")==null && DesktopNotifications.LoadAvatar("file://server/share/a.png")==null,"remote images and network shares are never requested");
        var a=new System.Collections.Generic.Dictionary<string,string>{{"Id","1"},{"ArrivalTime","100"},{"Payload","first"},{"Order","900"}};
        var b=new System.Collections.Generic.Dictionary<string,string>{{"Id","1"},{"ArrivalTime","101"},{"Payload","second"},{"Order","1"}};
        Check(DesktopNotifications.Identity(a)!=DesktopNotifications.Identity(b),"new notification detected when Windows reuses lower row order and ID");
        var original=DesktopNotifications.Identity(a); a["Order"]="1";
        Check(original==DesktopNotifications.Identity(a),"unchanged notification is not replayed after row reorder");
        var baseline=NotificationStore.Query("SELECT COALESCE(MAX(n.[Order]),0) AS LastOrder FROM Notification n JOIN NotificationHandler h ON n.HandlerId=h.RecordId WHERE h.PrimaryId='"+DesktopNotifications.AppId+"'");
        Check(baseline.Count==1,"current Windows notification store readable in read-only mode");
    }
}

}