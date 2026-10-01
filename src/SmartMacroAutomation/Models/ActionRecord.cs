namespace SmartMacroAutomation.Models;

public enum ActionType
{
    MouseClick,
    KeyDown,
    KeyUp,

    /// <summary>Finds a top-level window by title (and optionally process) and makes it the target of the next WindowClick actions.</summary>
    FindWindow,

    /// <summary>Clicks a point given relative to a corner of the window found by the last FindWindow.</summary>
    WindowClick
}

/// <summary>Which corner of the window a WindowClick's X/Y offset is measured from.</summary>
public enum WindowAnchor
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public enum WindowClickMode
{
    /// <summary>
    /// Posts mouse messages straight to the window (PostMessage). The real cursor does not move,
    /// focus does not change, and it works even when another window covers the target.
    /// </summary>
    Background,

    /// <summary>Brings the window to the front, moves the real cursor there and clicks with SendInput.</summary>
    Foreground
}

public enum MouseButtonKind
{
    Left,
    Right,
    Middle
}

/// <summary>
/// A single recorded step in a macro. Mouse clicks carry a reference screenshot;
/// keyboard steps do not, per the "no visual verification for typing" design.
/// WindowClick steps may carry one too; for them X/Y and the reference rectangle are
/// in window coordinates (see <see cref="Anchor"/>), not screen coordinates.
/// </summary>
public class ActionRecord
{
    public ActionType Type { get; set; }

    // Mouse click fields
    public int X { get; set; }
    public int Y { get; set; }
    public MouseButtonKind Button { get; set; }

    // Reference image rectangle, stored as an offset from (X, Y) so a crop
    // made after recording still lines up with the click point during playback.
    public string? ImageFile { get; set; }
    public int RefOffsetX { get; set; }
    public int RefOffsetY { get; set; }
    public int RefWidth { get; set; }
    public int RefHeight { get; set; }

    // Per-action override of the macro's similarity threshold (0 = use macro default)
    public double Threshold { get; set; }

    // Keyboard fields
    public int KeyCode { get; set; }
    public bool Shift { get; set; }
    public bool Control { get; set; }
    public bool Alt { get; set; }

    // Window fields (FindWindow / WindowClick). Nullable so they stay out of the JSON of other actions.

    /// <summary>FindWindow: text the window title must contain (case-insensitive). An exact title match wins over a partial one.</summary>
    public string? WindowTitle { get; set; }

    /// <summary>FindWindow: optional process name without ".exe", e.g. "NVIDIA Broadcast".</summary>
    public string? ProcessName { get; set; }

    /// <summary>FindWindow: how long to keep looking for the window before giving up. Default 3000ms.</summary>
    public int? TimeoutMs { get; set; }

    /// <summary>
    /// WindowClick: corner the X/Y offset is measured from. X/Y are distances inward from that corner,
    /// so TopRight with X=64, Y=15 means "64px left of the right edge, 15px below the top edge" -
    /// this keeps hitting a title-bar button even if the window is resized. Default TopLeft.
    /// </summary>
    public WindowAnchor? Anchor { get; set; }

    /// <summary>WindowClick: how the click is delivered. Default Background.</summary>
    public WindowClickMode? ClickMode { get; set; }

    // Milliseconds elapsed since the previous action, used to reproduce timing.
    public int DelayMs { get; set; }
}
