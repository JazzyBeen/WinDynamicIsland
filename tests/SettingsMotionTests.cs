using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
namespace WinDynamicIsland
{
    internal static class SettingsMotionTests
    {
        [DllImport("user32.dll", EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr window,int index);
        static readonly BindingFlags Fields=BindingFlags.NonPublic|BindingFlags.Instance;
        static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
        static T Field<T>(SettingsForm form,string name){return (T)typeof(SettingsForm).GetField(name,Fields).GetValue(form);}
        static void Page(SettingsForm form,string key){typeof(SettingsForm).GetMethod("ShowSection",Fields).Invoke(form,new object[]{key});Application.DoEvents();}
        static void Wait(Func<bool> condition){var time=Stopwatch.StartNew();while(!condition()&&time.ElapsedMilliseconds<2000){Application.DoEvents();Thread.Sleep(8);}Check(condition(),"animation settles without leaving a blocking overlay");}
        [STAThread] static void Main()
        {
            var original=Program.Settings;
            try
            {
                Application.EnableVisualStyles();Program.Settings=original.Clone();Program.Settings.InterfaceAnimations=true;
                using(var form=new SettingsForm(null))
                {
                    form.Show();Application.DoEvents();
                    var navigation=Field<AnimatedNavigationPanel>(form,"navigation");
                    var buttons=navigation.Controls.OfType<GlassButton>().ToArray();
                    Page(form,"Devices");Wait(()=>!form.PageAnimating);
                    var longPage=Field<IslandScrollPanel>(form,"body");
                    Check(longPage.MaximumOffset>0,"long settings pages remain scrollable");
                    Check(!longPage.AutoScroll && (GetWindowLong(longPage.Handle,-16)&0x00300000)==0,"page windows never create the white native scrollbars");
                    int firstTop=longPage.Controls[0].Top;longPage.ScrollTo(180);int position=longPage.ScrollOffset;
                    for(int i=0;i<4;i++)longPage.PerformLayout();
                    Check(position>0 && longPage.Controls[0].Top==firstTop && longPage.Top==-position,"repeated layout keeps card positions stable and translates one surface");
                    Page(form,"About");Check(Field<IslandScrollPanel>(form,"body").ScrollOffset==0,"a short page does not inherit the previous page scroll");
                    Page(form,"Devices");Check(Field<IslandScrollPanel>(form,"body").ScrollOffset==position,"each section remembers its own scroll position");
                    string[] sequence={"General","Appearance","Media","Notifications","Devices","VPN","About"};
                    var switching=Stopwatch.StartNew();for(int i=0;i<21;i++)Page(form,sequence[i%sequence.Length]);Console.WriteLine("Average rapid navigation: "+(switching.Elapsed.TotalMilliseconds/21).ToString("F1")+" ms");
                    Wait(()=>!form.PageAnimating);
                    Check(Field<string>(form,"section")=="About","rapid switches finish on the last selected section");
                    Check(Field<Panel>(form,"pageHost").Controls.Count==2,"rapid switches dispose the previous viewport without accumulating wrappers");
                    Check(buttons.SequenceEqual(navigation.Controls.OfType<GlassButton>())&&buttons.All(b=>!b.IsDisposed),"sidebar controls remain alive across tab changes");
                    Page(form,"Appearance");form.ClientSize=new System.Drawing.Size(860,660);Application.DoEvents();
                    var narrow=Field<IslandScrollPanel>(form,"body");
                    Check(narrow.Controls.Cast<Control>().All(c=>c.Right<=narrow.ClientSize.Width),"settings cards fit the narrowest supported window");
                    Check(narrow.Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<SelectionButton>()).All(c=>c.Right<c.Parent.Width),"dropdowns stay within their cards when resized");
                    Program.Settings.InterfaceAnimations=false;Page(form,"General");Check(!form.PageAnimating,"interface animations can be disabled without changing settings behavior");
                    form.CloseForExit();
                }
            }
            catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}
            finally{Program.Settings=original;}
        }
    }
}
