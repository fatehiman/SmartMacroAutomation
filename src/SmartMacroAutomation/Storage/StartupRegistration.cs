using Microsoft.Win32;

namespace SmartMacroAutomation.Storage;

/// <summary>
/// Registers macros to run when the user logs on to Windows, via the per-user Run key
/// (HKCU\Software\Microsoft\Windows\CurrentVersion\Run - no admin rights needed).
/// Each macro gets its own value: "SmartMacroAutomation: &lt;name&gt;" = "&lt;exe&gt;" --play "&lt;name&gt;".
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValuePrefix = "SmartMacroAutomation: ";

    private static string ValueName(string macroName) => ValuePrefix + macroName;

    /// <summary>The command line Windows runs at logon for this macro, using the currently running exe.</summary>
    public static string CommandFor(string macroName) =>
        $"\"{Environment.ProcessPath}\" --play \"{macroName}\"";

    public static bool IsEnabled(string macroName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName(macroName)) is string;
    }

    /// <summary>The registered command, or null when the macro is not set to run at startup.</summary>
    public static string? GetCommand(string macroName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName(macroName)) as string;
    }

    public static void Enable(string macroName)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName(macroName), CommandFor(macroName));
    }

    public static void Disable(string macroName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName(macroName), throwOnMissingValue: false);
    }
}
