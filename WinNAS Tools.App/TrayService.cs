using System.Drawing;
using System.IO;
using System.Text.Json;
using WinNASTools.Core;
using WinNASTools.Core.Backup;
using WinNASTools.Core.Features;
using WinNASTools.Core.Hosting;

namespace WinNASTools.App;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly AppHost _host;
    private readonly BackupFeature? _backupFeature;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly ToolStripMenuItem _powerPerfItem;
    private readonly ToolStripMenuItem _powerBalancedItem;
    private readonly ToolStripMenuItem _powerManualItem;
    private readonly Action _toggleWindow;
    private readonly Action _restoreDefaults;
    private readonly Action _leaveNow;
    private readonly Action _openModules;
    private readonly Action _editHotkey;
    private readonly Action _editLogRetention;
    private readonly Action _exportConfig;
    private readonly Action _importConfig;
    private readonly Action _refreshMainUi;
    private readonly Func<string?> _exePath;
    private readonly string _defaultTip = AppBranding.Name;
    private readonly SynchronizationContext? _sync;

    public TrayService(
        AppHost host,
        Action toggleWindow,
        Action exit,
        Action restoreDefaults,
        Action leaveNow,
        Action openModules,
        Action editHotkey,
        Action editLogRetention,
        Action exportConfig,
        Action importConfig,
        Action refreshMainUi,
        Func<string?> exePath)
    {
        _host = host;
        _toggleWindow = toggleWindow;
        _restoreDefaults = restoreDefaults;
        _leaveNow = leaveNow;
        _openModules = openModules;
        _editHotkey = editHotkey;
        _editLogRetention = editLogRetention;
        _exportConfig = exportConfig;
        _importConfig = importConfig;
        _refreshMainUi = refreshMainUi;
        _exePath = exePath;
        _sync = SynchronizationContext.Current;
        _backupFeature = host.Features.OfType<BackupFeature>().FirstOrDefault();

        _icon = new NotifyIcon
        {
            Text = ClipTip(_defaultTip),
            Visible = true,
            Icon = LoadIcon()
        };

        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => RefreshMenuTexts();

        var showItem = new ToolStripMenuItem("打开面板");
        showItem.Click += (_, _) => _toggleWindow();

        var leaveItem = new ToolStripMenuItem("一键离开");
        leaveItem.Click += (_, _) => _leaveNow();

        _powerPerfItem = new ToolStripMenuItem("性能") { CheckOnClick = false };
        _powerPerfItem.Click += (_, _) => SetPowerMode("Performance");
        _powerBalancedItem = new ToolStripMenuItem("平衡") { CheckOnClick = false };
        _powerBalancedItem.Click += (_, _) => SetPowerMode("Balanced");
        _powerManualItem = new ToolStripMenuItem("手动") { CheckOnClick = false };
        _powerManualItem.Click += (_, _) => SetPowerMode("Manual");

        var powerItem = new ToolStripMenuItem("电源偏好");
        powerItem.DropDownItems.Add(_powerPerfItem);
        powerItem.DropDownItems.Add(_powerBalancedItem);
        powerItem.DropDownItems.Add(_powerManualItem);

        var modulesItem = new ToolStripMenuItem("模块开关");
        modulesItem.Click += (_, _) => _openModules();

        var hotkeyItem = new ToolStripMenuItem("离开快捷键…");
        hotkeyItem.Click += (_, _) => _editHotkey();

        var logDaysItem = new ToolStripMenuItem("日志保存天数…");
        logDaysItem.Click += (_, _) => _editLogRetention();

        var exportItem = new ToolStripMenuItem("导出配置…");
        exportItem.Click += (_, _) => _exportConfig();

        var importItem = new ToolStripMenuItem("导入配置…");
        importItem.Click += (_, _) => _importConfig();

        _autostartItem = new ToolStripMenuItem("开机自启") { CheckOnClick = false };
        _autostartItem.Click += (_, _) =>
        {
            var exe = _exePath();
            if (string.IsNullOrWhiteSpace(exe)) return;
            AutostartService.SetEnabled(!AutostartService.IsEnabled(), exe);
            RefreshMenuTexts();
        };

        var restoreItem = new ToolStripMenuItem("恢复默认");
        restoreItem.Click += (_, _) => _restoreDefaults();

        var settingsItem = new ToolStripMenuItem("系统设置");
        settingsItem.DropDownItems.Add(modulesItem);
        settingsItem.DropDownItems.Add(hotkeyItem);
        settingsItem.DropDownItems.Add(logDaysItem);
        settingsItem.DropDownItems.Add(new ToolStripSeparator());
        settingsItem.DropDownItems.Add(exportItem);
        settingsItem.DropDownItems.Add(importItem);
        settingsItem.DropDownItems.Add(new ToolStripSeparator());
        settingsItem.DropDownItems.Add(_autostartItem);
        settingsItem.DropDownItems.Add(restoreItem);

        _toggleItem = new ToolStripMenuItem("停止监控");
        _toggleItem.Click += (_, _) =>
        {
            if (_host.IsRunning) _host.Stop();
            else _host.Start();
            RefreshMenuTexts();
        };

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => exit();

        menu.Items.Add(showItem);
        menu.Items.Add(leaveItem);
        menu.Items.Add(powerItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        _icon.ContextMenuStrip = menu;

        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _toggleWindow();
        };
        _host.StatusChanged += OnHostStatusChanged;
        if (_backupFeature is not null)
            _backupFeature.ProgressChanged += OnBackupProgressChanged;
        RefreshMenuTexts();
        RefreshTrayTip(_backupFeature?.LatestProgress);
    }

    private void SetPowerMode(string mode)
    {
        if (string.Equals(_host.Config.Power.Mode, mode, StringComparison.OrdinalIgnoreCase))
        {
            // 再点一次已选项：仍主动对齐一次当前系统计划。
            _host.Features.OfType<PowerFeature>().FirstOrDefault()?.ApplyPreferenceNow();
            RefreshMenuTexts();
            return;
        }

        var json = JsonSerializer.Serialize(_host.Config);
        var cfg = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        cfg.Power.Mode = mode;
        _host.ReloadConfig(cfg);
        try { _refreshMainUi(); } catch { /* ignore */ }
        RefreshMenuTexts();
    }

    private void OnBackupProgressChanged(BackupProgress? progress)
    {
        // NotifyIcon 必须在创建它的 UI 线程上访问，备份进度来自后台线程。
        PostToUi(() => RefreshTrayTip(progress));
    }

    private void RefreshTrayTip(BackupProgress? progress)
    {
        try
        {
            if (_backupFeature is null)
            {
                _icon.Text = ClipTip(_defaultTip);
                return;
            }

            var records = _backupFeature.History
                .OrderByDescending(x => x.IsActive)
                .ThenByDescending(x => x.StartedAtLocal)
                .ToList();

            // 回调参数是刚产生的最新快照，优先用它，避免托盘落后一拍。
            if (progress is not null)
            {
                records.RemoveAll(x => x.RunId == progress.RunId);
                records.Insert(0, progress);
                records = records
                    .OrderByDescending(x => x.IsActive)
                    .ThenByDescending(x => x.StartedAtLocal)
                    .ToList();
            }

            var entries = new List<string>();
            foreach (var record in records)
            {
                var entry = FormatTrayLine(record);
                var candidate = string.Join(" | ", entries.Append(entry));
                if (candidate.Length > 63)
                    break;
                entries.Add(entry);
                if (entries.Count == 3)
                    break;
            }

            // 尚无运行历史时也展示已配置任务，不能只在备份进行中显示。
            if (entries.Count == 0)
            {
                foreach (var task in _host.Config.Backup.Tasks)
                {
                    var entry = FormatTrayTask(task);
                    var candidate = string.Join(" | ", entries.Append(entry));
                    if (candidate.Length > 63)
                        break;
                    entries.Add(entry);
                    if (entries.Count == 3)
                        break;
                }
            }

            _icon.Text = entries.Count == 0
                ? "WinNAS Tools · 暂无备份记录"
                : string.Join(" | ", entries);
        }
        catch
        {
            try { _icon.Text = ClipTip(_defaultTip); } catch { /* ignore */ }
        }
    }

    private static string FormatTrayLine(BackupProgress progress)
    {
        var name = string.IsNullOrWhiteSpace(progress.TaskName)
            ? "备份"
            : progress.TaskName.Trim();
        if (name.Length > 12)
            name = name[..11] + "…";

        return progress.Phase switch
        {
            "Scanning" => $"{name} · 扫描中",
            "Running" when progress.Total > 0 => $"{name} · {progress.Percent}%",
            "Running" => $"{name} · 执行中",
            "Cancelling" => $"{name} · 取消中",
            _ => $"{name} · {progress.StatusText} · {(progress.EndedAtLocal ?? progress.StartedAtLocal):HH:mm}"
        };
    }

    private static string FormatTrayTask(BackupTaskConfig task)
    {
        var name = string.IsNullOrWhiteSpace(task.Name) ? "备份" : task.Name.Trim();
        if (name.Length > 12)
            name = name[..11] + "…";

        if (task.LastRunLocal is null)
            return $"{name} · 未运行";

        var status = task.LastResult?.Contains("已取消", StringComparison.Ordinal) == true
            ? "已取消"
            : "成功";
        return $"{name} · {status} · {task.LastRunLocal:HH:mm}";
    }

    private static string ClipTip(string text)
    {
        if (string.IsNullOrEmpty(text)) return "WinNAS Tools";
        return text.Length <= 63 ? text : text[..60] + "…";
    }

    private void OnHostStatusChanged()
    {
        PostToUi(() =>
        {
            RefreshMenuTexts();
            // BackupFeature 在托盘创建后才加载历史，启动完成时必须再刷新一次。
            RefreshTrayTip(_backupFeature?.LatestProgress);
        });
    }

    private void PostToUi(Action action)
    {
        try
        {
            if (_sync is not null)
            {
                _sync.Post(_ =>
                {
                    try { action(); } catch { /* ignore */ }
                }, null);
                return;
            }

            if (_icon.ContextMenuStrip?.InvokeRequired == true)
            {
                _icon.ContextMenuStrip.BeginInvoke(action);
                return;
            }

            action();
        }
        catch
        {
            /* ignore */
        }
    }

    private void RefreshMenuTexts()
    {
        _toggleItem.Text = _host.IsRunning ? "停止监控" : "开始监控";
        _autostartItem.Checked = AutostartService.IsEnabled();

        var mode = _host.Config.Power.Mode ?? "";
        _powerPerfItem.Checked = string.Equals(mode, "Performance", StringComparison.OrdinalIgnoreCase);
        _powerBalancedItem.Checked = string.Equals(mode, "Balanced", StringComparison.OrdinalIgnoreCase);
        _powerManualItem.Checked = string.Equals(mode, "Manual", StringComparison.OrdinalIgnoreCase);
    }

    private static Icon LoadIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(path))
                return new Icon(path);

            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe) && File.Exists(exe))
            {
                var embedded = Icon.ExtractAssociatedIcon(exe);
                if (embedded is not null)
                    return embedded;
            }
        }
        catch { /* fall through */ }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _host.StatusChanged -= OnHostStatusChanged;
        if (_backupFeature is not null)
            _backupFeature.ProgressChanged -= OnBackupProgressChanged;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
