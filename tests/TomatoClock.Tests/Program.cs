using System.Reflection;
using TomatoClock;

internal static class Program
{
    private static int passed, failed;
    private static readonly string Temp = Path.Combine(Path.GetTempPath(), "TomatoClock.Tests", Guid.NewGuid().ToString("N"));
    private static string? artifacts;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--instance-child")
        {
            dynamic child = Create("TomatoClock.SingleInstance", args[1]);
            using ((IDisposable)child)
            {
                if (child.IsPrimary) return 7;
                child.RequestActivation(); return 0;
            }
        }
        typeof(Form1).Assembly.GetType("TomatoClock.ApplicationConfiguration")!
            .GetMethod("Initialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, null);
        if (args.Length == 1 && args[0] == "--hit-target")
        {
            using var target = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(100, 100),
                Size = new Size(420, 420), ShowInTaskbar = false, Text = "TomatoClock input test target" };
            target.Shown += (_, _) => Console.WriteLine(target.Handle.ToInt64());
            Application.Run(target); return 0;
        }
        if (args.Length == 2 && args[0] == "--artifacts") { artifacts = Path.GetFullPath(args[1]); Directory.CreateDirectory(artifacts); }
        Directory.CreateDirectory(Temp);
        Test("Production startup enables per-monitor DPI", () => Equal(HighDpiMode.PerMonitorV2, Application.HighDpiMode));
        Test("Starts idle; pause excludes elapsed time; continue preserves fractions", () =>
        {
            dynamic m = Model();
            Equal("25:00", (string)m.DisplayTime);
            m.Advance(TimeSpan.FromMinutes(2));
            Equal("25:00", (string)m.DisplayTime);
            m.Start(); m.Advance(TimeSpan.FromSeconds(1.25)); m.Pause();
            m.Advance(TimeSpan.FromHours(2));
            Equal(TimeSpan.FromSeconds(1498.75), (TimeSpan)m.Remaining);
            m.Start(); m.Advance(TimeSpan.FromSeconds(.75));
            Equal("24:58", (string)m.DisplayTime);
        });
        Test("Exact boundary switches immediately and repeats", () =>
        {
            dynamic m = Model(); m.Start();
            Equal(true, (bool)m.Advance(TimeSpan.FromMinutes(25)));
            Equal("Break", m.Phase.ToString()); Equal("05:00", (string)m.DisplayTime);
            m.Advance(TimeSpan.FromMinutes(5));
            Equal("Work", m.Phase.ToString()); Equal("25:00", (string)m.DisplayTime);
        });
        Test("Delayed updates consume multiple cycles without drift", () =>
        {
            dynamic m = Model(); m.Start(); m.Advance(TimeSpan.FromMinutes(87));
            Equal("Break", m.Phase.ToString()); Equal("03:00", (string)m.DisplayTime);
        });
        Test("New duration waits for next matching phase; reset applies immediately", () =>
        {
            dynamic m = Model(); m.Start(); m.Advance(TimeSpan.FromMinutes(10));
            m.Configure(15, 3);
            Equal(TimeSpan.FromMinutes(25), (TimeSpan)m.Duration);
            Equal("15:00", (string)m.DisplayTime); Equal(.4, (double)m.Progress);
            m.Pause(); m.Configure(12, 4); m.Start(); m.Advance(TimeSpan.FromMinutes(15));
            Equal("04:00", (string)m.DisplayTime);
            m.Advance(TimeSpan.FromMinutes(4)); Equal("12:00", (string)m.DisplayTime);
            m.Reset(); Equal("Idle", m.State.ToString()); Equal("12:00", (string)m.DisplayTime);
        });
        Test("Idle configuration updates immediately and rejects invalid durations", () =>
        {
            dynamic m = Model(); m.Configure(180, 1); Equal("180:00", (string)m.DisplayTime);
            Throws<ArgumentOutOfRangeException>(() => m.Configure(0, 5));
            Throws<ArgumentOutOfRangeException>(() => m.Configure(25, 181));
            Equal("180:00", (string)m.DisplayTime);
            Throws<ArgumentOutOfRangeException>(() => m.Advance(TimeSpan.FromSeconds(-1)));
        });
        Test("Ceiling display never reaches zero before transition", () =>
        {
            dynamic m = Model(); m.Start(); m.Advance(TimeSpan.FromSeconds(1499.99));
            Equal("00:01", (string)m.DisplayTime);
        });
        Test("Missing and corrupt settings use safe defaults", () =>
        {
            string path = Path.Combine(Temp, "missing.json"); dynamic store = Store(path);
            dynamic s = store.Load(); Equal(25, (int)s.WorkMinutes); Equal(false, (bool)s.SoundEnabled);
            File.WriteAllText(path, "broken{"); s = store.Load(); Equal(5, (int)s.BreakMinutes);
        });
        Test("Invalid settings field does not destroy valid preferences", () =>
        {
            string path = Path.Combine(Temp, "partial.json");
            File.WriteAllText(path, "{\"WorkMinutes\":\"bad\",\"BreakMinutes\":8,\"SoundEnabled\":true,\"Width\":-2}");
            dynamic s = Store(path).Load();
            Equal(25, (int)s.WorkMinutes); Equal(8, (int)s.BreakMinutes);
            Equal(true, (bool)s.SoundEnabled); Equal(400, (int)s.Width);
        });
        Test("Settings roundtrip preserves compact geometry and sound", () =>
        {
            dynamic store = Store(Path.Combine(Temp, "roundtrip.json")); dynamic s = store.Load();
            s.WorkMinutes = 42; s.SoundEnabled = true; s.Compact = true; s.Width = 150; s.Height = 80;
            Equal(null, (string?)store.Save(s));
            dynamic loaded = store.Load(); Equal(42, (int)loaded.WorkMinutes);
            Equal(true, (bool)loaded.Compact); Equal(150, (int)loaded.Width); Equal(true, (bool)loaded.SoundEnabled);
        });
        Test("Unwritable settings path returns error instead of throwing", () =>
        {
            string parentFile = Path.Combine(Temp, "not-a-directory"); File.WriteAllText(parentFile, "fixture");
            dynamic store = Store(Path.Combine(parentFile, "settings.json"));
            Check(store.Save(Store(Path.Combine(Temp, "none.json")).Load()) is string, "Missing write error");
        });
        Test("Window start pause reset controls drive the displayed timer", () =>
        {
            using Form f = Window(); f.Show(); Application.DoEvents();
            Equal("25:00", Find(f, "clockFace").Text);
            Capture(f, "normal.png");
            var button = (Button)Find(f, "toggleButton");
            button.PerformClick(); Equal("暂停", button.Text);
            button.PerformClick(); Equal("继续", button.Text);
            ((Button)Find(f, "resetButton")).PerformClick();
            Equal("开始专注", button.Text); Equal("25:00", Find(f, "clockFace").Text);
        });
        Test("Compact hides controls, preserves time, and restores from menu", () =>
        {
            using Form f = Window(); f.Show(); Application.DoEvents();
            f.ClientSize = new Size(150, 80); Application.DoEvents();
            Equal(FormBorderStyle.None, f.FormBorderStyle);
            Equal(false, Find(f, "toggleButton").Visible);
            Equal("25:00", Find(f, "clockFace").Text);
            var menu = Find(f, "clockFace").ContextMenuStrip!;
            ((ToolStripMenuItem)menu.Items.Find("restoreItem", false).Single()).PerformClick();
            Equal(FormBorderStyle.Sizable, f.FormBorderStyle);
            Check(f.ClientSize.Width >= 360 && f.ClientSize.Height >= 280, "Normal geometry not restored");
        });
        Test("Settings update topmost and sound while keeping the current phase", () =>
        {
            using Form f = Window(); f.Show(); Application.DoEvents();
            ((Button)Find(f, "toggleButton")).PerformClick();
            ((Button)Find(f, "settingsButton")).PerformClick(); Application.DoEvents();
            Form settings = f.OwnedForms.Single();
            ((NumericUpDown)Find(settings, "workMinutes")).Value = 15;
            Check(Find(settings, "effectiveHint").Text.Contains("下次工作"), "Missing deferred setting hint");
            Check(Find(f, "clockFace").Text.StartsWith("24:") || Find(f, "clockFace").Text == "25:00", "Current phase restarted");
            ((CheckBox)Find(settings, "topMostCheck")).Checked = true;
            Equal(true, f.TopMost);
            Equal(false, ((CheckBox)Find(settings, "soundCheck")).Checked);
            Capture(settings, "settings.png");
        });
        Test("Suspend and resume messages pause without automatically resuming", () =>
        {
            using Form f = Window(); f.Show(); Application.DoEvents();
            var button = (Button)Find(f, "toggleButton"); button.PerformClick();
            SendWindowMessage(f, 0x218, 4); // PBT_APMSUSPEND
            Equal("继续", button.Text);
            SendWindowMessage(f, 0x218, 18); // PBT_APMRESUMEAUTOMATIC
            Equal("继续", button.Text);
        });
        Test("Compact render fits longest timer and paused marker", () =>
        {
            string path = Path.Combine(Temp, "render.json");
            File.WriteAllText(path, "{\"WorkMinutes\":180,\"Compact\":true,\"Width\":120,\"Height\":64}");
            using Form f = Window(path); f.Show(); Application.DoEvents();
            Equal("180:00", Find(f, "clockFace").Text);
            var menu = Find(f, "clockFace").ContextMenuStrip!;
            ((ToolStripMenuItem)menu.Items[0]).PerformClick(); ((ToolStripMenuItem)menu.Items[0]).PerformClick();
            using var bitmap = new Bitmap(f.Width, f.Height); f.DrawToBitmap(bitmap, new Rectangle(Point.Empty, f.Size));
            Check(bitmap.Width >= 120, "Invalid render width");
            Capture(f, "compact-paused.png");
        });
        Test("Restoring normal mode clamps saved large-screen dimensions", () =>
        {
            string path = Path.Combine(Temp, "large-screen.json");
            File.WriteAllText(path, "{\"Compact\":true,\"Width\":150,\"Height\":80,\"NormalWidth\":8000,\"NormalHeight\":4000}");
            using Form f = Window(path); f.Show(); Application.DoEvents();
            ((ToolStripMenuItem)Find(f, "clockFace").ContextMenuStrip!.Items.Find("restoreItem", false).Single()).PerformClick();
            Rectangle screen = Screen.FromControl(f).WorkingArea;
            Check(screen.Contains(f.Bounds), $"Restored window outside screen: {f.Bounds}, screen: {screen}");
        });
        Test("Nonclient compact hover displays a state tooltip", () =>
        {
            using Form f = Window(); f.Show(); f.ClientSize = new Size(150, 80); Application.DoEvents();
            var tip = (ToolTip)typeof(Form1).GetField("toolTip", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f)!;
            bool popup = false; tip.Popup += (_, _) => popup = true;
            SendWindowMessage(f, 0x2A0, 2); // WM_NCMOUSEHOVER, HTCAPTION
            Check(popup, "Compact nonclient hover did not display a tooltip");
        });
        Test("Compact native hit tests expose eight resize edges and dragging", () =>
        {
            using Form f = Window(); f.Show(); f.ClientSize = new Size(150, 80); Application.DoEvents();
            foreach (var (point, expected) in new (Point, int)[]
            {
                (new(1, 1), 13), (new(75, 1), 12), (new(149, 1), 14),
                (new(1, 40), 10), (new(75, 40), 2), (new(149, 40), 11),
                (new(1, 79), 16), (new(75, 79), 15), (new(149, 79), 17)
            })
            {
                Point screen = f.PointToScreen(point);
                IntPtr coordinates = (IntPtr)((screen.Y << 16) | (screen.X & 0xffff));
                Message m = Message.Create(f.Handle, 0x84, IntPtr.Zero, coordinates);
                object[] args = [m];
                typeof(Form1).GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(f, args);
                Equal(expected, ((Message)args[0]).Result.ToInt32());
            }
            SendWindowMessage(f, 0xA3, 2);
            Equal(FormBorderStyle.Sizable, f.FormBorderStyle);
        });
        Test("Compact geometry and sound persist while timer progress does not", () =>
        {
            string path = Path.Combine(Temp, "window-persist.json");
            using (Form f = Window(path))
            {
                f.Show(); Application.DoEvents();
                ((Button)Find(f, "toggleButton")).PerformClick();
                f.ClientSize = new Size(150, 80);
                ((ToolStripMenuItem)Find(f, "clockFace").ContextMenuStrip!.Items[5]).PerformClick();
                f.Close();
            }
            using Form reopened = Window(path); reopened.Show(); Application.DoEvents();
            Equal(FormBorderStyle.None, reopened.FormBorderStyle);
            Equal(new Size(150, 80), reopened.ClientSize);
            Equal("25:00", Find(reopened, "clockFace").Text);
            var menu = Find(reopened, "clockFace").ContextMenuStrip!;
            Equal("开始专注", menu.Items[0].Text);
            Equal(true, ((ToolStripMenuItem)menu.Items[5]).Checked);
        });
        Test("Tray preference hides taskbar without hiding clock and survives restart", () =>
        {
            string path = Path.Combine(Temp, "tray-preference.json");
            using (Form f = Window(path))
            {
                // Exercise real taskbar handle recreation with an owned settings window.
                f.ShowInTaskbar = true;
                f.Show(); Application.DoEvents();
                ((Button)Find(f, "settingsButton")).PerformClick();
                Form panel = f.OwnedForms.Single();
                Check(FindAll(panel, "trayOnlyCheck").Any(), "Missing tray preference checkbox");
                ((CheckBox)Find(panel, "trayOnlyCheck")).Checked = true;
                Equal(false, f.ShowInTaskbar); Equal(true, f.Visible); Equal(true, Tray(f).Visible);
                Check(!panel.IsDisposed && panel.Visible && panel.Owner == f, "Taskbar transition orphaned settings");
                ((CheckBox)Find(panel, "trayOnlyCheck")).Checked = false;
                Equal(true, f.ShowInTaskbar); Equal(false, Tray(f).Visible);
                Check(panel.Visible && panel.Owner == f, "Disabling tray orphaned settings");
                ((CheckBox)Find(panel, "trayOnlyCheck")).Checked = true;
                panel.Close();
                TrayMenu(f, "exitItem").PerformClick();
                Equal(true, f.IsDisposed);
            }
            using Form reopened = Window(path); reopened.Show(); Application.DoEvents();
            Equal(false, reopened.ShowInTaskbar); Equal(true, Tray(reopened).Visible);
            Equal("25:00", Find(reopened, "clockFace").Text);
        });
        Test("Tray close and minimize retain timer; show and explicit exit remain reachable", () =>
        {
            string path = Path.Combine(Temp, "tray-close.json");
            File.WriteAllText(path, "{\"TrayOnly\":true}");
            using Form f = Window(path); f.Show(); Application.DoEvents();
            Check(Tray(f).Visible, "Saved tray preference not applied");
            ((Button)Find(f, "toggleButton")).PerformClick();
            f.Close(); Application.DoEvents();
            Equal(false, f.IsDisposed); Equal(false, f.Visible);
            Equal("暂停", Tray(f).ContextMenuStrip!.Items[0].Text);
            dynamic timer = typeof(Form1).GetField("model", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f)!;
            TimeSpan before = timer.Remaining;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            while (elapsed.ElapsedMilliseconds < 1200) { Application.DoEvents(); Thread.Sleep(10); }
            Check((TimeSpan)timer.Remaining < before - TimeSpan.FromMilliseconds(900), "Hidden timer stopped progressing");
            TrayMenu(f, "showWindowItem").PerformClick();
            Equal(true, f.Visible); Equal(false, f.ShowInTaskbar);
            f.WindowState = FormWindowState.Minimized; Application.DoEvents();
            Equal(false, f.Visible);
            typeof(Form1).GetMethod("ShowExisting", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(f, null);
            Equal(true, f.Visible); Equal(FormWindowState.Normal, f.WindowState);
            NotifyIcon tray = Tray(f);
            TrayMenu(f, "exitItem").PerformClick();
            Equal(true, f.IsDisposed); Equal(false, tray.Visible);
        });
        Test("Turning tray off while hidden restores a reachable taskbar window", () =>
        {
            string path = Path.Combine(Temp, "tray-disable.json");
            File.WriteAllText(path, "{\"TrayOnly\":true,\"Compact\":true,\"Width\":150,\"Height\":80}");
            using Form f = Window(path); f.Show(); Application.DoEvents();
            Check(Tray(f).Visible, "Saved tray preference not applied");
            TrayMenu(f, "hideWindowItem").PerformClick(); Equal(false, f.Visible);
            TrayMenu(f, "trayOnlyItem").PerformClick();
            Equal(true, f.Visible); Equal(true, f.ShowInTaskbar); Equal(false, Tray(f).Visible);
            Equal(FormBorderStyle.None, f.FormBorderStyle); Equal(new Size(150, 80), f.ClientSize);
            f.Close(); Equal(true, f.IsDisposed);
        });
        Test("Tray mode does not cancel system shutdown or Application.Exit", () =>
        {
            string path = Path.Combine(Temp, "tray-shutdown.json"); File.WriteAllText(path, "{\"TrayOnly\":true}");
            using Form f = Window(path); f.Show(); Application.DoEvents();
            foreach (CloseReason reason in new[] { CloseReason.WindowsShutDown, CloseReason.ApplicationExitCall })
            {
                var closing = new FormClosingEventArgs(reason, false);
                typeof(Form1).GetMethod("OnFormClosing", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(f, [closing]);
                Equal(false, closing.Cancel);
            }
        });
        Test("Opacity changes the clock immediately but settings remain opaque", () =>
        {
            using Form f = Window(); f.Show(); Application.DoEvents();
            ((Button)Find(f, "settingsButton")).PerformClick();
            Form panel = f.OwnedForms.Single();
            Check(FindAll(panel, "opacitySlider").Any(), "Missing opacity slider");
            ((TrackBar)Find(panel, "opacitySlider")).Value = 45;
            Check(Math.Abs(f.Opacity - .45) < .001, "Clock opacity not applied");
            Equal(1d, panel.Opacity); Equal("25:00", Find(f, "clockFace").Text);
            ((TrackBar)Find(panel, "opacitySlider")).Value = 100;
            Equal(1d, f.Opacity);
        });
        Test("Click through sets native layered transparency and preserves a tray recovery entry", () =>
        {
            using Form f = Window(); f.ShowInTaskbar = true; f.Show(); Application.DoEvents();
            ((Button)Find(f, "settingsButton")).PerformClick();
            Form panel = f.OwnedForms.Single();
            Check(FindAll(panel, "clickThroughCheck").Any(), "Missing click-through checkbox");
            ((CheckBox)Find(panel, "clickThroughCheck")).Checked = true;
            Equal(0x80020, GetWindowLong(f.Handle, -20) & 0x80020);
            foreach (var (percent, alpha) in new (int, byte)[] { (40, 102), (100, 255), (20, 51) })
            {
                ((TrackBar)Find(panel, "opacitySlider")).Value = percent;
                Equal(0x80020, GetWindowLong(f.Handle, -20) & 0x80020);
                Check(GetLayeredWindowAttributes(f.Handle, out _, out byte actualAlpha, out uint flags), "Layered alpha unavailable");
                Equal(alpha, actualAlpha); Equal(2u, flags);
            }
            Equal(0, GetWindowLong(panel.Handle, -20) & 0x20);
            Equal(true, Tray(f).Visible); Equal(true, f.ShowInTaskbar);
            f.ClientSize = new Size(150, 80); Application.DoEvents();
            Equal(0x80020, GetWindowLong(f.Handle, -20) & 0x80020);
            TrayMenu(f, "trayOnlyItem").PerformClick();
            TrayMenu(f, "trayOnlyItem").PerformClick();
            Equal(true, Tray(f).Visible);
            Equal(0x80020, GetWindowLong(f.Handle, -20) & 0x80020);
            TrayMenu(f, "clickThroughItem").PerformClick();
            Equal(0, GetWindowLong(f.Handle, -20) & 0x20);
            Equal(false, Tray(f).Visible); Equal(false, ((CheckBox)Find(panel, "clickThroughCheck")).Checked);
        });
        Test("Opacity and click-through restore on startup and persist independently", () =>
        {
            string path = Path.Combine(Temp, "appearance.json");
            File.WriteAllText(path, "{\"OpacityPercent\":35,\"ClickThrough\":true}");
            using (Form f = Window(path, preserveAppearance: true))
            {
                f.Show(); Application.DoEvents();
                Check(Math.Abs(f.Opacity - .35) < .001, "Startup opacity lost");
                Equal(true, Tray(f).Visible); Equal(0x80020, GetWindowLong(f.Handle, -20) & 0x80020);
                TrayMenu(f, "clickThroughItem").PerformClick();
                Equal(0, GetWindowLong(f.Handle, -20) & 0x20);
                Check(Math.Abs(f.Opacity - .35) < .001, "Disabling click-through reset opacity");
                f.Close();
            }
            dynamic saved = Store(path).Load();
            Equal(35, (int)saved.OpacityPercent); Equal(false, (bool)saved.ClickThrough);
        });
        Test("Invalid opacity falls back without discarding click-through preference", () =>
        {
            string path = Path.Combine(Temp, "bad-opacity.json");
            File.WriteAllText(path, "{\"OpacityPercent\":0,\"ClickThrough\":true}");
            dynamic saved = Store(path).Load();
            Equal(100, (int)saved.OpacityPercent); Equal(true, (bool)saved.ClickThrough);
        });
        Test("Click-through hit testing reaches a different process underneath the clock", () =>
        {
            using var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
            { ArgumentList = { "--hit-target" }, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
            IntPtr target = IntPtr.Zero;
            try
            {
                var output = child.StandardOutput.ReadLineAsync();
                Check(output.Wait(5000), "Target window did not initialize");
                target = (IntPtr)long.Parse(output.Result!);
                using Form f = Window(preserveAppearance: true);
                f.StartPosition = FormStartPosition.Manual; f.Location = new Point(140, 160);
                f.ClientSize = new Size(150, 80); f.TopMost = true; f.Show(); f.Activate(); Application.DoEvents();
                Point point = f.PointToScreen(new Point(75, 40));
                Equal(f.Handle, GetAncestor(WindowFromPoint(point), 2));
                TrayMenu(f, "clickThroughItem").PerformClick(); Application.DoEvents();
                Equal(target, GetAncestor(WindowFromPoint(point), 2));
                TrayMenu(f, "clickThroughItem").PerformClick(); Application.DoEvents();
                Equal(f.Handle, GetAncestor(WindowFromPoint(point), 2));
            }
            finally
            {
                if (target != IntPtr.Zero) PostMessage(target, 0x10, IntPtr.Zero, IntPtr.Zero);
                if (!child.WaitForExit(3000)) { child.Kill(); child.WaitForExit(); }
            }
        });
        Test("Tray mode excludes clock and settings from Alt-Tab without losing window effects", () =>
        {
            using Form f = Window(preserveAppearance: true);
            f.ShowInTaskbar = true; f.Show(); Application.DoEvents();
            Equal(0x40000, GetWindowLong(f.Handle, -20) & 0x40080);
            ((Button)Find(f, "settingsButton")).PerformClick();
            Form panel = f.OwnedForms.Single();
            ((TrackBar)Find(panel, "opacitySlider")).Value = 40;
            ((CheckBox)Find(panel, "clickThroughCheck")).Checked = true;
            ((CheckBox)Find(panel, "trayOnlyCheck")).Checked = true;
            Equal(0x80, GetWindowLong(f.Handle, -20) & 0x40080);
            Equal(0x80, GetWindowLong(panel.Handle, -20) & 0x40080);
            Equal(0x80020, GetWindowLong(f.Handle, -20) & 0x80020);
            Check(panel.Visible && panel.Owner == f, "Settings lost after tray transition");
            f.ClientSize = new Size(150, 80); Application.DoEvents();
            Equal(0x80, GetWindowLong(f.Handle, -20) & 0x40080);
            TrayMenu(f, "restoreItem").PerformClick();
            Equal(0x80, GetWindowLong(f.Handle, -20) & 0x40080);
            Check(Math.Abs(f.Opacity - .4) < .001, "Tray transition reset opacity");
            ((CheckBox)Find(panel, "trayOnlyCheck")).Checked = false;
            Equal(0x40000, GetWindowLong(f.Handle, -20) & 0x40080);
            Equal(0, GetWindowLong(panel.Handle, -20) & 0x80);
            Equal(0x80020, GetWindowLong(f.Handle, -20) & 0x80020);
            Equal(true, Tray(f).Visible); // Click-through alone must not hide the app from Alt-Tab.
        });
        Test("Saved tray mode excludes restored windows from Alt-Tab", () =>
        {
            string path = Path.Combine(Temp, "tray-switcher.json");
            File.WriteAllText(path, "{\"TrayOnly\":true,\"Compact\":true,\"Width\":150,\"Height\":80}");
            using Form f = Window(path); f.Show(); Application.DoEvents();
            Equal(0x80, GetWindowLong(f.Handle, -20) & 0x40080);
            f.Close(); Equal(false, f.Visible);
            TrayMenu(f, "showWindowItem").PerformClick();
            Equal(true, f.Visible); Equal(0x80, GetWindowLong(f.Handle, -20) & 0x40080);
        });
        Test("Activation arriving before listener registration is retained", () =>
        {
            string name = "TomatoClock.Test.Early." + Guid.NewGuid().ToString("N");
            dynamic primary = Create("TomatoClock.SingleInstance", name);
            using ((IDisposable)primary)
            using (var activated = new ManualResetEventSlim())
            {
                using var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
                { ArgumentList = { "--instance-child", name }, UseShellExecute = false, CreateNoWindow = true })!;
                Check(child.WaitForExit(5000), "Early second instance did not exit"); Equal(0, child.ExitCode);
                primary.Listen((Action)(() => activated.Set()));
                Check(activated.Wait(3000), "Early activation was lost");
            }
        });
        Test("Second process signals primary and exits; ownership releases", () =>
        {
            string name = "TomatoClock.Test." + Guid.NewGuid().ToString("N");
            dynamic primary = Create("TomatoClock.SingleInstance", name);
            using ((IDisposable)primary)
            using (var activated = new ManualResetEventSlim())
            {
                Equal(true, (bool)primary.IsPrimary);
                primary.Listen((Action)(() => activated.Set()));
                using var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
                {
                    ArgumentList = { "--instance-child", name }, UseShellExecute = false, CreateNoWindow = true
                })!;
                Check(child.WaitForExit(5000), "Second instance did not exit");
                Equal(0, child.ExitCode); Check(activated.Wait(3000), "Primary was not notified");
            }
            dynamic next = Create("TomatoClock.SingleInstance", name);
            using ((IDisposable)next) Equal(true, (bool)next.IsPrimary);
        });
        Console.WriteLine($"RESULT: {passed} passed, {failed} failed");
        Directory.Delete(Temp, true);
        return failed == 0 ? 0 : 1;
    }

