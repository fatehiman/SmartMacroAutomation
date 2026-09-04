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

    public List<ActionRecord> Actions { get; set; } = new();
}
