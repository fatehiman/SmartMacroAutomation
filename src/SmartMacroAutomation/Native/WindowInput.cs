using System.Drawing;
using SmartMacroAutomation.Models;

namespace SmartMacroAutomation.Native;

/// <summary>
/// Clicks inside a specific window, either in the background (window messages, the real
/// cursor never moves) or in the foreground (activate the window, then real input).
/// </summary>
internal static class WindowInput
{
    /// <summary>How many hover (WM_MOUSEMOVE) messages lead up to the click, and how far apart in time.</summary>
    private const int HoverSteps = 10;
    private const int HoverStepMs = 15;
    private const int HoverApproachPx = 40;

    /// <summary>Wait after the hover moves, so the app has time to show its hover state before the press.</summary>
    private const int HoverSettleMs = 250;
    private const int PressMs = 60;

    /// <summary>Converts a WindowClick's anchor + offset into a point inside the window (window coordinates).</summary>
    public static Point ToWindowPoint(Size windowSize, WindowAnchor anchor, int x, int y) => anchor switch
    {
        WindowAnchor.TopRight => new Point(windowSize.Width - x, y),
        WindowAnchor.BottomLeft => new Point(x, windowSize.Height - y),
        WindowAnchor.BottomRight => new Point(windowSize.Width - x, windowSize.Height - y),
        _ => new Point(x, y)
    };

    /// <summary>The inverse of <see cref="ToWindowPoint"/>: offset of a window point from the given corner.</summary>
    public static Point ToAnchorOffset(Size windowSize, WindowAnchor anchor, Point windowPoint) =>
        // The mapping is its own inverse: distance from the right edge = width - x, and so on.
        ToWindowPoint(windowSize, anchor, windowPoint.X, windowPoint.Y);

    /// <summary>
    /// Clicks <paramref name="screenPoint"/> inside <paramref name="topWindow"/> by posting mouse messages
    /// to it (or to the child window under the point). A short series of WM_MOUSEMOVE messages comes
    /// first: Chromium/Electron apps (like NVIDIA Broadcast) ignore a press that was not preceded by hover.
    /// Works while the window is covered by other windows; the real cursor and focus do not change.
    /// </summary>
    public static void BackgroundClick(nint topWindow, Point screenPoint, MouseButtonKind button)
    {
        using var dpi = DpiScope.PerMonitor();

        nint target = FindDeepestChild(topWindow, screenPoint);
        var client = new NativeMethods.POINT { X = screenPoint.X, Y = screenPoint.Y };
        NativeMethods.ScreenToClient(target, ref client);

        // Approach from the left (or from the right when the point is near the left edge).
        int startX = client.X >= HoverApproachPx ? client.X - HoverApproachPx : client.X + HoverApproachPx;
        for (int i = 1; i <= HoverSteps; i++)
        {
            int x = startX + (client.X - startX) * i / HoverSteps;
            NativeMethods.PostMessage(target, NativeMethods.WM_MOUSEMOVE, 0, MakeLParam(x, client.Y));
            Thread.Sleep(HoverStepMs);
        }
        Thread.Sleep(HoverSettleMs);

        var (down, up, mk) = button switch
        {
            MouseButtonKind.Right => (NativeMethods.WM_RBUTTONDOWN, NativeMethods.WM_RBUTTONUP, NativeMethods.MK_RBUTTON),
            MouseButtonKind.Middle => (NativeMethods.WM_MBUTTONDOWN, NativeMethods.WM_MBUTTONUP, NativeMethods.MK_MBUTTON),
            _ => (NativeMethods.WM_LBUTTONDOWN, NativeMethods.WM_LBUTTONUP, NativeMethods.MK_LBUTTON)
        };

        nint lParam = MakeLParam(client.X, client.Y);
        NativeMethods.PostMessage(target, down, mk, lParam);
        Thread.Sleep(PressMs);
        NativeMethods.PostMessage(target, up, 0, lParam);

        // The real cursor never went there, so tell the app the (simulated) mouse has left again;
        // otherwise it may keep drawing the button in its hover state.
        Thread.Sleep(HoverStepMs);
        NativeMethods.PostMessage(target, NativeMethods.WM_MOUSELEAVE, 0, 0);
    }

    /// <summary>
    /// Restores (if minimized) and activates the window, glides the real cursor to the point and clicks with SendInput.
    /// </summary>
    public static void ForegroundClick(nint topWindow, Point screenPoint, MouseButtonKind button, double pixelsPerSecond)
    {
        using var dpi = DpiScope.PerMonitor();

        if (NativeMethods.IsIconic(topWindow))
            NativeMethods.ShowWindow(topWindow, NativeMethods.SW_RESTORE);

        // Windows only lets the foreground app hand over focus. A synthetic Alt press makes this
        // process count as "the last input source", which unlocks SetForegroundWindow.
        NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, 0, 0);
        NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
        NativeMethods.SetForegroundWindow(topWindow);
        Thread.Sleep(150);

        HumanMouse.MoveTo(screenPoint.X, screenPoint.Y, pixelsPerSecond);
        InputSimulator.Click(screenPoint.X, screenPoint.Y, button);
    }

    /// <summary>Walks down the child window tree to the visible, enabled child under the point.</summary>
    private static nint FindDeepestChild(nint parent, Point screenPoint)
    {
        const uint flags = NativeMethods.CWP_SKIPINVISIBLE | NativeMethods.CWP_SKIPDISABLED | NativeMethods.CWP_SKIPTRANSPARENT;

        nint current = parent;
        for (int depth = 0; depth < 32; depth++)
        {
            var pt = new NativeMethods.POINT { X = screenPoint.X, Y = screenPoint.Y };
            NativeMethods.ScreenToClient(current, ref pt);
            nint child = NativeMethods.ChildWindowFromPointEx(current, pt, flags);
            if (child == 0 || child == current)
                break;
            current = child;
        }
        return current;
    }

    private static nint MakeLParam(int x, int y) => (nint)((y << 16) | (x & 0xFFFF));
}
