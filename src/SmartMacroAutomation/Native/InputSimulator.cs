using SmartMacroAutomation.Models;

namespace SmartMacroAutomation.Native;

/// <summary>
/// Sends synthetic mouse and keyboard input using the Win32 SendInput API.
/// </summary>
internal static class InputSimulator
{
    public static void MoveTo(int x, int y)
    {
        NativeMethods.SetCursorPos(x, y);
    }

    /// <summary>
    /// Moves the cursor to (x, y) in a straight line over time, like a hand would.
    /// <paramref name="pixelsPerSecond"/> of 0 or less jumps there instantly.
    /// </summary>
    public static void MoveToHumanLike(int x, int y, double pixelsPerSecond)
    {
        HumanMouse.MoveTo(x, y, pixelsPerSecond);
    }

    public static void Click(int x, int y, MouseButtonKind button)
    {
        MoveTo(x, y);

        var (down, up) = button switch
        {
            MouseButtonKind.Right => (NativeMethods.MOUSEEVENTF_RIGHTDOWN, NativeMethods.MOUSEEVENTF_RIGHTUP),
            MouseButtonKind.Middle => (NativeMethods.MOUSEEVENTF_MIDDLEDOWN, NativeMethods.MOUSEEVENTF_MIDDLEUP),
            _ => (NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP)
        };

        SendMouse(down);
        SendMouse(up);
    }

    private static void SendMouse(uint flags)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT { dwFlags = flags }
            }
        };
        NativeMethods.SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }

    public static void SendKey(int vkCode, bool isKeyUp)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = (ushort)vkCode,
                    dwFlags = isKeyUp ? NativeMethods.KEYEVENTF_KEYUP : 0
                }
            }
        };
        NativeMethods.SendInput(1, new[] { input }, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
