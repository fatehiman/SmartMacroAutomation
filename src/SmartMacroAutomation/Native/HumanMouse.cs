using System.Diagnostics;

namespace SmartMacroAutomation.Native;

/// <summary>
/// Moves the cursor to a target position the way a hand would: along a straight
/// line, over time, starting slow, speeding up in the middle and slowing down
/// again at the end - instead of teleporting there with a single SetCursorPos.
/// </summary>
internal static class HumanMouse
{
    /// <summary>Shortest movement we bother to animate; below this the cursor is placed directly.</summary>
    private const double MinDistancePx = 3;

    /// <summary>Safety clamps so a very short or very long move stays usable.</summary>
    private const double MinDurationMs = 40;
    private const double MaxDurationMs = 5000;

    /// <summary>Target time between position updates (~100 updates per second).</summary>
    private const int FrameMs = 10;

    /// <summary>
    /// Moves the cursor from wherever it is now to (x, y) in a straight line.
    /// </summary>
    /// <param name="pixelsPerSecond">
    /// Average movement speed. 0 or less means "no animation" - jump straight to the target.
    /// </param>
    public static void MoveTo(int x, int y, double pixelsPerSecond)
    {
        if (pixelsPerSecond <= 0 || !NativeMethods.GetCursorPos(out var start))
        {
            NativeMethods.SetCursorPos(x, y);
            return;
        }

        double dx = x - start.X;
        double dy = y - start.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance < MinDistancePx)
        {
            NativeMethods.SetCursorPos(x, y);
            return;
        }

        // A human does not travel at a constant speed, but the average speed over
        // the whole move is what the user configures, so derive the duration from it.
        double durationMs = Math.Clamp(distance / pixelsPerSecond * 1000.0, MinDurationMs, MaxDurationMs);

        var clock = Stopwatch.StartNew();
        while (true)
        {
            double t = clock.Elapsed.TotalMilliseconds / durationMs;
            if (t >= 1.0)
                break;

            double eased = Ease(t);
            NativeMethods.SetCursorPos(
                (int)Math.Round(start.X + dx * eased),
                (int)Math.Round(start.Y + dy * eased));

            Thread.Sleep(FrameMs);
        }

        // Always finish exactly on the recorded coordinate.
        NativeMethods.SetCursorPos(x, y);
    }

    /// <summary>Ease-in-out curve: slow start, fast middle, slow stop.</summary>
    private static double Ease(double t) => 0.5 - 0.5 * Math.Cos(Math.PI * t);
}
