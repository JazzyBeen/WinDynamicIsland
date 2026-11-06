using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal static class IslandThemeMotionTests
    {
        const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
        static void Set(Island island, string name, object value) { typeof(Island).GetField(name,Instance).SetValue(island,value); }
        static RectangleF Frame(Island island, string method, DateTime time) { return (RectangleF)typeof(Island).GetMethod(method,Instance).Invoke(island,new object[]{time}); }
        static void Style(bool material) { Program.Settings.InterfaceDesign = material ? "Material" : "Classic"; AppDesign.Change(material,false); }
        static void Check(bool value, string message) { if(!value) throw new Exception(message); Console.WriteLine("PASS: "+message); }
        [STAThread] static void Main()
        {
            var original = Program.Settings;
            try
            {
                Program.Settings = new AppSettings();
                using(var island=(Island)Activator.CreateInstance(typeof(Island),true))
                {
                    ((Timer)typeof(Island).GetField("timer",Instance).GetValue(island)).Stop();
                    var now=DateTime.UtcNow;
                    double opening=(double)typeof(Island).GetProperty("OpeningMs",Static).GetValue(null,null);
                    Set(island,"compactKind",CompactNoticeKind.Headphones); Set(island,"appeared",now); Set(island,"changed",now); Set(island,"done",true);
                    foreach(double milliseconds in new[]{0d,80,140,200,280,opening,opening+70,opening+160,4500+80,4500+200,4500+opening})
                    {
                        Style(false); var classic=Frame(island,"MotionAt",now.AddMilliseconds(milliseconds));
                        Style(true); var material=Frame(island,"MotionAt",now.AddMilliseconds(milliseconds));
                        Check(classic==material,"both themes share compact-island geometry at "+milliseconds.ToString("F0")+" ms");
                    }
                    Set(island,"compactKind",CompactNoticeKind.None); Set(island,"notification",true);
                    foreach(double milliseconds in new[]{0d,140,200,280,opening+70})
                    {
                        Style(false); var classic=Frame(island,"MotionAt",now.AddMilliseconds(milliseconds));
                        Style(true); var material=Frame(island,"MotionAt",now.AddMilliseconds(milliseconds));
                        Check(classic==material,"both themes share notification timing and settling at "+milliseconds.ToString("F0")+" ms");
                    }
                    Set(island,"mediaEntering",false); Set(island,"mediaExiting",false); Set(island,"mediaFromWidth",124d); Set(island,"mediaFromHeight",40d); Set(island,"mediaFrom",0d); Set(island,"mediaTo",1d); Set(island,"mediaMorphAt",now); Set(island,"mediaPulseAt",now.AddMilliseconds(280));
                    for(int time=0;time<=280;time+=20)
                    {
                        Style(false); var classic=Frame(island,"MediaMotionAt",now.AddMilliseconds(time));
                        Style(true); var material=Frame(island,"MediaMotionAt",now.AddMilliseconds(time));
                        double a=(classic.Width-124)/(400-124d), b=(material.Width-124)/(416-124d);
                        Check(Math.Abs(a-b)<.000001,"media expansion uses the same easing and duration at "+time+" ms");
                    }
                    Set(island,"mediaTo",0d); Set(island,"mediaFrom",1d);
                    for(int time=0;time<=280;time+=40)
                    {
                        Style(false); Set(island,"mediaFromWidth",400d); Set(island,"mediaFromHeight",176d); var classic=Frame(island,"MediaMotionAt",now.AddMilliseconds(time));
                        Style(true); Set(island,"mediaFromWidth",416d); Set(island,"mediaFromHeight",200d); var material=Frame(island,"MediaMotionAt",now.AddMilliseconds(time));
                        Check(Math.Abs((classic.Width-124)/276d-(material.Width-124)/292d)<.000001,"media collapse preserves original timing at "+time+" ms");
                    }
                }
                Console.WriteLine("Island animation parity passed; no media or VPN connection was changed.");
            }
            catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}
            finally{Program.Settings=original;AppDesign.Change(original.InterfaceDesign=="Material",false);}
        }
    }
}
