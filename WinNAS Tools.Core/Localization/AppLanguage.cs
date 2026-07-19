using System.Globalization;

namespace WinNASTools.Core.Localization;

public enum AppLanguage
{
    ZhCn,
    ZhTw,
    En
}

public static class AppLanguageHelper
{
    public const string Auto = "auto";
    public const string ZhCn = "zh-CN";
    public const string ZhTw = "zh-TW";
    public const string En = "en";

    public static string ToConfig(AppLanguage lang) => lang switch
    {
        AppLanguage.ZhTw => ZhTw,
        AppLanguage.En => En,
        _ => ZhCn
    };

    public static AppLanguage ParseConfig(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals(Auto, StringComparison.OrdinalIgnoreCase))
            return FromSystem();

        if (value.Equals(ZhTw, StringComparison.OrdinalIgnoreCase)
            || value.Equals("zh-HK", StringComparison.OrdinalIgnoreCase)
            || value.Equals("zh-MO", StringComparison.OrdinalIgnoreCase))
            return AppLanguage.ZhTw;

        if (value.Equals(En, StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            return AppLanguage.En;

        if (value.Equals(ZhCn, StringComparison.OrdinalIgnoreCase)
            || value.Equals("zh", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase))
            return AppLanguage.ZhCn;

        return FromSystem();
    }

    public static AppLanguage FromSystem()
    {
        var name = CultureInfo.CurrentUICulture.Name;
        if (name.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase))
            return AppLanguage.ZhTw;

        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return AppLanguage.ZhCn;

        if (name.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            return AppLanguage.En;

        // 非中英环境：默认英文，便于开源仓库国际用户
        return AppLanguage.En;
    }

    public static string DisplayName(AppLanguage lang) => lang switch
    {
        AppLanguage.ZhTw => "繁體中文",
        AppLanguage.En => "English",
        _ => "简体中文"
    };
}
