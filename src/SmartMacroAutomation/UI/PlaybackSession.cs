using SmartMacroAutomation.Native;
using SmartMacroAutomation.Playback;

namespace SmartMacroAutomation.UI;

/// <summary>
/// Runs a <see cref="MacroPlayer"/> on a worker thread with the standard UI around it: the
/// mismatch / Esc / window-not-found dialogs and the global Esc hook. Used by the main window
/// and by the "--play" command line.
/// </summary>
internal static class PlaybackSession
{
    /// <param name="owner">A control with a handle on the UI thread; dialogs are shown through it.</param>
    /// <param name="onLog">Receives the player's log lines on the UI thread.</param>
    /// <param name="onFinished">Called on the UI thread when playback ends; the argument is the error, if any.</param>
    public static void Start(MacroPlayer player, Control owner, Action<string>? onLog, Action<Exception?> onFinished)
    {
        IWin32Window? DialogOwner() => owner.Visible ? owner : null;

        T OnUi<T>(Func<T> func) => (T)owner.Invoke(func)!;

        player.OnMismatch = (action, similarity, threshold, reference, current) => OnUi(() =>
        {
            using var form = new MismatchForm(similarity, threshold, reference, current);
            form.ShowDialog(DialogOwner());
            return form.Decision;
        });

        player.OnEscRequested = () => OnUi(() =>
        {
            using var form = new PlaybackPausedForm();
            form.ShowDialog(DialogOwner());
            return form.Decision;
        });

        player.OnWindowNotFound = action => OnUi(() =>
        {
            string reason = $"No window with a title containing '{action.WindowTitle}'" +
                            (string.IsNullOrWhiteSpace(action.ProcessName) ? "" : $" (process '{action.ProcessName}')") +
                            " was found.\n\nContinue skips the window clicks that need it.";
            using var form = new PlaybackPausedForm(reason, "Window not found");
            form.Height = 200;
            form.ShowDialog(DialogOwner());
            return form.Decision;
        });

        if (onLog != null)
            player.OnLog = line => owner.BeginInvoke(new Action(() => onLog(line)));

        // Global low-level keyboard hook so Esc stops playback even when a different
        // application has focus (playback moves focus/input to arbitrary target windows).
        // The hook lives on the UI thread, which keeps pumping messages while playback runs.
        var escHook = new GlobalHook { IgnoreOwnProcessClicks = false };
        escHook.KeyEvent += (_, args) =>
        {
            if (!args.IsKeyUp && args.VkCode == (int)Keys.Escape)
                player.RequestStop();
        };
        escHook.Start();

        Task.Run(() =>
        {
            Exception? error = null;
            try
            {
                player.Play();
            }
            catch (Exception ex)
            {
                error = ex;
            }

            owner.Invoke(new MethodInvoker(() =>
            {
                escHook.Dispose();
                onFinished(error);
            }));
        });
    }
}
