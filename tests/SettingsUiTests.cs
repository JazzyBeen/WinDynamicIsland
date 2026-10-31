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
                    var language=Body(form).Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<SelectionButton>()).Single();language.SelectIndex(1);Application.DoEvents();Check(L.Language=="en","language control immediately switches the settings UI");
                    var section=typeof(SettingsForm).GetMethod("ShowSection",BindingFlags.NonPublic|BindingFlags.Instance);
                    foreach(string name in new[]{"General","Appearance","Media","Notifications","Devices","VPN","About"}){section.Invoke(form,new object[]{name});Application.DoEvents();Check(Body(form).Controls.Count>0,name+" settings page renders");}
                    section.Invoke(form,new object[]{"VPN"});var provider=Body(form).Controls.OfType<Panel>().SelectMany(p=>p.Controls.OfType<SelectionButton>()).Single();provider.SelectIndex(3);Application.DoEvents();Check(Body(form).Controls.OfType<Panel>().Count(p=>p.Controls.OfType<TextBox>().Any())==7,"custom VPN exposes all command and status fields");
                    section.Invoke(form,new object[]{"General"});typeof(SettingsForm).GetMethod("Save",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,new object[]{null,EventArgs.Empty});Check(saved!=null && saved.Language=="en" && saved.VpnProvider=="Command","Save forwards the complete edited configuration");form.Close();
                }
            }catch(Exception ex){Console.WriteLine(ex);Environment.ExitCode=1;}
        }
    }
}
