using TomatoClock.Core;

namespace TomatoClock.UI;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown work = new() { Name = "workMinutes", Minimum = 1, Maximum = 180, Dock = DockStyle.Fill };
    private readonly NumericUpDown rest = new() { Name = "breakMinutes", Minimum = 1, Maximum = 180, Dock = DockStyle.Fill };
    private readonly CheckBox top = new() { Name = "topMostCheck", Text = "始终置顶", AutoSize = true };
    private readonly CheckBox sound = new() { Name = "soundCheck", Text = "声音提醒", AutoSize = true };
    private readonly CheckBox tray = new() { Name = "trayOnlyCheck", Text = "隐藏任务栏图标（使用托盘）", AutoSize = true };
    private readonly TrackBar opacity = new() { Name = "opacitySlider", Minimum = 10, Maximum = 100, Value = 100, TickFrequency = 10, SmallChange = 5, LargeChange = 10, AutoSize = false, Dock = DockStyle.Fill };
    private readonly Label opacityLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox clickThrough = new() { Name = "clickThroughCheck", Text = "鼠标穿透计时窗口", AutoSize = true };
    private readonly Label hint = new() { Name = "effectiveHint", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(115, 105, 91) };
    private readonly Label error = new() { Dock = DockStyle.Fill, ForeColor = Color.Firebrick };
    private bool syncing;
    private bool hideFromWindowSwitcher;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            if (hideFromWindowSwitcher)
            {
                parameters.ExStyle |= NativeMethods.WsExToolWindow;
                parameters.ExStyle &= ~NativeMethods.WsExAppWindow;
            }
            return parameters;
        }
    }
    public SettingsForm(AppSettings settings, Action<int, int, bool, bool> changed, Action<bool> trayChanged,
        Action<int> opacityChanged, Action<bool> clickThroughChanged)
    {
        SuspendLayout();
        Text = "番茄钟设置"; Font = new Font("Microsoft YaHei UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(380, 500);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent; BackColor = Color.FromArgb(250, 247, 240);
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(22), ColumnCount = 2, RowCount = 11 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        foreach (int height in new[] { 42, 42, 36, 36, 36, 44, 36, 32, 48, 48, 40 }) table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        table.Controls.Add(new Label { Text = "工作时长（分钟）", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); table.Controls.Add(work, 1, 0);
        table.Controls.Add(new Label { Text = "休息时长（分钟）", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); table.Controls.Add(rest, 1, 1);
        table.Controls.Add(top, 0, 2); table.SetColumnSpan(top, 2);
        table.Controls.Add(sound, 0, 3); table.SetColumnSpan(sound, 2);
        table.Controls.Add(tray, 0, 4); table.SetColumnSpan(tray, 2);
        table.Controls.Add(opacityLabel, 0, 5); table.Controls.Add(opacity, 1, 5);
        table.Controls.Add(clickThrough, 0, 6); table.SetColumnSpan(clickThrough, 2);
        var recoveryHint = new Label { Text = "穿透后从托盘取消；设置窗口不受影响。", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(115, 105, 91) };
        table.Controls.Add(recoveryHint, 0, 7); table.SetColumnSpan(recoveryHint, 2);
        table.Controls.Add(hint, 0, 8); table.SetColumnSpan(hint, 2);
        table.Controls.Add(error, 0, 9); table.SetColumnSpan(error, 2);
        var close = new Button { Text = "完成", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat }; close.Click += (_, _) => Close();
        table.Controls.Add(close, 1, 10);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(table); Controls.Add(scroll); Sync(settings);
        void Notify() { if (!syncing) changed((int)work.Value, (int)rest.Value, top.Checked, sound.Checked); }
        work.ValueChanged += (_, _) => Notify(); rest.ValueChanged += (_, _) => Notify();
        top.CheckedChanged += (_, _) => Notify(); sound.CheckedChanged += (_, _) => Notify();
        tray.CheckedChanged += (_, _) => { if (!syncing) trayChanged(tray.Checked); };
        opacity.ValueChanged += (_, _) => { if (!syncing) opacityChanged(opacity.Value); };
        clickThrough.CheckedChanged += (_, _) => { if (!syncing) clickThroughChanged(clickThrough.Checked); };
        AutoScaleDimensions = new SizeF(96, 96);
        ResumeLayout(true);
    }
    public void Sync(AppSettings settings)
    {
        if (hideFromWindowSwitcher != settings.TrayOnly)
        {
            hideFromWindowSwitcher = settings.TrayOnly;
            if (IsHandleCreated) UpdateStyles();
        }
        syncing = true;
        work.Value = settings.WorkMinutes; rest.Value = settings.BreakMinutes;
        top.Checked = settings.AlwaysOnTop; sound.Checked = settings.SoundEnabled;
        tray.Checked = settings.TrayOnly;
        opacity.Value = settings.OpacityPercent;
        opacityLabel.Text = $"不透明度 {settings.OpacityPercent}%";
        clickThrough.Checked = settings.ClickThrough;
        syncing = false;
    }
    public void SetStatus(string message, string? saveError) { hint.Text = message; error.Text = saveError ?? ""; }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Rectangle area = Screen.FromControl(this).WorkingArea;
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        Location = new Point(Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width)),
            Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height)));
    }
}
