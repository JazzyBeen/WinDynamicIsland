using WinDynamicIsland.Core;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;

namespace WinDynamicIsland {
internal static class MediaIntegrationTests {
    static readonly object Gate=new object();static MediaSnapshot latest;
    static void Require(Func<bool> predicate,string name,int timeout=10000) {
        var watch=Stopwatch.StartNew();while(!predicate() && watch.ElapsedMilliseconds<timeout){Application.DoEvents();Thread.Sleep(50);}
        if(!predicate())throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);
    }
    static void PresentationChecks() {
        var now=DateTime.UtcNow;var oldStamp=new DateTimeOffset(now.AddMinutes(-1));
        var clock=new MediaTimelineClock();
        Require(()=>Math.Abs(clock.Read("check",10,oldStamp,true,1,now)-10)<0.01,"live positions do not double-count old Windows timestamps");
        clock.Read("check",11,oldStamp,true,1,now.AddSeconds(1));
        Require(()=>Math.Abs(clock.Read("check",11,oldStamp,true,1,now.AddSeconds(1.5))-11.5)<0.01,"static Windows positions interpolate between updates");
        var view=new MediaPresentation();view.Accept(new MediaSnapshot{Id="check",Position=10,Playing=true,End=90},now);
        Require(()=>Math.Abs(view.PositionAt(now.AddSeconds(1))-11)<0.01,"timeline advances smoothly between snapshots");
        view.Seek(45,now.AddSeconds(1));view.Accept(new MediaSnapshot{Id="check",Position=10.5,Playing=true,End=90},now.AddSeconds(1.1));
        Require(()=>Math.Abs(view.PositionAt(now.AddSeconds(1.2))-45.2)<0.01,"stale player updates cannot undo a pending seek");
        view.Accept(new MediaSnapshot{Id="check",Position=45.3,Playing=true,End=90},now.AddSeconds(1.3));
        Require(()=>Math.Abs(view.PositionAt(now.AddSeconds(1.5))-45.5)<0.01,"seek acknowledgement rejoins the playback clock");
        AnimationChecks(now);
    }
    static void Set(Island island,string field,object value){typeof(Island).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(island,value);}
    static RectangleF Frame(Island island,string method,DateTime time){return (RectangleF)typeof(Island).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(island,new object[]{time});}
    static void AnimationChecks(DateTime now) {
        using(var island=(Island)Activator.CreateInstance(typeof(Island),true)) {
            ((System.Windows.Forms.Timer)typeof(Island).GetField("timer",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(island)).Stop();
            double opening=(double)typeof(Island).GetProperty("OpeningMs",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null,null);
            Set(island,"compactKind",CompactNoticeKind.Headphones);Set(island,"appeared",now);Set(island,"changed",now);Set(island,"done",true);
            var growing=Frame(island,"MotionAt",now.AddMilliseconds(200));
            Require(()=>growing.Width>64 && growing.Width<260 && growing.Height>24 && growing.Height<40,"compact notices grow in width and height while emerging");
            var closing=Frame(island,"MotionAt",now.AddTicks((long)((4500+opening-200)*TimeSpan.TicksPerMillisecond)));
            Require(()=>Math.Abs(growing.Width-closing.Width)<0.01 && Math.Abs(growing.Y-closing.Y)<0.01,"compact dismissal reverses the same animated path");
            var pulse=Frame(island,"MotionAt",now.AddMilliseconds(opening+70));
            Require(()=>pulse.Height>41.9 && pulse.Width<255,"compact notices retain the soft settling pulse");
            Set(island,"mediaMode",true);Set(island,"mediaEntering",true);Set(island,"mediaTo",0d);Set(island,"mediaAppeared",now);Set(island,"mediaPulseAt",now.AddMilliseconds(opening));
            var small=Frame(island,"MediaMotionAt",now.AddMilliseconds(200));
            Require(()=>small.Width>64 && small.Width<124 && small.Height<40,"compact media grows instead of only sliding");
            Set(island,"mediaEntering",false);Set(island,"mediaFromWidth",124d);Set(island,"mediaFromHeight",40d);Set(island,"mediaFrom",0d);Set(island,"mediaTo",1d);Set(island,"mediaMorphAt",now);Set(island,"mediaPulseAt",now.AddMilliseconds(280));
            var half=Frame(island,"MediaMotionAt",now.AddMilliseconds(140));
            Require(()=>Math.Abs(half.Width-262)<0.01 && Math.Abs(half.Height-108)<0.01,"small to large transition passes continuously through the midpoint");
            Set(island,"mediaFromWidth",400d);Set(island,"mediaFromHeight",176d);Set(island,"mediaFrom",1d);Set(island,"mediaTo",0d);
            var reverse=Frame(island,"MediaMotionAt",now.AddMilliseconds(140));
            Require(()=>Math.Abs(half.Width-reverse.Width)<0.01 && Math.Abs(half.Height-reverse.Height)<0.01,"large to small transition follows the reverse geometry");
            Set(island,"mediaTo",1d);
            typeof(Island).GetMethod("MediaOutsideClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(island,new object[]{new Point(0,500)});
            Require(()=>(double)typeof(Island).GetField("mediaTo",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(island)==0,"outside click collapses media immediately");
        }
    }
    [STAThread]
    static void Main(string[] args) {
        string wave=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-silence.wav");
        try {
            if(args.Length==0)PresentationChecks();
            using(var writer=new BinaryWriter(File.Create(wave))) {
                int bytes=8000*2*90;writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(bytes+36);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(8000);writer.Write(16000);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(bytes);writer.Write(new byte[bytes]);
            }
            using(var player=new MediaPlayer()) {
                player.Volume=0;
                var item=new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(wave)));
                var display=item.GetDisplayProperties();display.Type=MediaPlaybackType.Music;display.MusicProperties.Title="Проверка острова";display.MusicProperties.Artist="Беззвучный тест";item.ApplyDisplayProperties(display);player.Source=item;
                player.SystemMediaTransportControls.DisplayUpdater.Type=MediaPlaybackType.Music;
                player.SystemMediaTransportControls.DisplayUpdater.MusicProperties.Title="Island integration check";
                player.SystemMediaTransportControls.DisplayUpdater.Update();player.Play();
                if(args.Length>0 && args[0]=="--fixture") {Console.WriteLine("Silent media fixture ready");Thread.Sleep(85000);player.Pause();return;}
                using(var integration=new MediaSessionMonitor(s=>{lock(Gate){if(latest!=null)latest.Dispose();latest=s;}},false,"MediaIntegrationTests")) {
                    Require(()=>{lock(Gate)return latest!=null && latest.Playing && latest.CanToggle;},"live Windows media session discovered");
                    lock(Gate){Console.WriteLine("Source="+latest.Id+"; Seek="+latest.CanSeek);latest.Command("toggle",0);}
                    Require(()=>player.PlaybackSession.PlaybackState==MediaPlaybackState.Paused,"pause reaches the actual media player");
                    Require(()=>{lock(Gate)return latest!=null && !latest.Playing && latest.CanSeek;},"paused session and seek capability remain available");
                    lock(Gate)latest.Command("seek",30);
                    Require(()=>Math.Abs(player.PlaybackSession.Position.TotalSeconds-30)<1,"seek changes actual playback position");
                    Require(()=>{lock(Gate)return latest!=null && Math.Abs(latest.Position-30)<1;},"island timeline confirms the new position");
                    lock(Gate)latest.Command("seek",40);
                    Require(()=>Math.Abs(player.PlaybackSession.Position.TotalSeconds-40)<1,"forward command moves playback by ten seconds");
                    Require(()=>{lock(Gate)return latest!=null && Math.Abs(latest.Position-40)<1;},"forward seek updates the island timeline");
                    lock(Gate)latest.Command("seek",30);
                    Require(()=>Math.Abs(player.PlaybackSession.Position.TotalSeconds-30)<1,"backward command moves playback by ten seconds");
                    lock(Gate)latest.Command("toggle",0);
                    Require(()=>player.PlaybackSession.PlaybackState==MediaPlaybackState.Playing,"resume reaches the actual media player");
                    player.Pause();
                }
            }
            string appName;using(var icon=AppIdentity.IconFor("MSEdge",out appName))Require(()=>icon!=null,"installed application icon resolves from Windows");
            using(var icon=AppIdentity.IconFor("Yandex._test-app.Default",out appName))Require(()=>icon!=null,"Yandex sessions resolve the actual browser icon");
        }catch(Exception ex){Console.WriteLine(ex.ToString());Environment.ExitCode=1;}
        finally {lock(Gate){if(latest!=null){latest.Dispose();latest=null;}}if(File.Exists(wave))File.Delete(wave);}
    }
}

}