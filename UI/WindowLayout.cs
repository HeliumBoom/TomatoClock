namespace TomatoClock.UI;

internal static class WindowLayout
{
    public static bool IsCompact(bool current, Size size) => current
        ? size.Width < 360 || size.Height < 280 : size.Width < 320 || size.Height < 240;
    public static Size ToLogical(Size size, int dpi) => new((int)Math.Round(size.Width * 96d / dpi), (int)Math.Round(size.Height * 96d / dpi));
    public static Size ToPixels(Size size, int dpi) => new((int)Math.Round(size.Width * dpi / 96d), (int)Math.Round(size.Height * dpi / 96d));
}
