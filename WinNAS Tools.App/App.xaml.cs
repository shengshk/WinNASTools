using System.Diagnostics;
using System.IO;
using System.Windows;
using WinNASTools.Core;
using WinNASTools.Core.Hosting;

namespace WinNASTools.App;

public partial class App : System.Windows.Application
{
    private AppHost? _host;
    private TrayService? _tray;
    private HotkeyService? _hotkey;
    private MainWindow? _main;
    private ModulePanelWindow? _modulePanel;
    private int _uiExceptionLogCount;
    private DateTime _uiExceptionLogWindow = DateTime.MinValue;

    protected override void OnStartup(StartupEventArgs e)
    {
        EmbeddedBootstrap.EnsureDataFiles();
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                // 布局异常会每帧触发；限流，避免日志把磁盘/UI 打爆
                var now = DateTime.UtcNow;
                if ((now - _uiExceptionLogWindow).TotalSeconds >= 5)
                {
                    _uiExceptionLogWindow = now;
                    _uiExceptionLogCount = 0;
                }

                if (_uiExceptionLogCount < 3)
                {
                    _uiExceptionLogCount++;
                    _host?.Log.Error($"未处理 UI 异常: {args.Exception.Message}");
                    var path = Path.Combine(AppPaths.DataDirectory, "crash.log");
                    if (!File.Exists(path) || new FileInfo(path).Length < 2_000_000)
                    {
                        File.AppendAllText(
                            path,
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} UI: {args.Exception.GetType().Name}: {args.Exception.Message}\r\n");
                    }
                }
            }
            catch { /* ignore */ }
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                var ex = args.ExceptionObject as Exception;
                _host?.Log.Error($"未处理异常: {ex?.Message}");
                var path = Path.Combine(AppPaths.DataDirectory, "crash.log");
                if (!File.Exists(path) || new FileInfo(path).Length < 2_000_000)
                {
                    File.AppendAllText(
                        path,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} FATAL: {ex}\r\n\r\n");
                }
            }
            catch { /* ignore */ }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            try
            {
                _host?.Log.Error($"未观察任务异常: {args.Exception.Message}");
                var path = Path.Combine(AppPaths.DataDirectory, "crash.log");
                if (!File.Exists(path) || new FileInfo(path).Length < 2_000_000)
                {
                    File.AppendAllText(
                        path,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} TASK: {args.Exception.Message}\r\n");
                }
                args.SetObserved();
            }
            catch { /* ignore */ }
        };

        base.OnStartup(e);

        _host = new AppHost();
        _host.RegisterDefaults();
        _host.SetUiLogSink(line =>
        {
            Current.Dispatcher.InvokeAsync(() => _main?.AppendLog(line));
        });

        _main = new MainWindow(_host);
        // 尽早藏住，避免后续初始化期间闪出面板。
        _main.TryHideToTray();

        void LeaveNow()
        {
            // 离开含 powercfg / 关浏览器 / 媒体键，绝不能占 UI 线程。
            _ = Task.Run(() =>
            {
                try { _host?.LeaveNow(); }
                catch (Exception ex) { _host?.Log.Error($"一键离开失败: {ex.Message}"); }
            });
        }

        void OpenModules() => Current.Dispatcher.Invoke(() =>
        {
            if (_host is null) return;
            if (_modulePanel is { IsVisible: true })
            {
                _modulePanel.Activate();
                return;
            }

            _modulePanel = new ModulePanelWindow(_host, () => _main?.RefreshFromHost());
            _modulePanel.Closed += (_, _) => _modulePanel = null;
            _modulePanel.Show();
        });

        void EditHotkey() => Current.Dispatcher.Invoke(() =>
        {
            if (_host is null || _hotkey is null) return;
            var dlg = new HotkeyEditWindow(_host.Config.Leave.Hotkey)
            {
                Owner = _main?.IsVisible == true ? _main : null
            };
            if (dlg.ShowDialog() != true) return;

            if (!_hotkey.TryRebind(dlg.HotkeySpec))
            {
                System.Windows.MessageBox.Show(
                    $"热键「{dlg.HotkeySpec}」注册失败（可能被占用），已保持原快捷键。",
                    AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                _hotkey.TryRebind(_host.Config.Leave.Hotkey);
                return;
            }

            var cfg = CloneConfig(_host.Config);
            cfg.Leave.Hotkey = dlg.HotkeySpec;
            _host.ReloadConfig(cfg);
            _host.Log.Info($"离开热键已改为：{_hotkey.DisplayText}");
            _main?.RefreshHotkeyStatus(_hotkey.DisplayText);
        });

        void EditLogRetention() => Current.Dispatcher.Invoke(() =>
        {
            if (_host is null) return;
            var current = _host.Config.Logging.RetentionDays.ToString();
            if (_main is null || !_main.TryPromptLogRetention(current, out var days))
                return;
            _host.ApplyLogRetention(days);
            _main.ReloadLogView();
        });

        void ExportConfig() => Current.Dispatcher.Invoke(() =>
        {
            if (_host is null) return;
            try
            {
                ConfigStore.Save(_host.Config);
                using var dlg = new SaveFileDialog
                {
                    Title = "导出配置",
                    Filter = "WinNAS Tools 配置|*.json|所有文件|*.*",
                    FileName = "WinNasToolsConfig.json",
                    OverwritePrompt = true
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;
                File.Copy(ConfigStore.ConfigPath, dlg.FileName, overwrite: true);
                _host.Log.Info($"配置已导出：{dlg.FileName}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"导出失败：{ex.Message}",
                    AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        void ImportConfig() => Current.Dispatcher.Invoke(() =>
        {
            if (_host is null) return;

            using var dlg = new OpenFileDialog
            {
                Title = "导入配置",
                Filter = "WinNAS Tools 配置|*.json|所有文件|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            if (!ConfigStore.TryLoadFromFile(dlg.FileName, out var cfg, out var error) || cfg is null)
            {
                System.Windows.MessageBox.Show(
                    $"配置不合法，已取消导入。\n\n{error}",
                    AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = System.Windows.MessageBox.Show(
                "导入将覆盖当前配置并重启程序，是否继续？",
                AppBranding.Name, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                ConfigStore.ReplaceWith(cfg);
                _host.Log.Info($"配置已导入：{dlg.FileName}，即将重启。");
                RestartApplication();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"导入失败：{ex.Message}",
                    AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        _hotkey = new HotkeyService(LeaveNow, _host.Config.Leave.Hotkey);
        if (_hotkey.IsRegistered)
            _host.Log.Info($"热键已注册：{_hotkey.DisplayText} → 一键离开");
        else
            _host.Log.Warn($"热键注册失败（可能被占用）：{_host.Config.Leave.Hotkey}");

        _tray = new TrayService(
            _host,
            toggleWindow: () => Current.Dispatcher.Invoke(() =>
            {
                if (_main is null) return;
                if (_main.IsVisible)
                    _main.TryHideToTray();
                else
                    _main.ShowFromTray();
            }),
            exit: ShutdownApp,
            restoreDefaults: () => Current.Dispatcher.Invoke(() =>
            {
                _main?.RestoreDefaults();
                var hk = _host?.Config.Leave.Hotkey ?? "Ctrl+Alt+Shift+L";
                if (_hotkey is null) return;
                _hotkey.TryRebind(hk);
                _main?.RefreshHotkeyStatus(_hotkey.DisplayText);
            }),
            leaveNow: LeaveNow,
            openModules: OpenModules,
            editHotkey: EditHotkey,
            editLogRetention: EditLogRetention,
            exportConfig: ExportConfig,
            importConfig: ImportConfig,
            refreshMainUi: () => Current.Dispatcher.Invoke(() => _main?.RefreshFromHost()),
            exePath: () => Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName);

        _main.RefreshHotkeyStatus(_hotkey.DisplayText);
        _host.Start();
        // 再次确认：开机/手动启动均只落托盘，不弹面板。
        _main.TryHideToTray();
    }

    private static AppConfig CloneConfig(AppConfig src)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(src);
        return System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    private void RestartApplication()
    {
        var exe = Environment.ProcessPath
                  ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(exe))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
            });
        }

        ShutdownApp(force: true);
    }

    private void ShutdownApp() => ShutdownApp(force: false);

    private void ShutdownApp(bool force)
    {
        if (!force && _main is not null && !_main.ResolvePendingDrafts())
            return;
        _hotkey?.Dispose();
        _host?.Stop();
        _host?.Dispose();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _host?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
