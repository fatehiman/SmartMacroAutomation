using System.Text.Json;
using SmartMacroAutomation.Models;

namespace SmartMacroAutomation.Storage;

/// <summary>
/// Persists each macro as its own folder: Macros/&lt;name&gt;/actions.json plus
/// Macros/&lt;name&gt;/images/*.png reference screenshots. No database is used.
/// </summary>
internal static class MacroStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string RootFolder => Path.Combine(AppContext.BaseDirectory, "Macros");

    public static string GetMacroFolder(string name) => Path.Combine(RootFolder, SanitizeName(name));

    public static string GetImagesFolder(string name) => Path.Combine(GetMacroFolder(name), "images");

    private static string SanitizeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    public static List<string> ListMacros()
    {
        if (!Directory.Exists(RootFolder))
            return new List<string>();

        return Directory.GetDirectories(RootFolder)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void SaveMacro(Macro macro)
    {
        string folder = GetMacroFolder(macro.Name);
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(GetImagesFolder(macro.Name));

        string json = JsonSerializer.Serialize(macro, JsonOptions);
        File.WriteAllText(Path.Combine(folder, "actions.json"), json);
    }

    public static Macro? LoadMacro(string name)
    {
        string file = Path.Combine(GetMacroFolder(name), "actions.json");
        if (!File.Exists(file))
            return null;

        string json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<Macro>(json, JsonOptions);
    }

    public static void DeleteMacro(string name)
    {
        string folder = GetMacroFolder(name);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    public static bool Exists(string name) => Directory.Exists(GetMacroFolder(name));
}
