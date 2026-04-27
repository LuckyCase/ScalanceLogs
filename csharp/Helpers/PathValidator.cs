using System.IO;

namespace ScalanceLogs.Helpers;

/// <summary>
/// Defends against malicious settings.json setting LogPath to a system folder
/// (so retention cleanup would delete arbitrary *.log files).
/// </summary>
public static class PathValidator
{
    /// <summary>Default fallback location: %LocalAppData%\ScalanceLogs\logs.</summary>
    public static string DefaultLogDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScalanceLogs", "logs");

    private static readonly string[] ForbiddenRoots =
        new string[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
        };

    /// <summary>
    /// Returns a safe absolute path. If the supplied path is empty, unsafe,
    /// or outside writable user space, returns the default fallback.
    /// </summary>
    public static string ResolveSafe(string requested)
    {
        if (string.IsNullOrWhiteSpace(requested)) return DefaultLogDir;

        string full;
        try
        {
            full = Path.IsPathRooted(requested)
                ? Path.GetFullPath(requested)
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, requested));
        }
        catch { return DefaultLogDir; }

        foreach (var forbidden in ForbiddenRoots)
        {
            if (string.IsNullOrEmpty(forbidden)) continue;
            if (full.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase))
                return DefaultLogDir;
        }
        return full;
    }

    public static bool IsSafe(string path) =>
        !string.IsNullOrWhiteSpace(path) && ResolveSafe(path) == TryFullPath(path);

    private static string TryFullPath(string p)
    {
        try { return Path.GetFullPath(p); } catch { return ""; }
    }
}
