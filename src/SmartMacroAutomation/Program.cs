namespace SmartMacroAutomation;

internal static class Program
{
    private const string Usage =
        "Usage:\n" +
        "  SmartMacroAutomation.exe --play \"Macro name\" [--remove-delays]\n" +
        "  SmartMacroAutomation.exe --startup-add \"Macro name\"\n" +
        "  SmartMacroAutomation.exe --startup-remove \"Macro name\"";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // SmartMacroAutomation.exe --play "Macro name" [--remove-delays]
        // Plays one macro without showing the main window, e.g. from a desktop shortcut or at Windows startup.
        if (TryGetOption(args, "--play", out string? play))
        {
            if (play == null)
                return ShowUsage();

            bool removeDelays = args.Any(a => a.Equals("--remove-delays", StringComparison.OrdinalIgnoreCase));
            return UI.CommandLinePlayer.Run(play, removeDelays);
        }

        // --startup-add / --startup-remove "Macro name": run (or stop running) a macro when Windows starts.
        if (TryGetOption(args, "--startup-add", out string? add))
        {
            if (add == null)
                return ShowUsage();
            if (!Storage.MacroStorage.Exists(add))
            {
                MessageBox.Show($"Macro '{add}' was not found in {Storage.MacroStorage.RootFolder}.", "SmartMacroAutomation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }
            Storage.StartupRegistration.Enable(add);
            return 0;
        }

        if (TryGetOption(args, "--startup-remove", out string? remove))
        {
            if (remove == null)
                return ShowUsage();
            Storage.StartupRegistration.Disable(remove);
            return 0;
        }

        Application.Run(new UI.MainForm());
        return 0;
    }

    /// <summary>True if <paramref name="name"/> is present; <paramref name="value"/> is the argument after it, or null if missing.</summary>
    private static bool TryGetOption(string[] args, string name, out string? value)
    {
        int index = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        value = index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        return index >= 0;
    }

    private static int ShowUsage()
    {
        MessageBox.Show(Usage, "SmartMacroAutomation");
        return 2;
    }
}
