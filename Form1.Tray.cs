namespace TomatoClock;

public partial class Form1
{
    private NotifyIcon trayIcon = null!;
    private ToolStripMenuItem trayOnlyItem = null!;
    private ToolStripMenuItem showWindowItem = null!;
    private ToolStripMenuItem hideWindowItem = null!;
    private bool exitRequested;
    private bool restoringWindow;

    private void InitializeTray()
    {
        trayOnlyItem = new ToolStripMenuItem("隐藏任务栏图标（使用托盘）", null,
            (_, _) => SetTrayOnly(!settings.TrayOnly)) { Name = "trayOnlyItem" };
        showWindowItem = new ToolStripMenuItem("显示窗口", null,
            (_, _) => ShowExisting()) { Name = "showWindowItem" };
        hideWindowItem = new ToolStripMenuItem("收起到托盘", null,
            (_, _) => HideToTray()) { Name = "hideWindowItem" };
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(trayOnlyItem);
        menu.Items.Add(showWindowItem);
        menu.Items.Add(hideWindowItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitApplication()) { Name = "exitItem" });
        trayIcon = new NotifyIcon(components!)
        {
            Icon = applicationIcon,
            Text = "番茄钟",
            ContextMenuStrip = menu,
            Visible = settings.TrayOnly || settings.ClickThrough
        };
        trayIcon.DoubleClick += (_, _) => ShowExisting();
        ShowInTaskbar = !settings.TrayOnly;
    }

    private void SetTrayOnly(bool enabled)
    {
        settings.TrayOnly = enabled;
        // Publish the recovery entry before removing the taskbar entry.
        if (enabled) trayIcon.Visible = true;
        ShowInTaskbar = !enabled;
        // Also refresh when ShowInTaskbar was already false: that property alone
        // does not exclude a visible window from the Alt-Tab switcher.
        ApplyWindowAppearance();
        if (!enabled)
        {
            if (!Visible || WindowState == FormWindowState.Minimized) ShowExisting();
            trayIcon.Visible = settings.ClickThrough;
        }
        settingsForm?.Sync(settings);
        SaveSettings(); RenderState();
    }

    private void HideToTray()
    {
        if (!settings.TrayOnly) return;
        settingsForm?.Close();
        toolTip.Hide(this);
        Hide();
        SaveSettings();
        RenderState();
    }

    private void UpdateTray(string phase, string state)
    {
        trayOnlyItem.Checked = settings.TrayOnly;
        clickThroughItem.Checked = settings.ClickThrough;
        showWindowItem.Enabled = !Visible || WindowState == FormWindowState.Minimized;
        hideWindowItem.Enabled = settings.TrayOnly && Visible;
        // Short enough for Windows' notification tooltip length limit.
        trayIcon.Text = $"番茄钟 · {phase} {model.DisplayTime} · {state}";
    }

    private void ExitApplication()
    {
        exitRequested = true;
        Close();
    }
}
