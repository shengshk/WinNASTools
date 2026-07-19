using WinNASTools.Core;
using WinNASTools.Core.Contracts;
using WinNASTools.Core.Engine;
using WinNASTools.Core.Features;
using WinNASTools.Core.Localization;

namespace WinNASTools.Core.Hosting;

public sealed class AppHost : IDisposable, IFeatureContext
{
    private readonly List<IWinNASToolsFeature> _features = new();
    private readonly object _awayGate = new();
    private readonly object _configGate = new();
    private readonly object _tickGate = new();
    private IdleEngine? _engine;
    private SessionLockMonitor? _sessionLock;
    private ILogger _log = new FileLogger(ConfigStore.LogPath);
    private bool _skipAutoCached;
    private DateTime _skipAutoCachedAt = DateTime.MinValue;
    private bool _isAway;
    private bool _isWorkstationLocked;
    private bool _returnPending;
    private bool _shouldRunReturnActions;
    /// <summary>离开态下连续「有活动」的 tick 数；满 2 次（约 2s）才归来，滤单次幽灵输入。</summary>
    private int _returnActivityStreak;
    private const int ReturnActivityTicksRequired = 2;

    public AppConfig Config { get; private set; }
    public IFeatureBus Bus { get; } = new FeatureBus();
    public ILogger Log => _log;
    public AppState CurrentState => _engine?.State ?? AppState.Stopped;
    public double IdleSeconds => _engine?.LastIdleSeconds ?? 0;
    public bool IsRunning => CurrentState != AppState.Stopped;
    public bool IsUserActivitySuppressed => _engine?.IsUserActivitySuppressed ?? false;
    public bool IsAway
    {
        get { lock (_awayGate) return _isAway; }
    }
    public bool IsWorkstationLocked
    {
        get { lock (_awayGate) return _isWorkstationLocked; }
    }
    public bool IsIdleLeaveReturnFrozen
    {
        get { lock (_awayGate) return _isWorkstationLocked; }
    }
    public bool ShouldRunReturnActions
    {
        get { lock (_awayGate) return _shouldRunReturnActions; }
    }

    public bool ShouldSkipAutoIdleTriggers
    {
        get
        {
            if ((DateTime.UtcNow - _skipAutoCachedAt).TotalSeconds < 1)
                return _skipAutoCached;
            _skipAutoCached = SessionGuard.ShouldSkipAutoIdleTriggers();
            _skipAutoCachedAt = DateTime.UtcNow;
            return _skipAutoCached;
        }
    }

    public IReadOnlyList<IWinNASToolsFeature> Features => _features;

    public event Action? StatusChanged;
    public event Action<string>? LogLine;

    public AppHost(AppConfig? config = null)
    {
        Config = config ?? ConfigStore.Load();
        _log = new FileLogger(ConfigStore.LogPath, retentionDays: Config.Logging.RetentionDays);
    }

    public void SetUiLogSink(Action<string> sink)
    {
        LogLine = sink;
        _log = new FileLogger(
            ConfigStore.LogPath,
            line => LogLine?.Invoke(line),
            Config.Logging.RetentionDays);
    }

    public FileLogger? FileLog => _log as FileLogger;

    public void ApplyLogRetention(int days)
    {
        lock (_configGate)
        {
            Config.Logging.RetentionDays = Math.Clamp(days, 1, 3650);
            ConfigStore.Save(Config);
        }
        if (_log is FileLogger fl)
            fl.SetRetentionDays(Config.Logging.RetentionDays);
        else
            _log = new FileLogger(ConfigStore.LogPath, line => LogLine?.Invoke(line), Config.Logging.RetentionDays);
        Log.Info(Loc.T("Log.Config.LogRetentionSet", Config.Logging.RetentionDays));
    }

    public void RegisterDefaults()
    {
        Register(new WindowFeature());
        Register(new PowerFeature());
        Register(new MediaFeature());
        Register(new BrowserFeature());
        Register(new AppSwitchFeature());
        // 锁屏放在离开任务最后，同一轮空闲到点时先做其它动作再锁。
        Register(new LockFeature());
        Register(new UrlLauncherFeature());
        Register(new PrinterMaintenanceFeature());
        Register(new BackupFeature());
    }

