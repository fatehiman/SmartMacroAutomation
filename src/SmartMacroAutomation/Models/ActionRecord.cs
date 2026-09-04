namespace SmartMacroAutomation.Models;

public enum ActionType
{
    MouseClick,
    KeyDown,
    KeyUp
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

    // Milliseconds elapsed since the previous action, used to reproduce timing.
    public int DelayMs { get; set; }
}
