using Microsoft.Win32;

namespace ScalanceLogs.Helpers;

public static class AutostartHelper
{
    private const string RegPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ScalanceLogs";

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true)!;
        if (enabled)
            key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(AppName, throwOnMissingValue: false);
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegPath);
        return key?.GetValue(AppName) is not null;
    }
}