    public void Register(IWinNASToolsFeature feature)
    {
        if (_features.Any(f => f.Id == feature.Id))
            throw new InvalidOperationException($"Feature already registered: {feature.Id}");
        _features.Add(feature);
    }

    public void Start()
    {
        if (_engine is not null) return;

        ApplyEnabledFlags();
        _engine = new IdleEngine(Config);
        _engine.IdleTick += OnIdleTick;
        _engine.StateChanged += OnStateChanged;

        _sessionLock = new SessionLockMonitor(OnWorkstationLocked, OnWorkstationUnlocked);
        _sessionLock.Start();

        foreach (var feature in _features)
        {
            try { feature.Initialize(this); }
            catch (Exception ex) { Log.Error(Loc.T("Log.Feature.InitFailed", feature.DisplayName, ex.Message)); }
        }

        _engine.Start();
        Log.Info(Loc.T("Log.Monitor.Started", AppBranding.Name));
        StatusChanged?.Invoke();
    }

    public void Stop()
    {
        if (_engine is null) return;

        _engine.IdleTick -= OnIdleTick;
        _engine.StateChanged -= OnStateChanged;
        _engine.Stop();
        _engine.Dispose();
        _engine = null;

        _sessionLock?.Dispose();
        _sessionLock = null;

        foreach (var feature in _features)
        {
            try { feature.Dispose(); }
            catch { /* ignore */ }
        }

        lock (_awayGate)
        {
            _isAway = false;
            _isWorkstationLocked = false;
            _returnPending = false;
            _shouldRunReturnActions = false;
            _returnActivityStreak = 0;
        }

        Log.Info(Loc.T("Log.Monitor.Stopped", AppBranding.Name));
        StatusChanged?.Invoke();
    }

    public void MarkAway(string reason)
    {
        lock (_awayGate)
        {
            if (_isAway) return;
            _isAway = true;
            _returnActivityStreak = 0;
        }

        // 一键离开与自动离开共用：短时阻止归来。
        if (Config.Modules.LeaveGrace && Config.Leave.Enabled)
        {
            var graceSec = Math.Max(1, Config.Leave.ReturnGraceSeconds);
            SuppressUserActivity(TimeSpan.FromSeconds(graceSec));
            Log.Info(Loc.T("Log.Leave.EnterAwayWithGrace", reason, graceSec));
        }
        else
        {
            Log.Info(Loc.T("Log.Leave.EnterAway", reason));
        }

        StatusChanged?.Invoke();
    }

    /// <summary>一键离开：立刻执行到期动作；短时阻止归来与自动离开共用同一参数。</summary>
    public void LeaveNow()
    {
        if (!IsRunning)
        {
            Log.Warn(Loc.T("Log.LeaveNow.NotRunning"));
            return;
        }

        void Run()
        {
            // 未启用「离开短时阻止归来」时，仍给热键最短保护，避免抬手即归来。
            if (!(Config.Modules.LeaveGrace && Config.Leave.Enabled))
            {
                SuppressUserActivity(TimeSpan.FromSeconds(2));
                Log.Info(Loc.T("Log.LeaveNow.SkipWaitProtected"));
            }
            else
            {
                Log.Info(Loc.T("Log.LeaveNow.SkipWait"));
            }

            MarkAway(Loc.T("Log.Reason.LeaveNow"));
            foreach (var feature in _features)
            {
                if (!feature.IsEnabled) continue;
                try { feature.ForceTrigger(); }
                catch (Exception ex) { Log.Error(Loc.T("Log.LeaveNow.FeatureFailed", feature.DisplayName, ex.Message)); }
            }

            StatusChanged?.Invoke();
        }

        if (_engine is not null)
            _engine.RunExclusive(Run);
        else
            Run();
    }

    public void PersistConfig() => SaveConfigLocked();

    public void SaveConfigLocked(Action<AppConfig>? mutate = null)
    {
        lock (_configGate)
        {
            mutate?.Invoke(Config);
            ConfigStore.Save(Config);
        }
    }

