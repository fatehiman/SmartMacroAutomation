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

    /// <summary>Called before each action executes, for status/progress display. Args: repeatIndex, repeatCount, actionIndex, actionCount, action.</summary>
    public Action<int, int, int, int, ActionRecord>? OnStep;

    /// <summary>Called when the user presses Esc during playback. Return whether to continue or stop.</summary>
    public Func<MismatchDecision>? OnEscRequested;

    public bool IsCancelled { get; private set; }

    /// <summary>
    /// When true, any recorded delay longer than 500ms is clamped to 500ms during this
    /// playback run only - the underlying macro's DelayMs values are never modified.
    /// </summary>
    public bool RemoveAllDelays { get; set; }

    private volatile bool _stopRequested;

    public MacroPlayer(Macro macro)
    {
        _macro = macro;
        _macroFolder = MacroStorage.GetMacroFolder(macro.Name);
    }

    /// <summary>Requests that playback stop as soon as possible (e.g. the user pressed Esc).</summary>
    public void RequestStop()
    {
        _stopRequested = true;
    }

    public void Play()
    {
        IsCancelled = false;
        _stopRequested = false;
        var actions = _macro.Actions;
        int repeatCount = Math.Max(1, _macro.RepeatCount);

        for (int repeat = 0; repeat < repeatCount; repeat++)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                if (HandleStopRequestIfAny())
                    return;

                var action = actions[i];
                OnStep?.Invoke(repeat, repeatCount, i, actions.Count, action);

                if (action.Type == ActionType.MouseClick)
                {
                    // Move to the target first so any hover-triggered UI change (e.g. a
                    // button lighting up) has the chance to happen before we verify and click,
                    // exactly like it did live when this macro was recorded. The cursor glides
                    // there in a straight line at a human-like speed rather than teleporting,
                    // so applications that watch mouse movement see a plausible gesture.
                    InputSimulator.MoveToHumanLike(action.X, action.Y, EffectiveMouseMoveSpeed());
                    Sleep(action.DelayMs);

                    if (HandleStopRequestIfAny())
                        return;

                    if (!ExecuteMouseClick(action))
                    {
                        IsCancelled = true;
                        return;
                    }
                }
                else
                {
                    Sleep(action.DelayMs);

                    if (HandleStopRequestIfAny())
                        return;

                    InputSimulator.SendKey(action.KeyCode, action.Type == ActionType.KeyUp);
                }
            }
        }
    }

    /// <summary>
    /// If Esc was pressed, asks the user (via <see cref="OnEscRequested"/>) whether to continue or
    /// stop, and returns true if playback should stop. Playback is already paused by the time this
    /// is called, since <see cref="Sleep"/> itself stops waiting as soon as a stop is requested.
    /// </summary>
    private bool HandleStopRequestIfAny()
    {
        if (!_stopRequested)
            return false;

        _stopRequested = false;
        var decision = OnEscRequested?.Invoke() ?? MismatchDecision.Stop;
        if (decision == MismatchDecision.Stop)
        {
            IsCancelled = true;
            return true;
        }

        return false;
    }

    /// <summary>Sleeps for the recorded delay scaled by the macro's speed setting (0.1x-10x; 10x = no delay).</summary>
    private void Sleep(int recordedDelayMs)
    {
        if (RemoveAllDelays && recordedDelayMs > 500)
            recordedDelayMs = 500;

        if (recordedDelayMs <= 0)
            return;

        double speed = _macro.Speed <= 0 ? 1.0 : _macro.Speed;
        if (speed >= 10.0)
            return;

        int scaled = (int)Math.Round(recordedDelayMs / speed);
        SleepInterruptible(scaled);
    }

    /// <summary>Sleeps in small chunks so a pending Esc stop request interrupts a long wait almost immediately.</summary>
    private void SleepInterruptible(int ms)
    {
        const int chunkMs = 20;
        int remaining = ms;
        while (remaining > 0 && !_stopRequested)
        {
            int step = Math.Min(chunkMs, remaining);
            System.Threading.Thread.Sleep(step);
            remaining -= step;
        }
    }

    /// <summary>
    /// Cursor travel speed in pixels per second, scaled by the macro's speed multiplier.
    /// Returns 0 when the move should be an instant jump (mouse speed disabled, or speed = 10x).
    /// </summary>
    private double EffectiveMouseMoveSpeed()
    {
        double configured = _macro.MouseMoveSpeed;
        if (configured <= 0)
            return 0;

        double speed = _macro.Speed <= 0 ? 1.0 : _macro.Speed;
        if (speed >= 10.0)
            return 0;

        return configured * speed;
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