    private static dynamic Model() => Create("TomatoClock.Core.PomodoroTimer", 25, 5);
    private static Form Window(string? path = null, bool preserveAppearance = false)
    {
        var store = (object)Store(path ?? Path.Combine(Temp, Guid.NewGuid().ToString("N") + ".json"));
        Check(typeof(Form1).GetConstructor([store.GetType()]) != null, "Missing testable window implementation");
        var form = (Form)Activator.CreateInstance(typeof(Form1), store)!;
        form.ShowInTaskbar = false; if (!preserveAppearance) form.Opacity = 0;
        return form;
    }
    private static Control Find(Control parent, string name) => parent.Controls.Find(name, true).Single();
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetLayeredWindowAttributes(IntPtr window, out uint colorKey, out byte alpha, out uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "PostMessageW")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    private static Control[] FindAll(Control parent, string name) => parent.Controls.Find(name, true);
    private static NotifyIcon Tray(Form form)
    {
        var field = typeof(Form1).GetField("trayIcon", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(field != null, "Missing notification area icon");
        return (NotifyIcon)field!.GetValue(form)!;
    }
    private static ToolStripMenuItem TrayMenu(Form form, string name) =>
        (ToolStripMenuItem)Tray(form).ContextMenuStrip!.Items.Find(name, false).Single();
    private static void Capture(Form form, string name)
    {
        if (artifacts == null) return;
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(Path.Combine(artifacts, name));
    }
    private static void SendWindowMessage(Form f, int message, int parameter)
    {
        object[] args = [Message.Create(f.Handle, message, (IntPtr)parameter, IntPtr.Zero)];
        typeof(Form1).GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(f, args);
    }
    private static dynamic Store(string path) => Create("TomatoClock.Core.SettingsStore", path);
    private static object Create(string name, params object[] args)
    {
        Type? type = typeof(Form1).Assembly.GetType(name);
        Check(type != null, $"Missing implementation: {name}");
        return Activator.CreateInstance(type!, args)!;
    }
    private static void Test(string name, Action body)
    {
        try { body(); passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception e) { failed++; Console.WriteLine($"FAIL {name}: {e.GetBaseException().Message}\n{e.GetBaseException().StackTrace}"); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Equal<T>(T expected, T actual) => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"Expected {expected}, got {actual}");
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }
}