    public void ReloadConfig(AppConfig config)
    {
        string previousPowerMode;
        bool previousPowerEnabled;
        lock (_configGate)
        {
            previousPowerMode = Config.Power.Mode;
            previousPowerEnabled = Config.Modules.Power && Config.Power.Enabled;
            Config = config;
            Config.Logging.RetentionDays = Math.Clamp(Config.Logging.RetentionDays, 1, 3650);
            ConfigStore.Save(config);
        }
        ApplyEnabledFlags();
        _engine?.UpdateConfig(config);
        if (_log is FileLogger fl && fl.RetentionDays != Config.Logging.RetentionDays)
            fl.SetRetentionDays(Config.Logging.RetentionDays);
        if (_features.FirstOrDefault(f => f is PrinterMaintenanceFeature) is PrinterMaintenanceFeature printer)
            printer.RefreshSchedule();
        if (_features.FirstOrDefault(f => f is UrlLauncherFeature) is UrlLauncherFeature urlLauncher)
            urlLauncher.RefreshSchedule();
        if (_features.FirstOrDefault(f => f is BackupFeature) is BackupFeature backup)
            backup.RefreshSchedule();

        var powerEnabled = Config.Modules.Power && Config.Power.Enabled;
        var powerModeChanged = !string.Equals(previousPowerMode, Config.Power.Mode, StringComparison.OrdinalIgnoreCase);
        if (powerEnabled
            && (powerModeChanged || !previousPowerEnabled)
            && _features.FirstOrDefault(f => f is PowerFeature) is PowerFeature power)
        {
            power.ApplyPreferenceNow();
        }

        StatusChanged?.Invoke();
        Log.Info(Loc.T("Log.Config.Saved"));
    }

    public void RequestState(AppState state) => _engine?.RequestState(state);

    public void SuppressUserActivity(TimeSpan duration) =>
        _engine?.SuppressUserActivity(duration);

    private void ApplyEnabledFlags()
    {
        var m = Config.Modules;
        foreach (var f in _features)
        {
            f.IsEnabled = f.Id switch
            {
                "window" => m.Window && Config.Window.Enabled,
                "power" => m.Power && Config.Power.Enabled,
                "media" => m.Media && Config.Media.Enabled,
                "browser" => m.Browser && Config.Browser.Enabled,
                "appswitch" => m.AppSwitch && Config.AppSwitch.Enabled,
                "lock" => m.Lock && Config.Lock.Enabled,
                "url-launcher" => m.UrlLauncher && Config.UrlLauncher.Enabled,
                "printer" => m.Printer && Config.Printer.Enabled,
                "backup" => m.Backup && Config.Backup.Enabled,
                _ => f.IsEnabled
            };
        }
    }

