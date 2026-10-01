using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace SmartMacroAutomation.Native;

/// <summary>One physical monitor, in physical (not DPI-scaled) virtual-screen pixels.</summary>
internal sealed record MonitorInfo(int Number, string DeviceName, Rectangle Bounds, bool IsPrimary);

/// <summary>A snapshot of a top-level window's identity and geometry.</summary>
internal sealed class WindowInfo
{
    public nint Handle { get; init; }
    public string Title { get; init; } = "";
    public string ProcessName { get; init; } = "";
    public string ClassName { get; init; } = "";

    /// <summary>
    /// The visible frame of the window in virtual-screen pixels (all monitors form one coordinate space).
    /// Uses the DWM frame bounds, so the invisible resize border Windows 10/11 adds around normal windows
    /// is not counted. Empty while the window is minimized.
    /// </summary>
    public Rectangle Bounds { get; init; }

    public bool IsMinimized { get; init; }

    /// <summary>Monitors the window overlaps. One entry when the window sits on a single monitor.</summary>
    public IReadOnlyList<MonitorInfo> Monitors { get; init; } = Array.Empty<MonitorInfo>();

    /// <summary>A one-line human readable description: position, size and monitor.</summary>
    public string Describe()
    {
        if (IsMinimized)
            return $"'{Title}' ({ProcessName}) is minimized.";

        string text = $"'{Title}' ({ProcessName}): x={Bounds.X} y={Bounds.Y} w={Bounds.Width}px h={Bounds.Height}px";
        if (Monitors.Count == 1)
        {
            var m = Monitors[0];
            text += $", monitor {m.Number}{(m.IsPrimary ? " (primary)" : "")}" +
                    $", position on that monitor x={Bounds.X - m.Bounds.X} y={Bounds.Y - m.Bounds.Y}";
        }
        else if (Monitors.Count > 1)
        {
            text += $", spans monitors {string.Join(", ", Monitors.Select(m => m.Number))}";
        }
        return text;
    }
}

/// <summary>
/// Finds top-level windows by title / process name and reads their position, size and monitor.
/// All calls run per-monitor DPI aware, so the numbers are real screen pixels even on scaled monitors.
/// </summary>
internal static class WindowFinder
{
    /// <summary>
    /// Returns the best visible top-level window whose title contains <paramref name="titleContains"/>
    /// (case-insensitive), optionally limited to a process. An exact title match beats a partial one;
    /// otherwise the topmost match in Z-order wins. Windows of this application are ignored.
    /// </summary>
    public static WindowInfo? Find(string? titleContains, string? processName)
    {
        using var dpi = DpiScope.PerMonitor();

        string title = titleContains?.Trim() ?? "";
        string process = NormalizeProcessName(processName);
        if (title.Length == 0 && process.Length == 0)
            return null; // would match any window

        int ownPid = Environment.ProcessId;

        nint exact = 0, partial = 0;
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!IsCandidate(hWnd))
                return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == ownPid)
                return true;

            string text = GetTitle(hWnd);
            if (text.Length == 0 || !text.Contains(title, StringComparison.OrdinalIgnoreCase))
                return true;

            if (process.Length > 0 && !string.Equals(GetProcessName(pid), process, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(text, title, StringComparison.OrdinalIgnoreCase))
            {
                exact = hWnd;
                return false; // can't do better than an exact match
            }

            if (partial == 0)
                partial = hWnd;
            return true;
        }, 0);

        nint found = exact != 0 ? exact : partial;
        return found != 0 ? Describe(found) : null;
    }

    /// <summary>Re-reads the current geometry of an already found window. Null if the window no longer exists.</summary>
    public static WindowInfo? Refresh(nint hWnd)
    {
        using var dpi = DpiScope.PerMonitor();
        return NativeMethods.IsWindow(hWnd) ? Describe(hWnd) : null;
    }

    private static WindowInfo Describe(nint hWnd)
    {
        NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
        bool minimized = NativeMethods.IsIconic(hWnd);
        var bounds = minimized ? Rectangle.Empty : GetVisibleBounds(hWnd);

        var className = new StringBuilder(256);
        NativeMethods.GetClassName(hWnd, className, className.Capacity);

        return new WindowInfo
        {
            Handle = hWnd,
            Title = GetTitle(hWnd),
            ProcessName = GetProcessName(pid),
            ClassName = className.ToString(),
            Bounds = bounds,
            IsMinimized = minimized,
            Monitors = minimized ? Array.Empty<MonitorInfo>() : GetMonitors().Where(m => m.Bounds.IntersectsWith(bounds)).ToList()
        };
    }

    /// <summary>Window bounds without the invisible resize border (falls back to GetWindowRect).</summary>
    public static Rectangle GetVisibleBounds(nint hWnd)
    {
        if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out NativeMethods.RECT r, Marshal.SizeOf<NativeMethods.RECT>()) != 0)
        {
            NativeMethods.GetWindowRect(hWnd, out r);
        }
        return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
    }

    /// <summary>All monitors. The number matches Windows' "\\.\DISPLAYn" device name (n), falling back to list order.</summary>
    public static List<MonitorInfo> GetMonitors()
    {
        using var dpi = DpiScope.PerMonitor();

        var result = new List<MonitorInfo>();
        EnumDisplayMonitors(0, 0, (hMonitor, _, _, _) =>
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                string device = info.szDevice ?? "";
                string digits = new string(device.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
                int number = int.TryParse(digits, out int n) ? n : result.Count + 1;
                var r = info.rcMonitor;
                result.Add(new MonitorInfo(number, device, Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom), (info.dwFlags & 1) != 0));
            }
            return true;
        }, 0);
        return result.OrderBy(m => m.Number).ToList();
    }

    private static bool IsCandidate(nint hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd))
            return false;

        // Cloaked windows (e.g. suspended UWP apps, windows on another virtual desktop) are "visible" but not on screen.
        return NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) != 0 || cloaked == 0;
    }

    private static string GetTitle(nint hWnd)
    {
        int length = NativeMethods.GetWindowTextLength(hWnd);
        if (length <= 0)
            return "";
        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetProcessName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return "";
        }
    }

    private static string NormalizeProcessName(string? name)
    {
        name = name?.Trim() ?? "";
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    // ---- monitor enumeration ----

    private delegate bool MonitorEnumProc(nint hMonitor, nint hdc, nint lprcMonitor, nint dwData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public NativeMethods.RECT rcMonitor;
        public NativeMethods.RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX lpmi);
}

/// <summary>
/// Temporarily makes the current thread per-monitor DPI aware (V2), so window and monitor
/// coordinates come back in real pixels instead of being scaled for a "system DPI" app.
/// Restores the previous setting on Dispose.
/// </summary>
internal readonly struct DpiScope : IDisposable
{
    private static readonly nint PerMonitorAwareV2 = -4;
    private readonly nint _previous;

    private DpiScope(nint previous) => _previous = previous;

    public static DpiScope PerMonitor()
    {
        try
        {
            return new DpiScope(SetThreadDpiAwarenessContext(PerMonitorAwareV2));
        }
        catch (EntryPointNotFoundException)
        {
            return new DpiScope(0); // Windows older than 10 1607
        }
    }

    public void Dispose()
    {
        if (_previous != 0)
            SetThreadDpiAwarenessContext(_previous);
    }

    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint dpiContext);
}
