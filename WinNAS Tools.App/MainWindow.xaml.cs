using System.Text.Json;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinNASTools.Core;
using WinNASTools.Core.Backup;
using WinNASTools.Core.Features;
using WinNASTools.Core.Hosting;
using WinNASTools.Core.Localization;
using WinNASTools.Core.Services;

namespace WinNASTools.App;

public partial class MainWindow : Window
{
    private readonly AppHost _host;
    private bool _allowExit;
    private bool _loading;
    private bool _printerUiLoading;
    private bool _powerRowExpanded;
    private bool _mediaRowExpanded;
    private bool _mediaHotkeysExpanded;
    private bool _mediaHotkeyDialogOpen;
    private bool _mediaHotkeysToggleGuard;
    private bool _browserRowExpanded;
    private bool _printerRowExpanded = true;
    private bool _backupRowExpanded;
    private bool _backupUiLoading;
    private bool _backupModeUiUpdating;
    private bool _printerDirty;
    private bool _backupDirty;
    private bool _urlRowExpanded;
    private bool _urlUiLoading;
    private bool _urlDirty;
    private string? _selectedUrlId;
    private bool _appSwitchRowExpanded;
    private bool _appSwitchUiLoading;
    private bool _appSwitchDirty;
    private bool _appListComplex;
    private string? _selectedAppSwitchId;
    /// <summary>当前编辑中的规范进程名（不含「· 未运行」等展示后缀）。</summary>
    private string _appSwitchProcessName = "";
    private AppSwitchBindMode _appSwitchBindMode = AppSwitchBindMode.Select;
    private List<AppProcessChoice> _availableAppChoices = new();
    private readonly Dictionary<string, PrinterTaskDraft> _printerDrafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UrlTaskDraft> _urlDrafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BackupTaskDraft> _backupDrafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AppSwitchTaskDraft> _appSwitchDrafts = new(StringComparer.Ordinal);
    private enum AppSwitchBindMode { Select, Path }
    private static readonly HashSet<string> HiddenSystemProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost",
        "SearchApp", "ApplicationFrameHost", "SystemSettings", "Taskmgr", "LockApp",
        "TextInputHost", "SecurityHealthSystray", "RuntimeBroker", "dllhost", "conhost",
        "sihost", "fontdrvhost", "ctfmon", "dwm", "csrss", "smss", "winlogon", "wininit",
        "services", "lsass", "svchost", "System", "Idle", "Registry", "Memory Compression",
        "WmiPrvSE", "spoolsv", "SearchIndexer", "audiodg", "smartscreen", "SecurityHealthService",
        "MsMpEng", "NisSrv", "WinNASTools", "WinNASTools.App"
    };
    private string _backupScheduleMode = "Planned";
    private string _hotkeyDisplay = "Ctrl+Alt+Shift+L";
    private readonly string[] _mediaHotkeys =
    [
        MediaFeatureConfig.DefaultPlayPauseHotkeys[0],
        MediaFeatureConfig.DefaultPlayPauseHotkeys[1],
        MediaFeatureConfig.DefaultPlayPauseHotkeys[2]
    ];
    private LogViewFilter _logFilter = LogViewFilter.Normal;
    private DateTime? _logClearedAt;
    private readonly DispatcherTimer _logFollowTimer;
    private bool _logAutoFollow = true;
    /// <summary>内容末尾预留空行：仅在滚到底时形成呼吸感，不占用固定可视遮罩。</summary>
    private static readonly string LogBottomPad =
        Environment.NewLine + Environment.NewLine + Environment.NewLine;
    private string? _selectedTaskId;
    private string? _selectedBackupId;
    private string? _copyTrashAutoDefault;
    private BackupFeature? _backupFeature;
    private int _backupProgressUiCount = -1;
    private DispatcherTimer? _backupProgressUiTimer;
    private bool _backupProgressUiDirty;
    private const double PrinterListItemHeight = 22;
    private const int PrinterListMaxVisible = 5;
    private const double DefaultWindowHeight = 600;
    private const double MaxWindowHeightFactor = 2.5;

    public MainWindow(AppHost host)
    {
        _host = host;
        _loading = true;
        InitializeComponent();
        // 启动即托盘静默：不进任务栏、不显示面板。
        ShowInTaskbar = false;
        Visibility = Visibility.Hidden;
        WindowState = WindowState.Normal;
        CmbAppProcess.AddHandler(
            System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(CmbAppProcess_TextChanged));
        CmbBackupSrcPath.AddHandler(
            System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(BackupPathText_Changed));
        CmbBackupDstPath.AddHandler(
            System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(BackupPathText_Changed));
        _logFollowTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(300)
        };
        _logFollowTimer.Tick += (_, _) => RestoreLogAutoFollow();
        MaxHeight = DefaultWindowHeight * MaxWindowHeightFactor;
        MinHeight = DefaultWindowHeight;
        ApplyConfigToUi(_host.Config);
        UpdateConditionalRows();
        RefreshPrinterList();
        RefreshUrlList();
        RefreshBackupList();
        RefreshAppSwitchList();
        FillBackupEndpointCombos();
        ApplyTabEdgeMargins();
        _loading = false;
        _host.StatusChanged += OnStatusChanged;
        _backupFeature = _host.Features.OfType<BackupFeature>().FirstOrDefault();
        if (_backupFeature is not null)
            _backupFeature.ProgressChanged += OnBackupProgressChanged;
        _backupProgressUiTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _backupProgressUiTimer.Tick += (_, _) =>
        {
            if (!_backupProgressUiDirty) return;
            _backupProgressUiDirty = false;
            RefreshBackupProgressList();
        };
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
                RefreshBackupProgressList(forceFit: true);
        };
        MainTabs.SelectionChanged += (_, _) => FitWindowToContent();
        Loaded += (_, _) =>
        {
            UiLocalizer.Apply(this);
            ApplyTabEdgeMargins();
            FitWindowToContent();
            RefreshBackupProgressList();
        };
        OnStatusChanged();
        ReloadLogView();
    }

    public void AppendLog(string line)
    {
        if (!IsWithinLogDisplayRange(line))
            return;
        if (!FileLogger.MatchesFilter(line, _logFilter))
            return;

        InsertLogLine(line);
        if (_logAutoFollow)
            ScrollLogToBottom();
    }

    public void ReloadLogView()
    {
        var lines = _host.FileLog?.ReadForDisplay(
            _logFilter,
            since: GetLogDisplayStart()) ?? Array.Empty<string>();
        LogBox.Text = lines.Count == 0
            ? ""
            : string.Join(Environment.NewLine, lines) + Environment.NewLine + LogBottomPad;
        if (_logAutoFollow)
            ScrollLogToBottom();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        _logClearedAt = DateTime.Now;
        LogBox.Clear();
    }

    /// <summary>在末尾留白之前插入新行，避免空行变成日志中间的空隙。</summary>
    private void InsertLogLine(string line)
    {
        var entry = line + Environment.NewLine;
        var text = LogBox.Text ?? "";
        if (text.EndsWith(LogBottomPad, StringComparison.Ordinal))
        {
            var insertAt = text.Length - LogBottomPad.Length;
            LogBox.Select(insertAt, 0);
            LogBox.SelectedText = entry;
            LogBox.Select(0, 0);
        }
        else if (string.IsNullOrEmpty(text))
        {
            LogBox.Text = entry + LogBottomPad;
        }
        else
        {
            LogBox.Text = text + entry + LogBottomPad;
        }
    }

    private void BtnOpenLogFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(AppPaths.LogPath))
                File.WriteAllText(AppPaths.LogPath, string.Empty);

            Process.Start(new ProcessStartInfo
            {
                FileName = AppPaths.LogPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _host.Log.Error(Loc.T("Log.Config.OpenLogFailed", ex.Message));
            System.Windows.MessageBox.Show(
                Loc.T("Msg.OpenLogFailed", ex.Message),
                AppBranding.Name,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void BtnLogLevel_Click(object sender, RoutedEventArgs e)
    {
        _logFilter = _logFilter == LogViewFilter.Normal
            ? LogViewFilter.Error
            : LogViewFilter.Normal;
        BtnLogLevel.Content = _logFilter == LogViewFilter.Normal
            ? "运行日志 · 普通"
            : "运行日志 · 错误";
        BtnLogLevel.IsChecked = true;
        ReloadLogView();
    }

    private DateTime GetLogDisplayStart()
    {
        var last24Hours = DateTime.Now.AddHours(-24);
        return _logClearedAt is { } clearedAt && clearedAt > last24Hours
            ? clearedAt
            : last24Hours;
    }

    private bool IsWithinLogDisplayRange(string line)
    {
        return FileLogger.TryParseTimestamp(line, out var timestamp)
               && timestamp >= GetLogDisplayStart();
    }

    private void LogBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        PauseLogAutoFollow();
    }

    private void LogBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
            PauseLogAutoFollow();
    }

    private void LogBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        RestoreLogAutoFollow();
    }

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_logAutoFollow && !LogBox.IsMouseOver)
            RestoreLogAutoFollow();
    }

    private void PauseLogAutoFollow()
    {
        _logAutoFollow = false;
        _logFollowTimer.Stop();
        _logFollowTimer.Start();
    }

    private void RestoreLogAutoFollow()
    {
        _logFollowTimer.Stop();
        _logAutoFollow = true;
        ScrollLogToBottom();
    }

    private void ScrollLogToBottom()
    {
        LogBox.CaretIndex = LogBox.Text.Length;
        LogBox.ScrollToEnd();

        // 等布局完成后再贴底，确保最后一行与末尾空行都进入视口。
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (_logAutoFollow)
                LogBox.ScrollToEnd();
        });
    }

    public void RefreshHotkeyStatus(string display)
    {
        _hotkeyDisplay = string.IsNullOrWhiteSpace(display) ? "Ctrl+Alt+Shift+L" : display.Trim();
        OnStatusChanged();
    }

    /// <summary>模块开关变更后刷新主界面。</summary>
    public void RefreshFromHost()
    {
        _loading = true;
        ApplyConfigToUi(_host.Config);
        UpdateConditionalRows();
        RefreshPrinterList();
        RefreshUrlList();
        RefreshBackupList();
        RefreshAppSwitchList();
        _loading = false;
    }

    public void RestoreDefaults()
    {
        _loading = true;
        var defaults = new AppConfig();
        ApplyConfigToUi(defaults);
        _powerRowExpanded = false;
        _mediaRowExpanded = false;
        _browserRowExpanded = false;
        _appSwitchRowExpanded = false;
        _printerRowExpanded = true;
        _urlRowExpanded = false;
        _backupRowExpanded = false;
        UpdateConditionalRows();
        RefreshPrinterList();
        RefreshUrlList();
        RefreshBackupList();
        RefreshAppSwitchList();
        _loading = false;
        _host.ReloadConfig(defaults);
        _host.Log.Info("已恢复默认设置。");
    }

    private void ApplyConfigToUi(AppConfig c)
    {
        ChkWindow.IsChecked = c.Window.Enabled;
        ChkPower.IsChecked = c.Power.Enabled;
        ChkMedia.IsChecked = c.Media.Enabled;
        ChkBrowser.IsChecked = c.Browser.Enabled;
        ChkAppSwitchModule.IsChecked = c.AppSwitch.Enabled;
        ChkLock.IsChecked = c.Lock.Enabled;
        ChkLeaveGrace.IsChecked = c.Leave.Enabled;
        ChkRunLeaveOnManualLock.IsChecked = c.Leave.RunLeaveOnManualLock;
        ChkPrinterModule.IsChecked = c.Printer.Enabled;
        ChkUrlLauncherModule.IsChecked = c.UrlLauncher.Enabled;
        ChkBackupModule.IsChecked = c.Backup.Enabled;
        TxtHideAfter.Text = c.Window.HideAfterSeconds.ToString();
        TxtSaverAfter.Text = c.Power.SaverAfterSeconds.ToString();
        TxtMediaStop.Text = c.Media.StopAfterSeconds.ToString();
        TxtBrowserClose.Text = c.Browser.CloseAfterSeconds.ToString();
        TxtLockAfter.Text = c.Lock.LockAfterSeconds.ToString();
        TxtBrowserProcs.Text = c.Browser.ProcessNames;
        TxtReturnGrace.Text = c.Leave.ReturnGraceSeconds.ToString();
        TxtReturnGrace.IsEnabled = c.Leave.Enabled;
        if (!string.IsNullOrWhiteSpace(c.Leave.Hotkey))
            _hotkeyDisplay = c.Leave.Hotkey.Trim();

        foreach (ComboBoxItem item in CmbPowerMode.Items)
        {
            if (string.Equals(item.Tag?.ToString(), c.Power.Mode, StringComparison.OrdinalIgnoreCase))
            {
                CmbPowerMode.SelectedItem = item;
                break;
            }
        }

        var resumeTag = c.Media.AutoResume ? "True" : "False";
        foreach (ComboBoxItem item in CmbMediaResume.Items)
        {
            if (string.Equals(item.Tag?.ToString(), resumeTag, StringComparison.OrdinalIgnoreCase))
            {
                CmbMediaResume.SelectedItem = item;
                break;
            }
        }

        for (var i = 0; i < _mediaHotkeys.Length; i++)
            _mediaHotkeys[i] = MediaFeatureConfig.DefaultPlayPauseHotkeys[i];
        if (c.Media.PlayPauseHotkeys is { Count: > 0 })
        {
            for (var i = 0; i < _mediaHotkeys.Length; i++)
            {
                if (i >= c.Media.PlayPauseHotkeys.Count)
                {
                    _mediaHotkeys[i] = "";
                    continue;
                }
                var raw = c.Media.PlayPauseHotkeys[i]?.Trim() ?? "";
                if (raw.Length == 0)
                    _mediaHotkeys[i] = "";
                else if (HotkeySpec.TryParse(raw, out _, out _, out var display))
                    _mediaHotkeys[i] = display;
                else
                    _mediaHotkeys[i] = MediaFeatureConfig.DefaultPlayPauseHotkeys[i];
            }
        }
        _mediaHotkeysExpanded = false;
        RefreshMediaHotkeyLabels();
        UpdateMediaHotkeySlotsVisibility();

        ApplyModuleBlocks(c.Modules);
    }

    private void ApplyModuleBlocks(ModulesConfig m)
    {
        BlockWindow.Visibility = m.Window ? Visibility.Visible : Visibility.Collapsed;
        BlockPower.Visibility = m.Power ? Visibility.Visible : Visibility.Collapsed;
        BlockMedia.Visibility = m.Media ? Visibility.Visible : Visibility.Collapsed;
        BlockBrowser.Visibility = m.Browser ? Visibility.Visible : Visibility.Collapsed;
        BlockAppSwitch.Visibility = m.AppSwitch ? Visibility.Visible : Visibility.Collapsed;
        BlockLock.Visibility = m.Lock ? Visibility.Visible : Visibility.Collapsed;
        BlockLeaveGrace.Visibility = m.LeaveGrace ? Visibility.Visible : Visibility.Collapsed;
        BlockPrinter.Visibility = m.Printer ? Visibility.Visible : Visibility.Collapsed;
        BlockUrlLauncher.Visibility = m.UrlLauncher ? Visibility.Visible : Visibility.Collapsed;
        BlockBackup.Visibility = m.Backup ? Visibility.Visible : Visibility.Collapsed;
        TabSchedule.Visibility = (m.Printer || m.UrlLauncher || m.Backup) ? Visibility.Visible : Visibility.Collapsed;
        if (!m.Printer && !m.UrlLauncher && !m.Backup && MainTabs.SelectedItem == TabSchedule)
            MainTabs.SelectedItem = TabLeave;
        ApplyTabEdgeMargins();
    }

    /// <summary>按钮行：中间留缝，首尾与两侧齐平。</summary>
    private static void ApplyEdgeGaps(IReadOnlyList<FrameworkElement> items, double gap = 6)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var left = i == 0 ? 0 : gap / 2;
            var right = i == items.Count - 1 ? 0 : gap / 2;
            items[i].Margin = new Thickness(left, 0, right, 0);
        }
    }

    private void ApplyTabEdgeMargins()
    {
        var tabs = MainTabs.Items.OfType<TabItem>()
            .Where(t => t.Visibility == Visibility.Visible)
            .ToList();
        ApplyEdgeGaps(tabs);
        ApplyPrinterActionGaps();
    }

    private void ApplyPrinterActionGaps()
    {
        if (PanelPrinterActions is null) return;
        var buttons = PanelPrinterActions.Children
            .OfType<System.Windows.Controls.Button>()
            .OrderBy(Grid.GetColumn)
            .Cast<FrameworkElement>()
            .ToList();
        ApplyEdgeGaps(buttons);

        if (PanelBackupActions is not null)
        {
            var backupBtns = PanelBackupActions.Children
                .OfType<System.Windows.Controls.Button>()
                .OrderBy(Grid.GetColumn)
                .Cast<FrameworkElement>()
                .ToList();
            ApplyEdgeGaps(backupBtns);
        }

        if (PanelUrlActions is not null)
        {
            var urlBtns = PanelUrlActions.Children
                .OfType<System.Windows.Controls.Button>()
                .OrderBy(Grid.GetColumn)
                .Cast<FrameworkElement>()
                .ToList();
            ApplyEdgeGaps(urlBtns);
        }

        if (PanelAppSwitchActions is null) return;
        var appBtns = PanelAppSwitchActions.Children
            .OfType<System.Windows.Controls.Button>()
            .OrderBy(Grid.GetColumn)
            .Cast<FrameworkElement>()
            .ToList();
        ApplyEdgeGaps(appBtns);
    }

    private void UpdateConditionalRows()
    {
        ApplyModuleBlocks(_host.Config.Modules);

        RowPowerMode.Visibility = BlockPower.Visibility == Visibility.Visible
                                  && ChkPower.IsChecked == true
                                  && _powerRowExpanded
            ? Visibility.Visible : Visibility.Collapsed;
        var mediaDetail = BlockMedia.Visibility == Visibility.Visible
                          && ChkMedia.IsChecked == true
                          && _mediaRowExpanded;
        RowMediaResume.Visibility = mediaDetail ? Visibility.Visible : Visibility.Collapsed;
        RowMediaHotkeys.Visibility = mediaDetail ? Visibility.Visible : Visibility.Collapsed;
        if (!mediaDetail)
            _mediaHotkeysExpanded = false;
        UpdateMediaHotkeySlotsVisibility();
        RowBrowserProcs.Visibility = BlockBrowser.Visibility == Visibility.Visible
                                     && ChkBrowser.IsChecked == true
                                     && _browserRowExpanded
            ? Visibility.Visible : Visibility.Collapsed;

        var appSwitchOn = BlockAppSwitch.Visibility == Visibility.Visible && ChkAppSwitchModule.IsChecked == true;
        PanelAppSwitchBody.Visibility = appSwitchOn && _appSwitchRowExpanded
            ? Visibility.Visible : Visibility.Collapsed;
        BtnAppSwitchSave.Visibility = appSwitchOn && _appSwitchRowExpanded
            ? Visibility.Visible : Visibility.Collapsed;

        // 取消勾选打印机维护 → 隐藏下面整块；勾选但折叠标题 → 同样隐藏
        var printerOn = BlockPrinter.Visibility == Visibility.Visible && ChkPrinterModule.IsChecked == true;
        PanelPrinterBody.Visibility = printerOn && _printerRowExpanded
            ? Visibility.Visible : Visibility.Collapsed;
        BtnPrinterSave.Visibility = printerOn && _printerRowExpanded
            ? Visibility.Visible : Visibility.Collapsed;

        var urlOn = BlockUrlLauncher.Visibility == Visibility.Visible && ChkUrlLauncherModule.IsChecked == true;
        PanelUrlBody.Visibility = urlOn && _urlRowExpanded
            ? Visibility.Visible : Visibility.Collapsed;
        BtnUrlSave.Visibility = urlOn && _urlRowExpanded
            ? Visibility.Visible : Visibility.Collapsed;

        var backupOn = BlockBackup.Visibility == Visibility.Visible && ChkBackupModule.IsChecked == true;
        PanelBackupBody.Visibility = backupOn && _backupRowExpanded
            ? Visibility.Visible : Visibility.Collapsed;
        BtnBackupSave.Visibility = backupOn && _backupRowExpanded
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateBackupModeRows();
        FitWindowToContent();
    }

    /// <summary>
    /// 任务/模块展开后按内容撑高窗口；不低于默认高度，不超过默认的 2.5 倍，超出则滚动。
    /// </summary>
    private void FitWindowToContent()
    {
        // 托盘隐藏时绝不改尺寸/触发布局，避免计划备份把面板顶到前台。
        if (!IsLoaded || !IsVisible) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (!IsVisible) return;
            var scroll = MainTabs.SelectedItem == TabSchedule ? ScrollSchedule : ScrollLeave;
            if (scroll.Content is not FrameworkElement content) return;

            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

            UpdateLayout();
            var width = scroll.ActualWidth;
            if (width <= 0) width = Math.Max(100, ActualWidth - 40);

            content.Measure(new System.Windows.Size(width, double.PositiveInfinity));
            var contentNeed = content.DesiredSize.Height + scroll.Padding.Top + scroll.Padding.Bottom;

            UpdateLayout();
            var viewport = scroll.ViewportHeight > 0 ? scroll.ViewportHeight : scroll.ActualHeight;
            if (viewport <= 0) return;

            var overflow = contentNeed - viewport;
            var maxH = DefaultWindowHeight * MaxWindowHeightFactor;

            if (overflow > 1)
            {
                Height = Math.Min(ActualHeight + overflow, maxH);
                UpdateLayout();
                content.Measure(new System.Windows.Size(Math.Max(100, scroll.ActualWidth), double.PositiveInfinity));
                contentNeed = content.DesiredSize.Height + scroll.Padding.Top + scroll.Padding.Bottom;
                viewport = scroll.ViewportHeight > 0 ? scroll.ViewportHeight : scroll.ActualHeight;
                if (contentNeed > viewport + 1)
                    scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            else if (overflow < -1 && ActualHeight > DefaultWindowHeight + 0.5)
            {
                Height = Math.Max(DefaultWindowHeight, ActualHeight + overflow);
                scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void RefreshMediaHotkeyLabels()
    {
        TxtMediaHotkey1.Text = string.IsNullOrWhiteSpace(_mediaHotkeys[0]) ? "（空）" : _mediaHotkeys[0];
        TxtMediaHotkey2.Text = string.IsNullOrWhiteSpace(_mediaHotkeys[1]) ? "（空）" : _mediaHotkeys[1];
        TxtMediaHotkey3.Text = string.IsNullOrWhiteSpace(_mediaHotkeys[2]) ? "（空）" : _mediaHotkeys[2];
    }

    private void UpdateMediaHotkeySlotsVisibility()
    {
        if (PanelMediaHotkeySlots is null) return;
        PanelMediaHotkeySlots.Visibility = _mediaHotkeysExpanded && RowMediaHotkeys.Visibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void LblMediaHotkeys_Click(object sender, MouseButtonEventArgs e)
    {
        // 避免「失焦先折叠 → 点击再 toggle 又展开」，导致只能展开不能收起。
        _mediaHotkeysToggleGuard = true;
        _mediaHotkeysExpanded = !_mediaHotkeysExpanded;
        UpdateMediaHotkeySlotsVisibility();
        if (_mediaHotkeysExpanded)
            RowMediaHotkeys.Focus();
        e.Handled = true;
        Dispatcher.BeginInvoke(() => _mediaHotkeysToggleGuard = false, DispatcherPriority.Input);
    }

    private void RowMediaHotkeys_LostFocus(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_mediaHotkeyDialogOpen || _mediaHotkeysToggleGuard) return;
            if (RowMediaHotkeys.IsKeyboardFocusWithin) return;
            // 鼠标仍在本块（点标题折叠/展开）时不要当失焦处理
            if (RowMediaHotkeys.IsMouseOver) return;
            if (!_mediaHotkeysExpanded) return;
            _mediaHotkeysExpanded = false;
            UpdateMediaHotkeySlotsVisibility();
        }, DispatcherPriority.Input);
    }

    private void BtnMediaHotkey1_Click(object sender, RoutedEventArgs e) => EditMediaHotkey(0);
    private void BtnMediaHotkey2_Click(object sender, RoutedEventArgs e) => EditMediaHotkey(1);
    private void BtnMediaHotkey3_Click(object sender, RoutedEventArgs e) => EditMediaHotkey(2);

    private void EditMediaHotkey(int index)
    {
        if (index is < 0 or > 2) return;
        _mediaHotkeyDialogOpen = true;
        try
        {
            var dlg = new HotkeyEditWindow(
                _mediaHotkeys[index],
                title: $"播放/暂停快捷键 {index + 1}",
                allowClear: true)
            {
                Owner = this
            };
            if (dlg.ShowDialog() != true) return;

            var spec = dlg.HotkeySpec?.Trim() ?? "";
            if (spec.Length == 0)
            {
                _mediaHotkeys[index] = "";
            }
            else if (HotkeySpec.TryParse(spec, out _, out _, out var display))
            {
                _mediaHotkeys[index] = display;
            }
            else
            {
                System.Windows.MessageBox.Show(Loc.T("Msg.HotkeyInvalid"), AppBranding.Name,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RefreshMediaHotkeyLabels();
            if (!_loading)
                CommitSettings();
        }
        finally
        {
            _mediaHotkeyDialogOpen = false;
            if (_mediaHotkeysExpanded)
                RowMediaHotkeys.Focus();
        }
    }

    private void MainRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string tag) return;

        // 同组内互斥：再点已展开的则折叠
        if (tag is "power" or "media" or "browser")
        {
            if (tag == "power" && ChkPower.IsChecked != true) return;
            if (tag == "media" && ChkMedia.IsChecked != true) return;
            if (tag == "browser" && ChkBrowser.IsChecked != true) return;

            var already = tag switch
            {
                "power" => _powerRowExpanded,
                "media" => _mediaRowExpanded,
                _ => _browserRowExpanded
            };
            _powerRowExpanded = !already && tag == "power";
            _mediaRowExpanded = !already && tag == "media";
            _browserRowExpanded = !already && tag == "browser";
        }
        else if (tag is "printer" or "url-launcher" or "backup")
        {
            if (tag == "printer" && ChkPrinterModule.IsChecked != true) return;
            if (tag == "url-launcher" && ChkUrlLauncherModule.IsChecked != true) return;
            if (tag == "backup" && ChkBackupModule.IsChecked != true) return;

            var already = tag switch
            {
                "printer" => _printerRowExpanded,
                "url-launcher" => _urlRowExpanded,
                _ => _backupRowExpanded
            };
            _printerRowExpanded = !already && tag == "printer";
            _urlRowExpanded = !already && tag == "url-launcher";
            _backupRowExpanded = !already && tag == "backup";
        }
        else if (tag == "appswitch")
        {
            if (ChkAppSwitchModule.IsChecked != true) return;
            _appSwitchRowExpanded = !_appSwitchRowExpanded;
        }
        else return;

        UpdateConditionalRows();
        e.Handled = true;
    }

    private void FeatureCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender == ChkPower && ChkPower.IsChecked == true)
        {
            _powerRowExpanded = true;
            _mediaRowExpanded = false;
            _browserRowExpanded = false;
        }
        if (sender == ChkMedia && ChkMedia.IsChecked == true)
        {
            _mediaRowExpanded = true;
            _powerRowExpanded = false;
            _browserRowExpanded = false;
        }
        if (sender == ChkBrowser && ChkBrowser.IsChecked == true)
        {
            _browserRowExpanded = true;
            _powerRowExpanded = false;
            _mediaRowExpanded = false;
        }
        if (sender == ChkLeaveGrace)
            TxtReturnGrace.IsEnabled = ChkLeaveGrace.IsChecked == true;
        UpdateConditionalRows();
        CommitSettings();
    }

    private void Setting_LostFocus(object sender, RoutedEventArgs e) => CommitSettings();

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        CommitSettings();
    }

    private void CommitSettings()
    {
        if (_loading || _host is null) return;
        if (!TryBuildConfig(out var cfg, out var lockAdjustedTo))
        {
            _loading = true;
            ApplyConfigToUi(_host.Config);
            UpdateConditionalRows();
            _loading = false;
            return;
        }

        if (lockAdjustedTo is int adjusted)
        {
            _loading = true;
            TxtLockAfter.Text = adjusted.ToString();
            _loading = false;
            _host.Log.Info(Loc.T("Log.Ui.LockSecondsAdjusted", adjusted));
        }

        _host.ReloadConfig(cfg);
    }

    private bool TryBuildConfig(out AppConfig config) => TryBuildConfig(out config, out _);

    private bool TryBuildConfig(out AppConfig config, out int? lockAdjustedTo)
    {
        lockAdjustedTo = null;
        config = CloneConfig(_host.Config);
        var m = config.Modules;

        if (m.Window)
        {
            if (!int.TryParse(TxtHideAfter.Text, out var hide) || hide < 0) return false;
            config.Window.Enabled = ChkWindow.IsChecked == true;
            config.Window.HideAfterSeconds = hide;
        }
        else
        {
            config.Window.Enabled = false;
        }

        if (m.Power)
        {
            if (!int.TryParse(TxtSaverAfter.Text, out var saver) || saver < 0) return false;
            config.Power.Enabled = ChkPower.IsChecked == true;
            config.Power.SaverAfterSeconds = saver;
            config.Power.Mode = (CmbPowerMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Performance";
        }
        else
        {
            config.Power.Enabled = false;
        }

        if (m.Media)
        {
            if (!int.TryParse(TxtMediaStop.Text, out var mediaStop) || mediaStop < 0) return false;
            var autoResume = true;
            if (CmbMediaResume.SelectedItem is ComboBoxItem resumeItem
                && bool.TryParse(resumeItem.Tag?.ToString(), out var parsed))
            {
                autoResume = parsed;
            }
            config.Media.Enabled = ChkMedia.IsChecked == true;
            config.Media.StopAfterSeconds = mediaStop;
            config.Media.AutoResume = autoResume;
            config.Media.PlayPauseHotkeys = new List<string>
            {
                _mediaHotkeys[0] ?? "",
                _mediaHotkeys[1] ?? "",
                _mediaHotkeys[2] ?? ""
            };
        }
        else
        {
            config.Media.Enabled = false;
        }

        if (m.Browser)
        {
            if (!int.TryParse(TxtBrowserClose.Text, out var browserClose) || browserClose < 0) return false;
            config.Browser.Enabled = ChkBrowser.IsChecked == true;
            config.Browser.CloseAfterSeconds = browserClose;
            config.Browser.ProcessNames = string.IsNullOrWhiteSpace(TxtBrowserProcs.Text)
                ? "chrome,msedge,firefox"
                : TxtBrowserProcs.Text.Trim();
        }
        else
        {
            config.Browser.Enabled = false;
        }

        if (m.Lock)
        {
            if (!int.TryParse(TxtLockAfter.Text, out var lockAfter) || lockAfter < 0) return false;
            config.Lock.Enabled = ChkLock.IsChecked == true;
            config.Lock.LockAfterSeconds = lockAfter;
        }
        else
        {
            config.Lock.Enabled = false;
        }

        config.Leave.RunLeaveOnManualLock = ChkRunLeaveOnManualLock.IsChecked == true;

        if (m.LeaveGrace)
        {
            if (!int.TryParse(TxtReturnGrace.Text, out var grace) || grace < 1) return false;
            config.Leave.Enabled = ChkLeaveGrace.IsChecked == true;
            config.Leave.ReturnGraceSeconds = grace;
        }
        else
        {
            config.Leave.Enabled = false;
        }

        if (m.AppSwitch)
            config.AppSwitch.Enabled = ChkAppSwitchModule.IsChecked == true;
        else
            config.AppSwitch.Enabled = false;

        if (m.Printer)
            config.Printer.Enabled = ChkPrinterModule.IsChecked == true;
        else
            config.Printer.Enabled = false;

        if (m.UrlLauncher)
            config.UrlLauncher.Enabled = ChkUrlLauncherModule.IsChecked == true;
        else
            config.UrlLauncher.Enabled = false;

        if (m.Backup)
            config.Backup.Enabled = ChkBackupModule.IsChecked == true;
        else
            config.Backup.Enabled = false;

        // 自动锁屏偏后执行：不足时自动抬高到其它已启用离开任务的最大值，不弹窗拦截。
        if (config.Lock.Enabled)
        {
            var minRequired = GetMinLockAfterSeconds(config);
            if (config.Lock.LockAfterSeconds < minRequired)
            {
                config.Lock.LockAfterSeconds = minRequired;
                lockAdjustedTo = minRequired;
            }
        }

        return true;
    }

    /// <summary>自动锁屏秒数下限：其它已启用且空闲触发的离开任务最大值。</summary>
    private static int GetMinLockAfterSeconds(AppConfig config)
    {
        var max = 0;
        if (config.Window.Enabled && config.Window.HideAfterSeconds > 0)
            max = Math.Max(max, config.Window.HideAfterSeconds);
        if (config.Power.Enabled
            && !string.Equals(config.Power.Mode, "Manual", StringComparison.OrdinalIgnoreCase)
            && config.Power.SaverAfterSeconds > 0)
            max = Math.Max(max, config.Power.SaverAfterSeconds);
        if (config.Media.Enabled && config.Media.StopAfterSeconds > 0)
            max = Math.Max(max, config.Media.StopAfterSeconds);
        if (config.Browser.Enabled && config.Browser.CloseAfterSeconds > 0)
            max = Math.Max(max, config.Browser.CloseAfterSeconds);
        if (config.AppSwitch.Enabled)
        {
            foreach (var task in config.AppSwitch.Tasks)
            {
                if (task.Enabled && task.StopAfterSeconds > 0)
                    max = Math.Max(max, task.StopAfterSeconds);
            }
        }
        return max;
    }

    private static AppConfig CloneConfig(AppConfig src)
    {
        var json = JsonSerializer.Serialize(src);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    #region Printer UI

    private void RefreshPrinterList()
    {
        _printerUiLoading = true;
        var tasks = _host.Config.Printer.Tasks;
        LstPrinterTasks.Items.Clear();
        foreach (var t in tasks)
        {
            var due = t.NextDueLocal?.ToString("yyyy-MM-dd HH:mm") ?? "未排期";
            var mark = t.Enabled ? "" : "[停] ";
            LstPrinterTasks.Items.Add(new TaskListItem(t.Id, $"{mark}{t.Name} · {due}"));
        }

        UpdatePrinterListHeight(tasks.Count);

        PanelPrinterEdit.IsEnabled = false;
        if (!string.IsNullOrEmpty(_selectedTaskId))
        {
            for (var i = 0; i < LstPrinterTasks.Items.Count; i++)
            {
                if (LstPrinterTasks.Items[i] is TaskListItem item && item.Id == _selectedTaskId)
                {
                    LstPrinterTasks.SelectedIndex = i;
                    break;
                }
            }
        }

        if (LstPrinterTasks.SelectedItem is TaskListItem selected)
            LoadTaskEditor(selected.Id);
        else
            ClearTaskEditor();

        UpdatePauseButton();
        UpdateConditionalRows();
        _printerUiLoading = false;
    }

    private void UpdatePrinterListHeight(int count)
    {
        // 按条数刚好包住，不留大块空白；>5 才滚动
        var n = Math.Max(1, Math.Min(count <= 0 ? 1 : count, PrinterListMaxVisible));
        LstPrinterTasks.Height = n * PrinterListItemHeight + 2;
        ScrollViewer.SetVerticalScrollBarVisibility(
            LstPrinterTasks,
            count > PrinterListMaxVisible ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
    }

    private void LoadTaskEditor(string taskId)
    {
        var task = _host.Config.Printer.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null)
        {
            ClearTaskEditor();
            return;
        }

        _printerUiLoading = true;
        _selectedTaskId = task.Id;
        PanelPrinterEdit.IsEnabled = true;

        TxtPrinterName.Text = task.Name;
        TxtImagePath.Text = string.IsNullOrWhiteSpace(task.ImagePath)
            ? AppPaths.DefaultPrinterTestImagePath
            : task.ImagePath;
        TxtIntervalDays.Text = Math.Max(1, task.IntervalDays).ToString();
        TxtHour.Text = task.Hour.ToString();
        TxtMinute.Text = task.Minute.ToString("D2");
        TxtNextDue.Text = task.NextDueLocal?.ToString("yyyy-MM-dd HH:mm") ?? "—";
        TxtLastRun.Text = task.LastRunLocal?.ToString("yyyy-MM-dd HH:mm") ?? "—";

        CmbPrinter.Items.Clear();
        foreach (var name in PrinterService.GetInstalledPrinters())
            CmbPrinter.Items.Add(name);
        if (!string.IsNullOrWhiteSpace(task.PrinterName)
            && !CmbPrinter.Items.Cast<object>().Any(x => string.Equals(x.ToString(), task.PrinterName, StringComparison.OrdinalIgnoreCase)))
        {
            CmbPrinter.Items.Add(task.PrinterName);
        }
        CmbPrinter.SelectedItem = CmbPrinter.Items.Cast<object>()
            .FirstOrDefault(x => string.Equals(x.ToString(), task.PrinterName, StringComparison.OrdinalIgnoreCase));

        var grayscale = string.Equals(task.ColorMode, "Grayscale", StringComparison.OrdinalIgnoreCase);
        BtnColorMode.Tag = grayscale ? "Grayscale" : "Color";
        BtnColorMode.Content = grayscale ? "灰度" : "彩色";

        UpdatePauseButton();
        if (_printerDrafts.TryGetValue(task.Id, out var draft))
            ApplyPrinterDraft(draft);
        else
            SetPrinterDirty(false);
        _printerUiLoading = false;
    }

    private void ClearTaskEditor()
    {
        _selectedTaskId = null;
        PanelPrinterEdit.IsEnabled = false;
        SetPrinterDirty(false);
        TxtImagePath.Text = AppPaths.DefaultPrinterTestImagePath;
        TxtIntervalDays.Text = "7";
        TxtHour.Text = "6";
        TxtMinute.Text = "00";
        TxtNextDue.Text = "—";
        TxtLastRun.Text = "—";
        UpdatePauseButton();
    }

    private void UpdatePauseButton()
    {
        var task = string.IsNullOrEmpty(_selectedTaskId)
            ? null
            : _host.Config.Printer.Tasks.FirstOrDefault(t => t.Id == _selectedTaskId);
        BtnPrinterPause.IsEnabled = task is not null;
        BtnPrinterPause.Content = task is { Enabled: false } ? "恢复" : "暂停";
    }

    private static string NormalizeImagePath(string? path)
    {
        var trimmed = path?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmed))
            return "";
        if (string.Equals(trimmed, AppPaths.DefaultPrinterTestImagePath, StringComparison.OrdinalIgnoreCase))
            return "";
        return trimmed;
    }

    private void LstPrinterTasks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_printerUiLoading) return;
        if (!string.IsNullOrEmpty(_selectedTaskId)
            && LstPrinterTasks.SelectedItem is TaskListItem next
            && next.Id != _selectedTaskId)
            StashPrinterDraftIfNeeded();

        if (LstPrinterTasks.SelectedItem is TaskListItem selectedItem)
            LoadTaskEditor(selectedItem.Id);
        else
            ClearTaskEditor();
    }

    private void PrinterModule_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (ChkPrinterModule.IsChecked != true)
            StashPrinterDraftIfNeeded();
        if (ChkPrinterModule.IsChecked == true)
        {
            _printerRowExpanded = true;
            _urlRowExpanded = false;
            _backupRowExpanded = false;
        }
        UpdateConditionalRows();
        CommitSettings();
    }

    private void PrinterDraft_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || _printerUiLoading) return;
        SetPrinterDirty(true);
    }

    private void BtnColorMode_Click(object sender, RoutedEventArgs e)
    {
        if (_loading || _printerUiLoading) return;
        var grayscale = string.Equals(BtnColorMode.Tag?.ToString(), "Grayscale", StringComparison.OrdinalIgnoreCase);
        BtnColorMode.Tag = grayscale ? "Color" : "Grayscale";
        BtnColorMode.Content = grayscale ? "彩色" : "灰度";
        SetPrinterDirty(true);
    }

    private void SetPrinterDirty(bool dirty)
    {
        _printerDirty = dirty;
        BtnPrinterSave.IsEnabled = dirty && !string.IsNullOrEmpty(_selectedTaskId);
    }

    private void RestorePrinterSelection()
    {
        _printerUiLoading = true;
        LstPrinterTasks.SelectedItem = LstPrinterTasks.Items.OfType<TaskListItem>()
            .FirstOrDefault(x => x.Id == _selectedTaskId);
        _printerUiLoading = false;
    }

    private void CommitPrinterTask()
    {
        if (string.IsNullOrEmpty(_selectedTaskId)) return;

        var cfg = CloneConfig(_host.Config);
        var task = cfg.Printer.Tasks.FirstOrDefault(t => t.Id == _selectedTaskId);
        if (task is null) return;

        if (!int.TryParse(TxtIntervalDays.Text, out var days) || days < 1) return;
        if (!int.TryParse(TxtHour.Text, out var hour) || hour < 0 || hour > 23) return;
        if (!int.TryParse(TxtMinute.Text, out var minute) || minute < 0 || minute > 59) return;

        var scheduleChanged = task.IntervalDays != days || task.Hour != hour || task.Minute != minute;

        task.Name = TxtPrinterName.Text.Trim();
        task.PrinterName = CmbPrinter.SelectedItem?.ToString() ?? "";
        task.ImagePath = NormalizeImagePath(TxtImagePath.Text);
        task.ColorMode = BtnColorMode.Tag?.ToString() ?? "Color";
        task.IntervalDays = days;
        task.Hour = hour;
        task.Minute = minute;

        if (scheduleChanged || task.NextDueLocal is null)
        {
            task.NextDueLocal = IntervalSchedule.ComputeInitialNextDue(days, hour, minute, DateTime.Now);
        }

        _host.ReloadConfig(cfg);
        RefreshPrinterList();
    }

    private void BtnPrinterSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedTaskId)) return;
        if (string.IsNullOrWhiteSpace(TxtPrinterName.Text)
            || CmbPrinter.SelectedItem is null
            || !int.TryParse(TxtIntervalDays.Text, out var days) || days < 1
            || !int.TryParse(TxtHour.Text, out var hour) || hour is < 0 or > 23
            || !int.TryParse(TxtMinute.Text, out var minute) || minute is < 0 or > 59)
        {
            System.Windows.MessageBox.Show(Loc.T("Msg.PrinterValidation"), AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var imagePath = NormalizeImagePath(TxtImagePath.Text);
        if (!string.IsNullOrEmpty(imagePath) && !System.IO.File.Exists(imagePath))
        {
            System.Windows.MessageBox.Show(Loc.T("Msg.ImageNotFound"), AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        CommitPrinterTask();
        if (!string.IsNullOrEmpty(_selectedTaskId))
            _printerDrafts.Remove(_selectedTaskId);
        SetPrinterDirty(false);
        _host.Log.Info(Loc.T("Log.Ui.PrinterTaskSaved", TxtPrinterName.Text.Trim()));
    }

    private void BtnPrinterAdd_Click(object sender, RoutedEventArgs e)
    {
        StashPrinterDraftIfNeeded();
        var cfg = CloneConfig(_host.Config);
        var printers = PrinterService.GetInstalledPrinters();
        var task = new PrinterTaskConfig
        {
            Name = $"维护任务 {cfg.Printer.Tasks.Count + 1}",
            PrinterName = printers.FirstOrDefault() ?? "",
            IntervalDays = 7,
            Hour = 6,
            Minute = 0,
            ColorMode = "Color",
            Enabled = true,
            NextDueLocal = IntervalSchedule.ComputeInitialNextDue(7, 6, 0, DateTime.Now)
        };
        cfg.Printer.Tasks.Add(task);
        cfg.Printer.Enabled = true;
        _selectedTaskId = task.Id;
        _host.ReloadConfig(cfg);
        RefreshPrinterList();
    }

    private void BtnPrinterDelete_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedTaskId)) return;
        var id = _selectedTaskId;
        var cfg = CloneConfig(_host.Config);
        cfg.Printer.Tasks.RemoveAll(t => t.Id == id);
        _selectedTaskId = null;
        _printerDrafts.Remove(id);
        _host.ReloadConfig(cfg);
        RefreshPrinterList();
    }

    private void BtnPrinterPause_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedTaskId)) return;
        StashPrinterDraftIfNeeded();
        var cfg = CloneConfig(_host.Config);
        var task = cfg.Printer.Tasks.FirstOrDefault(t => t.Id == _selectedTaskId);
        if (task is null) return;

        task.Enabled = !task.Enabled;
        _host.ReloadConfig(cfg);
        RefreshPrinterList();
        _host.Log.Info(task.Enabled
            ? $"打印机「{task.Name}」：已恢复"
            : $"打印机「{task.Name}」：已暂停");
    }

    private void MenuRenameTask_Click(object sender, RoutedEventArgs e)
    {
        if (LstPrinterTasks.SelectedItem is not TaskListItem selected)
            return;

        _selectedTaskId = selected.Id;
        var cfg = CloneConfig(_host.Config);
        var task = cfg.Printer.Tasks.FirstOrDefault(t => t.Id == _selectedTaskId);
        if (task is null) return;

        if (!TryPromptText("重命名任务", "名称：", task.Name, out var name))
            return;

        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        TxtPrinterName.Text = name;
        SetPrinterDirty(true);
    }

    private void LstPrinterTasks_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var src = e.OriginalSource as DependencyObject;
        while (src is not null && src is not ListBoxItem && src is not System.Windows.Controls.ListBox)
            src = System.Windows.Media.VisualTreeHelper.GetParent(src);

        if (src is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private bool TryPromptText(string title, string label, string initial, out string value)
    {
        value = initial;
        var dlg = new Window
        {
            Title = title,
            Width = 360,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = Background,
            FontFamily = FontFamily,
            FontSize = FontSize
        };

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6) });
        var box = new System.Windows.Controls.TextBox { Text = initial, Height = 23, Margin = new Thickness(0, 0, 0, 12) };
        Grid.SetRow(box, 1);
        root.Children.Add(box);

        var buttons = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        Grid.SetRow(buttons, 2);
        var ok = new System.Windows.Controls.Button { Content = "确定", Width = 72, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        var cancel = new System.Windows.Controls.Button { Content = "取消", Width = 72, IsCancel = true };
        var accepted = false;
        ok.Click += (_, _) => { accepted = true; dlg.Close(); };
        cancel.Click += (_, _) => dlg.Close();
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        dlg.Content = root;
        box.SelectAll();
        box.Focus();
        dlg.ShowDialog();
        if (!accepted) return false;
        value = box.Text ?? "";
        return true;
    }

    public bool TryPromptLogRetention(string current, out int days)
    {
        days = 90;
        if (!int.TryParse(current.Trim(), out var initial) || initial is < 1 or > 3650)
            initial = 90;

        var dlg = new LogRetentionWindow(initial)
        {
            Owner = this
        };
        if (dlg.ShowDialog() != true)
            return false;
        days = dlg.Days;
        return true;
    }

    private async void BtnPrinterPrintNow_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedTaskId)) return;
        StashPrinterDraftIfNeeded();

        var task = _host.Config.Printer.Tasks.FirstOrDefault(t => t.Id == _selectedTaskId);
        if (task is null) return;

        try
        {
            if (string.IsNullOrWhiteSpace(task.PrinterName))
            {
                System.Windows.MessageBox.Show(Loc.T("Msg.SelectPrinterFirst"), AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var path = PrinterService.ResolveImagePath(task.ImagePath);
            var color = !string.Equals(task.ColorMode, "Grayscale", StringComparison.OrdinalIgnoreCase);
            await PrinterService.PrintImageAsync(task.PrinterName, path, color);

            var cfg = CloneConfig(_host.Config);
            var t = cfg.Printer.Tasks.First(x => x.Id == task.Id);
            t.LastRunLocal = DateTime.Now;
            t.LastResult = "手动打印成功";
            _host.ReloadConfig(cfg);
            RefreshPrinterList();
            _host.Log.Info(Loc.T("Log.Ui.PrinterManualSent", task.Name, task.PrinterName));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, Loc.T("Msg.PrintFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
            _host.Log.Error(Loc.T("Log.Ui.PrinterManualFailed", ex.Message));
        }
    }

    private void BtnBrowseImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.T("Dialog.PickImage.Title"),
            Filter = Loc.T("Dialog.PickImage.Filter")
        };
        if (dlg.ShowDialog() != true) return;
        TxtImagePath.Text = dlg.FileName;
        SetPrinterDirty(true);
    }

    #endregion

    #region Url Launcher UI

    private void RefreshUrlList()
    {
        _urlUiLoading = true;
        var tasks = _host.Config.UrlLauncher.Tasks;
        LstUrlTasks.Items.Clear();
        foreach (var task in tasks)
        {
            var due = task.NextDueLocal?.ToString("yyyy-MM-dd HH:mm") ?? "未排期";
            var mark = task.Enabled ? "" : "[停] ";
            LstUrlTasks.Items.Add(new TaskListItem(task.Id, $"{mark}{task.Name} · {due}"));
        }

        UpdatePrinterListHeightFor(LstUrlTasks, tasks.Count);
        PanelUrlEdit.IsEnabled = false;
        if (!string.IsNullOrEmpty(_selectedUrlId))
        {
            for (var i = 0; i < LstUrlTasks.Items.Count; i++)
            {
                if (LstUrlTasks.Items[i] is TaskListItem item && item.Id == _selectedUrlId)
                {
                    LstUrlTasks.SelectedIndex = i;
                    break;
                }
            }
        }

        if (LstUrlTasks.SelectedItem is TaskListItem selected)
            LoadUrlEditor(selected.Id);
        else
            ClearUrlEditor();

        UpdateUrlPauseButton();
        UpdateConditionalRows();
        ApplyPrinterActionGaps();
        _urlUiLoading = false;
    }

    private void LoadUrlEditor(string taskId)
    {
        var task = _host.Config.UrlLauncher.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) { ClearUrlEditor(); return; }

        _urlUiLoading = true;
        _selectedUrlId = task.Id;
        PanelUrlEdit.IsEnabled = true;
        TxtUrlName.Text = task.Name;
        TxtUrlAddress.Text = task.Url;
        TxtUrlBrowserPath.Text = task.BrowserPath;
        SelectComboByTag(CmbUrlAutoClose, task.AutoCloseBrowser ? "True" : "False");
        TxtUrlCloseDelay.Text = Math.Max(0, task.CloseDelaySeconds).ToString();
        TxtUrlIntervalDays.Text = Math.Max(1, task.IntervalDays).ToString();
        TxtUrlHour.Text = task.Hour.ToString();
        TxtUrlMinute.Text = task.Minute.ToString("D2");
        TxtUrlNextDue.Text = task.NextDueLocal?.ToString("yyyy-MM-dd HH:mm") ?? "—";
        TxtUrlLastRun.Text = task.LastRunLocal?.ToString("yyyy-MM-dd HH:mm") ?? "—";
        UpdateUrlCloseControls();
        UpdateUrlPauseButton();
        if (_urlDrafts.TryGetValue(task.Id, out var draft))
            ApplyUrlDraft(draft);
        else
            SetUrlDirty(false);
        _urlUiLoading = false;
    }

    private void ClearUrlEditor()
    {
        var wasLoading = _urlUiLoading;
        _urlUiLoading = true;
        _selectedUrlId = null;
        PanelUrlEdit.IsEnabled = false;
        SetUrlDirty(false);
        TxtUrlName.Text = "";
        TxtUrlAddress.Text = "";
        TxtUrlBrowserPath.Text = "";
        TxtUrlCloseDelay.Text = "60";
        TxtUrlIntervalDays.Text = "1";
        TxtUrlHour.Text = "8";
        TxtUrlMinute.Text = "00";
        TxtUrlNextDue.Text = "—";
        TxtUrlLastRun.Text = "—";
        SelectComboByTag(CmbUrlAutoClose, "False");
        UpdateUrlCloseControls();
        UpdateUrlPauseButton();
        _urlUiLoading = wasLoading;
    }

    private void UpdateUrlPauseButton()
    {
        var task = string.IsNullOrEmpty(_selectedUrlId)
            ? null
            : _host.Config.UrlLauncher.Tasks.FirstOrDefault(t => t.Id == _selectedUrlId);
        BtnUrlPause.IsEnabled = task is not null;
        BtnUrlPause.Content = task is { Enabled: false } ? "恢复" : "暂停";
    }

    private void UpdateUrlCloseControls()
    {
        var autoClose = string.Equals(
            (CmbUrlAutoClose.SelectedItem as ComboBoxItem)?.Tag?.ToString(),
            "True",
            StringComparison.OrdinalIgnoreCase);
        TxtUrlCloseDelay.IsEnabled = autoClose;
    }

    private void LstUrlTasks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_urlUiLoading) return;
        if (!string.IsNullOrEmpty(_selectedUrlId)
            && LstUrlTasks.SelectedItem is TaskListItem next
            && next.Id != _selectedUrlId)
            StashUrlDraftIfNeeded();

        if (LstUrlTasks.SelectedItem is TaskListItem selected)
            LoadUrlEditor(selected.Id);
        else
            ClearUrlEditor();
    }

    private void LstUrlTasks_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var src = e.OriginalSource as DependencyObject;
        while (src is not null && src is not ListBoxItem && src is not System.Windows.Controls.ListBox)
            src = System.Windows.Media.VisualTreeHelper.GetParent(src);
        if (src is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void UrlLauncherModule_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (ChkUrlLauncherModule.IsChecked != true)
            StashUrlDraftIfNeeded();
        if (ChkUrlLauncherModule.IsChecked == true)
        {
            _urlRowExpanded = true;
            _printerRowExpanded = false;
            _backupRowExpanded = false;
        }
        UpdateConditionalRows();
        CommitSettings();
    }

    private void UrlDraft_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || _urlUiLoading) return;
        SetUrlDirty(true);
    }

    private void UrlAutoClose_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _urlUiLoading) return;
        UpdateUrlCloseControls();
        SetUrlDirty(true);
    }

    private void SetUrlDirty(bool dirty)
    {
        _urlDirty = dirty;
        BtnUrlSave.IsEnabled = dirty && !string.IsNullOrEmpty(_selectedUrlId);
    }

    private void RestoreUrlSelection()
    {
        _urlUiLoading = true;
        LstUrlTasks.SelectedItem = LstUrlTasks.Items.OfType<TaskListItem>()
            .FirstOrDefault(x => x.Id == _selectedUrlId);
        _urlUiLoading = false;
    }

    private bool ValidateUrlDraft(out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(TxtUrlName.Text))
            return Fail(Loc.T("Msg.TaskNameRequired"), out error);
        if (!Uri.TryCreate(TxtUrlAddress.Text.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Fail("请输入有效的 HTTP/HTTPS 链接。", out error);
        var browser = TxtUrlBrowserPath.Text.Trim();
        if (!string.IsNullOrEmpty(browser) && !File.Exists(browser))
            return Fail("浏览器程序不存在。", out error);
        var autoClose = string.Equals(
            (CmbUrlAutoClose.SelectedItem as ComboBoxItem)?.Tag?.ToString(),
            "True",
            StringComparison.OrdinalIgnoreCase);
        if (autoClose && (!int.TryParse(TxtUrlCloseDelay.Text, out var delay) || delay < 0))
            return Fail("关闭等待秒数必须为不小于 0 的整数。", out error);
        if (!int.TryParse(TxtUrlIntervalDays.Text, out var days) || days < 1
            || !int.TryParse(TxtUrlHour.Text, out var hour) || hour is < 0 or > 23
            || !int.TryParse(TxtUrlMinute.Text, out var minute) || minute is < 0 or > 59)
            return Fail("计划时间必须为有效的间隔天数和时刻。", out error);
        return true;
    }

    private void CommitUrlTask()
    {
        if (string.IsNullOrEmpty(_selectedUrlId)) return;
        var cfg = CloneConfig(_host.Config);
        var task = cfg.UrlLauncher.Tasks.FirstOrDefault(t => t.Id == _selectedUrlId);
        if (task is null) return;

        int.TryParse(TxtUrlCloseDelay.Text, out var delay);
        int.TryParse(TxtUrlIntervalDays.Text, out var days);
        int.TryParse(TxtUrlHour.Text, out var hour);
        int.TryParse(TxtUrlMinute.Text, out var minute);
        var scheduleChanged = task.IntervalDays != days || task.Hour != hour || task.Minute != minute;

        task.Name = TxtUrlName.Text.Trim();
        task.Url = TxtUrlAddress.Text.Trim();
        task.BrowserPath = TxtUrlBrowserPath.Text.Trim();
        task.AutoCloseBrowser = string.Equals(
            (CmbUrlAutoClose.SelectedItem as ComboBoxItem)?.Tag?.ToString(),
            "True",
            StringComparison.OrdinalIgnoreCase);
        task.CloseDelaySeconds = delay;
        task.IntervalDays = days;
        task.Hour = hour;
        task.Minute = minute;
        if (scheduleChanged || task.NextDueLocal is null)
            task.NextDueLocal = IntervalSchedule.ComputeInitialNextDue(days, hour, minute, DateTime.Now);

        _host.ReloadConfig(cfg);
        RefreshUrlList();
    }

    private void BtnUrlSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedUrlId)) return;
        if (!ValidateUrlDraft(out var error))
        {
            System.Windows.MessageBox.Show(error, AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        CommitUrlTask();
        if (!string.IsNullOrEmpty(_selectedUrlId))
            _urlDrafts.Remove(_selectedUrlId);
        SetUrlDirty(false);
        _host.Log.Info(Loc.T("Log.Ui.UrlTaskSaved", TxtUrlName.Text.Trim()));
    }

    private void BtnUrlAdd_Click(object sender, RoutedEventArgs e)
    {
        StashUrlDraftIfNeeded();
        var cfg = CloneConfig(_host.Config);
        var task = new UrlLauncherTaskConfig
        {
            Name = $"链接任务 {cfg.UrlLauncher.Tasks.Count + 1}",
            Url = "https://example.com",
            IntervalDays = 1,
            Hour = 8,
            Minute = 0,
            CloseDelaySeconds = 60,
            NextDueLocal = IntervalSchedule.ComputeInitialNextDue(1, 8, 0, DateTime.Now)
        };
        cfg.UrlLauncher.Tasks.Add(task);
        cfg.UrlLauncher.Enabled = true;
        _selectedUrlId = task.Id;
        _host.ReloadConfig(cfg);
        RefreshUrlList();
    }

    private void BtnUrlDelete_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedUrlId)) return;
        var id = _selectedUrlId;
        var cfg = CloneConfig(_host.Config);
        cfg.UrlLauncher.Tasks.RemoveAll(t => t.Id == id);
        _selectedUrlId = null;
        _urlDrafts.Remove(id);
        _host.ReloadConfig(cfg);
        RefreshUrlList();
    }

    private void BtnUrlPause_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedUrlId)) return;
        StashUrlDraftIfNeeded();
        var cfg = CloneConfig(_host.Config);
        var task = cfg.UrlLauncher.Tasks.FirstOrDefault(t => t.Id == _selectedUrlId);
        if (task is null) return;
        task.Enabled = !task.Enabled;
        _host.ReloadConfig(cfg);
        RefreshUrlList();
        _host.Log.Info(task.Enabled ? $"打开链接「{task.Name}」：已恢复" : $"打开链接「{task.Name}」：已暂停");
    }

    private async void BtnUrlOpenNow_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedUrlId)) return;
        StashUrlDraftIfNeeded();
        var feature = _host.Features.OfType<WinNASTools.Core.Features.UrlLauncherFeature>().FirstOrDefault();
        if (feature is null) return;
        try
        {
            BtnUrlPause.IsEnabled = false;
            await feature.RunTaskNowAsync(_selectedUrlId);
            RefreshUrlList();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, Loc.T("Msg.OpenUrlFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
            _host.Log.Error(Loc.T("Log.Ui.UrlManualFailed", ex.Message));
        }
        finally
        {
            UpdateUrlPauseButton();
        }
    }

    private void BtnBrowseUrlBrowser_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.T("Dialog.PickBrowser.Title"),
            Filter = Loc.T("Dialog.PickBrowser.Filter")
        };
        if (dialog.ShowDialog() != true) return;
        TxtUrlBrowserPath.Text = dialog.FileName;
        SetUrlDirty(true);
    }

    private void MenuRenameUrl_Click(object sender, RoutedEventArgs e)
    {
        if (LstUrlTasks.SelectedItem is not TaskListItem selected) return;
        _selectedUrlId = selected.Id;
        var task = _host.Config.UrlLauncher.Tasks.FirstOrDefault(t => t.Id == _selectedUrlId);
        if (task is null || !TryPromptText("重命名任务", "名称：", task.Name, out var name)) return;
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        TxtUrlName.Text = name;
        SetUrlDirty(true);
    }

    #endregion

    #region Backup UI

    private void RefreshBackupList()
    {
        _backupUiLoading = true;
        var tasks = _host.Config.Backup.Tasks;
        LstBackupTasks.Items.Clear();
        foreach (var t in tasks)
        {
            var due = string.Equals(t.ScheduleMode, "Realtime", StringComparison.OrdinalIgnoreCase)
                ? "实时"
                : (t.NextDueLocal?.ToString("yyyy-MM-dd HH:mm") ?? "未排期");
            var mark = t.Enabled ? "" : "[停] ";
            LstBackupTasks.Items.Add(new TaskListItem(t.Id, $"{mark}{t.Name} · {due}"));
        }

        UpdatePrinterListHeightFor(LstBackupTasks, tasks.Count);
        PanelBackupEdit.IsEnabled = false;

        if (!string.IsNullOrEmpty(_selectedBackupId))
        {
            for (var i = 0; i < LstBackupTasks.Items.Count; i++)
            {
                if (LstBackupTasks.Items[i] is TaskListItem item && item.Id == _selectedBackupId)
                {
                    LstBackupTasks.SelectedIndex = i;
                    break;
                }
            }
        }

        if (LstBackupTasks.SelectedItem is TaskListItem selected)
            LoadBackupEditor(selected.Id);
        else
            ClearBackupEditor();

        UpdateBackupPauseButton();
        UpdateConditionalRows();
        ApplyPrinterActionGaps();
        _backupUiLoading = false;
    }

    private void UpdatePrinterListHeightFor(System.Windows.Controls.ListBox list, int count)
    {
        var n = Math.Max(1, Math.Min(count <= 0 ? 1 : count, PrinterListMaxVisible));
        list.Height = n * PrinterListItemHeight + 2;
        ScrollViewer.SetVerticalScrollBarVisibility(
            list,
            count > PrinterListMaxVisible ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
    }

    private void LoadBackupEditor(string taskId)
    {
        var task = _host.Config.Backup.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) { ClearBackupEditor(); return; }

        _backupUiLoading = true;
        _selectedBackupId = task.Id;
        PanelBackupEdit.IsEnabled = true;

        TxtBackupName.Text = task.Name;
        SelectBackupEndpointCombo(CmbBackupSrcKind, task.Source);
        SelectBackupEndpointCombo(CmbBackupDstKind, task.Target);
        var modeTag = string.Equals(task.Mode, "Add", StringComparison.OrdinalIgnoreCase) ? "Copy" : task.Mode;
        SelectComboByTag(CmbBackupMode, modeTag);

        CmbBackupSrcPath.Text = task.Source.PathOrUrl;
        CmbBackupDstPath.Text = task.Target.PathOrUrl;
        UpdateBackupPathHints();

        TxtCopyTrashPath.Text = task.TrashPathTarget ?? "";
        TxtTrashPathA.Text = task.TrashPathSource ?? "";
        TxtTrashPathB.Text = task.TrashPathTarget ?? "";
        TxtBackupExclude.Text = task.ExcludePatterns;
        TxtBackupInterval.Text = Math.Max(1, task.IntervalDays).ToString();
        TxtBackupHour.Text = task.Hour.ToString();
        TxtBackupMinute.Text = task.Minute.ToString("D2");
        TxtBackupNextDue.Text = task.NextDueLocal?.ToString("yyyy-MM-dd HH:mm") ?? "—";
        TxtBackupLastRun.Text = task.LastRunLocal?.ToString("yyyy-MM-dd HH:mm") ?? "—";
        _backupScheduleMode = string.Equals(task.ScheduleMode, "Realtime", StringComparison.OrdinalIgnoreCase)
            ? "Realtime" : "Planned";

        UpdateBackupModeRows();
        SelectComboByTag(CmbBackupConflict, task.ConflictPolicy);
        EnsureCopyTrashPathFilled(forceIfEmpty: true);
        UpdateBackupPauseButton();
        if (_backupDrafts.TryGetValue(task.Id, out var draft))
            ApplyBackupDraft(draft);
        else
            SetBackupDirty(false);
        _backupUiLoading = false;
    }

    private void FillBackupEndpointCombos()
    {
        FillEndpointCombo(CmbBackupSrcKind);
        FillEndpointCombo(CmbBackupDstKind);
        UpdateBackupPathHints();
    }

    private void FillEndpointCombo(System.Windows.Controls.ComboBox cmb)
    {
        var selected = (cmb.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        cmb.Items.Clear();
        cmb.Items.Add(new ComboBoxItem { Content = "本机", Tag = "Local" });
        foreach (var h in _host.Config.Backup.Hosts)
            cmb.Items.Add(new ComboBoxItem { Content = h.Name, Tag = "Host:" + h.Id });

        // 恢复选中
        if (!string.IsNullOrEmpty(selected))
        {
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (string.Equals(item.Tag?.ToString(), selected, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedItem = item;
                    return;
                }
            }
        }
        if (cmb.Items.Count > 0)
            cmb.SelectedIndex = 0;
    }

    private void SelectBackupEndpointCombo(System.Windows.Controls.ComboBox cmb, BackupEndpointConfig ep)
    {
        FillEndpointCombo(cmb);
        string tag;
        if (string.Equals(ep.Kind, "Host", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ep.HostId))
            tag = "Host:" + ep.HostId;
        else if (string.Equals(ep.Kind, "Smb", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(ep.Kind, "WebDav", StringComparison.OrdinalIgnoreCase))
        {
            // 旧任务：尽量匹配同路径主机，否则保留 Kind 并在下拉中显示第一个主机提示。
            var host = _host.Config.Backup.Hosts.FirstOrDefault(h =>
                string.Equals(h.Kind, ep.Kind, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(ep.PathOrUrl)
                && ep.PathOrUrl.StartsWith(h.PathOrUrl.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase));
            tag = host is not null ? "Host:" + host.Id : "Local";
            if (host is not null)
            {
                ep.Kind = "Host";
                ep.HostId = host.Id;
                // PathOrUrl 改为相对子路径
                var root = host.PathOrUrl.TrimEnd('/', '\\');
                if (ep.PathOrUrl.Length > root.Length)
                    ep.PathOrUrl = ep.PathOrUrl[(root.Length)..].TrimStart('/', '\\');
                else
                    ep.PathOrUrl = "";
            }
        }
        else
            tag = "Local";

        foreach (ComboBoxItem item in cmb.Items)
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                cmb.SelectedItem = item;
                return;
            }
        }
        cmb.SelectedIndex = 0;
    }

    private static void ApplyEndpointFromCombo(ComboBoxItem? item, BackupEndpointConfig ep, string path)
    {
        var tag = item?.Tag?.ToString() ?? "Local";
        ep.PathOrUrl = path?.Trim() ?? "";
        ep.UserName = "";
        ep.PasswordProtected = null;
        ep.Domain = "";
        if (tag.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
        {
            ep.Kind = "Host";
            ep.HostId = tag["Host:".Length..];
        }
        else
        {
            ep.Kind = "Local";
            ep.HostId = null;
        }
    }

    private void ClearBackupEditor()
    {
        _selectedBackupId = null;
        PanelBackupEdit.IsEnabled = false;
        SetBackupDirty(false);
        UpdateBackupPauseButton();
        UpdateBackupPathHints();
    }

    private void UpdateBackupPathHints()
    {
        var srcTag = (CmbBackupSrcKind.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Local";
        var dstTag = (CmbBackupDstKind.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Local";
        TxtBackupSrcRootHint.Visibility =
            srcTag.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(CmbBackupSrcPath.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        TxtBackupDstRootHint.Visibility =
            dstTag.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(CmbBackupDstPath.Text)
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateBackupPauseButton()
    {
        var task = string.IsNullOrEmpty(_selectedBackupId)
            ? null
            : _host.Config.Backup.Tasks.FirstOrDefault(t => t.Id == _selectedBackupId);
        BtnBackupPause.IsEnabled = task is not null;
        BtnBackupPause.Content = task is { Enabled: false } ? "恢复" : "暂停";
        UpdateBackupRunButton();
    }

    private void UpdateBackupRunButton()
    {
        if (BtnBackupRunNow is null) return;
        var running = !string.IsNullOrEmpty(_selectedBackupId)
                      && _backupFeature?.IsTaskRunning(_selectedBackupId) == true;
        BtnBackupRunNow.IsEnabled = !string.IsNullOrEmpty(_selectedBackupId) && !running;
        BtnBackupRunNow.Content = running ? "执行中…" : "立即执行";
    }

    private void OnBackupProgressChanged(BackupProgress? progress)
    {
        // 进度回调来自后台线程：合并刷新，避免每次进度都触发布局撑高导致崩溃。
        _backupProgressUiDirty = true;
        Dispatcher.InvokeAsync(() =>
        {
            if (_backupProgressUiTimer is { IsEnabled: false })
                _backupProgressUiTimer.Start();

            if (progress is null || progress.Phase is "Done" or "Failed" or "Cancelled" or "Scanning")
            {
                _backupProgressUiDirty = false;
                RefreshBackupProgressList(forceFit: true);
                if (progress is null || progress.Phase is "Done" or "Failed" or "Cancelled")
                    RefreshBackupList();
            }
        });
    }

    private void RefreshBackupProgressList(bool forceFit = false)
    {
        if (LstBackupProgress is null || _backupFeature is null) return;
        try
        {
            var records = _backupFeature.History
                .OrderByDescending(x => x.IsActive)
                .ThenByDescending(x => x.StartedAtLocal)
                .Select(x => new BackupProgressViewItem(x))
                .ToList();

            var previousCount = _backupProgressUiCount;
            LstBackupProgress.ItemsSource = records;
            _backupProgressUiCount = records.Count;

            // 面板在托盘时只更新数据，不改布局/高度，保证计划备份静默。
            if (!IsVisible)
            {
                UpdateBackupRunButton();
                return;
            }

            if (records.Count == 0)
            {
                LstBackupProgress.Visibility = Visibility.Collapsed;
                LstBackupProgress.Height = double.NaN;
                UpdateBackupRunButton();
                if (forceFit || previousCount != 0)
                    FitWindowToContent();
                return;
            }

            const double recordHeight = 52;
            LstBackupProgress.Visibility = Visibility.Visible;
            LstBackupProgress.Height = Math.Min(records.Count, 5) * recordHeight + 2;
            ScrollViewer.SetVerticalScrollBarVisibility(
                LstBackupProgress,
                records.Count > 5 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
            UpdateBackupRunButton();
            if (forceFit || previousCount != records.Count)
                FitWindowToContent();
        }
        catch (Exception ex)
        {
            _host.Log.Error(Loc.T("Log.Ui.BackupProgressUiFailed", ex.Message));
        }
    }

    private void BtnBackupProgressAction_Click(object sender, RoutedEventArgs e)
    {
        if (_backupFeature is null || sender is not System.Windows.Controls.Button button) return;
        var runId = button.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(runId)) return;

        var record = _backupFeature.History.FirstOrDefault(x => x.RunId == runId);
        if (record is null) return;
        if (record.IsActive)
            _backupFeature.CancelTask(record.TaskId);
        else
            _backupFeature.ClearHistoryRecord(runId);
        RefreshBackupProgressList(forceFit: true);
    }

    private void UpdateBackupModeRows()
    {
        _backupModeUiUpdating = true;
        var mode = (CmbBackupMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Copy";
        var isSync = string.Equals(mode, "Sync", StringComparison.OrdinalIgnoreCase);
        var conflict = (CmbBackupConflict.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        CmbBackupConflict.Items.Clear();
        if (isSync)
        {
            AddConflictItem("跳过", "Skip");
            AddConflictItem("较新为准", "NewerWins");
            AddConflictItem("以A为准", "PreferA");
            AddConflictItem("以B为准", "PreferB");
        }
        else
        {
            AddConflictItem("覆盖", "Overwrite");
            AddConflictItem("跳过", "Skip");
            AddConflictItem("覆盖+回收站", "OverwriteTrash");
        }
        SelectComboByTag(CmbBackupConflict, conflict ?? (isSync ? "Skip" : "OverwriteTrash"));

        var useCopyTrash = !isSync && string.Equals(
            (CmbBackupConflict.SelectedItem as ComboBoxItem)?.Tag?.ToString(), "OverwriteTrash",
            StringComparison.OrdinalIgnoreCase);
        LblConflict.Visibility = Visibility.Visible;
        CmbBackupConflict.Visibility = Visibility.Visible;
        LblTrash.Visibility = (isSync || useCopyTrash) ? Visibility.Visible : Visibility.Collapsed;
        PanelTrashRows.Visibility = LblTrash.Visibility;
        RowCopyTrash.Visibility = useCopyTrash ? Visibility.Visible : Visibility.Collapsed;
        RowSyncTrash.Visibility = isSync ? Visibility.Visible : Visibility.Collapsed;
        var planned = !string.Equals(GetBackupScheduleMode(), "Realtime", StringComparison.OrdinalIgnoreCase);
        LblBackupInterval.Visibility = planned ? Visibility.Visible : Visibility.Collapsed;
        RowBackupSchedule.Visibility = planned ? Visibility.Visible : Visibility.Collapsed;
        LblBackupNext.Visibility = planned ? Visibility.Visible : Visibility.Collapsed;
        RowBackupNextLast.Visibility = planned ? Visibility.Visible : Visibility.Collapsed;
        BtnScheduleRealtime.IsChecked = !planned;
        BtnSchedulePlanned.IsChecked = planned;
        if (useCopyTrash)
            EnsureCopyTrashPathFilled(forceIfEmpty: true);
        _backupModeUiUpdating = false;
        FitWindowToContent();
    }

    private void AddConflictItem(string content, string tag)
        => CmbBackupConflict.Items.Add(new ComboBoxItem { Content = content, Tag = tag });

    private string GetBackupScheduleMode()
        => _backupScheduleMode;

    /// <summary>复制/镜像：启用时填入 B 同级 .syncbak 默认路径。</summary>
    private void EnsureCopyTrashPathFilled(bool forceIfEmpty = false, bool followAuto = false)
    {
        var mode = (CmbBackupMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Copy";
        if (string.Equals(mode, "Sync", StringComparison.OrdinalIgnoreCase)) return;
        if (!string.Equals((CmbBackupConflict.SelectedItem as ComboBoxItem)?.Tag?.ToString(), "OverwriteTrash",
                StringComparison.OrdinalIgnoreCase)) return;

        var def = TryGetDefaultCopyTrashPath();
        if (def is null) return;

        var cur = TxtCopyTrashPath.Text?.Trim() ?? "";
        var shouldFill = (forceIfEmpty && string.IsNullOrEmpty(cur))
            || (followAuto && (string.IsNullOrEmpty(cur)
                || string.Equals(cur, _copyTrashAutoDefault, StringComparison.OrdinalIgnoreCase)));
        if (shouldFill)
        {
            TxtCopyTrashPath.Text = def;
            _copyTrashAutoDefault = def;
        }
        else if (string.Equals(cur, def, StringComparison.OrdinalIgnoreCase))
        {
            _copyTrashAutoDefault = def;
        }
    }

    private string? TryGetDefaultCopyTrashPath()
    {
        var tag = (CmbBackupDstKind.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Local";
        var b = CmbBackupDstPath.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(b)) return null;

        if (string.Equals(tag, "Local", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var full = System.IO.Path.GetFullPath(b.TrimEnd('\\', '/'));
                var parent = System.IO.Directory.GetParent(full);
                return parent is null ? null : System.IO.Path.Combine(parent.FullName, ".syncbak");
            }
            catch
            {
                return null;
            }
        }

        // 主机：相对主机根；B 为单级目录时默认 .syncbak（与同级语义接近）
        var rel = b.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(rel)) return ".syncbak";
        var i = rel.LastIndexOf('/');
        if (i < 0) return ".syncbak";
        return rel[..i].Replace('/', '\\') + "\\.syncbak";
    }

    private static void SelectComboByTag(System.Windows.Controls.ComboBox cmb, string? tag)
    {
        foreach (ComboBoxItem item in cmb.Items)
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                cmb.SelectedItem = item;
                return;
            }
        }
    }

    private void LstBackupTasks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_backupUiLoading) return;
        if (!string.IsNullOrEmpty(_selectedBackupId)
            && LstBackupTasks.SelectedItem is TaskListItem next
            && next.Id != _selectedBackupId)
            StashBackupDraftIfNeeded();

        if (LstBackupTasks.SelectedItem is TaskListItem selectedItem)
            LoadBackupEditor(selectedItem.Id);
        else
            ClearBackupEditor();
    }

    private void LstBackupTasks_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var src = e.OriginalSource as DependencyObject;
        while (src is not null && src is not ListBoxItem && src is not System.Windows.Controls.ListBox)
            src = System.Windows.Media.VisualTreeHelper.GetParent(src);
        if (src is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void BackupModule_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (ChkBackupModule.IsChecked != true)
            StashBackupDraftIfNeeded();
        if (ChkBackupModule.IsChecked == true)
        {
            _backupRowExpanded = true;
            _printerRowExpanded = false;
            _urlRowExpanded = false;
        }
        UpdateConditionalRows();
        CommitSettings();
    }

    private void RestoreBackupSelection()
    {
        _backupUiLoading = true;
        LstBackupTasks.SelectedItem = LstBackupTasks.Items.OfType<TaskListItem>()
            .FirstOrDefault(x => x.Id == _selectedBackupId);
        _backupUiLoading = false;
    }

    private async void BackupPathCombo_DropDownOpened(object sender, EventArgs e)
    {
        if (sender is not System.Windows.Controls.ComboBox pathCombo) return;
        var kindCombo = pathCombo == CmbBackupSrcPath ? CmbBackupSrcKind : CmbBackupDstKind;
        await FillHostPathDropdownAsync(kindCombo, pathCombo);
    }

    private void BackupMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || _backupUiLoading) return;
        UpdateBackupModeRows();
        SetBackupDirty(true);
    }

    private void BackupConflict_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || _backupUiLoading || _backupModeUiUpdating) return;
        UpdateBackupModeRows();
        SetBackupDirty(true);
    }

    private void BackupDraft_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || _backupUiLoading) return;
        if (ReferenceEquals(sender, CmbBackupSrcPath)
            || ReferenceEquals(sender, CmbBackupSrcKind)
            || ReferenceEquals(sender, CmbBackupDstPath)
            || ReferenceEquals(sender, CmbBackupDstKind))
            UpdateBackupPathHints();
        if (ReferenceEquals(sender, CmbBackupDstPath) || ReferenceEquals(sender, CmbBackupDstKind))
            EnsureCopyTrashPathFilled(followAuto: true);
        SetBackupDirty(true);
    }

    private void BackupPathText_Changed(object sender, TextChangedEventArgs e)
    {
        UpdateBackupPathHints();
        if (_loading || _backupUiLoading) return;
        SetBackupDirty(true);
    }

    private void BtnScheduleMode_Click(object sender, RoutedEventArgs e)
    {
        // 互斥：始终选中一个，禁止点当前项后变成两个都灭
        _backupScheduleMode = ReferenceEquals(sender, BtnScheduleRealtime) ? "Realtime" : "Planned";
        BtnScheduleRealtime.IsChecked = string.Equals(_backupScheduleMode, "Realtime", StringComparison.OrdinalIgnoreCase);
        BtnSchedulePlanned.IsChecked = string.Equals(_backupScheduleMode, "Planned", StringComparison.OrdinalIgnoreCase);
        UpdateBackupModeRows();
        if (!_loading && !_backupUiLoading) SetBackupDirty(true);
    }

    private void SetBackupDirty(bool dirty)
    {
        _backupDirty = dirty;
        BtnBackupSave.IsEnabled = dirty && !string.IsNullOrEmpty(_selectedBackupId);
    }

    private void BtnHostManage_Click(object sender, RoutedEventArgs e)
    {
        StashBackupDraftIfNeeded();
        var dlg = new HostManageWindow(_host, () =>
        {
            _backupUiLoading = true;
            FillBackupEndpointCombos();
            if (!string.IsNullOrEmpty(_selectedBackupId))
                LoadBackupEditor(_selectedBackupId);
            _backupUiLoading = false;
        })
        {
            Owner = this
        };
        dlg.ShowDialog();
        FillBackupEndpointCombos();
        if (!string.IsNullOrEmpty(_selectedBackupId))
            LoadBackupEditor(_selectedBackupId);
    }

    private void CommitBackupTask()
    {
        if (string.IsNullOrEmpty(_selectedBackupId)) return;
        var cfg = CloneConfig(_host.Config);
        var task = cfg.Backup.Tasks.FirstOrDefault(t => t.Id == _selectedBackupId);
        if (task is null) return;

        var planned = string.Equals(GetBackupScheduleMode(), "Planned", StringComparison.OrdinalIgnoreCase);
        var days = 1;
        var hour = 0;
        var minute = 0;
        if (planned
            && (!int.TryParse(TxtBackupInterval.Text, out days) || days < 1
                || !int.TryParse(TxtBackupHour.Text, out hour) || hour is < 0 or > 23
                || !int.TryParse(TxtBackupMinute.Text, out minute) || minute is < 0 or > 59))
            return;

        var scheduleChanged = task.IntervalDays != days || task.Hour != hour || task.Minute != minute;

        task.Name = TxtBackupName.Text.Trim();
        ApplyEndpointFromCombo(CmbBackupSrcKind.SelectedItem as ComboBoxItem, task.Source, CmbBackupSrcPath.Text ?? "");
        ApplyEndpointFromCombo(CmbBackupDstKind.SelectedItem as ComboBoxItem, task.Target, CmbBackupDstPath.Text ?? "");

        task.Mode = (CmbBackupMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Copy";
        task.ConflictPolicy = (CmbBackupConflict.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Skip";
        var isSync = string.Equals(task.Mode, "Sync", StringComparison.OrdinalIgnoreCase);
        if (isSync)
        {
            task.UseTrashOnTarget = false;
            task.TrashPathSource = TxtTrashPathA.Text?.Trim() ?? "";
            task.TrashPathTarget = TxtTrashPathB.Text?.Trim() ?? "";
        }
        else
        {
            task.UseTrashOnTarget = string.Equals(task.ConflictPolicy, "OverwriteTrash", StringComparison.OrdinalIgnoreCase);
            task.TrashPathSource = "";
            if (task.UseTrashOnTarget)
                EnsureCopyTrashPathFilled(forceIfEmpty: true);
            task.TrashPathTarget = TxtCopyTrashPath.Text?.Trim() ?? "";
        }
        task.ExcludePatterns = TxtBackupExclude.Text ?? "";
        task.ScheduleMode = GetBackupScheduleMode();
        task.IntervalDays = days;
        task.Hour = hour;
        task.Minute = minute;

        if (planned && (scheduleChanged || task.NextDueLocal is null))
            task.NextDueLocal = IntervalSchedule.ComputeInitialNextDue(days, hour, minute, DateTime.Now);

        _host.ReloadConfig(cfg);
        RefreshBackupList();
    }

    private void BtnBackupSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedBackupId)) return;
        if (!ValidateBackupDraft(out var error))
        {
            System.Windows.MessageBox.Show(error, AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        CommitBackupTask();
        if (!string.IsNullOrEmpty(_selectedBackupId))
            _backupDrafts.Remove(_selectedBackupId);
        SetBackupDirty(false);
        _host.Log.Info(Loc.T("Log.Ui.BackupTaskSaved", TxtBackupName.Text.Trim()));
    }

    private bool ValidateBackupDraft(out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(TxtBackupName.Text))
            return Fail(Loc.T("Msg.TaskNameRequired"), out error);
        if (!ValidateBackupEndpoint("源 A", CmbBackupSrcKind, CmbBackupSrcPath.Text, out error)
            || !ValidateBackupEndpoint("目标 B", CmbBackupDstKind, CmbBackupDstPath.Text, out error))
            return false;

        var excludeError = ExcludeMatcher.ValidatePatterns(TxtBackupExclude.Text);
        if (excludeError is not null) return Fail(excludeError, out error);

        var mode = (CmbBackupMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Copy";
        var conflict = (CmbBackupConflict.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Skip";
        if (!string.Equals(mode, "Sync", StringComparison.OrdinalIgnoreCase)
            && string.Equals(conflict, "OverwriteTrash", StringComparison.OrdinalIgnoreCase))
        {
            EnsureCopyTrashPathFilled(forceIfEmpty: true);
            if (string.IsNullOrWhiteSpace(TxtCopyTrashPath.Text))
                return Fail("请填写回收站路径。", out error);
            if (IsLocalEndpoint(CmbBackupDstKind) && !Path.IsPathRooted(TxtCopyTrashPath.Text.Trim()))
                return Fail("本机回收站路径必须是绝对路径。", out error);
        }
        if (string.Equals(mode, "Sync", StringComparison.OrdinalIgnoreCase))
        {
            if (!ValidateOptionalLocalDirectory("源 A 回收站", TxtTrashPathA.Text, out error)
                || !ValidateOptionalLocalDirectory("目标 B 回收站", TxtTrashPathB.Text, out error))
                return false;
        }
        if (string.Equals(GetBackupScheduleMode(), "Planned", StringComparison.OrdinalIgnoreCase)
            && (!int.TryParse(TxtBackupInterval.Text, out var days) || days < 1
                || !int.TryParse(TxtBackupHour.Text, out var hour) || hour is < 0 or > 23
                || !int.TryParse(TxtBackupMinute.Text, out var minute) || minute is < 0 or > 59))
            return Fail("计划时间必须为有效的间隔天数和时刻。", out error);
        return true;
    }

    private bool ValidateBackupEndpoint(string label, System.Windows.Controls.ComboBox kind, string? path, out string error)
    {
        error = "";
        var tag = (kind.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Local";
        if (!tag.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
        {
            var localPath = path?.Trim() ?? "";
            if (string.IsNullOrEmpty(localPath) || !Path.IsPathRooted(localPath) || !Directory.Exists(localPath))
                return Fail($"{label}必须是已存在的本机绝对目录。", out error);
            return true;
        }
        var hostId = tag["Host:".Length..];
        return _host.Config.Backup.Hosts.Any(h => h.Id == hostId)
            ? true : Fail($"{label}所选主机不存在，请重新选择。", out error);
    }

    private static bool ValidateOptionalLocalDirectory(string label, string? path, out string error)
    {
        error = "";
        var value = path?.Trim() ?? "";
        if (string.IsNullOrEmpty(value) || !Path.IsPathRooted(value)) return true;
        return Directory.Exists(value) ? true : Fail($"{label}目录不存在。", out error);
    }

    private static bool IsLocalEndpoint(System.Windows.Controls.ComboBox kind)
        => !((kind.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Local")
            .StartsWith("Host:", StringComparison.OrdinalIgnoreCase);

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }

    private void BtnBackupAdd_Click(object sender, RoutedEventArgs e)
    {
        StashBackupDraftIfNeeded();
        var cfg = CloneConfig(_host.Config);
        var task = new BackupTaskConfig
        {
            Name = $"备份任务 {cfg.Backup.Tasks.Count + 1}",
            Mode = "Copy",
            ConflictPolicy = "OverwriteTrash",
            ScheduleMode = "Planned",
            IntervalDays = 1,
            Hour = 3,
            Minute = 0,
            Enabled = true,
            UseTrashOnTarget = true,
            NextDueLocal = IntervalSchedule.ComputeInitialNextDue(1, 3, 0, DateTime.Now)
        };
        cfg.Backup.Tasks.Add(task);
        cfg.Backup.Enabled = true;
        _selectedBackupId = task.Id;
        _host.ReloadConfig(cfg);
        RefreshBackupList();
    }

    private void BtnBackupDelete_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedBackupId)) return;
        var id = _selectedBackupId;
        var cfg = CloneConfig(_host.Config);
        cfg.Backup.Tasks.RemoveAll(t => t.Id == id);
        _selectedBackupId = null;
        _backupDrafts.Remove(id);
        _host.ReloadConfig(cfg);
        RefreshBackupList();
    }

    private void BtnBackupPause_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedBackupId)) return;
        StashBackupDraftIfNeeded();
        var cfg = CloneConfig(_host.Config);
        var task = cfg.Backup.Tasks.FirstOrDefault(t => t.Id == _selectedBackupId);
        if (task is null) return;
        task.Enabled = !task.Enabled;
        _host.ReloadConfig(cfg);
        RefreshBackupList();
        _host.Log.Info(task.Enabled ? $"备份「{task.Name}」：已恢复" : $"备份「{task.Name}」：已暂停");
    }

    private async void BtnBackupRunNow_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedBackupId)) return;
        StashBackupDraftIfNeeded();

        var feature = _backupFeature ?? _host.Features.OfType<BackupFeature>().FirstOrDefault();
        if (feature is null) return;
        if (feature.IsTaskRunning(_selectedBackupId)) return;

        UpdateBackupRunButton();
        BtnBackupPause.IsEnabled = false;
        try
        {
            await feature.RunTaskNowAsync(_selectedBackupId).ConfigureAwait(true);
            RefreshBackupList();
            RefreshBackupProgressList(forceFit: true);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, Loc.T("Msg.BackupFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            UpdateBackupPauseButton();
        }
    }

    private void BtnBrowseBackupSrc_Click(object sender, RoutedEventArgs e)
        => BrowseBackupPath(CmbBackupSrcKind, CmbBackupSrcPath);

    private void BtnBrowseBackupDst_Click(object sender, RoutedEventArgs e)
        => BrowseBackupPath(CmbBackupDstKind, CmbBackupDstPath);

    private void BtnBrowseCopyTrash_Click(object sender, RoutedEventArgs e)
    {
        if (TryPickFolder(out var path))
        {
            TxtCopyTrashPath.Text = path;
            SetBackupDirty(true);
        }
    }

    private void BtnBrowseTrashA_Click(object sender, RoutedEventArgs e)
    {
        if (TryPickFolder(out var path))
        {
            TxtTrashPathA.Text = path;
            SetBackupDirty(true);
        }
    }

    private void BtnBrowseTrashB_Click(object sender, RoutedEventArgs e)
    {
        if (TryPickFolder(out var path))
        {
            TxtTrashPathB.Text = path;
            SetBackupDirty(true);
        }
    }

    private async void BrowseBackupPath(System.Windows.Controls.ComboBox kindCombo, System.Windows.Controls.ComboBox pathCombo)
    {
        var tag = (kindCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Local";
        if (string.Equals(tag, "Local", StringComparison.OrdinalIgnoreCase))
        {
            if (TryPickFolder(out var path))
            {
                pathCombo.Text = path;
                if (pathCombo == CmbBackupDstPath)
                    EnsureCopyTrashPathFilled(followAuto: true);
                SetBackupDirty(true);
            }
            return;
        }

        // 主机：弹出相对路径选择
        if (!TryBuildHostRootEndpoint(tag, out var rootEp, out var err))
        {
            System.Windows.MessageBox.Show(err, AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new RemoteFolderPickerWindow(rootEp, pathCombo.Text)
        {
            Owner = this
        };
        if (dlg.ShowDialog() == true)
        {
            pathCombo.Text = dlg.SelectedRelativePath;
            if (pathCombo == CmbBackupDstPath)
                EnsureCopyTrashPathFilled(followAuto: true);
            SetBackupDirty(true);
        }

        await Task.CompletedTask;
    }

    private async Task FillHostPathDropdownAsync(
        System.Windows.Controls.ComboBox kindCombo,
        System.Windows.Controls.ComboBox pathCombo)
    {
        var tag = (kindCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Local";
        var current = pathCombo.Text?.Trim() ?? "";

        if (string.Equals(tag, "Local", StringComparison.OrdinalIgnoreCase))
        {
            // 本机保持可编辑文本，不下拉扫盘
            pathCombo.Items.Clear();
            if (!string.IsNullOrEmpty(current))
                pathCombo.Items.Add(current);
            pathCombo.Text = current;
            return;
        }

        if (!TryBuildHostRootEndpoint(tag, out var rootEp, out _))
        {
            pathCombo.Items.Clear();
            pathCombo.Items.Add(new ComboBoxItem { Content = "（请先配置主机）", IsEnabled = false });
            return;
        }

        // 下拉列主机根下的直接子目录；更深路径用「浏览」
        pathCombo.Items.Clear();
        pathCombo.Items.Add(new ComboBoxItem { Content = "（根目录）", Tag = "" });

        try
        {
            await using var ep = FileEndpointFactory.Create(rootEp);
            await ep.ConnectAsync(CancellationToken.None);
            var dirs = await ep.ListChildDirectoriesAsync("", CancellationToken.None);
            foreach (var d in dirs)
                pathCombo.Items.Add(d);

            if (!string.IsNullOrEmpty(current) && !pathCombo.Items.Cast<object>().Any(x =>
                    string.Equals(x switch
                    {
                        ComboBoxItem ci => ci.Tag?.ToString() ?? ci.Content?.ToString(),
                        _ => x?.ToString()
                    }, current, StringComparison.OrdinalIgnoreCase)))
                pathCombo.Items.Insert(1, current);
        }
        catch
        {
            if (!string.IsNullOrEmpty(current))
                pathCombo.Items.Add(current);
            pathCombo.Items.Add(new ComboBoxItem { Content = "（无法连接主机）", IsEnabled = false });
        }

        pathCombo.Text = current;
    }

    private bool TryBuildHostRootEndpoint(string kindTag, out BackupEndpointConfig ep, out string error)
    {
        ep = new BackupEndpointConfig();
        error = "";
        if (!kindTag.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
        {
            error = "未选择主机。";
            return false;
        }

        var hostId = kindTag["Host:".Length..];
        var host = _host.Config.Backup.Hosts.FirstOrDefault(h => h.Id == hostId);
        if (host is null)
        {
            error = "未找到主机，请先在主机管理中配置。";
            return false;
        }

        // 列目录直接连主机根（已解析，无需再传 Hosts）
        ep = new BackupEndpointConfig
        {
            Kind = host.Kind,
            PathOrUrl = host.PathOrUrl,
            UserName = host.UserName,
            PasswordProtected = host.PasswordProtected,
            Domain = host.Domain
        };
        return true;
    }

    private static bool TryPickFolder(out string path)
    {
        path = "";
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择文件夹",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;
        path = dlg.SelectedPath;
        return !string.IsNullOrWhiteSpace(path);
    }

    private void MenuRenameBackup_Click(object sender, RoutedEventArgs e)
    {
        if (LstBackupTasks.SelectedItem is not TaskListItem selected) return;
        _selectedBackupId = selected.Id;
        var task = _host.Config.Backup.Tasks.FirstOrDefault(t => t.Id == _selectedBackupId);
        if (task is null) return;
        if (!TryPromptText("重命名任务", "名称：", task.Name, out var name)) return;
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        TxtBackupName.Text = name;
        SetBackupDirty(true);
    }

    #endregion

    #region AppSwitch UI

    private void RefreshAppSwitchList()
    {
        _appSwitchUiLoading = true;
        var tasks = _host.Config.AppSwitch.Tasks;
        LstAppSwitchTasks.Items.Clear();
        foreach (var t in tasks)
        {
            var proc = string.IsNullOrWhiteSpace(t.ProcessName) ? "未设进程" : t.ProcessName.Trim();
            var when = t.StopAfterSeconds > 0 ? $"{t.StopAfterSeconds}s" : "仅离开";
            var mark = t.Enabled ? "" : "[停] ";
            LstAppSwitchTasks.Items.Add(new TaskListItem(t.Id, $"{mark}{t.Name} · {proc} · {when}"));
        }

        UpdatePrinterListHeightFor(LstAppSwitchTasks, tasks.Count);
        PanelAppSwitchEdit.IsEnabled = false;

        if (!string.IsNullOrEmpty(_selectedAppSwitchId))
        {
            for (var i = 0; i < LstAppSwitchTasks.Items.Count; i++)
            {
                if (LstAppSwitchTasks.Items[i] is TaskListItem item && item.Id == _selectedAppSwitchId)
                {
                    LstAppSwitchTasks.SelectedIndex = i;
                    break;
                }
            }
        }

        if (LstAppSwitchTasks.SelectedItem is TaskListItem selected)
            LoadAppSwitchEditor(selected.Id);
        else
            ClearAppSwitchEditor();

        UpdateAppSwitchPauseButton();
        UpdateConditionalRows();
        ApplyPrinterActionGaps();
        _appSwitchUiLoading = false;
    }

    private void LoadAppSwitchEditor(string taskId)
    {
        var task = _host.Config.AppSwitch.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) { ClearAppSwitchEditor(); return; }

        _appSwitchUiLoading = true;
        _selectedAppSwitchId = task.Id;
        PanelAppSwitchEdit.IsEnabled = true;

        TxtAppName.Text = task.Name;
        TxtAppStopAfter.Text = task.StopAfterSeconds.ToString();
        SelectComboByTag(CmbAppRestart, task.RestartOnReturn ? "True" : "False");

        var path = (task.LaunchPath ?? "").Trim();
        var processName = ResolveProcessName(task.ProcessName, path);
        _appSwitchProcessName = processName;
        // 有路径账本 → 指定路径模式；否则选择应用模式
        _appSwitchBindMode = string.IsNullOrWhiteSpace(path)
            ? AppSwitchBindMode.Select
            : AppSwitchBindMode.Path;

        TxtAppLaunch.Text = path;
        ApplyAppSwitchModeUi();
        if (_appSwitchDrafts.TryGetValue(task.Id, out var draft))
        {
            ApplyAppSwitchDraft(draft);
        }
        else if (_appSwitchBindMode == AppSwitchBindMode.Select)
        {
            SetAppProcessText(processName, deferRewrite: false);
            SetAppSwitchDirty(false);
        }
        else
        {
            RefreshAppBoundFromPath(markDirty: false);
            SetAppSwitchDirty(false);
        }

        UpdateAppSwitchPauseButton();
        _appSwitchUiLoading = false;
    }

    private void ClearAppSwitchEditor()
    {
        _selectedAppSwitchId = null;
        PanelAppSwitchEdit.IsEnabled = false;
        SetAppSwitchDirty(false);
        _appSwitchProcessName = "";
        _appSwitchBindMode = AppSwitchBindMode.Select;
        TxtAppName.Text = "";
        SetAppProcessText("", deferRewrite: false);
        TxtAppBound.Text = "";
        TxtAppLaunch.Text = "";
        TxtAppStopAfter.Text = "900";
        SelectComboByTag(CmbAppRestart, "True");
        ApplyAppSwitchModeUi();
        UpdateAppSwitchPauseButton();
    }

    private void UpdateAppSwitchPauseButton()
    {
        var task = string.IsNullOrEmpty(_selectedAppSwitchId)
            ? null
            : _host.Config.AppSwitch.Tasks.FirstOrDefault(t => t.Id == _selectedAppSwitchId);
        BtnAppSwitchPause.IsEnabled = task is not null;
        BtnAppSwitchPause.Content = task is { Enabled: false } ? "恢复" : "暂停";
    }

    private void LstAppSwitchTasks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_appSwitchUiLoading) return;
        if (!string.IsNullOrEmpty(_selectedAppSwitchId)
            && LstAppSwitchTasks.SelectedItem is TaskListItem next
            && next.Id != _selectedAppSwitchId)
            StashAppSwitchDraftIfNeeded();

        if (LstAppSwitchTasks.SelectedItem is TaskListItem selectedItem)
            LoadAppSwitchEditor(selectedItem.Id);
        else
            ClearAppSwitchEditor();
    }

    private void LstAppSwitchTasks_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var src = e.OriginalSource as DependencyObject;
        while (src is not null && src is not ListBoxItem && src is not System.Windows.Controls.ListBox)
            src = System.Windows.Media.VisualTreeHelper.GetParent(src);
        if (src is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void AppSwitchModule_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (ChkAppSwitchModule.IsChecked != true)
            StashAppSwitchDraftIfNeeded();
        if (ChkAppSwitchModule.IsChecked == true)
            _appSwitchRowExpanded = true;
        UpdateConditionalRows();
        CommitSettings();
    }

    private void BtnAppModeSelect_Click(object sender, RoutedEventArgs e)
    {
        if (_loading || _appSwitchUiLoading) return;
        if (_appSwitchBindMode == AppSwitchBindMode.Select) return;
        _appSwitchBindMode = AppSwitchBindMode.Select;
        ApplyAppSwitchModeUi();
        SetAppProcessText(_appSwitchProcessName, deferRewrite: false);
        // 切换到选择模式后，若当前路径能对应运行中应用则保持；否则清空路径等待重选
        if (!string.IsNullOrWhiteSpace(TxtAppLaunch.Text)
            && !TryFindRunningProcessByPath(TxtAppLaunch.Text.Trim(), out _, out _))
        {
            // 保留路径显示但只读；用户重选应用时会覆盖
        }
        SetAppSwitchDirty(true);
    }

    private void BtnAppModePath_Click(object sender, RoutedEventArgs e)
    {
        if (_loading || _appSwitchUiLoading) return;
        if (_appSwitchBindMode == AppSwitchBindMode.Path) return;
        _appSwitchBindMode = AppSwitchBindMode.Path;
        ApplyAppSwitchModeUi();
        RefreshAppBoundFromPath(markDirty: false);
        SetAppSwitchDirty(true);
    }

    private void ApplyAppSwitchModeUi()
    {
        var select = _appSwitchBindMode == AppSwitchBindMode.Select;
        CmbAppProcess.Visibility = select ? Visibility.Visible : Visibility.Collapsed;
        TxtAppBound.Visibility = select ? Visibility.Collapsed : Visibility.Visible;
        TxtAppLaunch.IsReadOnly = select;

        BtnAppModeSelect.FontWeight = select ? FontWeights.SemiBold : FontWeights.Normal;
        BtnAppModePath.FontWeight = select ? FontWeights.Normal : FontWeights.SemiBold;

        TxtAppLaunch.Tag = select
            ? "由所选应用自动带出"
            : "填写或浏览 exe 路径";

        // 当前模式的结果字段仍保留显示，但用灰底、禁止光标和提示明确告知不可编辑。
        TxtAppLaunch.Background = select
            ? System.Windows.SystemColors.ControlBrush
            : System.Windows.SystemColors.WindowBrush;
        TxtAppLaunch.Cursor = select
            ? System.Windows.Input.Cursors.No
            : System.Windows.Input.Cursors.IBeam;
        TxtAppLaunch.ForceCursor = select;
        TxtAppLaunch.ToolTip = select
            ? "选择应用模式：路径由所选应用自动同步，禁止手动修改"
            : "指定路径模式：填写或浏览 exe 路径";

        BtnBrowseAppLaunch.Opacity = select ? 0.55 : 1;
        BtnBrowseAppLaunch.Cursor = select
            ? System.Windows.Input.Cursors.No
            : System.Windows.Input.Cursors.Arrow;
        BtnBrowseAppLaunch.ForceCursor = select;
        BtnBrowseAppLaunch.ToolTip = select
            ? "选择应用模式：浏览路径已禁止"
            : "浏览并选择 exe 文件";

        TxtAppBound.Background = System.Windows.SystemColors.ControlBrush;
        TxtAppBound.Cursor = System.Windows.Input.Cursors.No;
        TxtAppBound.ForceCursor = true;
        TxtAppBound.ToolTip = "指定路径模式：应用由路径自动识别，禁止手动修改";

        BtnAppListMode.Opacity = select ? 1 : 0.55;
        BtnAppListMode.Cursor = select
            ? System.Windows.Input.Cursors.Arrow
            : System.Windows.Input.Cursors.No;
        BtnAppListMode.ForceCursor = !select;
        BtnAppListMode.ToolTip = select
            ? "普通：仅有可见窗口的应用\n复杂：当前会话真实应用（含托盘/后台，排除系统进程）"
            : "指定路径模式：应用选择已禁止";
    }

    private void CmbAppProcess_DropDownOpened(object sender, EventArgs e)
    {
        if (_appSwitchBindMode != AppSwitchBindMode.Select) return;

        var keep = string.IsNullOrWhiteSpace(_appSwitchProcessName)
            ? (CmbAppProcess.Text ?? "")
            : _appSwitchProcessName;
        _availableAppChoices = GetRunningAppChoices(_appListComplex);
        CmbAppProcess.ItemsSource = _availableAppChoices;
        SetAppProcessText(keep);

        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(CmbAppProcess.ItemsSource);
        if (view is not null)
        {
            view.Filter = null;
            view.Refresh();
        }
    }

    private void BtnAppListMode_Click(object sender, RoutedEventArgs e)
    {
        if (_appSwitchBindMode != AppSwitchBindMode.Select) return;
        _appListComplex = !_appListComplex;
        BtnAppListMode.Content = _appListComplex ? "复杂" : "普通";
        if (CmbAppProcess.IsDropDownOpen)
            CmbAppProcess_DropDownOpened(CmbAppProcess, EventArgs.Empty);
    }

    private void CmbAppProcess_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || _appSwitchUiLoading || !CmbAppProcess.IsDropDownOpen) return;
        if (_appSwitchBindMode != AppSwitchBindMode.Select) return;
        var query = (CmbAppProcess.Text ?? "").Trim();
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(CmbAppProcess.ItemsSource);
        if (view is null) return;

        view.Filter = item =>
        {
            if (string.IsNullOrEmpty(query)) return true;
            return item is AppProcessChoice app
                   && (app.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)
                       || app.Display.Contains(query, StringComparison.OrdinalIgnoreCase)
                       || app.WindowTitle.Contains(query, StringComparison.OrdinalIgnoreCase));
        };
        view.Refresh();
    }

    private void CmbAppProcess_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _appSwitchUiLoading) return;
        if (_appSwitchBindMode != AppSwitchBindMode.Select) return;
        if (CmbAppProcess.SelectedItem is not AppProcessChoice selected) return;

        _appSwitchProcessName = selected.ProcessName;
        SetAppProcessText(selected.ProcessName);
        // 换应用必须同步切换路径（能识别则写入；识别不了则清空）
        _appSwitchUiLoading = true;
        TxtAppLaunch.Text = string.IsNullOrWhiteSpace(selected.ExecutablePath)
            ? ""
            : selected.ExecutablePath;
        _appSwitchUiLoading = false;
        SetAppSwitchDirty(true);
    }

    /// <summary>
    /// 可编辑 ComboBox：清空 SelectedItem / 更换 ItemsSource 后，WPF 会异步清掉 Text。
    /// </summary>
    private void SetAppProcessText(string? text, bool deferRewrite = true)
    {
        text ??= "";
        void Apply()
        {
            CmbAppProcess.SelectedItem = null;
            CmbAppProcess.Text = text;
        }

        if (!deferRewrite)
        {
            Apply();
            return;
        }

        _appSwitchUiLoading = true;
        Apply();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _appSwitchUiLoading = true;
            Apply();
            _appSwitchUiLoading = false;
        });
    }

    private void TxtAppLaunch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || _appSwitchUiLoading) return;
        if (_appSwitchBindMode != AppSwitchBindMode.Path) return;
        RefreshAppBoundFromPath(markDirty: true);
    }

    private void RefreshAppBoundFromPath(bool markDirty)
    {
        var path = (TxtAppLaunch.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            _appSwitchProcessName = "";
            TxtAppBound.Text = "";
            if (markDirty) SetAppSwitchDirty(true);
            return;
        }

        var baseName = ResolveProcessName(null, path);
        var running = TryFindRunningProcessByPath(path, out var runningName, out _);
        if (!string.IsNullOrWhiteSpace(runningName))
            baseName = runningName;

        _appSwitchProcessName = baseName;
        if (string.IsNullOrWhiteSpace(baseName))
            TxtAppBound.Text = running ? "已匹配 · 运行中" : "未识别";
        else
            TxtAppBound.Text = $"{baseName} · {(running ? "运行中" : "未运行")}";

        if (markDirty) SetAppSwitchDirty(true);
    }

    private static bool TryFindRunningProcessByPath(
        string path,
        out string processName,
        out string? matchedPath)
    {
        processName = "";
        matchedPath = null;
        if (string.IsNullOrWhiteSpace(path)) return false;

        string full;
        try { full = Path.GetFullPath(path); }
        catch { full = path.Trim(); }

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                string? file;
                try { file = process.MainModule?.FileName; }
                catch { continue; }
                if (string.IsNullOrWhiteSpace(file)) continue;

                string other;
                try { other = Path.GetFullPath(file); }
                catch { other = file; }

                if (!string.Equals(full, other, StringComparison.OrdinalIgnoreCase))
                    continue;

                processName = process.ProcessName;
                matchedPath = file;
                return true;
            }
            catch { /* 进程退出或拒绝访问 */ }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }

    private static string ResolveProcessName(string? processName, string? launchPath)
    {
        if (!string.IsNullOrWhiteSpace(processName))
        {
            var name = processName.Trim();
            // 兼容误存的展示文案
            var sep = name.IndexOf('·');
            if (sep > 0) name = name[..sep].Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];
            return name;
        }
        if (string.IsNullOrWhiteSpace(launchPath))
            return "";
        try
        {
            var file = Path.GetFileNameWithoutExtension(launchPath) ?? "";
            return file;
        }
        catch { return ""; }
    }

    /// <summary>
    /// 普通：仅有可见主窗口的应用。
    /// 复杂：当前会话真实应用（含托盘/后台），排除系统进程与 Windows 目录程序。
    /// </summary>
    private static List<AppProcessChoice> GetRunningAppChoices(bool complex)
    {
        var currentPid = Environment.ProcessId;
        var currentSession = Process.GetCurrentProcess().SessionId;
        var windowsRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var result = new Dictionary<string, AppProcessChoice>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == currentPid) continue;
                if (HiddenSystemProcesses.Contains(process.ProcessName)) continue;

                var hasWindow = process.MainWindowHandle != IntPtr.Zero
                                && !string.IsNullOrWhiteSpace(process.MainWindowTitle);

                if (!complex)
                {
                    if (!hasWindow) continue;
                }
                else
                {
                    try
                    {
                        if (process.SessionId != currentSession) continue;
                    }
                    catch { continue; }
                }

                string? path = null;
                string? description = null;
                try
                {
                    path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        if (complex
                            && !string.IsNullOrWhiteSpace(windowsRoot)
                            && path.StartsWith(windowsRoot, StringComparison.OrdinalIgnoreCase))
                            continue;
                        description = FileVersionInfo.GetVersionInfo(path).FileDescription;
                    }
                }
                catch
                {
                    // 提权进程读不到路径：普通列表仍可按窗口选；复杂列表若无窗口也保留按进程名。
                    if (complex && !hasWindow) { /* keep */ }
                }

                var title = hasWindow ? process.MainWindowTitle.Trim() : "";
                var displayName = string.IsNullOrWhiteSpace(description)
                    ? process.ProcessName
                    : description.Trim();
                var display = string.Equals(displayName, process.ProcessName, StringComparison.OrdinalIgnoreCase)
                    ? process.ProcessName
                    : $"{displayName} · {process.ProcessName}";

                if (!result.TryGetValue(process.ProcessName, out var old)
                    || (!old.HasWindow && hasWindow)
                    || (string.IsNullOrWhiteSpace(old.ExecutablePath) && !string.IsNullOrWhiteSpace(path)))
                {
                    result[process.ProcessName] = new AppProcessChoice(
                        process.ProcessName, path, display, title, hasWindow);
                }
            }
            catch { /* 进程在枚举期间退出或拒绝访问 */ }
            finally
            {
                process.Dispose();
            }
        }

        return result.Values
            .OrderByDescending(x => x.HasWindow)
            .ThenBy(x => x.Display, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void AppSwitchDraft_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || _appSwitchUiLoading) return;
        SetAppSwitchDirty(true);
    }

    private void SetAppSwitchDirty(bool dirty)
    {
        _appSwitchDirty = dirty;
        BtnAppSwitchSave.IsEnabled = dirty && !string.IsNullOrEmpty(_selectedAppSwitchId);
    }

    private void RestoreAppSwitchSelection()
    {
        _appSwitchUiLoading = true;
        LstAppSwitchTasks.SelectedItem = LstAppSwitchTasks.Items.OfType<TaskListItem>()
            .FirstOrDefault(x => x.Id == _selectedAppSwitchId);
        _appSwitchUiLoading = false;
    }

    private void CommitAppSwitchTask()
    {
        if (string.IsNullOrEmpty(_selectedAppSwitchId)) return;
        var cfg = CloneConfig(_host.Config);
        var task = cfg.AppSwitch.Tasks.FirstOrDefault(t => t.Id == _selectedAppSwitchId);
        if (task is null) return;

        if (!int.TryParse(TxtAppStopAfter.Text, out var stopAfter) || stopAfter < 0) return;

        var path = (TxtAppLaunch.Text ?? "").Trim();
        var processName = string.IsNullOrWhiteSpace(_appSwitchProcessName)
            ? ResolveProcessName(null, path)
            : _appSwitchProcessName.Trim();

        task.Name = TxtAppName.Text.Trim();
        task.LaunchPath = path;
        task.ProcessName = processName;
        task.RestartOnReturn = string.Equals(
            (CmbAppRestart.SelectedItem as ComboBoxItem)?.Tag?.ToString(), "True",
            StringComparison.OrdinalIgnoreCase);
        task.StopAfterSeconds = stopAfter;

        _host.ReloadConfig(cfg);
        RefreshAppSwitchList();
    }

    private void BtnAppSwitchSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedAppSwitchId)) return;
        if (string.IsNullOrWhiteSpace(TxtAppName.Text))
        {
            System.Windows.MessageBox.Show(Loc.T("Msg.TaskNameRequired"), AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_appSwitchBindMode == AppSwitchBindMode.Path)
            RefreshAppBoundFromPath(markDirty: false);

        var path = (TxtAppLaunch.Text ?? "").Trim();
        var processName = string.IsNullOrWhiteSpace(_appSwitchProcessName)
            ? ResolveProcessName(null, path)
            : _appSwitchProcessName.Trim();

        if (_appSwitchBindMode == AppSwitchBindMode.Select)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                System.Windows.MessageBox.Show(Loc.T("Msg.SelectAppRequired"), AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                System.Windows.MessageBox.Show(Loc.T("Msg.AppPathRequired"), AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show(Loc.T("Msg.AppPathExeOnly"), AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(processName))
            {
                System.Windows.MessageBox.Show(Loc.T("Msg.AppNameFromPath"), AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (!int.TryParse(TxtAppStopAfter.Text, out var stopAfter) || stopAfter < 0)
        {
            System.Windows.MessageBox.Show(Loc.T("Msg.IdleSecondsInvalid"), AppBranding.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!string.IsNullOrEmpty(path) && !File.Exists(path))
        {
            var go = System.Windows.MessageBox.Show(
                Loc.T("Msg.PathFileMissing"),
                AppBranding.Name, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (go != MessageBoxResult.Yes) return;
        }

        _appSwitchProcessName = processName;
        CommitAppSwitchTask();
        if (!string.IsNullOrEmpty(_selectedAppSwitchId))
            _appSwitchDrafts.Remove(_selectedAppSwitchId);
        SetAppSwitchDirty(false);
        _host.Log.Info(Loc.T("Log.Ui.AppSwitchTaskSaved", TxtAppName.Text.Trim()));
    }

    private void BtnAppSwitchAdd_Click(object sender, RoutedEventArgs e)
    {
        StashAppSwitchDraftIfNeeded();
        var cfg = CloneConfig(_host.Config);
        var task = new AppSwitchTaskConfig
        {
            Name = $"停止应用 {cfg.AppSwitch.Tasks.Count + 1}",
            ProcessName = "",
            LaunchPath = "",
            RestartOnReturn = true,
            StopAfterSeconds = 900,
            Enabled = true
        };
        cfg.AppSwitch.Tasks.Add(task);
        cfg.AppSwitch.Enabled = true;
        _selectedAppSwitchId = task.Id;
        _host.ReloadConfig(cfg);
        RefreshAppSwitchList();
    }

    private void BtnAppSwitchDelete_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedAppSwitchId)) return;
        var id = _selectedAppSwitchId;
        var cfg = CloneConfig(_host.Config);
        cfg.AppSwitch.Tasks.RemoveAll(t => t.Id == id);
        _selectedAppSwitchId = null;
        _appSwitchDrafts.Remove(id);
        _host.ReloadConfig(cfg);
        RefreshAppSwitchList();
    }

    private void BtnAppSwitchPause_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedAppSwitchId)) return;
        StashAppSwitchDraftIfNeeded();
        var cfg = CloneConfig(_host.Config);
        var task = cfg.AppSwitch.Tasks.FirstOrDefault(t => t.Id == _selectedAppSwitchId);
        if (task is null) return;
        task.Enabled = !task.Enabled;
        _host.ReloadConfig(cfg);
        RefreshAppSwitchList();
        _host.Log.Info(task.Enabled ? $"停止应用「{task.Name}」：已恢复" : $"停止应用「{task.Name}」：已暂停");
    }

    private void MenuRenameAppSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (LstAppSwitchTasks.SelectedItem is not TaskListItem selected) return;
        _selectedAppSwitchId = selected.Id;
        var task = _host.Config.AppSwitch.Tasks.FirstOrDefault(t => t.Id == _selectedAppSwitchId);
        if (task is null) return;
        if (!TryPromptText("重命名任务", "名称：", task.Name, out var name)) return;
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        TxtAppName.Text = name;
        SetAppSwitchDirty(true);
    }

    private void BtnBrowseAppLaunch_Click(object sender, RoutedEventArgs e)
    {
        if (_appSwitchBindMode != AppSwitchBindMode.Path) return;

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.T("Dialog.PickApp.Title"),
            Filter = Loc.T("Dialog.PickApp.Filter")
        };
        if (dlg.ShowDialog() != true) return;

        _appSwitchUiLoading = true;
        TxtAppLaunch.Text = dlg.FileName;
        _appSwitchUiLoading = false;
        RefreshAppBoundFromPath(markDirty: true);
    }

    #endregion

    private sealed record AppProcessChoice(
        string ProcessName,
        string? ExecutablePath,
        string Display,
        string WindowTitle,
        bool HasWindow)
    {
        public string FullDisplay => string.IsNullOrWhiteSpace(WindowTitle)
            ? Display
            : $"{Display} · {WindowTitle}";
        public override string ToString() => Display;
    }

    private sealed class TaskListItem
    {
        public string Id { get; }
        public string Display { get; }
        public TaskListItem(string id, string display)
        {
            Id = id;
            Display = display;
        }
        public override string ToString() => Display;
    }

    private void OnStatusChanged()
    {
        Dispatcher.InvokeAsync(() =>
        {
            var run = _host.IsRunning ? Loc.T("Status.Monitoring") : Loc.T("Status.Stopped");
            var phase = _host.IsWorkstationLocked
                ? Loc.T("Status.LockedAway")
                : _host.IsAway ? Loc.T("Status.Away") : "";
            var skip = _host.ShouldSkipAutoIdleTriggers ? Loc.T("Status.Exempt") : "";
            var grace = _host.IsUserActivitySuppressed ? Loc.T("Status.GraceActive") : "";
            TxtStatus.Text = Loc.T("Status.Full", run, phase, (int)_host.IdleSeconds, skip, grace);
            TxtStatusHotkey.Text = Loc.T("Status.HotkeyLeave", _hotkeyDisplay);
        });
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            if (!TryHideToTray())
                WindowState = WindowState.Normal;
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowExit) return;
        e.Cancel = true;
        TryHideToTray();
    }

    /// <summary>
    /// 退出前确认草稿。无草稿直接通过；
    /// 仅 1 条：继续编辑（跳转）/ 丢弃；
    /// 多条：返回（取消退出）/ 丢弃。
    /// </summary>
    public bool ResolvePendingDrafts()
    {
        StashAllCurrentDrafts();
        var drafts = CollectDraftEntries();
        if (drafts.Count == 0) return true;

        string message;
        bool single = drafts.Count == 1;
        if (single)
        {
            var d = drafts[0];
            message = $"有 1 个{d.Category}任务草稿未保存（{d.TaskName}）。\n\n继续编辑：跳转到该任务\n丢弃：丢弃草稿并退出";
        }
        else
        {
            var lines = drafts
                .GroupBy(x => x.Category)
                .Select(g => $"{g.Count()} 个{g.Key}任务草稿");
            message = "当前有未保存草稿：\n" + string.Join("\n", lines)
                      + "\n\n返回：取消本次退出\n丢弃：丢弃全部草稿并退出";
        }

        var dlg = new DraftExitWindow(message, single)
        {
            Owner = IsVisible ? this : null
        };
        if (dlg.ShowDialog() != true) return false;

        if (dlg.Choice == DraftExitChoice.Discard)
        {
            DiscardAllDrafts();
            return true;
        }

        // Stay：单草稿跳转继续编辑；多草稿仅取消退出
        if (single)
        {
            ShowFromTray();
            NavigateToDraft(drafts[0]);
        }
        return false;
    }

    public void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public bool TryHideToTray()
    {
        StashAllCurrentDrafts();
        ShowInTaskbar = false;
        Hide();
        return true;
    }

    public void ForceClose()
    {
        if (!ResolvePendingDrafts()) return;
        _allowExit = true;
        Close();
    }

    private void StashAllCurrentDrafts()
    {
        StashPrinterDraftIfNeeded();
        StashUrlDraftIfNeeded();
        StashBackupDraftIfNeeded();
        StashAppSwitchDraftIfNeeded();
    }

    private void DiscardAllDrafts()
    {
        _printerDrafts.Clear();
        _urlDrafts.Clear();
        _backupDrafts.Clear();
        _appSwitchDrafts.Clear();
        _printerDirty = false;
        _urlDirty = false;
        _backupDirty = false;
        _appSwitchDirty = false;
    }

    private List<DraftEntry> CollectDraftEntries()
    {
        var list = new List<DraftEntry>();
        foreach (var (id, draft) in _printerDrafts)
        {
            var name = string.IsNullOrWhiteSpace(draft.Name)
                ? _host.Config.Printer.Tasks.FirstOrDefault(t => t.Id == id)?.Name ?? id
                : draft.Name;
            list.Add(new DraftEntry(DraftKind.Printer, id, "打印机", name));
        }
        foreach (var (id, draft) in _urlDrafts)
        {
            var name = string.IsNullOrWhiteSpace(draft.Name)
                ? _host.Config.UrlLauncher.Tasks.FirstOrDefault(t => t.Id == id)?.Name ?? id
                : draft.Name;
            list.Add(new DraftEntry(DraftKind.Url, id, "打开链接", name));
        }
        foreach (var (id, draft) in _backupDrafts)
        {
            var name = string.IsNullOrWhiteSpace(draft.Name)
                ? _host.Config.Backup.Tasks.FirstOrDefault(t => t.Id == id)?.Name ?? id
                : draft.Name;
            list.Add(new DraftEntry(DraftKind.Backup, id, "备份", name));
        }
        foreach (var (id, draft) in _appSwitchDrafts)
        {
            var name = string.IsNullOrWhiteSpace(draft.Name)
                ? _host.Config.AppSwitch.Tasks.FirstOrDefault(t => t.Id == id)?.Name ?? id
                : draft.Name;
            list.Add(new DraftEntry(DraftKind.AppSwitch, id, "停止应用", name));
        }
        return list;
    }

    private void NavigateToDraft(DraftEntry draft)
    {
        switch (draft.Kind)
        {
            case DraftKind.Printer:
                MainTabs.SelectedItem = TabSchedule;
                ChkPrinterModule.IsChecked = true;
                _printerRowExpanded = true;
                _urlRowExpanded = false;
                _backupRowExpanded = false;
                UpdateConditionalRows();
                _selectedTaskId = draft.TaskId;
                RefreshPrinterList();
                break;
            case DraftKind.Url:
                MainTabs.SelectedItem = TabSchedule;
                ChkUrlLauncherModule.IsChecked = true;
                _urlRowExpanded = true;
                _printerRowExpanded = false;
                _backupRowExpanded = false;
                UpdateConditionalRows();
                _selectedUrlId = draft.TaskId;
                RefreshUrlList();
                break;
            case DraftKind.Backup:
                MainTabs.SelectedItem = TabSchedule;
                ChkBackupModule.IsChecked = true;
                _backupRowExpanded = true;
                _printerRowExpanded = false;
                _urlRowExpanded = false;
                UpdateConditionalRows();
                _selectedBackupId = draft.TaskId;
                RefreshBackupList();
                break;
            case DraftKind.AppSwitch:
                MainTabs.SelectedItem = TabLeave;
                ChkAppSwitchModule.IsChecked = true;
                _appSwitchRowExpanded = true;
                UpdateConditionalRows();
                _selectedAppSwitchId = draft.TaskId;
                RefreshAppSwitchList();
                break;
        }
        FitWindowToContent();
    }

    private void StashPrinterDraftIfNeeded()
    {
        if (!_printerDirty || string.IsNullOrEmpty(_selectedTaskId)) return;
        _printerDrafts[_selectedTaskId] = CapturePrinterDraft();
    }

    private void StashUrlDraftIfNeeded()
    {
        if (!_urlDirty || string.IsNullOrEmpty(_selectedUrlId)) return;
        _urlDrafts[_selectedUrlId] = CaptureUrlDraft();
    }

    private void StashBackupDraftIfNeeded()
    {
        if (!_backupDirty || string.IsNullOrEmpty(_selectedBackupId)) return;
        _backupDrafts[_selectedBackupId] = CaptureBackupDraft();
    }

    private void StashAppSwitchDraftIfNeeded()
    {
        if (!_appSwitchDirty || string.IsNullOrEmpty(_selectedAppSwitchId)) return;
        _appSwitchDrafts[_selectedAppSwitchId] = CaptureAppSwitchDraft();
    }

    private PrinterTaskDraft CapturePrinterDraft() => new()
    {
        Name = TxtPrinterName.Text,
        ImagePath = TxtImagePath.Text,
        IntervalDays = TxtIntervalDays.Text,
        Hour = TxtHour.Text,
        Minute = TxtMinute.Text,
        PrinterName = CmbPrinter.SelectedItem?.ToString() ?? CmbPrinter.Text ?? "",
        ColorMode = BtnColorMode.Tag?.ToString() ?? "Color"
    };

    private void ApplyPrinterDraft(PrinterTaskDraft draft)
    {
        _printerUiLoading = true;
        TxtPrinterName.Text = draft.Name;
        TxtImagePath.Text = draft.ImagePath;
        TxtIntervalDays.Text = draft.IntervalDays;
        TxtHour.Text = draft.Hour;
        TxtMinute.Text = draft.Minute;
        if (!string.IsNullOrWhiteSpace(draft.PrinterName)
            && !CmbPrinter.Items.Cast<object>().Any(x => string.Equals(x.ToString(), draft.PrinterName, StringComparison.OrdinalIgnoreCase)))
            CmbPrinter.Items.Add(draft.PrinterName);
        CmbPrinter.SelectedItem = CmbPrinter.Items.Cast<object>()
            .FirstOrDefault(x => string.Equals(x.ToString(), draft.PrinterName, StringComparison.OrdinalIgnoreCase));
        var grayscale = string.Equals(draft.ColorMode, "Grayscale", StringComparison.OrdinalIgnoreCase);
        BtnColorMode.Tag = grayscale ? "Grayscale" : "Color";
        BtnColorMode.Content = grayscale ? "灰度" : "彩色";
        _printerUiLoading = false;
        SetPrinterDirty(true);
    }

    private UrlTaskDraft CaptureUrlDraft() => new()
    {
        Name = TxtUrlName.Text,
        Url = TxtUrlAddress.Text,
        BrowserPath = TxtUrlBrowserPath.Text,
        AutoClose = (CmbUrlAutoClose.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "False",
        CloseDelay = TxtUrlCloseDelay.Text,
        IntervalDays = TxtUrlIntervalDays.Text,
        Hour = TxtUrlHour.Text,
        Minute = TxtUrlMinute.Text
    };

    private void ApplyUrlDraft(UrlTaskDraft draft)
    {
        _urlUiLoading = true;
        TxtUrlName.Text = draft.Name;
        TxtUrlAddress.Text = draft.Url;
        TxtUrlBrowserPath.Text = draft.BrowserPath;
        SelectComboByTag(CmbUrlAutoClose, draft.AutoClose);
        TxtUrlCloseDelay.Text = draft.CloseDelay;
        TxtUrlIntervalDays.Text = draft.IntervalDays;
        TxtUrlHour.Text = draft.Hour;
        TxtUrlMinute.Text = draft.Minute;
        UpdateUrlCloseControls();
        _urlUiLoading = false;
        SetUrlDirty(true);
    }

    private BackupTaskDraft CaptureBackupDraft() => new()
    {
        Name = TxtBackupName.Text,
        SrcKind = (CmbBackupSrcKind.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Local",
        DstKind = (CmbBackupDstKind.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Local",
        SrcPath = CmbBackupSrcPath.Text ?? "",
        DstPath = CmbBackupDstPath.Text ?? "",
        Mode = (CmbBackupMode.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Copy",
        Conflict = (CmbBackupConflict.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Skip",
        CopyTrash = TxtCopyTrashPath.Text ?? "",
        TrashA = TxtTrashPathA.Text ?? "",
        TrashB = TxtTrashPathB.Text ?? "",
        Exclude = TxtBackupExclude.Text ?? "",
        ScheduleMode = GetBackupScheduleMode(),
        IntervalDays = TxtBackupInterval.Text,
        Hour = TxtBackupHour.Text,
        Minute = TxtBackupMinute.Text
    };

    private void ApplyBackupDraft(BackupTaskDraft draft)
    {
        _backupUiLoading = true;
        TxtBackupName.Text = draft.Name;
        FillEndpointCombo(CmbBackupSrcKind);
        FillEndpointCombo(CmbBackupDstKind);
        SelectComboByTag(CmbBackupSrcKind, draft.SrcKind);
        SelectComboByTag(CmbBackupDstKind, draft.DstKind);
        SelectComboByTag(CmbBackupMode, draft.Mode);
        SelectComboByTag(CmbBackupConflict, draft.Conflict);
        CmbBackupSrcPath.Text = draft.SrcPath;
        CmbBackupDstPath.Text = draft.DstPath;
        TxtCopyTrashPath.Text = draft.CopyTrash;
        TxtTrashPathA.Text = draft.TrashA;
        TxtTrashPathB.Text = draft.TrashB;
        TxtBackupExclude.Text = draft.Exclude;
        _backupScheduleMode = string.Equals(draft.ScheduleMode, "Realtime", StringComparison.OrdinalIgnoreCase)
            ? "Realtime" : "Planned";
        BtnScheduleRealtime.IsChecked = string.Equals(_backupScheduleMode, "Realtime", StringComparison.OrdinalIgnoreCase);
        BtnSchedulePlanned.IsChecked = string.Equals(_backupScheduleMode, "Planned", StringComparison.OrdinalIgnoreCase);
        TxtBackupInterval.Text = draft.IntervalDays;
        TxtBackupHour.Text = draft.Hour;
        TxtBackupMinute.Text = draft.Minute;
        UpdateBackupPathHints();
        UpdateBackupModeRows();
        _backupUiLoading = false;
        SetBackupDirty(true);
    }

    private AppSwitchTaskDraft CaptureAppSwitchDraft() => new()
    {
        Name = TxtAppName.Text,
        ProcessName = _appSwitchProcessName,
        LaunchPath = TxtAppLaunch.Text ?? "",
        BindMode = _appSwitchBindMode == AppSwitchBindMode.Path ? "Path" : "Select",
        RestartOnReturn = (CmbAppRestart.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "True",
        StopAfter = TxtAppStopAfter.Text
    };

    private void ApplyAppSwitchDraft(AppSwitchTaskDraft draft)
    {
        _appSwitchUiLoading = true;
        TxtAppName.Text = draft.Name;
        _appSwitchProcessName = draft.ProcessName;
        _appSwitchBindMode = string.Equals(draft.BindMode, "Path", StringComparison.OrdinalIgnoreCase)
            ? AppSwitchBindMode.Path
            : AppSwitchBindMode.Select;
        TxtAppLaunch.Text = draft.LaunchPath;
        SelectComboByTag(CmbAppRestart, draft.RestartOnReturn);
        TxtAppStopAfter.Text = draft.StopAfter;
        ApplyAppSwitchModeUi();
        if (_appSwitchBindMode == AppSwitchBindMode.Select)
            SetAppProcessText(draft.ProcessName, deferRewrite: false);
        else
            RefreshAppBoundFromPath(markDirty: false);
        _appSwitchUiLoading = false;
        SetAppSwitchDirty(true);
    }

    private enum DraftKind { Printer, Url, Backup, AppSwitch }

    private sealed record DraftEntry(DraftKind Kind, string TaskId, string Category, string TaskName);

    private sealed class BackupProgressViewItem
    {
        // ProgressBar.Value 默认 TwoWay；属性必须可写，避免布局循环抛异常卡死 UI
        public string RunId { get; set; } = "";
        public string HeaderText { get; set; } = "";
        public string TimeText { get; set; } = "";
        public string ActionText { get; set; } = "";
        public bool ActionEnabled { get; set; }
        public bool IsIndeterminate { get; set; }
        public int Percent { get; set; }

        public BackupProgressViewItem(BackupProgress progress)
        {
            RunId = progress.RunId;
            HeaderText = progress.HeaderText;
            TimeText = progress.TimeText;
            ActionText = progress.IsActive ? "取消" : "清除";
            ActionEnabled = progress.Phase != "Cancelling";
            IsIndeterminate = progress.Phase == "Scanning";
            Percent = progress.Phase == "Done" ? 100 : progress.Percent;
        }
    }

    private sealed class PrinterTaskDraft
    {
        public string Name { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public string IntervalDays { get; set; } = "7";
        public string Hour { get; set; } = "6";
        public string Minute { get; set; } = "00";
        public string PrinterName { get; set; } = "";
        public string ColorMode { get; set; } = "Color";
    }

    private sealed class UrlTaskDraft
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string BrowserPath { get; set; } = "";
        public string AutoClose { get; set; } = "False";
        public string CloseDelay { get; set; } = "60";
        public string IntervalDays { get; set; } = "1";
        public string Hour { get; set; } = "8";
        public string Minute { get; set; } = "00";
    }

    private sealed class BackupTaskDraft
    {
        public string Name { get; set; } = "";
        public string SrcKind { get; set; } = "Local";
        public string DstKind { get; set; } = "Local";
        public string SrcPath { get; set; } = "";
        public string DstPath { get; set; } = "";
        public string Mode { get; set; } = "Copy";
        public string Conflict { get; set; } = "Skip";
        public string CopyTrash { get; set; } = "";
        public string TrashA { get; set; } = "";
        public string TrashB { get; set; } = "";
        public string Exclude { get; set; } = "";
        public string ScheduleMode { get; set; } = "Planned";
        public string IntervalDays { get; set; } = "1";
        public string Hour { get; set; } = "3";
        public string Minute { get; set; } = "00";
    }

    private sealed class AppSwitchTaskDraft
    {
        public string Name { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public string LaunchPath { get; set; } = "";
        public string BindMode { get; set; } = "Select";
        public string RestartOnReturn { get; set; } = "True";
        public string StopAfter { get; set; } = "900";
    }
}
