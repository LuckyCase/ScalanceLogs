using SyslogViewer.Helpers;
using System.IO;
using System.Text;
using Timer = System.Threading.Timer;

namespace SyslogViewer.Services;

public static class LogManager
{
    // Cap on simultaneously open writers — defends against handle exhaustion
    // from spoofed UDP source IPs (each unique IP would otherwise open a file).
    private const int MaxOpenWriters = 64;

    private static string _logDir = "";
    // (key, writer, lastUsedTicks) — small enough that O(N) eviction is fine
    private static readonly Dictionary<string, (StreamWriter writer, long lastUsed)> Writers = [];
    private static readonly object Lock = new();
    private static string _currentDate = "";
    private static Timer? _cleanupTimer;
    private static long _useCounter;

    public static string LogDir => _logDir;

    public static void Initialize()
    {
        lock (Lock)
        {
            var requested = App.Settings.LogPath;
            _logDir = PathValidator.ResolveSafe(requested);
            if (_logDir != requested)
                AppLog.Warn($"LogPath '{requested}' rejected by validator → using '{_logDir}'");
            Directory.CreateDirectory(_logDir);
            _currentDate = Today();
        }
        CleanupOld();
        ScheduleCleanup();
    }

    public static void Reinitialize()
    {
        lock (Lock)
        {
            foreach (var (w, _) in Writers.Values) w.Dispose();
            Writers.Clear();
        }
        _cleanupTimer?.Dispose();
        Initialize();
    }

    public static void Write(string key, string filename, string line)
    {
        // Sanitize filename — refuse path separators and traversal
        var safeName = Path.GetFileName(filename);
        if (string.IsNullOrEmpty(safeName) || safeName != filename) return;

        var today = Today();
        lock (Lock)
        {
            if (today != _currentDate)
            {
                foreach (var (w, _) in Writers.Values) w.Dispose();
                Writers.Clear();
                _currentDate = today;
            }

            if (!Writers.TryGetValue(key, out var entry))
            {
                EvictIfFull();
                var path = Path.Combine(_logDir, safeName);
                var fs   = new FileStream(path, FileMode.Append, FileAccess.Write,
                                          FileShare.ReadWrite, 4096, FileOptions.None);
                var writer = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = true };
                entry = (writer, ++_useCounter);
                Writers[key] = entry;
            }
            else
            {
                Writers[key] = (entry.writer, ++_useCounter);
            }
            entry.writer.WriteLine(line);
        }
    }

    private static void EvictIfFull()
    {
        if (Writers.Count < MaxOpenWriters) return;
        // Drop ~25% of least-recently-used writers in one pass
        var dropCount = Math.Max(1, MaxOpenWriters / 4);
        var victims = Writers.OrderBy(kv => kv.Value.lastUsed)
                             .Take(dropCount)
                             .Select(kv => kv.Key)
                             .ToList();
        foreach (var k in victims)
        {
            Writers[k].writer.Dispose();
            Writers.Remove(k);
        }
    }

    public static string[] GetLogFiles()
    {
        try
        {
            return Directory.GetFiles(_logDir, "*.log")
                            .Select(Path.GetFileName)
                            .Where(f => f != null)
                            .OrderBy(f => f)
                            .ToArray()!;
        }
        catch { return []; }
    }

    public static string[] ReadLines(string filename, int count, string search)
    {
        var safeName = Path.GetFileName(filename);
        if (string.IsNullOrEmpty(safeName)) return [];
        var path = Path.Combine(_logDir, safeName);
        try
        {
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
