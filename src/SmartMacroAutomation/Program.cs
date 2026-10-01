namespace SmartMacroAutomation;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // SmartMacroAutomation.exe --play "Macro name" [--remove-delays]
        // Plays one macro without showing the main window, e.g. from a desktop shortcut.
        int play = Array.FindIndex(args, a => a.Equals("--play", StringComparison.OrdinalIgnoreCase));
        if (play >= 0)
        {
            if (play + 1 >= args.Length)
            {
                MessageBox.Show("Usage: SmartMacroAutomation.exe --play \"Macro name\" [--remove-delays]", "SmartMacroAutomation");
                return 2;
            }

            bool removeDelays = args.Any(a => a.Equals("--remove-delays", StringComparison.OrdinalIgnoreCase));
            return UI.CommandLinePlayer.Run(args[play + 1], removeDelays);
        }

        Application.Run(new UI.MainForm());
        return 0;
    }
}
