using SmartMacroAutomation.Playback;
using SmartMacroAutomation.Storage;

namespace SmartMacroAutomation.UI;

/// <summary>
/// Plays a macro from the command line ("--play"), with no main window. Dialogs (mismatch, Esc,
/// window not found) still appear when needed. The log is written to Macros\&lt;name&gt;\last-run.log.
/// Exit code: 0 = finished, 1 = stopped / error, 2 = macro not found.
/// </summary>
internal static class CommandLinePlayer
{
    public static int Run(string macroName, bool removeDelays)
    {
        var macro = MacroStorage.LoadMacro(macroName);
        if (macro == null || macro.Actions.Count == 0)
        {
            MessageBox.Show($"Macro '{macroName}' was not found or has no actions.\n\nMacros folder: {MacroStorage.RootFolder}",
                "SmartMacroAutomation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 2;
        }

        string logPath = Path.Combine(MacroStorage.GetMacroFolder(macroName), "last-run.log");
        var log = new List<string>();
        void Log(string line) => log.Add($"[{DateTime.Now:HH:mm:ss.fff}] {line}");

        // An invisible form gives the worker thread a UI thread to marshal dialogs to,
        // and its message loop keeps the global Esc hook alive.
        using var host = new Form { ShowInTaskbar = false };
        _ = host.Handle;

        var player = new MacroPlayer(macro) { RemoveAllDelays = removeDelays };
        int exitCode = 1;

        Log($"Playing '{macroName}'...");
        PlaybackSession.Start(player, host, Log, error =>
        {
            if (error != null)
                Log("Error: " + error);
            else
                Log(player.IsCancelled ? "Stopped." : "Finished.");

            exitCode = error == null && !player.IsCancelled ? 0 : 1;
            Application.ExitThread();
        });

        Application.Run();

        try { File.WriteAllLines(logPath, log); } catch (IOException) { }
        return exitCode;
    }
}
