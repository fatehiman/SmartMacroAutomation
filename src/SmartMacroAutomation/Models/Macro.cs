namespace SmartMacroAutomation.Models;

public class Macro
{
    public string Name { get; set; } = "";
    public double SimilarityThreshold { get; set; } = 99.0;
    public List<ActionRecord> Actions { get; set; } = new();
}
