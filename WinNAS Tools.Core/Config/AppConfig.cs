using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinNASTools.Core;

public sealed class AppConfig
{
    public int PollIntervalMs { get; set; } = 1000;
    public ModulesConfig Modules { get; set; } = new();
    public LeaveConfig Leave { get; set; } = new();
    public WindowFeatureConfig Window { get; set; } = new();
    public PowerFeatureConfig Power { get; set; } = new();
    public MediaFeatureConfig Media { get; set; } = new();
    public BrowserFeatureConfig Browser { get; set; } = new();
    public LockFeatureConfig Lock { get; set; } = new();
    public AppSwitchConfig AppSwitch { get; set; } = new();
    public UrlLauncherConfig UrlLauncher { get; set; } = new();
    public PrinterConfig Printer { get; set; } = new();
    public BackupConfig Backup { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

public sealed class LoggingConfig
{
    /// <summary>后台日志保留天数，超过则丢弃；默认 90。</summary>
    public int RetentionDays { get; set; } = 90;
}

public sealed class ModulesConfig
{
    public bool Window { get; set; } = true;
    public bool Power { get; set; } = true;
    public bool Media { get; set; } = true;
    public bool Browser { get; set; } = true;
    public bool AppSwitch { get; set; } = true;
    public bool Lock { get; set; } = true;
    public bool LeaveGrace { get; set; } = true;
    public bool UrlLauncher { get; set; } = true;
    public bool Printer { get; set; } = true;
    public bool Backup { get; set; } = true;
}

public sealed class LeaveConfig
{
    public bool Enabled { get; set; } = true;
    /// <summary>进入离开态后短时阻止归来的秒数（一键离开与自动离开共用）。</summary>
    public int ReturnGraceSeconds { get; set; } = 10;
    /// <summary>一键离开热键，如 Ctrl+Alt+Shift+L</summary>
    public string Hotkey { get; set; } = "Ctrl+Alt+Shift+L";
    /// <summary>手动锁屏（Win+L 等）时，若当前仍是正常使用，是否先执行一轮离开任务。</summary>
    public bool RunLeaveOnManualLock { get; set; } = true;
}

public sealed class WindowFeatureConfig
{
    public bool Enabled { get; set; } = true;
    public int HideAfterSeconds { get; set; } = 900;
}

public sealed class PowerFeatureConfig
{
    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = "Performance";
    public int SaverAfterSeconds { get; set; } = 180;
}

public sealed class MediaFeatureConfig
{
    public bool Enabled { get; set; } = true;
    public int StopAfterSeconds { get; set; } = 900;
    public bool AutoResume { get; set; } = true;
    /// <summary>播放/暂停备用快捷键，最多 3 组；空项忽略。放在系统媒体键之后、静音之前。</summary>
    public List<string> PlayPauseHotkeys { get; set; } = new()
    {
        "Ctrl+Alt+Up",
        "Ctrl+Alt+P",
        "Ctrl+Shift+P"
    };

    public static IReadOnlyList<string> DefaultPlayPauseHotkeys { get; } =
        ["Ctrl+Alt+Up", "Ctrl+Alt+P", "Ctrl+Shift+P"];
}

public sealed class BrowserFeatureConfig
{
    public bool Enabled { get; set; } = false;
    public int CloseAfterSeconds { get; set; } = 1800;
    public string ProcessNames { get; set; } = "chrome,msedge,firefox";
}

public sealed class LockFeatureConfig
{
    /// <summary>默认关闭，避免升级后突然自动锁屏。</summary>
    public bool Enabled { get; set; } = false;
    public int LockAfterSeconds { get; set; } = 900;
}

/// <summary>自动停止应用：离开时停止指定进程，归来时按需重启。</summary>
public sealed class AppSwitchConfig
{
    public bool Enabled { get; set; } = false;
    public List<AppSwitchTaskConfig> Tasks { get; set; } = new();
}

public sealed class AppSwitchTaskConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "停止应用";
    /// <summary>应用的主进程名（不含扩展名）；运行时会包含同名实例及其全部子进程。</summary>
    public string ProcessName { get; set; } = "";
    /// <summary>exe 路径账本；选择模式由运行进程带出，路径模式由用户指定。留空则停止前再尝试读取运行路径。</summary>
    public string LaunchPath { get; set; } = "";
    /// <summary>归来时若上次因离开被本程序停止，是否重新启动。</summary>
    public bool RestartOnReturn { get; set; } = true;
    /// <summary>空闲多久后停止；&lt;=0 表示仅一键离开时停止。</summary>
    public int StopAfterSeconds { get; set; } = 900;
}

public sealed class UrlLauncherConfig
{
    public bool Enabled { get; set; } = true;
    public List<UrlLauncherTaskConfig> Tasks { get; set; } = new();
}

public sealed class UrlLauncherTaskConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "打开链接";
    public string Url { get; set; } = "";
    /// <summary>浏览器 exe 路径；留空则调用系统默认浏览器。</summary>
    public string BrowserPath { get; set; } = "";
    /// <summary>打开后是否按进程名关闭整个浏览器。</summary>
    public bool AutoCloseBrowser { get; set; }
    public int CloseDelaySeconds { get; set; } = 60;
    public int IntervalDays { get; set; } = 1;
    public int Hour { get; set; } = 8;
    public int Minute { get; set; }
    public DateTime? NextDueLocal { get; set; }
    public DateTime? LastRunLocal { get; set; }
    public string? LastResult { get; set; }
}

