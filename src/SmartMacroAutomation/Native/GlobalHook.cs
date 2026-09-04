using System.Runtime.InteropServices;

namespace SmartMacroAutomation.Native;

internal sealed class MouseClickEventArgs : EventArgs
{
    public int X { get; init; }
    public int Y { get; init; }
    public Models.MouseButtonKind Button { get; init; }
}

internal sealed class KeyEventArgsRaw : EventArgs
{
    public int VkCode { get; init; }
    public bool IsKeyUp { get; init; }
}

/// <summary>
/// Installs process-wide low-level mouse and keyboard hooks so the recorder
/// can see actions performed in any application, not just this one.
/// Must be created/disposed on a thread that pumps Windows messages (the UI thread).
/// </summary>
internal sealed class GlobalHook : IDisposable
{
    private nint _mouseHookId;
    private nint _keyboardHookId;
    private readonly NativeMethods.LowLevelHookProc _mouseProc;
    private readonly NativeMethods.LowLevelHookProc _keyboardProc;

    public event EventHandler<MouseClickEventArgs>? MouseClick;
    public event EventHandler<KeyEventArgsRaw>? KeyEvent;

    /// <summary>When set, clicks landing on windows owned by this process are ignored.</summary>
    public bool IgnoreOwnProcessClicks { get; set; } = true;

    public GlobalHook()
    {
        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;
    }

    public void Start()
    {
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        nint hModule = NativeMethods.GetModuleHandle(curModule.ModuleName);

        _mouseHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, hModule, 0);
        _keyboardHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, hModule, 0);
    }

    private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN or NativeMethods.WM_MBUTTONDOWN)
            {
                var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

                bool isOwn = false;
                if (IgnoreOwnProcessClicks)
                {
                    nint hwnd = NativeMethods.WindowFromPoint(data.pt);
                    if (hwnd != 0)
                    {
                        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                        isOwn = pid == (uint)Environment.ProcessId;
                    }
                }

                if (!isOwn)
                {
                    var button = msg switch
                    {
                        NativeMethods.WM_RBUTTONDOWN => Models.MouseButtonKind.Right,
                        NativeMethods.WM_MBUTTONDOWN => Models.MouseButtonKind.Middle,
                        _ => Models.MouseButtonKind.Left
                    };
                    MouseClick?.Invoke(this, new MouseClickEventArgs { X = data.pt.X, Y = data.pt.Y, Button = button });
                }
            }
        }
        return NativeMethods.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN or NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP)
            {
                var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                bool isUp = msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
                KeyEvent?.Invoke(this, new KeyEventArgsRaw { VkCode = (int)data.vkCode, IsKeyUp = isUp });
            }
        }
        return NativeMethods.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_mouseHookId != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = 0;
        }
        if (_keyboardHookId != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = 0;
        }
    }
}
