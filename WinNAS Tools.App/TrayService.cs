using System.Drawing;
using System.IO;
using System.Text.Json;
using WinNASTools.Core;
using WinNASTools.Core.Backup;
using WinNASTools.Core.Features;
using WinNASTools.Core.Hosting;
using WinNASTools.Core.Localization;

namespace WinNASTools.App;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly AppHost _host;
    private readonly BackupFeature? _backupFeature;
    private readonly ToolStripMenuItem _showItem;
    private readonly ToolStripMenuItem _leaveItem;
    private readonly ToolStripMenuItem _powerItem;
    private readonly ToolStripMenuItem _modulesItem;
    private readonly ToolStripMenuItem _hotkeyItem;
    private readonly ToolStripMenuItem _logDaysItem;
    private readonly ToolStripMenuItem _exportItem;
    private readonly ToolStripMenuItem _importItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _languageItem;
    private readonly ToolStripMenuItem _languageAutoItem;
    private readonly ToolStripMenuItem _languageZhCnItem;
    private readonly ToolStripMenuItem _languageZhTwItem;
    private readonly ToolStripMenuItem _languageEnItem;
    private readonly ToolStripMenuItem _restoreItem;
    private readonly ToolStripMenuItem _exitItem;
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
    private readonly Action<string> _changeLanguageRestart;
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
        Action<string> changeLanguageRestart,
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
        _changeLanguageRestart = changeLanguageRestart;
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

        _showItem = new ToolStripMenuItem();
        _showItem.Click += (_, _) => _toggleWindow();

        _leaveItem = new ToolStripMenuItem();
        _leaveItem.Click += (_, _) => _leaveNow();

        _powerPerfItem = new ToolStripMenuItem { CheckOnClick = false };
        _powerPerfItem.Click += (_, _) => SetPowerMode("Performance");
        _powerBalancedItem = new ToolStripMenuItem { CheckOnClick = false };
        _powerBalancedItem.Click += (_, _) => SetPowerMode("Balanced");
        _powerManualItem = new ToolStripMenuItem { CheckOnClick = false };
        _powerManualItem.Click += (_, _) => SetPowerMode("Manual");

        _powerItem = new ToolStripMenuItem();
        _powerItem.DropDownItems.Add(_powerPerfItem);
        _powerItem.DropDownItems.Add(_powerBalancedItem);
        _powerItem.DropDownItems.Add(_powerManualItem);

        _modulesItem = new ToolStripMenuItem();
        _modulesItem.Click += (_, _) => _openModules();

        _hotkeyItem = new ToolStripMenuItem();
        _hotkeyItem.Click += (_, _) => _editHotkey();

        _logDaysItem = new ToolStripMenuItem();
        _logDaysItem.Click += (_, _) => _editLogRetention();

        _exportItem = new ToolStripMenuItem();
        _exportItem.Click += (_, _) => _exportConfig();

        _importItem = new ToolStripMenuItem();
        _importItem.Click += (_, _) => _importConfig();

        _languageAutoItem = new ToolStripMenuItem { CheckOnClick = false };
        _languageAutoItem.Click += (_, _) => SetLanguage(AppLanguageHelper.Auto);
        _languageZhCnItem = new ToolStripMenuItem { CheckOnClick = false };
        _languageZhCnItem.Click += (_, _) => SetLanguage(AppLanguageHelper.ZhCn);
        _languageZhTwItem = new ToolStripMenuItem { CheckOnClick = false };
        _languageZhTwItem.Click += (_, _) => SetLanguage(AppLanguageHelper.ZhTw);
        _languageEnItem = new ToolStripMenuItem { CheckOnClick = false };
        _languageEnItem.Click += (_, _) => SetLanguage(AppLanguageHelper.En);

        _languageItem = new ToolStripMenuItem();
        _languageItem.DropDownItems.Add(_languageAutoItem);
        _languageItem.DropDownItems.Add(_languageZhCnItem);
        _languageItem.DropDownItems.Add(_languageZhTwItem);
        _languageItem.DropDownItems.Add(_languageEnItem);

        _autostartItem = new ToolStripMenuItem { CheckOnClick = false };
        _autostartItem.Click += (_, _) =>
        {
            var exe = _exePath();
            if (string.IsNullOrWhiteSpace(exe)) return;
            AutostartService.SetEnabled(!AutostartService.IsEnabled(), exe);
            RefreshMenuTexts();
        };

        _restoreItem = new ToolStripMenuItem();
        _restoreItem.Click += (_, _) => _restoreDefaults();

        _settingsItem = new ToolStripMenuItem();
        _settingsItem.DropDownItems.Add(_modulesItem);
        _settingsItem.DropDownItems.Add(_hotkeyItem);
        _settingsItem.DropDownItems.Add(_logDaysItem);
        _settingsItem.DropDownItems.Add(_languageItem);
        _settingsItem.DropDownItems.Add(new ToolStripSeparator());
        _settingsItem.DropDownItems.Add(_exportItem);
        _settingsItem.DropDownItems.Add(_importItem);
        _settingsItem.DropDownItems.Add(new ToolStripSeparator());
        _settingsItem.DropDownItems.Add(_autostartItem);
        _settingsItem.DropDownItems.Add(_restoreItem);

        _toggleItem = new ToolStripMenuItem();
        _toggleItem.Click += (_, _) =>
        {
            if (_host.IsRunning) _host.Stop();
            else _host.Start();
            RefreshMenuTexts();
        };

        _exitItem = new ToolStripMenuItem();
        _exitItem.Click += (_, _) => exit();

        menu.Items.Add(_showItem);
        menu.Items.Add(_leaveItem);
        menu.Items.Add(_powerItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);
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

    private void SetLanguage(string languageCode)
    {
        var current = _host.Config.Ui.Language ?? AppLanguageHelper.Auto;
        if (string.Equals(current, languageCode, StringComparison.OrdinalIgnoreCase))
            return;
        _changeLanguageRestart(languageCode);
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
                ? Loc.T("Tray.NoBackupHistory")
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
            ? Loc.T("Tray.Backup.DefaultName")
            : progress.TaskName.Trim();
        if (name.Length > 12)
            name = name[..11] + "…";

        return progress.Phase switch
        {
            "Scanning" => Loc.T("Tray.Backup.Scanning", name),
            "Running" when progress.Total > 0 => Loc.T("Tray.Backup.RunningPercent", name, progress.Percent),
            "Running" => Loc.T("Tray.Backup.Running", name),
            "Cancelling" => Loc.T("Tray.Backup.Cancelling", name),
            _ => Loc.T("Tray.Backup.StatusLine", name, progress.StatusText, (progress.EndedAtLocal ?? progress.StartedAtLocal).ToString("HH:mm"))
        };
    }

    private static string FormatTrayTask(BackupTaskConfig task)
    {
        var name = string.IsNullOrWhiteSpace(task.Name) ? Loc.T("Tray.Backup.DefaultName") : task.Name.Trim();
        if (name.Length > 12)
            name = name[..11] + "…";

        if (task.LastRunLocal is null)
            return Loc.T("Tray.Backup.NotRun", name);

        var cancelled = Loc.T("Tray.Backup.Cancelled");
        var status = task.LastResult is not null
            && (task.LastResult.Contains("已取消", StringComparison.Ordinal)
                || task.LastResult.Contains(cancelled, StringComparison.Ordinal)
                || task.LastResult.Contains("Cancelled", StringComparison.OrdinalIgnoreCase))
            ? cancelled
            : Loc.T("Tray.Backup.Success");
        return Loc.T("Tray.Backup.StatusLine", name, status, task.LastRunLocal.Value.ToString("HH:mm"));
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
        _showItem.Text = Loc.T("Tray.OpenPanel");
        _leaveItem.Text = Loc.T("Tray.LeaveNow");
        _powerItem.Text = Loc.T("Tray.PowerPreference");
        _powerPerfItem.Text = Loc.T("Power.Performance");
        _powerBalancedItem.Text = Loc.T("Power.Balanced");
        _powerManualItem.Text = Loc.T("Power.Manual");
        _modulesItem.Text = Loc.T("Tray.Modules");
        _hotkeyItem.Text = Loc.T("Tray.LeaveHotkey");
        _logDaysItem.Text = Loc.T("Tray.LogRetention");
        _languageItem.Text = Loc.T("Language.Title");
        _languageAutoItem.Text = Loc.T("Language.Auto");
        _languageZhCnItem.Text = Loc.T("Language.ZhCn");
        _languageZhTwItem.Text = Loc.T("Language.ZhTw");
        _languageEnItem.Text = Loc.T("Language.En");
        _exportItem.Text = Loc.T("Tray.ExportConfig");
        _importItem.Text = Loc.T("Tray.ImportConfig");
        _autostartItem.Text = Loc.T("Tray.Autostart");
        _restoreItem.Text = Loc.T("Tray.RestoreDefaults");
        _settingsItem.Text = Loc.T("Tray.Settings");
        _exitItem.Text = Loc.T("Tray.Exit");
        _toggleItem.Text = _host.IsRunning ? Loc.T("Tray.StopMonitoring") : Loc.T("Tray.StartMonitoring");
        _autostartItem.Checked = AutostartService.IsEnabled();

        var lang = _host.Config.Ui.Language ?? AppLanguageHelper.Auto;
        if (string.IsNullOrWhiteSpace(lang)) lang = AppLanguageHelper.Auto;
        _languageAutoItem.Checked = lang.Equals(AppLanguageHelper.Auto, StringComparison.OrdinalIgnoreCase);
        _languageZhCnItem.Checked = lang.Equals(AppLanguageHelper.ZhCn, StringComparison.OrdinalIgnoreCase);
        _languageZhTwItem.Checked = lang.Equals(AppLanguageHelper.ZhTw, StringComparison.OrdinalIgnoreCase);
        _languageEnItem.Checked = lang.Equals(AppLanguageHelper.En, StringComparison.OrdinalIgnoreCase);

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
