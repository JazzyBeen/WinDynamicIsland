using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed class NotificationChoice
    {
        internal string Name; internal string[] Ids;
        public override string ToString()
        {
            return Name;
        }
    }
    internal sealed partial class SettingsForm
    {
        void NotificationApps()
        {
            var sources = NotificationCatalog.Snapshot();
            if (sources.Length == 0)
            {
                Help("LoadingApps");
                Task.Run(() => { try { NotificationCatalog.Refresh(); } catch { } }).ContinueWith(_ => { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { if (!IsDisposed && section == "Notifications" && NotificationCatalog.Snapshot().Length > 0) ShowSection(section); }); });
                return;
            }
            var groups = sources.Where(s => s.Id != "Windows.SystemToast.Power").GroupBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).Select(g => new NotificationChoice { Name = g.Key, Ids = g.Select(s => s.Id).ToArray() }).OrderByDescending(g => g.Ids.Any(id => NotificationRouter.Routes(draft, id))).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            var selected = new HashSet<string>(sources.Where(s => NotificationRouter.Routes(draft, s.Id)).Select(s => s.Id), StringComparer.OrdinalIgnoreCase);
            var card = new GlassCard { Width = 620, Height = 318, Margin = new Padding(0, 6, 0, 8) };
            var search = new SearchInput { Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Hint = L.T("SearchApps"), Width = 574, Location = new Point(22, 17), BorderStyle = BorderStyle.None, BackColor = AppDesign.Material ? AppDesign.Container : Color.FromArgb(18, 18, 20), ForeColor = IslandTheme.Foreground, Font = Font, AccessibleName = L.T("SearchApps") };
            var list = new AppSelectionList(groups, selected, draft.NotificationMode == "All") { Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Location = new Point(18, 56), Width = 584, Height = 244, Font = Font };
            search.TextChanged += delegate
            {
                list.Filter(search.Text);
            };
            card.Controls.Add(search);
            card.Controls.Add(list);
            body.Controls.Add(card);
            list.SelectionChanged += delegate { ScheduleSave(); };
            collect.Add(delegate
            {
                draft.NotificationApps = selected.ToArray();
                draft.NotificationSelectionConfigured = true;
            });
        }
    }
}
