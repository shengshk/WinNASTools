using System.Globalization;
using System.Text;
using WinNASTools.Core.Contracts;

namespace WinNASTools.Core.Hosting;

public enum LogViewFilter
{
    /// <summary>INFO / WARN</summary>
    Normal,
    /// <summary>ERROR only</summary>
    Error
}

/// <summary>持久化日志：按天保留，前台清屏不影响文件。</summary>
public sealed class FileLogger : ILogger
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly string[] LevelTags = ["INFO", "WARN", "ERROR"];

    private readonly object _gate = new();
    private readonly string _path;
    private readonly Action<string>? _uiSink;
    private int _retentionDays;
    private DateTime _lastPruneUtc = DateTime.MinValue;

    public FileLogger(string path, Action<string>? uiSink = null, int retentionDays = 90)
    {
        _path = path;
        _uiSink = uiSink;
        _retentionDays = Math.Clamp(retentionDays, 1, 3650);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PruneIfNeeded(force: true);
    }

    public int RetentionDays => _retentionDays;

    public void SetRetentionDays(int days)
    {
        _retentionDays = Math.Clamp(days, 1, 3650);
        PruneIfNeeded(force: true);
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    public static bool MatchesFilter(string line, LogViewFilter filter)
    {
        var level = ParseLevel(line);
        return filter switch
        {
            LogViewFilter.Error => level == "ERROR",
            _ => level is "INFO" or "WARN" or null
        };
    }

    /// <summary>读取供界面展示的行，支持独立于文件保留期的界面时间范围。</summary>
    public IReadOnlyList<string> ReadForDisplay(
        LogViewFilter filter,
        int maxLines = 3000,
        DateTime? since = null)
    {
        lock (_gate)
        {
            PruneIfNeeded(force: false);
            if (!File.Exists(_path))
                return Array.Empty<string>();

            try
            {
                var matched = new List<string>();
                foreach (var line in File.ReadLines(_path, Utf8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (since is not null
                        && (!TryParseTimestamp(line, out var timestamp) || timestamp < since.Value))
                        continue;
                    if (!MatchesFilter(line, filter)) continue;
                    matched.Add(line);
                }

                if (matched.Count <= maxLines)
                    return matched;
                return matched.GetRange(matched.Count - maxLines, maxLines);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  [{level}]  {message}";
        lock (_gate)
        {
            try
            {
                PruneIfNeeded(force: false);
                File.AppendAllText(_path, line + Environment.NewLine, Utf8);
            }
            catch { /* ignore IO */ }
        }

        try { _uiSink?.Invoke(line); } catch { /* UI may be closing */ }
    }

    private void PruneIfNeeded(bool force)
    {
        if (!force && (DateTime.UtcNow - _lastPruneUtc).TotalHours < 6)
            return;
        _lastPruneUtc = DateTime.UtcNow;

        if (!File.Exists(_path))
            return;

        try
        {
            var cutoff = DateTime.Now.Date.AddDays(-_retentionDays);
            var kept = new List<string>();
            var changed = false;

            foreach (var line in File.ReadLines(_path, Utf8))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    changed = true;
                    continue;
                }

                if (TryParseTimestamp(line, out var ts) && ts.Date < cutoff)
                {
                    changed = true;
                    continue;
                }

                kept.Add(line);
            }

            if (!changed)
                return;

            var tmp = _path + ".tmp";
            File.WriteAllLines(tmp, kept, Utf8);
            File.Copy(tmp, _path, overwrite: true);
            File.Delete(tmp);
        }
        catch { /* ignore prune errors */ }
    }

    public static string? ParseLevel(string line)
    {
        var i = line.IndexOf('[');
        if (i < 0) return null;
        var j = line.IndexOf(']', i + 1);
        if (j < 0) return null;
        var tag = line[(i + 1)..j].Trim().ToUpperInvariant();
        return LevelTags.Contains(tag) ? tag : null;
    }

    public static bool TryParseTimestamp(string line, out DateTime ts)
    {
        ts = default;
        if (line.Length < 19) return false;
        if (line.Length >= 23
            && DateTime.TryParseExact(
                line[..23],
                "yyyy-MM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out ts))
            return true;

        return DateTime.TryParseExact(
            line[..19],
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out ts);
    }
}
