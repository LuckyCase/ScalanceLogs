using Microsoft.Win32;

namespace ScalanceLogs.Helpers;

public static class AutostartHelper
{
    private const string RegPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ScalanceLogs";

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RegPath, writable: true);
            if (key is null) return;

            if (enabled)
            {
                var path = Environment.ProcessPath;
                if (string.IsNullOrEmpty(path)) return;
                // Quote path; ProcessPath cannot contain '"' on Windows so this is safe.
                key.SetValue(AppName, $"\"{path}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutostartHelper] {ex.Message}");
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath);
            return key?.GetValue(AppName) is not null;
        }
        catch { return false; }
    }
}