public sealed class PrinterConfig
{
    public bool Enabled { get; set; } = true;
    public List<PrinterTaskConfig> Tasks { get; set; } = new();
}

public sealed class PrinterTaskConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "维护任务";
    public string PrinterName { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public string ColorMode { get; set; } = "Color";
    public int IntervalDays { get; set; } = 7;
    public int Hour { get; set; } = 6;
    public int Minute { get; set; } = 0;
    public DateTime? NextDueLocal { get; set; }
    public DateTime? LastRunLocal { get; set; }
    public string? LastResult { get; set; }
}

public sealed class BackupConfig
{
    public bool Enabled { get; set; } = true;
    public List<BackupHostConfig> Hosts { get; set; } = new();
    public List<BackupTaskConfig> Tasks { get; set; } = new();
}

public sealed class BackupHostConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "主机";
    /// <summary>Smb | WebDav</summary>
    public string Kind { get; set; } = "Smb";
    /// <summary>SMB: \\server\share ；WebDAV: https://host/base</summary>
    public string PathOrUrl { get; set; } = "";
    public string UserName { get; set; } = "";
    public string? PasswordProtected { get; set; }
    public string Domain { get; set; } = "";
}

public sealed class BackupTaskConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "备份任务";
    public BackupEndpointConfig Source { get; set; } = new();
    public BackupEndpointConfig Target { get; set; } = new();
    /// <summary>Copy | Mirror | Sync（旧值 Add 视为 Copy）</summary>
    public string Mode { get; set; } = "Copy";
    /// <summary>
    /// 复制/镜像：Skip | Overwrite | OverwriteTrash；
    /// 同步：Skip | NewerWins | PreferA | PreferB
    /// </summary>
    public string ConflictPolicy { get; set; } = "OverwriteTrash";
    /// <summary>Planned | Realtime</summary>
    public string ScheduleMode { get; set; } = "Planned";
    /// <summary>复制/镜像且 OverwriteTrash 时使用；同步忽略</summary>
    public bool UseTrashOnTarget { get; set; } = true;
    /// <summary>同步：源侧回收站路径，空=不使用</summary>
    public string TrashPathSource { get; set; } = "";
    /// <summary>复制/镜像或同步 B 侧回收站。复制空=同级 .syncbak</summary>
    public string TrashPathTarget { get; set; } = "";
    public string ExcludePatterns { get; set; } = "/.syncbak\n.winnas-sync\nThumbs.db\n*.tmp";
    public int FileTimeoutSeconds { get; set; } = 120;
    public int RetryCount { get; set; } = 2;
    public int MtimeToleranceSeconds { get; set; } = 2;
    public int IntervalDays { get; set; } = 1;
    public int Hour { get; set; } = 3;
    public int Minute { get; set; } = 0;
    public DateTime? NextDueLocal { get; set; }
    public DateTime? LastRunLocal { get; set; }
    public string? LastResult { get; set; }
}

