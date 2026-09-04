namespace SmartMacroAutomation.Models;

public class Macro
{
    public string Name { get; set; } = "";
    public double SimilarityThreshold { get; set; } = 99.0;

    /// <summary>How many times Play runs the whole action list in a row. 1-999.</summary>
    public int RepeatCount { get; set; } = 1;

    /// <summary>
    /// Playback speed multiplier applied to every recorded delay. 1.0 = exactly as recorded,
    /// 1.5 = 1.5x faster, 10.0 = remove all delays. Range 0.1-10.0, one decimal place.
    /// </summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>
    /// How fast the cursor travels to a recorded click position during playback, in pixels
    /// per second (average over the move). The cursor glides there in a straight line with a
    /// human-like slow-fast-slow profile instead of jumping. 0 = jump instantly (old behaviour).
    /// Also scaled by <see cref="Speed"/>.
    /// </summary>
    public double MouseMoveSpeed { get; set; } = 1600.0;

    public List<ActionRecord> Actions { get; set; } = new();
}
