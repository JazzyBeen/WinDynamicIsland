using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland {
    internal static class SettingsUiTests {
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: "+message);}
        static FlowLayoutPanel Body(SettingsForm form){return (FlowLayoutPanel)typeof(SettingsForm).GetField("body",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);}
        [STAThread]static void Main(){
            try{Application.EnableVisualStyles();AppSettings saved=null;
                using(var form=new SettingsForm(s=>saved=s)){
                    form.Show();Application.DoEvents();
                    var language=Body(form).Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<SelectionButton>()).Single();
                    language.PerformClick();Application.DoEvents();var menu=(ContextMenuStrip)typeof(SelectionButton).GetField("menu",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(language);
                    using(var preview=new System.Drawing.Bitmap(menu.Width,menu.Height)){menu.DrawToBitmap(preview,new System.Drawing.Rectangle(0,0,menu.Width,menu.Height));preview.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"menu-preview.png"));}
                    menu.Close();Application.DoEvents();Check(!menu.IsDisposed,"closing a dropdown keeps its menu alive for the next click");
                    language.PerformClick();Application.DoEvents();menu.Items[1].PerformClick();Application.DoEvents();
                    Check(L.Language=="en","selecting a language safely rebuilds settings after the menu click");
                    var section=typeof(SettingsForm).GetMethod("ShowSection",BindingFlags.NonPublic|BindingFlags.Instance);
                    foreach(string name in new[]{"General","Appearance","Media","Notifications","Devices","VPN","About"}){section.Invoke(form,new object[]{name});Application.DoEvents();Check(Body(form).Controls.Count>0,name+" settings page renders");}
                    section.Invoke(form,new object[]{"VPN"});var provider=Body(form).Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<SelectionButton>()).Single();provider.SelectIndex(3);Application.DoEvents();Check(Body(form).Controls.OfType<Panel>().Count(p=>p.Controls.OfType<TextBox>().Any())==7,"custom VPN exposes all command and status fields");
                    section.Invoke(form,new object[]{"General"});typeof(SettingsForm).GetMethod("Save",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,new object[]{null,EventArgs.Empty});Check(saved!=null && saved.Language=="en" && saved.VpnProvider=="Command","Save forwards the complete edited configuration");
                    form.Close();Application.DoEvents();Check(!form.Visible && !form.IsDisposed,"window close hides settings instead of disposing the background app UI");
                    form.ReloadSaved();form.Show();Application.DoEvents();Check(form.Visible,"hidden settings can be restored from the tray");form.CloseForExit();
                }
            }catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}
        }
    }
}