public sealed class BackupEndpointConfig
{
    /// <summary>Local | Host（旧配置 Smb/WebDav 仍可读）</summary>
    public string Kind { get; set; } = "Local";
    /// <summary>Kind=Host 时引用 BackupHostConfig.Id</summary>
    public string? HostId { get; set; }
    /// <summary>本机完整路径；主机下为相对子路径（可空=主机根）</summary>
    public string PathOrUrl { get; set; } = "";
    public string UserName { get; set; } = "";
    public string? PasswordProtected { get; set; }
    public string Domain { get; set; } = "";
}

public static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string ConfigDirectory => AppPaths.DataDirectory;
    public static string ConfigPath => AppPaths.ConfigPath;
    public static string LogPath => AppPaths.LogPath;

    public static AppConfig Load()
    {
        AppPaths.EnsureDataLayout();
        TryMigrateFromAppData();

        if (!File.Exists(ConfigPath))
            return new AppConfig();

        if (TryLoadFromFile(ConfigPath, out var cfg, out _))
            return cfg!;
        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        AppPaths.EnsureDataLayout();
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>校验并读取外部配置文件；失败返回 false 与错误信息。</summary>
    public static bool TryLoadFromFile(string path, out AppConfig? config, out string error)
    {
        config = null;
        error = "";
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            error = "配置文件不存在。";
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "配置文件为空。";
                return false;
            }

            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "配置根节点必须是 JSON 对象。";
                return false;
            }

            config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (config is null)
            {
                error = "无法解析为有效配置。";
                return false;
            }

            // 基本合法性：数值范围
            if (config.Logging.RetentionDays is < 1 or > 3650)
            {
                error = "日志保留天数必须在 1～3650 之间。";
                return false;
            }
            if (config.Leave.ReturnGraceSeconds < 1)
            {
                error = "离开短时阻止归来秒数必须 ≥ 1。";
                return false;
            }
            if (config.UrlLauncher is null || config.UrlLauncher.Tasks is null)
            {
                error = "定时打开链接配置无效。";
                return false;
            }
            foreach (var task in config.UrlLauncher.Tasks)
            {
                if (task.IntervalDays < 1 || task.Hour is < 0 or > 23 || task.Minute is < 0 or > 59)
                {
                    error = $"打开链接任务「{task.Name}」的计划时间无效。";
                    return false;
                }
                if (task.CloseDelaySeconds < 0)
                {
                    error = $"打开链接任务「{task.Name}」的关闭等待时间不能小于 0。";
                    return false;
                }
                if (!Uri.TryCreate(task.Url, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    error = $"打开链接任务「{task.Name}」的链接必须是有效的 HTTP/HTTPS 地址。";
                    return false;
                }
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON 无效：{ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"读取失败：{ex.Message}";
            return false;
        }
    }

    /// <summary>用已校验的配置覆盖本地文件。</summary>
    public static void ReplaceWith(AppConfig config) => Save(config);

    private static void TryMigrateFromAppData()
    {
        if (File.Exists(ConfigPath)) return;

        var legacyDirs = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppBranding.Name),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppBranding.LegacyExeName)
        };

        foreach (var legacyDir in legacyDirs)
        {
            foreach (var name in new[] { "WinNasToolsConfig.json", "config.json" })
            {
                var legacyConfig = Path.Combine(legacyDir, name);
                if (!File.Exists(legacyConfig)) continue;

                try
                {
                    File.Copy(legacyConfig, ConfigPath, overwrite: false);
                    var legacyLog = Path.Combine(legacyDir, "winnas-tools.log");
                    if (File.Exists(legacyLog) && !File.Exists(LogPath))
                        File.Copy(legacyLog, LogPath, overwrite: false);
                    return;
                }
                catch { /* try next */ }
            }
        }
    }
}
