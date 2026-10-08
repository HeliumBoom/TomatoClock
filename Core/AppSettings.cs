namespace TomatoClock.Core;

public sealed class AppSettings
{
    public int WorkMinutes { get; set; } = 25;
    public int BreakMinutes { get; set; } = 5;
    public bool AlwaysOnTop { get; set; }
    public bool SoundEnabled { get; set; }
    public bool TrayOnly { get; set; }
    public int OpacityPercent { get; set; } = 100;
    public bool ClickThrough { get; set; }
    public bool Compact { get; set; }
    public int Width { get; set; } = 400;
    public int Height { get; set; } = 420;
    public int NormalWidth { get; set; } = 400;
    public int NormalHeight { get; set; } = 420;
}
