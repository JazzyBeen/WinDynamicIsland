using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal static class SettingsInputTests
    {
        const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
        static int checks;
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
        static T Field<T>(object owner, string name) { return (T)owner.GetType().GetField(name, Instance).GetValue(owner); }
        static void Mouse(Control control, string method, int x, int y, int delta = 0)
        {
            control.GetType().GetMethod(method, Instance).Invoke(control, new object[] { new HandledMouseEventArgs(MouseButtons.Left, 1, x, y, delta) });
        }
        [STAThread] static void Main()
        {
            var original = Program.Settings;
            try
            {
                Application.EnableVisualStyles();
                foreach (bool material in new[] { false, true })
                {
                    Program.Settings = new AppSettings { InterfaceDesign = material ? "Material" : "Classic", InterfaceAnimations = true };
                    AppDesign.Change(material, false);
                    using (var host = new Form { ClientSize = new Size(760, 380) })
                    using (var slider = new SnapSlider("Fixture", 20, 0, 100))
                    {
                        host.Controls.Add(slider); host.Show(); Application.DoEvents();
                        bool synchronized = true;
                        slider.ValueChanged += delegate { synchronized &= Math.Abs(Field<float>(slider, "displayValue") - slider.Value) <= .5f; };
                        Mouse(slider, "OnMouseDown", 82, 63);
                        foreach (int x in new[] { 120, 225, 410, 570, 310, 70, 590 })
                        {
                            // No message pumping: a busy input queue must not prevent the visual update.
                            Mouse(slider, "OnMouseMove", x, 63);
                            float expected = Math.Max(0, Math.Min(100, (x - 26) * 100f / (slider.Width - 52)));
                            float shown = Field<float>(slider, "displayValue");
                            Check(Math.Abs(shown - expected) < .01f || shown == slider.Value, (material ? "Material" : "Classic") + " thumb immediately follows pointer at " + x);
                            Check(Math.Abs(shown - slider.Value) <= .5f, (material ? "Material" : "Classic") + " number matches the thumb at " + x);
                        }
                        Check(synchronized, "ValueChanged observers see the synchronized thumb and number");
                        Mouse(slider, "OnMouseUp", 26, 63);
                        Check(slider.Value == 0 && Field<float>(slider, "displayValue") == 0 && !slider.Capture, "release commits the final position without a timer tick");
                        Mouse(slider, "OnMouseDown", 300, 63); slider.Capture = false;
                        Check(!Field<bool>(slider, "dragging"), "capture loss releases the drag state");
                        slider.Magnetic = false;
                        Mouse(slider, "OnMouseDown", 26 + (slider.Width - 52) / 2 + 3, 63);
                        Check(slider.Value != 50 && Math.Abs(Field<float>(slider, "displayValue") - 50) > .2f, "non-magnetic sliders do not snap to the midpoint");
                        Mouse(slider, "OnMouseUp", 26 + (slider.Width - 52) / 2 + 3, 63);
                        slider.Magnetic = true;
                        Program.Settings.InterfaceAnimations = false; slider.Value = 100;
                        Check(Field<float>(slider, "displayValue") == 100, "disabled animation updates the rendered track immediately");
                    }
                    using (var form = new SettingsForm(null))
                    {
                        form.Show(); Application.DoEvents();
                        typeof(SettingsForm).GetMethod("ShowSection", Instance).Invoke(form, new object[] { "Devices" });
                        Application.DoEvents();
                        var body = Field<IslandScrollPanel>(form, "body");
                        var cards = body.Controls.Cast<Control>().ToArray();
                        var bounds = cards.Select(c => c.Bounds).ToArray();
                        int layouts = 0, cardMoves = 0;
                        body.Layout += delegate { layouts++; };
                        foreach (var card in cards) card.LocationChanged += delegate { cardMoves++; };
                        body.ScrollTo(0);
                        for (int i = 0; i < 300; i++) Mouse(body, "OnMouseWheel", 30, 30, i < 150 ? -4 : 4);
                        Check(layouts == 0 && cardMoves == 0 && cards.Select(c => c.Bounds).SequenceEqual(bounds), "300 high resolution wheel events do not relayout or reposition any card");
                        Check(body.ScrollOffset == 0 && body.Top == 0, "opposite wheel bursts return to the exact start");
                        Mouse(body, "OnMouseWheel", 30, 30, -15);
                        Check(body.ScrollOffset == 6 && body.Top == -6, "fractional touchpad input moves immediately instead of being discarded");
                        body.ScrollTo(int.MaxValue);
                        Check(body.ScrollOffset == body.MaximumOffset, "scrolling clamps to the bottom of the content");
                        body.ScrollTo(0);
                        var bar = body.Viewport.Controls.Cast<Control>().Single(c => c != body);
                        Mouse(bar, "OnMouseDown", 6, 4); Mouse(bar, "OnMouseMove", 6, bar.Height + 100); Mouse(bar, "OnMouseUp", 6, bar.Height + 100);
                        Check(body.ScrollOffset == body.MaximumOffset && !bar.Capture, "fixed scrollbar dragging reaches the bottom and releases capture");
                        for (int i = 0; i < 5; i++) body.PerformLayout();
                        Check(cards.Select(c => c.Bounds).SequenceEqual(bounds) && body.Top == -body.ScrollOffset, "repeated layouts retain logical card bounds at the scroll limit");
                        form.ClientSize = new Size(1000, 900); Application.DoEvents();
                        Check(body.ScrollOffset <= body.MaximumOffset && body.Top == -body.ScrollOffset && body.Height >= body.Viewport.Height, "resizing clamps the translation and fills the viewport");
                        form.ClientSize = new Size(900, 680); Application.DoEvents(); body.ScrollTo(0);
                        var lastSlider = cards.OfType<SnapSlider>().Last(); lastSlider.Focus(); Application.DoEvents();
                        var at = body.Viewport.PointToClient(lastSlider.PointToScreen(Point.Empty));
                        Check(at.Y >= 0 && at.Y + lastSlider.Height <= body.Viewport.Height, "keyboard focus exposes the complete offscreen control");
                        form.CloseForExit();
                    }
                }
                Console.WriteLine(checks + " settings input checks passed; no VPN commands were run.");
            }
            catch (Exception ex) { Console.WriteLine(ex); Environment.ExitCode = 1; }
            finally { Program.Settings = original; AppDesign.Change(original.InterfaceDesign == "Material", false); }
        }
    }
}
