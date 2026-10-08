using System.ComponentModel;

namespace TomatoClock;

public partial class Form1
{
    private ToolStripMenuItem clickThroughItem = null!;

    // Keep styles in CreateParams so taskbar and border changes cannot lose them
    // when WinForms recreates the native window.
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            if (settings?.TrayOnly == true)
            {
                parameters.ExStyle |= NativeMethods.WsExToolWindow;
                parameters.ExStyle &= ~NativeMethods.WsExAppWindow;
            }
            if (settings?.ClickThrough == true)
                parameters.ExStyle |= NativeMethods.WsExTransparent | NativeMethods.WsExLayered;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (ready) ApplyWindowAppearance();
    }

    private void InitializeAppearance()
    {
        clickThroughItem = new ToolStripMenuItem("鼠标穿透", null,
            (_, _) => SetClickThrough(!settings.ClickThrough)) { Name = "clickThroughItem" };
        menu.Items.Insert(menu.Items.Count - 2, clickThroughItem);
    }

    private void SetOpacityPercent(int percent)
    {
        settings.OpacityPercent = Math.Clamp(percent, 10, 100);
        ApplyWindowAppearance();
        settingsForm?.Sync(settings);
        SaveSettings(); RenderState();
    }

    private void SetClickThrough(bool enabled)
    {
        // Ensure an interactive recovery entry exists before the window stops
        // accepting mouse input. Taskbar visibility remains an independent choice.
        if (enabled) trayIcon.Visible = true;
        settings.ClickThrough = enabled;
        toolTip.Hide(this); trackingHover = false;
        ApplyWindowAppearance();
        trayIcon.Visible = settings.TrayOnly || enabled;
        settingsForm?.Sync(settings);
        SaveSettings(); RenderState();
    }

    private void ApplyWindowAppearance()
    {
        Opacity = settings.OpacityPercent / 100d;
        if (!IsHandleCreated) return;
        UpdateStyles();
        // Opacity == 1 normally removes layered-window support in WinForms.
        // Explicitly initialize alpha for fully opaque click-through windows too.
        if (settings.ClickThrough && !NativeMethods.SetLayeredWindowAttributes(
            Handle, 0, (byte)Math.Round(settings.OpacityPercent * 255d / 100), 2))
            throw new Win32Exception();
    }
}
