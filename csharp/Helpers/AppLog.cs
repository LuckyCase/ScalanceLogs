using System.IO;
using System.Reflection;

namespace ScalanceLogs.Helpers;

/// <summary>
/// Per-session diagnostic log written next to the exe (or to %LocalAppData%
/// if that's read-only). The file is reset at every Initialize() so each run
/// starts clean — keeps the file small and the focus on "what just happened".
/// Never throws — log failures are silent so they can't crash the app.
/// </summary>
public static class AppLog
{
    private static string _path = "";
    private static readonly object Lock = new();

    public static string Path => _path;

    /// <summary>Wipe previous session, write a header. Call once at app startup.</summary>
    public static void Initialize()
    {
        _path = ResolvePath();
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            var asm = Assembly.GetExecutingAssembly().GetName();
            var header =
                $"=== SW-LOG session {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}" +
                $"Version : {asm.Version}{Environment.NewLine}" +
                $"OS      : {Environment.OSVersion}{Environment.NewLine}" +
                $"Runtime : {Environment.Version}{Environment.NewLine}" +
                $"User    : {Environment.UserName}{Environment.NewLine}" +
                $"Exe     : {Environment.ProcessPath}{Environment.NewLine}" +
                $"Log path: {_path}{Environment.NewLine}" +
                new string('=', 60) + Environment.NewLine;
            File.WriteAllText(_path, header);
        }
        catch { /* nowhere to log to — give up silently */ }
    }

    public static void Info (string msg)                    => Write("INFO ", msg, null);
    public static void Warn (string msg, Exception? ex = null) => Write("WARN ", msg, ex);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", msg, ex);

    private static void Write(string level, string msg, Exception? ex)
    {
        if (string.IsNullOrEmpty(_path)) return;
        lock (Lock)
        {
            try
            {
                var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {msg}";
                if (ex != null)
                    line += Environment.NewLine + ex;
                File.AppendAllText(_path, line + Environment.NewLine);
            }
            catch { /* never throw from a logger */ }
        }
    }

    private static string ResolvePath()
    {
        // Preferred: next to the exe (visible to the operator).
        var primary = System.IO.Path.Combine(AppContext.BaseDirectory, "app.log");
        try
        {
            // Probe-write a single byte to detect read-only locations
            // (Program Files without admin, network share without write rights, …).
            using var fs = new FileStream(primary, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            return primary;
        }
        catch
        {
            // Fallback: %LocalAppData%\ScalanceLogs\app.log
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ScalanceLogs", "app.log");
        }
    }
}
