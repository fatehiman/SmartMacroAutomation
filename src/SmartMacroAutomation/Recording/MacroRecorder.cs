using System.Diagnostics;
using SmartMacroAutomation.Models;
using SmartMacroAutomation.Native;
using SmartMacroAutomation.Playback;

namespace SmartMacroAutomation.Recording;

/// <summary>
/// Captures mouse clicks and key presses (via global hooks) into a Macro,
/// taking a 50x50 reference screenshot for every mouse click.
/// </summary>
internal sealed class MacroRecorder : IDisposable
{
    private readonly GlobalHook _hook = new();
    private readonly List<ActionRecord> _actions = new();
    private readonly Dictionary<int, (string File, System.Drawing.Bitmap Bitmap)> _pendingImages = new();
    private readonly Stopwatch _stopwatch = new();
    private long _lastActionMs;
    private int _clickCounter;

    public IReadOnlyList<ActionRecord> Actions => _actions;

    /// <summary>Maps an ActionRecord index (for mouse clicks) to its in-memory reference bitmap.</summary>
    public Dictionary<int, System.Drawing.Bitmap> CapturedImages { get; } = new();

    public void Start()
    {
        _actions.Clear();
        CapturedImages.Clear();
        _clickCounter = 0;
        _lastActionMs = 0;
        _stopwatch.Restart();

        _hook.MouseClick += OnMouseClick;
        _hook.KeyEvent += OnKeyEvent;
        _hook.Start();
    }

    public void Stop()
    {
        _stopwatch.Stop();
        _hook.MouseClick -= OnMouseClick;
        _hook.KeyEvent -= OnKeyEvent;
        _hook.Dispose();
    }

    private int NextDelay()
    {
        long now = _stopwatch.ElapsedMilliseconds;
        int delay = (int)Math.Min(int.MaxValue, now - _lastActionMs);
        _lastActionMs = now;
        return Math.Max(0, delay);
    }

    private void OnMouseClick(object? sender, MouseClickEventArgs e)
    {
        const int size = 50;
        var bitmap = ScreenCapture.CaptureAround(e.X, e.Y, size);

        var action = new ActionRecord
        {
            Type = ActionType.MouseClick,
            X = e.X,
            Y = e.Y,
            Button = e.Button,
            RefOffsetX = -size / 2,
            RefOffsetY = -size / 2,
            RefWidth = size,
            RefHeight = size,
            DelayMs = NextDelay()
        };

        int index = _actions.Count;
        _actions.Add(action);
        CapturedImages[index] = bitmap;
        _clickCounter++;
    }

    private void OnKeyEvent(object? sender, KeyEventArgsRaw e)
    {
        var mods = System.Windows.Forms.Control.ModifierKeys;

        var action = new ActionRecord
        {
            Type = e.IsKeyUp ? ActionType.KeyUp : ActionType.KeyDown,
            KeyCode = e.VkCode,
            Shift = mods.HasFlag(System.Windows.Forms.Keys.Shift),
            Control = mods.HasFlag(System.Windows.Forms.Keys.Control),
            Alt = mods.HasFlag(System.Windows.Forms.Keys.Alt),
            DelayMs = NextDelay()
        };
        _actions.Add(action);
    }

    public void Dispose()
    {
        _hook.Dispose();
    }
}
