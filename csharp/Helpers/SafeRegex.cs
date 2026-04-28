using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SyslogViewer.Helpers;

/// <summary>
/// Compiled, cached regex with a hard timeout. Defeats ReDoS attacks
/// from crafted UDP messages run against user-supplied patterns.
/// </summary>
public static class SafeRegex
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(50);

    private static readonly ConcurrentDictionary<(string pattern, RegexOptions opts), Regex?> Cache = new();

    private static Regex? Get(string pattern, RegexOptions opts) =>
        Cache.GetOrAdd((pattern, opts), key =>
        {
            try { return new Regex(key.pattern, key.opts | RegexOptions.Compiled, Timeout); }
            catch { return null; } // invalid pattern → never matches
        });

    public static bool IsMatch(string input, string pattern,
                               RegexOptions opts = RegexOptions.IgnoreCase)
    {
        var rx = Get(pattern, opts);
        if (rx is null) return false;
        try { return rx.IsMatch(input); }
        catch (RegexMatchTimeoutException) { return false; }
    }

    public static Match? Match(string input, string pattern,
                               RegexOptions opts = RegexOptions.IgnoreCase)
    {
        var rx = Get(pattern, opts);
        if (rx is null) return null;
        try { var m = rx.Match(input); return m.Success ? m : null; }
        catch (RegexMatchTimeoutException) { return null; }
    }

    /// <summary>Drop cache when settings change (patterns may be edited/removed).</summary>
    public static void ClearCache() => Cache.Clear();
}
