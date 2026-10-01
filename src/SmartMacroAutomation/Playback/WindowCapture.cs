using System.Drawing;
using System.Drawing.Imaging;
using SmartMacroAutomation.Native;

namespace SmartMacroAutomation.Playback;

internal static class WindowCapture
{
    /// <summary>
    /// Captures what a window itself draws, even if other windows cover it, using PrintWindow
    /// (PW_RENDERFULLCONTENT also works for GPU-drawn apps like Chromium/Electron).
    /// The bitmap covers the window's visible bounds, so pixel (0,0) is the window's top-left corner.
    /// Returns null if the window is minimized or cannot be captured.
    /// </summary>
    public static Bitmap? Capture(nint hWnd)
    {
        using var dpi = DpiScope.PerMonitor();

        if (!NativeMethods.IsWindow(hWnd) || NativeMethods.IsIconic(hWnd))
            return null;

        NativeMethods.GetWindowRect(hWnd, out var full);
        var visible = WindowFinder.GetVisibleBounds(hWnd);
        int fullW = full.Right - full.Left, fullH = full.Bottom - full.Top;
        if (fullW <= 0 || fullH <= 0 || visible.Width <= 0 || visible.Height <= 0)
            return null;

        using var whole = new Bitmap(fullW, fullH, PixelFormat.Format32bppArgb);
        bool ok;
        using (var g = Graphics.FromImage(whole))
        {
            nint hdc = g.GetHdc();
            try { ok = NativeMethods.PrintWindow(hWnd, hdc, NativeMethods.PW_RENDERFULLCONTENT); }
            finally { g.ReleaseHdc(hdc); }
        }
        if (!ok)
            return null;

        // PrintWindow draws the full window rect, including the invisible resize border; cut it off.
        var crop = new Rectangle(visible.X - full.Left, visible.Y - full.Top, visible.Width, visible.Height);
        crop.Intersect(new Rectangle(0, 0, fullW, fullH));
        return whole.Clone(crop, PixelFormat.Format32bppArgb);
    }

    /// <summary>Copies a rectangle out of a window capture; parts outside the capture stay black.</summary>
    public static Bitmap Crop(Bitmap source, Rectangle area)
    {
        var result = new Bitmap(Math.Max(1, area.Width), Math.Max(1, area.Height), PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result);
        g.Clear(Color.Black);
        g.DrawImage(source, new Rectangle(0, 0, area.Width, area.Height), area, GraphicsUnit.Pixel);
        return result;
    }
}
