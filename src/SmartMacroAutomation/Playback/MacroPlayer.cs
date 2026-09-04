using System.Drawing;
using SmartMacroAutomation.Models;
using SmartMacroAutomation.Native;
using SmartMacroAutomation.Storage;

namespace SmartMacroAutomation.Playback;

internal enum MismatchDecision
{
    Continue,
    Stop
}

/// <summary>
/// Replays a macro. Every mouse click is preceded by a visual verification
/// against its reference screenshot; keyboard actions are replayed as-is.
/// </summary>
internal sealed class MacroPlayer
{
    private readonly Macro _macro;
    private readonly string _macroFolder;

    /// <summary>Called when a click's similarity is below threshold. Return the user's decision.</summary>
    public Func<ActionRecord, double, double, Bitmap, Bitmap, MismatchDecision>? OnMismatch;

    /// <summary>Called before each action executes, for status/progress display.</summary>
    public Action<int, int, ActionRecord>? OnStep;

    public bool IsCancelled { get; private set; }

    public MacroPlayer(Macro macro)
    {
        _macro = macro;
        _macroFolder = MacroStorage.GetMacroFolder(macro.Name);
    }

    public void Play()
    {
        IsCancelled = false;
        var actions = _macro.Actions;

        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            OnStep?.Invoke(i, actions.Count, action);

            if (action.DelayMs > 0)
                System.Threading.Thread.Sleep(Math.Min(action.DelayMs, 5000));

            if (action.Type == ActionType.MouseClick)
            {
                if (!ExecuteMouseClick(action))
                {
                    IsCancelled = true;
                    return;
                }
            }
            else
            {
                InputSimulator.SendKey(action.KeyCode, action.Type == ActionType.KeyUp);
            }
        }
    }

    private bool ExecuteMouseClick(ActionRecord action)
    {
        double threshold = action.Threshold > 0 ? action.Threshold : _macro.SimilarityThreshold;

        int left = action.X + action.RefOffsetX;
        int top = action.Y + action.RefOffsetY;

        using var current = ScreenCapture.Capture(left, top, action.RefWidth, action.RefHeight);

        string refPath = Path.Combine(_macroFolder, "images", action.ImageFile ?? "");
        if (!File.Exists(refPath))
        {
            // No reference image available; proceed without verification.
            InputSimulator.Click(action.X, action.Y, action.Button);
            return true;
        }

        using var reference = new Bitmap(refPath);
        double similarity = ImageComparer.CalculateSimilarity(reference, current);

        if (similarity >= threshold)
        {
            InputSimulator.Click(action.X, action.Y, action.Button);
            return true;
        }

        var decision = OnMismatch?.Invoke(action, similarity, threshold, reference, current) ?? MismatchDecision.Stop;
        if (decision == MismatchDecision.Continue)
        {
            InputSimulator.Click(action.X, action.Y, action.Button);
            return true;
        }

        return false;
    }
}
