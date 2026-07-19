using System.Windows;
using WinNASTools.Core;
using WinNASTools.Core.Hosting;
using WinNASTools.Core.Localization;

namespace WinNASTools.App;

public partial class ModulePanelWindow : Window
{
    private readonly AppHost _host;
    private readonly Action _onChanged;
    private bool _loading;

    public ModulePanelWindow(AppHost host, Action onChanged)
    {
        InitializeComponent();
        _host = host;
        _onChanged = onChanged;
        Loaded += (_, _) => UiLocalizer.Apply(this);
        _loading = true;
        LoadFromConfig();
        _loading = false;
    }

    private void LoadFromConfig()
    {
        var m = _host.Config.Modules;
        ChkWindow.IsChecked = m.Window;
        ChkPower.IsChecked = m.Power;
        ChkMedia.IsChecked = m.Media;
        ChkBrowser.IsChecked = m.Browser;
        ChkAppSwitch.IsChecked = m.AppSwitch;
        ChkLock.IsChecked = m.Lock;
        ChkLeaveGrace.IsChecked = m.LeaveGrace;
        ChkUrlLauncher.IsChecked = m.UrlLauncher;
        ChkPrinter.IsChecked = m.Printer;
        ChkBackup.IsChecked = m.Backup;
    }

    private void Module_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Apply();
    }

    private void Apply()
    {
        var cfg = CloneConfig(_host.Config);
        cfg.Modules = new ModulesConfig
        {
            Window = ChkWindow.IsChecked == true,
            Power = ChkPower.IsChecked == true,
            Media = ChkMedia.IsChecked == true,
            Browser = ChkBrowser.IsChecked == true,
            AppSwitch = ChkAppSwitch.IsChecked == true,
            Lock = ChkLock.IsChecked == true,
            LeaveGrace = ChkLeaveGrace.IsChecked == true,
            UrlLauncher = ChkUrlLauncher.IsChecked == true,
            Printer = ChkPrinter.IsChecked == true,
            Backup = ChkBackup.IsChecked == true
        };

        cfg.Window.Enabled = cfg.Modules.Window;
        cfg.Power.Enabled = cfg.Modules.Power;
        cfg.Media.Enabled = cfg.Modules.Media;
        cfg.Browser.Enabled = cfg.Modules.Browser;
        cfg.AppSwitch.Enabled = cfg.Modules.AppSwitch;
        if (!cfg.Modules.Lock)
            cfg.Lock.Enabled = false;
        // 模块开关只控制是否显示/启用该区块，不覆盖用户勾选的 Leave.Enabled 秒数偏好。
        cfg.UrlLauncher.Enabled = cfg.Modules.UrlLauncher;
        cfg.Printer.Enabled = cfg.Modules.Printer;
        cfg.Backup.Enabled = cfg.Modules.Backup;

        _host.ReloadConfig(cfg);
        _host.Log.Info(Loc.T(
            "Log.Modules.Updated",
            cfg.Modules.Window, cfg.Modules.Power, cfg.Modules.Media,
            cfg.Modules.Browser, cfg.Modules.AppSwitch, cfg.Modules.Lock, cfg.Modules.LeaveGrace,
            cfg.Modules.UrlLauncher, cfg.Modules.Printer, cfg.Modules.Backup));
        _onChanged();
    }

    private static AppConfig CloneConfig(AppConfig src)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(src);
        return System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }
}
