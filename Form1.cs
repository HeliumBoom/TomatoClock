using System.Diagnostics;
using System.Media;
using TomatoClock.Core;
using TomatoClock.UI;

namespace TomatoClock;

public partial class Form1 : Form
{
    private readonly SettingsStore store;
    private readonly Icon applicationIcon = new(typeof(Form1), "Assets.TomatoClock.ico");
    private readonly AppSettings settings;
    private readonly PomodoroTimer model;
    private readonly ClockFace face = new();
    private readonly Label phaseLabel = new() { TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label statusLabel = new() { TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label helpLabel = new() { Text = "缩小窗口，专注于时间", TextAlign = ContentAlignment.MiddleCenter };
    private readonly ProgressBar progress = new() { Maximum = 1000, Style = ProgressBarStyle.Continuous };
    private readonly Button toggleButton = new() { Name = "toggleButton" };
    private readonly Button resetButton = new() { Name = "resetButton", Text = "重置" };
    private readonly Button settingsButton = new() { Name = "settingsButton", Text = "设置" };
    private readonly System.Windows.Forms.Timer refreshTimer;
    private readonly ToolTip toolTip;
    private readonly ContextMenuStrip menu;
    private readonly ToolStripMenuItem toggleItem, topItem, soundItem;
    private SettingsForm? settingsForm;
    private bool compact, ready, changingLayout, trackingHover;
    private long lastTick;
    private string? saveError;
    private string settingsHint = "设置自动保存";
    private string lastTip = "";

    public Form1() : this(new SettingsStore(SettingsStore.DefaultPath)) { }

    public Form1(SettingsStore settingsStore)
    {
        store = settingsStore; settings = store.Load();
        model = new PomodoroTimer(settings.WorkMinutes, settings.BreakMinutes);
        InitializeComponent();
        Icon = applicationIcon;
        Font = new Font("Microsoft YaHei UI", 10);
        BackColor = Color.FromArgb(250, 247, 240); ForeColor = Color.FromArgb(62, 58, 52);
        phaseLabel.Font = new Font(Font.FontFamily, 14, FontStyle.Bold);
        statusLabel.ForeColor = helpLabel.ForeColor = Color.FromArgb(122, 113, 100);
        Controls.AddRange([face, phaseLabel, statusLabel, progress, toggleButton, resetButton, settingsButton, helpLabel]);
        foreach (var button in new[] { toggleButton, resetButton, settingsButton })
        {
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Color.FromArgb(221, 213, 199);
            button.Cursor = Cursors.Hand;
        }
        toggleButton.Click += (_, _) => Toggle(); resetButton.Click += (_, _) => ResetTimer();
        settingsButton.Click += (_, _) => OpenSettings();
        menu = new ContextMenuStrip(components!);
        toggleItem = new ToolStripMenuItem("开始", null, (_, _) => Toggle()); menu.Items.Add(toggleItem);
        menu.Items.Add("重置", null, (_, _) => ResetTimer());
        menu.Items.Add("时长设置", null, (_, _) => OpenSettings()); menu.Items.Add(new ToolStripSeparator());
        topItem = new ToolStripMenuItem("始终置顶", null, (_, _) => ApplySettings(settings.WorkMinutes, settings.BreakMinutes, !settings.AlwaysOnTop, settings.SoundEnabled));
        soundItem = new ToolStripMenuItem("声音提醒", null, (_, _) => ApplySettings(settings.WorkMinutes, settings.BreakMinutes, settings.AlwaysOnTop, !settings.SoundEnabled));
        menu.Items.Add(topItem); menu.Items.Add(soundItem);
        menu.Items.Add(new ToolStripMenuItem("恢复普通窗口", null, (_, _) => RestoreNormal()) { Name = "restoreItem" });
        InitializeTray();
        InitializeAppearance();
        ContextMenuStrip = face.ContextMenuStrip = menu; menu.Opening += (_, _) => RenderState();
        toolTip = new ToolTip(components!) { InitialDelay = 350, ReshowDelay = 100 };
        refreshTimer = new System.Windows.Forms.Timer(components!) { Interval = 100 };
        refreshTimer.Tick += (_, _) => { AdvanceTime(); RenderState(); };
        compact = settings.Compact; FormBorderStyle = compact ? FormBorderStyle.None : FormBorderStyle.Sizable;
        TopMost = settings.AlwaysOnTop;
        ClientSize = WindowLayout.ToPixels(new Size(settings.Width, settings.Height), DeviceDpi);
        lastTick = Stopwatch.GetTimestamp(); ready = true;
        ApplyWindowAppearance(); UpdateLayoutMode(); RenderState(); refreshTimer.Start();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitToWorkingArea();
    }

    private void FitToWorkingArea()
    {
        var workArea = Screen.FromControl(this).WorkingArea;
        if (Width > workArea.Width || Height > workArea.Height)
            Size = new Size(Math.Min(Width, workArea.Width), Math.Min(Height, workArea.Height));
        Location = new Point(Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width)),
            Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height)));
    }

    private void Toggle()
    {
        AdvanceTime();
        if (model.State == TimerState.Running) model.Pause(); else model.Start();
        lastTick = Stopwatch.GetTimestamp(); RenderState();
    }
    private void ResetTimer()
    {
        model.Reset(); lastTick = Stopwatch.GetTimestamp(); settingsHint = "设置自动保存"; RenderState();
    }
    private void AdvanceTime(bool allowSound = true)
    {
        long now = Stopwatch.GetTimestamp();
        bool changed = model.Advance(Stopwatch.GetElapsedTime(lastTick, now)); lastTick = now;
        if (changed && allowSound && settings.SoundEnabled) SystemSounds.Exclamation.Play();
    }
    private void RenderState()
    {
        face.UpdateTime(model);
        string phase = model.Phase == TimerPhase.Work ? "工作" : "休息";
        string state = model.State switch { TimerState.Running => "运行中", TimerState.Paused => "已暂停", _ => "待开始" };
        phaseLabel.Text = model.Phase == TimerPhase.Work ? "专注时间" : "休息一下"; phaseLabel.ForeColor = face.Accent;
        statusLabel.Text = model.State == TimerState.Idle ? "准备好，开始一段专注" : $"{state} · 本轮 {model.Duration.TotalMinutes:0} 分钟";
        toggleButton.Text = model.State switch { TimerState.Running => "暂停", TimerState.Paused => "继续", _ => "开始专注" };
        toggleItem.Text = toggleButton.Text;
        toggleButton.BackColor = face.Accent; toggleButton.ForeColor = Color.White;
        topItem.Checked = settings.AlwaysOnTop; soundItem.Checked = settings.SoundEnabled;
        progress.Value = Math.Clamp((int)(model.Progress * 1000), 0, 1000);
        string tip = $"{phase} · {state}（右键打开菜单，双击展开）";
        if (tip != lastTip) { toolTip.SetToolTip(face, tip); lastTip = tip; }
        Text = $"{model.DisplayTime} · {phase} · 番茄钟";
        UpdateTray(phase, state);
        settingsForm?.SetStatus(settingsHint, saveError);
    }
    private void OpenSettings()
    {
        if (!Visible || WindowState == FormWindowState.Minimized) ShowExisting();
        if (settingsForm == null || settingsForm.IsDisposed)
        {
            settingsForm = new SettingsForm(settings, ApplySettings, SetTrayOnly, SetOpacityPercent, SetClickThrough) { Icon = applicationIcon };
            settingsForm.FormClosed += (_, _) => settingsForm = null;
            settingsForm.SetStatus(settingsHint, saveError); settingsForm.Show(this);
        }
        else settingsForm.Activate();
    }
    private void ApplySettings(int work, int rest, bool top, bool sound)
    {
        AdvanceTime();
        bool workChanged = work != settings.WorkMinutes, breakChanged = rest != settings.BreakMinutes;
        model.Configure(work, rest); settings.WorkMinutes = work; settings.BreakMinutes = rest;
        settings.AlwaysOnTop = top; settings.SoundEnabled = sound; TopMost = top;
        if (model.State != TimerState.Idle && (workChanged || breakChanged))
            settingsHint = workChanged && breakChanged ? "下次工作／休息开始时分别生效" : workChanged ? "下次工作开始时生效" : "下次休息开始时生效";
        else if (model.State == TimerState.Idle) settingsHint = "设置已生效";
        settingsForm?.Sync(settings); SaveSettings(); RenderState();
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e); if (ready && !changingLayout) UpdateLayoutMode();
        if (ready && !restoringWindow && settings.TrayOnly && WindowState == FormWindowState.Minimized && Visible) HideToTray();
    }
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (ready && !IsDisposed) RenderState();
    }
    private void UpdateLayoutMode()
    {
        if (WindowState == FormWindowState.Minimized || changingLayout) return;
        changingLayout = true;
        try
        {
            Size logical = WindowLayout.ToLogical(ClientSize, DeviceDpi);
            bool next = WindowState != FormWindowState.Maximized && WindowLayout.IsCompact(compact, logical);
            if (compact != next)
            {
                toolTip.Hide(this); trackingHover = false;
                Size client = ClientSize; Point location = Location; MinimumSize = Size.Empty; compact = next;
                FormBorderStyle = compact ? FormBorderStyle.None : FormBorderStyle.Sizable;
                if (WindowState == FormWindowState.Normal) { ClientSize = client; Location = location; }
            }
            MinimumSize = SizeFromClientSize(WindowLayout.ToPixels(new Size(120, 64), DeviceDpi)); LayoutChildren();
            if (WindowState == FormWindowState.Normal)
            {
                logical = WindowLayout.ToLogical(ClientSize, DeviceDpi);
                settings.Width = logical.Width; settings.Height = logical.Height; settings.Compact = compact;
                if (!compact && logical.Width >= 360 && logical.Height >= 280)
                { settings.NormalWidth = logical.Width; settings.NormalHeight = logical.Height; }
            }
        }
        finally { changingLayout = false; }
    }
    private void LayoutChildren()
    {
        face.Compact = compact;
        foreach (Control child in Controls) child.Visible = !compact || child == face;
        if (compact) { face.Bounds = ClientRectangle; face.Invalidate(); return; }
        int S(int value) => (int)Math.Round(value * DeviceDpi / 96d);
        int margin = S(24), width = ClientSize.Width - 2 * margin;
        phaseLabel.SetBounds(margin, S(12), width, S(32));
        int footer = S(136);
        face.SetBounds(S(10), S(46), ClientSize.Width - S(20), Math.Max(S(50), ClientSize.Height - S(46) - footer));
        int y = ClientSize.Height - footer;
        statusLabel.SetBounds(margin, y, width, S(26)); progress.SetBounds(margin, y + S(32), width, S(5));
        int buttonWidth = (width - S(16)) / 3;
        toggleButton.SetBounds(margin, y + S(52), buttonWidth, S(38));
        resetButton.SetBounds(margin + buttonWidth + S(8), y + S(52), buttonWidth, S(38));
        settingsButton.SetBounds(margin + 2 * (buttonWidth + S(8)), y + S(52), buttonWidth, S(38));
        helpLabel.SetBounds(margin, y + S(99), width, S(24)); face.Invalidate();
    }
    private void RestoreNormal()
    {
        if (!Visible) ShowExisting();
        if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
        ClientSize = WindowLayout.ToPixels(new Size(settings.NormalWidth, settings.NormalHeight), DeviceDpi);
        UpdateLayoutMode(); FitToWorkingArea(); SaveSettings();
    }
    protected override void OnResizeEnd(EventArgs e) { base.OnResizeEnd(e); if (ready) SaveSettings(); }
    protected override void OnDpiChanged(DpiChangedEventArgs e) { base.OnDpiChanged(e); if (ready) UpdateLayoutMode(); }
    private void SaveSettings() { saveError = store.Save(settings); settingsForm?.SetStatus(settingsHint, saveError); }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSettings();
        if (settings.TrayOnly && !exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
        base.OnFormClosing(e);
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Space) { Toggle(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    internal void ShowExisting()
    {
        restoringWindow = true;
        try
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate(); NativeMethods.SetForegroundWindow(Handle);
        }
        finally { restoringWindow = false; }
        RenderState();
    }
    protected override void WndProc(ref Message m)
    {
        if (ready && settings.ClickThrough && m.Msg == NativeMethods.WmNcHitTest)
        {
            m.Result = (IntPtr)(-1); return;
        }
        if (ready && m.Msg == NativeMethods.WmPowerBroadcast)
        {
            int kind = m.WParam.ToInt32();
            if (kind == 4) { AdvanceTime(false); model.Pause(); }
            else if (kind is 7 or 18) model.Pause();
            if (kind is 4 or 7 or 18) { lastTick = Stopwatch.GetTimestamp(); RenderState(); }
        }
        if (ready && compact)
        {
            if (m.Msg == 0xA0 && !trackingHover) // WM_NCMOUSEMOVE
            {
                NativeMethods.TrackNonclientHover(Handle); trackingHover = true;
            }
            if (m.Msg == 0x2A0) // WM_NCMOUSEHOVER
            {
                if (!menu.Visible)
                {
                    Point p = PointToClient(Cursor.Position);
                    p.X = Math.Clamp(p.X + 12, 0, ClientSize.Width);
                    p.Y = Math.Clamp(p.Y + 20, 0, ClientSize.Height);
                    toolTip.Show(lastTip, this, p, 3500);
                }
                return;
            }
            if (m.Msg == 0x2A2) // WM_NCMOUSELEAVE
            {
                toolTip.Hide(this); trackingHover = false;
            }
            if (m.Msg == NativeMethods.WmNcHitTest)
            {
                Point p = PointToClient(NativeMethods.MessagePoint(m.LParam));
                int edge = Math.Max(5, (int)Math.Round(6 * DeviceDpi / 96d));
                bool left = p.X < edge, right = p.X >= ClientSize.Width - edge, top = p.Y < edge, bottom = p.Y >= ClientSize.Height - edge;
                int hit = top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 2;
                m.Result = (IntPtr)hit; return;
            }
            if (m.Msg == NativeMethods.WmNcDoubleClick && m.WParam.ToInt32() == NativeMethods.HtCaption) { RestoreNormal(); return; }
            if (m.Msg == NativeMethods.WmNcRightButtonUp) { menu.Show(Cursor.Position); return; }
        }
        base.WndProc(ref m);
    }
}
