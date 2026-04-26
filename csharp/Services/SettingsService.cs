using ScalanceLogs.Helpers;
using ScalanceLogs.Models;
using System.IO;
using System.Text.Json;

namespace ScalanceLogs.Services;

public static class SettingsService
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ScalanceLogs", "settings.json");

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var raw = File.ReadAllText(Path);
                var s   = JsonSerializer.Deserialize<AppSettings>(raw);
                if (s != null) return s;
            }
        }
        catch (Exception ex)
        {
            // Corrupt JSON — preserve a backup so the user can recover, then start fresh.
            try
            {
                var backup = Path + $".broken-{DateTime.Now:yyyyMMddHHmmss}.bak";
                if (File.Exists(Path)) File.Copy(Path, backup, overwrite: true);
                System.Diagnostics.Debug.WriteLine(
                    $"[SettingsService] settings.json unreadable ({ex.Message}); backed up to {backup}");
            }
            catch { }
        }
        return new AppSettings();
    }

    public static void Save(AppSettings s)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(s, Opts));
        // Patterns may have changed → blow the regex cache so updates take effect.
        SafeRegex.ClearCache();
    }
}