    private void OnWorkstationLocked()
    {
        bool needLeave;
        lock (_awayGate)
        {
            _isWorkstationLocked = true;
            needLeave = !_isAway;
            if (needLeave)
                _isAway = true;
        }

        if (needLeave)
        {
            Log.Info(Loc.T("Log.Leave.LockDetected"));
            if (Config.Leave.RunLeaveOnManualLock && IsRunning)
            {
                Log.Info(Loc.T("Log.Leave.ManualLockRun"));
                // SystemEvents 常落在 UI 线程，离开任务必须丢到后台。
                _ = Task.Run(() =>
                {
                    try
                    {
                        if (_engine is not null)
                        {
                            _engine.RunExclusive(() =>
                            {
                                foreach (var feature in _features)
                                {
                                    if (!feature.IsEnabled) continue;
                                    if (feature.Id == "lock") continue;
                                    try { feature.ForceTrigger(); }
                                    catch (Exception ex) { Log.Error(Loc.T("Log.Leave.ManualLockFeatureFailed", feature.DisplayName, ex.Message)); }
                                }
                            });
                        }
                        else
                        {
                            foreach (var feature in _features)
                            {
                                if (!feature.IsEnabled) continue;
                                if (feature.Id == "lock") continue;
                                try { feature.ForceTrigger(); }
                                catch (Exception ex) { Log.Error(Loc.T("Log.Leave.ManualLockFeatureFailed", feature.DisplayName, ex.Message)); }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(Loc.T("Log.Leave.ManualLockFailed", ex.Message));
                    }
                    finally
                    {
                        try { StatusChanged?.Invoke(); } catch { /* ignore */ }
                    }
                });
            }
        }

        StatusChanged?.Invoke();
    }

    private void OnWorkstationUnlocked()
    {
        lock (_awayGate)
        {
            _isWorkstationLocked = false;
            if (_isAway)
                _returnPending = true;
        }

        if (_features.FirstOrDefault(f => f is LockFeature) is LockFeature lockFeature)
            lockFeature.OnUnlocked();

        Log.Info(Loc.T("Log.Return.Unlocked"));
        // 解锁回调也可能在 UI 线程：归来（电源切换等）放到后台，避免界面未响应。
        _ = Task.Run(() =>
        {
            try
            {
                if (_engine is not null)
                    _engine.RunExclusive(ProcessPendingReturn);
                else
                    ProcessPendingReturn();
            }
            catch (Exception ex)
            {
                Log.Error(Loc.T("Log.Return.UnlockFailed", ex.Message));
            }
            finally
            {
                try { StatusChanged?.Invoke(); } catch { /* ignore */ }
            }
        });
    }

    private void ProcessPendingReturn()
    {
        lock (_tickGate)
        {
            bool run;
            lock (_awayGate)
            {
                run = _returnPending || _isAway;
                if (!run)
                {
                    _shouldRunReturnActions = false;
                    return;
                }

                _returnPending = false;
                _isAway = false;
                _returnActivityStreak = 0;
                _shouldRunReturnActions = true;
            }

            Log.Info(Loc.T("Log.Return.Ended"));
            var snap = new IdleSnapshot(0, DateTime.UtcNow);
            foreach (var feature in _features)
            {
                if (!feature.IsEnabled) continue;
                try { feature.OnIdleTick(snap); }
                catch (Exception ex) { Log.Error(Loc.T("Log.Feature.Error", feature.DisplayName, ex.Message)); }
            }

            lock (_awayGate)
                _shouldRunReturnActions = false;
        }
    }

    private void OnIdleTick(IdleSnapshot snapshot)
    {
        lock (_tickGate)
        {
            // 解锁已在 OnWorkstationUnlocked 里处理归来；此处只处理「未锁屏时的键鼠归来」。
            bool completeReturn;
            lock (_awayGate)
            {
                completeReturn = false;
                _shouldRunReturnActions = false;

                if (_isWorkstationLocked || _returnPending)
                {
                    // 锁屏中冻结；若已排队解锁归来，留给 ProcessPendingReturn。
                    _returnActivityStreak = 0;
                }
                else if (_isAway && !IsUserActivitySuppressed)
                {
                    if (snapshot.IdleSeconds < 1.0)
                    {
                        _returnActivityStreak++;
                        if (_returnActivityStreak >= ReturnActivityTicksRequired)
                        {
                            completeReturn = true;
                            _isAway = false;
                            _returnActivityStreak = 0;
                            _shouldRunReturnActions = true;
                        }
                    }
                    else
                    {
                        _returnActivityStreak = 0;
                    }
                }
            }

            if (completeReturn)
                Log.Info(Loc.T("Log.Return.Ended"));

            foreach (var feature in _features)
            {
                if (!feature.IsEnabled) continue;
                try { feature.OnIdleTick(snapshot); }
                catch (Exception ex) { Log.Error(Loc.T("Log.Feature.Error", feature.DisplayName, ex.Message)); }
            }

            lock (_awayGate)
                _shouldRunReturnActions = false;
        }

        StatusChanged?.Invoke();
    }

    private void OnStateChanged(AppState from, AppState to)
    {
        Log.Info(Loc.T("Log.State.Transition", StateLabel(from), StateLabel(to)));
        foreach (var feature in _features)
        {
            try { feature.OnStateChanged(from, to); }
            catch (Exception ex) { Log.Error(Loc.T("Log.Feature.Error", feature.DisplayName, ex.Message)); }
        }
        StatusChanged?.Invoke();
    }

    private static string StateLabel(AppState state) => state switch
    {
        AppState.Watching => Loc.T("Log.State.Watching"),
        _ => Loc.T("Log.State.Stopped")
    };

    public void Dispose() => Stop();
}
