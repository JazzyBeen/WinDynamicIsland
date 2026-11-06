using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class SettingsForm
    {
        readonly BufferedPanel pageHost = new BufferedPanel();
        readonly Panel footer = new BufferedPanel();
        readonly PageTransitionSurface transition = new PageTransitionSurface();
        readonly Dictionary<string, int> scrollPositions = new Dictionary<string, int>();
        readonly Timer revealTimer = new FrameTimer();
        readonly Stopwatch revealClock = new Stopwatch();
        bool exporting, firstReveal = true;
        internal bool PageAnimating
        {
            get
            {
                return transition.Running;
            }
        }
        void SelectNavigation(bool animate)
        {
            foreach (Control control in navigation.Controls)
            {
                var button = control as GlassButton;
                if (button != null && button.Tag as string == section)
                {
                    navigation.Select(button, animate);
                    return;
                }
            }
        }
        void InitializePageHost()
        {
            pageHost.Dock = DockStyle.Fill;
            footer.Dock = DockStyle.Bottom;
            footer.Height = 60;
            heading.Font = AppTypography.Create(25, FontStyle.Bold);
            heading.Dock = DockStyle.Top;
            heading.Height = 68;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            subtitle.ForeColor = muted;
            subtitle.Dock = DockStyle.None;
            subtitle.SetBounds(8, 28, 370, 25);
            pageHost.Controls.Add(heading);
            save.ForeColor = Color.Black;
            save.TextAlign = ContentAlignment.MiddleCenter;
            save.Font = new Font(Font, FontStyle.Bold);
            save.Size = new Size(190, 42);
            save.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            save.Top = 18;
            save.Click += Save;
            footer.Controls.Add(save);
            footer.Controls.Add(subtitle);
            footer.Resize += delegate
            {
                save.Left = footer.ClientSize.Width - save.Width;
                subtitle.Width = Math.Max(80, save.Left - 20);
            };
            content.Controls.Add(pageHost);
            content.Controls.Add(footer);
            content.Controls.Add(transition);
            content.Layout += delegate
            {
                transition.Bounds = pageHost.Bounds;
            };
            revealTimer.Tick += delegate
            {
                double t = PageTransitionSurface.Ease(revealClock.Elapsed.TotalMilliseconds / 180);
                Opacity = Math.Max(.05, t);
                if (t >= 1)
                    CompleteReveal();
            };
        }
        void CompleteReveal()
        {
            revealTimer.Stop();
            Opacity = 1;
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible && revealTimer != null)
            {
                CompleteReveal();
                if (transition != null)
                    transition.Finish();
            }
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (firstReveal && !exporting && Program.Settings.InterfaceAnimations)
            {
                firstReveal = false;
                Opacity = .05;
                revealClock.Restart();
                revealTimer.Start();
            }
        }
        internal void RevealFromTray()
        {
            bool hidden = !Visible;
            if (hidden && !exporting && Program.Settings.InterfaceAnimations)
                Opacity = .05;
            Show();
            if (hidden && !exporting && Program.Settings.InterfaceAnimations)
            {
                revealClock.Restart();
                revealTimer.Start();
            }
        }
        internal void ExportNavigationPreview(string folder)
        {
            Directory.CreateDirectory(folder);
            exporting = true;
            Show();
            CompleteReveal();
            ShowSection("General");
            Application.DoEvents();
            var outgoing = PageTransitionSurface.CapturePage(pageHost);
            ShowSection("Appearance");
            Application.DoEvents();
            var incoming = PageTransitionSurface.CapturePage(pageHost);
            using (var background = new Bitmap(Width, Height))
            {
                DrawToBitmap(background, new Rectangle(0, 0, Width, Height));
                transition.Cover(outgoing);
                transition.Begin(incoming, 1);
                var location = pageHost.PointToScreen(Point.Empty);
                location.Offset(-Left, -Top);
                for (int i = 0; i <= 8; i++)
                    using (var frame = new Bitmap(background))
                    using (var page = transition.RenderPreview(i / 8d))
                    {
                        using (var graphics = Graphics.FromImage(frame))
                            graphics.DrawImageUnscaled(page, location);
                        frame.Save(Path.Combine(folder, "navigation-" + i + ".png"));
                    }
                transition.Finish();
            }
            CloseForExit();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                autoSave.Dispose();
                captureUi.Dispose();
                revealTimer.Dispose();
                transition.Dispose();
                designTransition.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
