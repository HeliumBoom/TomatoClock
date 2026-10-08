using System.Drawing.Drawing2D;
using TomatoClock.Core;

namespace TomatoClock.UI;

internal sealed class ClockFace : Control
{
    private TimerState state;
    private TimerPhase phase;
    public bool Compact { get; set; }
    public Color Accent => phase == TimerPhase.Work ? Color.FromArgb(198, 79, 53) : Color.FromArgb(35, 124, 98);
    public ClockFace()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Name = "clockFace"; AccessibleName = "番茄钟剩余时间"; TabStop = false;
    }
    public void UpdateTime(PomodoroTimer model)
    {
        bool changed = Text != model.DisplayTime || state != model.State || phase != model.Phase;
        Text = model.DisplayTime; state = model.State; phase = model.Phase;
        AccessibleDescription = $"{(phase == TimerPhase.Work ? "工作" : "休息")}，{Text}，{state}";
        if (changed) Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 1 || Height < 1) return;
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f, margin = (Compact ? 8 : 12) * scale;
        float marker = state == TimerState.Paused ? 14 * scale : 0;
        var area = new RectangleF(margin, margin, Math.Max(1, Width - 2 * margin - marker), Math.Max(1, Height - 2 * margin));
        using var format = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        float size = Math.Max(1, Math.Min(area.Height * .78f, area.Width));
        Font? clockFont = null;
        while (true)
        {
            clockFont?.Dispose(); clockFont = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
            SizeF measured = g.MeasureString(Text, clockFont, int.MaxValue, format);
            if ((measured.Width <= area.Width && measured.Height <= area.Height) || size <= 1) break;
            size = Math.Max(1, size - scale);
        }
        using (clockFont)
        using (var brush = new SolidBrush(state == TimerState.Paused ? Color.FromArgb(123, 118, 110) : Accent))
        {
            g.DrawString(Text, clockFont, brush, area, format);
            if (marker > 0)
            {
                float x = Width - margin - marker + 3 * scale, y = Height / 2f - 6 * scale;
                g.FillRectangle(brush, x, y, 3 * scale, 12 * scale);
                g.FillRectangle(brush, x + 6 * scale, y, 3 * scale, 12 * scale);
            }
        }
        if (Compact)
        {
            using var pen = new Pen(Color.FromArgb(225, 218, 206));
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
    protected override void WndProc(ref Message m)
    {
        // Forward native hit testing to the parent for border resizing and caption dragging.
        if (Compact && m.Msg == NativeMethods.WmNcHitTest) { m.Result = (IntPtr)(-1); return; }
        base.WndProc(ref m);
    }
}
