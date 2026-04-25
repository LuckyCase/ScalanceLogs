using System.IO;
using System.Text;
using Timer = System.Threading.Timer;

namespace ScalanceLogs.Services;

public static class LogManager
{
    private static string _logDir = "";
    private static readonly Dictionary<string, StreamWriter> Writers = [];
    private static readonly object Lock = new();
    private static string _currentDate = "";
    private static Timer? _cleanupTimer;

    public static string LogDir => _logDir;

    public static void Initialize()
    {
        _logDir = ResolveLogDir(App.Settings.LogPath);
        Directory.CreateDirectory(_logDir);
        _currentDate = Today();
        CleanupOld();
        ScheduleCleanup();
    }

    public static void Reinitialize()
    {
        lock (Lock)
        {
            foreach (var w in Writers.Values) w.Dispose();
            Writers.Clear();
        }
        _cleanupTimer?.Dispose();
        Initialize();
    }

    public static void Write(string key, string filename, string line)
    {
        var today = Today();
        lock (Lock)
        {
            if (today != _currentDate)
            {
                foreach (var w in Writers.Values) w.Dispose();
                Writers.Clear();
                _currentDate = today;
            }

            if (!Writers.TryGetValue(key, out var writer))
            {
                var path = Path.Combine(_logDir, filename);
                var fs   = new FileStream(path, FileMode.Append, FileAccess.Write,
                                          FileShare.ReadWrite, 4096, FileOptions.None);
                writer = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = true };
                Writers[key] = writer;
            }
            writer.WriteLine(line);
        }
    }

    public static string[] GetLogFiles()
    {
        try { return Directory.GetFiles(_logDir, "*.log").Select(Path.GetFileName).Where(f => f != null).OrderBy(f => f).ToArray()!; }
        catch { return []; }
    }

    public static string[] ReadLines(string filename, int count, string search)
    {
        var path = Path.Combine(_logDir, Path.GetFileName(filename));
        try
        {
            // Open with ReadWrite sharing so we can read while the StreamWriter has the file open
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            var lines = new List<string>();
            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine();
                if (line != null) lines.Add(line);
            }
            IEnumerable<string> all = lines;
            if (!string.IsNullOrEmpty(search))
                all = all.Where(l => l.Contains(search, StringComparison.OrdinalIgnoreCase));
            return all.TakeLast(count).ToArray();
        }
        catch { return []; }
    }

    private static string Today() => DateTime.Today.ToString("yyyy-MM-dd");

    private static string ResolveLogDir(string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));

    private static void CleanupOld()
    {
        var cutoff = DateTime.Now.AddDays(-App.Settings.LogRetentionDays);
        try
        {
            foreach (var f in Directory.GetFiles(_logDir, "*.log"))
                if (File.GetLastWriteTime(f) < cutoff)
                    File.Delete(f);
        }
        catch { }
    }

    private static void ScheduleCleanup()
    {
        var next  = DateTime.Today.AddDays(1).AddSeconds(5);
        var delay = (long)(next - DateTime.Now).TotalMilliseconds;
        _cleanupTimer = new Timer(_ =>
        {
            CleanupOld();
            ScheduleCleanup();
        }, null, delay, Timeout.Infinite);
    }
}
