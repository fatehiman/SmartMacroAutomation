using System.Drawing;
using System.Drawing.Imaging;

namespace SmartMacroAutomation.Playback;

internal static class ScreenCapture
{
    /// <summary>Captures a rectangle of the desktop in screen coordinates.</summary>
    public static Bitmap Capture(int left, int top, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(left, top, 0, 0, new Size(width, height));
        return bitmap;
    }

    /// <summary>Captures the default 50x50 area centered on a point.</summary>
    public static Bitmap CaptureAround(int x, int y, int size = 50)
    {
        int half = size / 2;
        return Capture(x - half, y - half, size, size);
    }
}
