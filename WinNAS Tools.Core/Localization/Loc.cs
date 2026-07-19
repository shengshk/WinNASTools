using System.Diagnostics.CodeAnalysis;

namespace WinNASTools.Core.Localization;

/// <summary>运行时本地化：UI 与日志共用。切换语言后由宿主重启进程生效。</summary>
public static class Loc
{
    private static readonly object Gate = new();
    private static AppLanguage _lang = AppLanguage.ZhCn;
    private static Dictionary<string, Entry> _byKey = new(StringComparer.Ordinal);
    private static Dictionary<string, string> _zhCnToKey = new(StringComparer.Ordinal);

    public static AppLanguage Language
    {
        get { lock (Gate) return _lang; }
    }

    public static void Initialize(AppLanguage language)
    {
        lock (Gate)
        {
            EnsureCatalogLocked();
            _lang = language;
        }
    }

    public static void Register(string key, string zhCn, string zhTw, string en)
    {
        lock (Gate)
        {
            _byKey[key] = new Entry(zhCn, zhTw, en);
            if (!string.IsNullOrEmpty(zhCn))
                _zhCnToKey[zhCn] = key;
        }
    }

    public static string T(string key)
    {
        lock (Gate)
        {
            EnsureCatalogLocked();
            if (_byKey.TryGetValue(key, out var e))
                return Pick(e);
            return key;
        }
    }

    private static void EnsureCatalogLocked()
    {
        if (_byKey.Count == 0)
            Catalog.EnsureRegistered();
    }

    public static string T(string key, params object[] args)
    {
        var fmt = T(key);
        try { return string.Format(fmt, args); }
        catch { return fmt; }
    }

    /// <summary>若文本是已登记的简中原文，返回当前语言译文。</summary>
    public static bool TryTranslateZh(string? zhOrAny, [NotNullWhen(true)] out string? translated)
    {
        translated = null;
        if (string.IsNullOrEmpty(zhOrAny)) return false;
        lock (Gate)
        {
            EnsureCatalogLocked();
            if (!_zhCnToKey.TryGetValue(zhOrAny, out var key)) return false;
            if (!_byKey.TryGetValue(key, out var e)) return false;
            translated = Pick(e);
            return true;
        }
    }

    public static string TranslateText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        return TryTranslateZh(text, out var t) ? t : text;
    }

    private static string Pick(Entry e) => _lang switch
    {
        AppLanguage.ZhTw => e.ZhTw,
        AppLanguage.En => e.En,
        _ => e.ZhCn
    };

    private readonly record struct Entry(string ZhCn, string ZhTw, string En);
}
