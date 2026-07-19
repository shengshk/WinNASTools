using WinNASTools.Core.Backup;
using WinNASTools.Core.Contracts;
using WinNASTools.Core.Localization;
using WinNASTools.Core.Services;
using System.Text.Json;
using Timer = System.Threading.Timer;

namespace WinNASTools.Core.Features;

public sealed class BackupFeature : IWinNASToolsFeature
{
    private IFeatureContext? _ctx;
    private Timer? _timer;
    private readonly object _gate = new();
    private bool _runningCheck;
    private readonly HashSet<string> _runningTasks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CancellationTokenSource> _cancelTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _realtimeDue = new(StringComparer.Ordinal);
    private readonly object _watchGate = new();
    private readonly object _progressGate = new();
    private readonly List<BackupProgress> _history = new();
    private BackupProgress? _latestProgress;
    private static readonly TimeSpan HistoryRetention = TimeSpan.FromHours(24);
    private static string HistoryPath =>
        Path.Combine(AppPaths.DataDirectory, "backup-run-history.json");
    private static readonly TimeSpan RealtimeDebounce = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RealtimeHostPoll = TimeSpan.FromSeconds(45);

    public string Id => "backup";
    public string DisplayName => Loc.T("Feature.Backup");
    public bool IsEnabled { get; set; } = true;

    /// <summary>当前展示用进度；null 表示无活动备份。</summary>
    public BackupProgress? LatestProgress
    {
        get { lock (_progressGate) return _latestProgress; }
    }

    public IReadOnlyList<BackupProgress> History
    {
        get
        {
            lock (_progressGate)
            {
                var count = _history.Count;
                PruneHistoryLocked();
                if (_history.Count != count)
                    SaveHistoryLocked();
                return _history
                    .OrderByDescending(x => x.StartedAtLocal)
                    .Select(CloneProgress)
                    .ToList();
            }
        }
    }

    public int RunningCount
    {
        get { lock (_runningTasks) return _runningTasks.Count; }
    }

    public event Action<BackupProgress?>? ProgressChanged;

    public bool IsTaskRunning(string taskId)
    {
        lock (_runningTasks) return _runningTasks.Contains(taskId);
    }

    public void CancelTask(string taskId)
    {
        lock (_cancelTokens)
        {
            if (!_cancelTokens.TryGetValue(taskId, out var cts)) return;
            try { cts.Cancel(); } catch { /* ignore */ }
        }

        BackupProgress? cur;
        lock (_progressGate)
            cur = _history.FirstOrDefault(x => x.TaskId == taskId && x.IsActive);
        var name = _ctx?.Config.Backup.Tasks.FirstOrDefault(t => t.Id == taskId)?.Name
                   ?? cur?.TaskName
                   ?? Loc.T("Tray.Backup.DefaultName");
        PublishProgress(new BackupProgress
        {
            RunId = cur?.RunId ?? Guid.NewGuid().ToString("N"),
            TaskId = taskId,
            TaskName = name,
            Phase = "Cancelling",
            Current = cur?.Current ?? 0,
            Total = cur?.Total ?? 0,
            StartedAtLocal = cur?.StartedAtLocal ?? DateTime.Now
        });
    }

    public void ClearHistoryRecord(string runId)
    {
        lock (_progressGate)
        {
            var item = _history.FirstOrDefault(x => x.RunId == runId);
            if (item is null || item.IsActive) return;
            _history.Remove(item);
            SaveHistoryLocked();
        }
        try { ProgressChanged?.Invoke(null); } catch { /* ignore */ }
    }

    public void CancelAll()
    {
        lock (_cancelTokens)
        {
            foreach (var kv in _cancelTokens.ToList())
            {
                try { kv.Value.Cancel(); } catch { /* ignore */ }
            }
        }
    }

    public void Initialize(IFeatureContext context)
    {
        _ctx = context;
        LoadHistory();
        EnsureTaskDefaults();
        RebuildRealtimeWatchers();
        _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(15));
    }

    public void OnIdleTick(IdleSnapshot snapshot) { }
    public void ForceTrigger() { }
    public void OnStateChanged(AppState from, AppState to) { }

    public void Dispose()
    {
        CancelAll();
        _timer?.Dispose();
        _timer = null;
        ClearWatchers();
    }

    public void RefreshSchedule()
    {
        if (_ctx is null) return;
        EnsureTaskDefaults();
        RebuildRealtimeWatchers();
        _ctx.PersistConfig();
    }

    public async Task RunTaskNowAsync(string taskId)
    {
        if (_ctx is null) return;
        var task = _ctx.Config.Backup.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return;
        await ExecuteTaskAsync(task, manual: true).ConfigureAwait(false);
    }

    private void EnsureTaskDefaults()
    {
        if (_ctx is null) return;
        var now = DateTime.Now;
        var changed = false;
        foreach (var task in _ctx.Config.Backup.Tasks)
        {
            if (string.IsNullOrWhiteSpace(task.ScheduleMode))
            {
                task.ScheduleMode = "Planned";
                changed = true;
            }

            if (IsPlanned(task) && task.NextDueLocal is null)
            {
                task.NextDueLocal = IntervalSchedule.ComputeInitialNextDue(
                    task.IntervalDays, task.Hour, task.Minute, now);
                changed = true;
            }
        }
        if (changed) _ctx.PersistConfig();
    }

    private static bool IsPlanned(BackupTaskConfig task)
        => !string.Equals(task.ScheduleMode, "Realtime", StringComparison.OrdinalIgnoreCase);

    private static bool IsRealtime(BackupTaskConfig task)
        => string.Equals(task.ScheduleMode, "Realtime", StringComparison.OrdinalIgnoreCase);

    private void Tick()
    {
        PruneExpiredHistory();
        if (_ctx is null || !IsEnabled) return;
        if (!_ctx.Config.Backup.Enabled) return;

        lock (_gate)
        {
            if (_runningCheck) return;
            _runningCheck = true;
        }

        try
        {
            _ = RunDueTasksAsync();
        }
        finally
        {
        }
    }

    private async Task RunDueTasksAsync()
    {
        try
        {
            if (_ctx is null) return;
            var now = DateTime.Now;
            var cfg = _ctx.Config;
            var dirty = false;

            foreach (var task in cfg.Backup.Tasks.ToList())
            {
                if (!task.Enabled) continue;

                if (IsRealtime(task))
                {
                    // 本机：靠 FileSystemWatcher 入队；主机：轮询触发
                    if (IsLocalEndpoint(task.Source))
                    {
                        DateTime due;
                        lock (_watchGate)
                        {
                            if (!_realtimeDue.TryGetValue(task.Id, out due) || due > now)
                                continue;
                            _realtimeDue.Remove(task.Id);
                        }
                        await ExecuteTaskAsync(task, manual: false).ConfigureAwait(false);
                        dirty = true;
                    }
                    else
                    {
                        // 主机源：按间隔轮询（用 NextDueLocal 存下次轮询点）
                        task.NextDueLocal ??= now;
                        if (task.NextDueLocal > now) continue;
                        await ExecuteTaskAsync(task, manual: false).ConfigureAwait(false);
                        task.NextDueLocal = now.Add(RealtimeHostPoll);
                        dirty = true;
                    }
                    continue;
                }

                task.NextDueLocal ??= IntervalSchedule.ComputeInitialNextDue(
                    task.IntervalDays, task.Hour, task.Minute, now);

                var duePlan = task.NextDueLocal.Value;
                if (!IntervalSchedule.TryEvaluate(
                        ref duePlan, now, task.IntervalDays, task.Hour, task.Minute, out var skipped))
                {
                    if (skipped)
                    {
                        task.NextDueLocal = duePlan;
                        task.LastResult = $"已跳过错过的计划，下次 {duePlan:yyyy-MM-dd HH:mm}";
                        dirty = true;
                        _ctx.Log.Info(Loc.T("Log.Backup.MissedSkipped", task.Name, duePlan.ToString("yyyy-MM-dd HH:mm")));
                    }
                    continue;
                }

                await ExecuteTaskAsync(task, manual: false).ConfigureAwait(false);
                dirty = true;
            }

            if (dirty) _ctx.PersistConfig();
        }
        catch (Exception ex)
        {
            _ctx?.Log.Error(Loc.T("Log.Backup.ScheduleError", ex.Message));
        }
        finally
        {
            lock (_gate) _runningCheck = false;
        }
    }

    private static bool IsLocalEndpoint(BackupEndpointConfig ep)
        => string.Equals(ep.Kind, "Local", StringComparison.OrdinalIgnoreCase);

    private void RebuildRealtimeWatchers()
    {
        if (_ctx is null) return;
        ClearWatchers();

        foreach (var task in _ctx.Config.Backup.Tasks)
        {
            if (!task.Enabled || !IsRealtime(task) || !IsLocalEndpoint(task.Source))
                continue;
            var path = task.Source.PathOrUrl?.Trim() ?? "";
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                continue;

            try
            {
                var w = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                                   | NotifyFilters.DirectoryName
                                   | NotifyFilters.LastWrite
                                   | NotifyFilters.Size
                                   | NotifyFilters.CreationTime
                };
                var id = task.Id;
                void OnChange(object _, FileSystemEventArgs __) => ArmRealtime(id);
                void OnRename(object _, RenamedEventArgs __) => ArmRealtime(id);
                w.Changed += OnChange;
                w.Created += OnChange;
                w.Deleted += OnChange;
                w.Renamed += OnRename;
                w.Error += (_, args) =>
                {
                    _ctx?.Log.Warn(Loc.T("Log.Backup.WatchOverflow", task.Name, args.GetException().Message));
                    try { RebuildRealtimeWatchers(); } catch { /* ignore */ }
                };
                w.EnableRaisingEvents = true;
                _watchers[id] = w;
            }
            catch (Exception ex)
            {
                _ctx.Log.Warn(Loc.T("Log.Backup.WatchStartFailed", task.Name, ex.Message));
            }
        }
    }

    private void ArmRealtime(string taskId)
    {
        lock (_watchGate)
            _realtimeDue[taskId] = DateTime.Now.Add(RealtimeDebounce);
    }

    private void ClearWatchers()
    {
        foreach (var w in _watchers.Values)
        {
            try
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            catch { /* ignore */ }
        }
        _watchers.Clear();
        lock (_watchGate) _realtimeDue.Clear();
    }

    private async Task ExecuteTaskAsync(BackupTaskConfig task, bool manual)
    {
        if (_ctx is null) return;
        lock (_runningTasks)
        {
            if (!_runningTasks.Add(task.Id))
            {
                // 运行中又有实时变更：重新入队，避免丢触发。
                if (IsRealtime(task))
                    ArmRealtime(task.Id);
                _ctx.Log.Warn(Loc.T("Log.Backup.AlreadyRunning", task.Name));
                return;
            }
        }

        var userCts = new CancellationTokenSource();
        lock (_cancelTokens)
            _cancelTokens[task.Id] = userCts;

        var trigger = manual ? Loc.T("Log.Backup.Trigger.Manual")
            : IsRealtime(task) ? Loc.T("Log.Backup.Trigger.Realtime")
            : Loc.T("Log.Backup.Trigger.Planned");
        var runId = Guid.NewGuid().ToString("N");
        var startedAt = DateTime.Now;
        try
        {
            _ctx.Log.Info(Loc.T("Log.Backup.Started", task.Name, trigger));
            PublishProgress(new BackupProgress
            {
                RunId = runId,
                TaskId = task.Id,
                TaskName = task.Name,
                Phase = "Scanning",
                Current = 0,
                Total = 0,
                StartedAtLocal = startedAt
            });

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromHours(6));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(userCts.Token, timeoutCts.Token);
            var progress = new CallbackProgress<BackupProgress>(p =>
            {
                p.RunId = runId;
                p.TaskId = task.Id;
                p.TaskName = task.Name;
                p.StartedAtLocal = startedAt;
                PublishProgress(p);
            });
            var result = await BackupSyncEngine.RunAsync(
                task, _ctx.Config.Backup.Hosts, _ctx.Log, linked.Token, progress).ConfigureAwait(false);
            var now = DateTime.Now;
            task.LastRunLocal = now;
            task.LastResult = $"[{trigger}] {result.Summary}";
            if (IsPlanned(task))
            {
                var due = task.NextDueLocal ?? now;
                task.NextDueLocal = IntervalSchedule.AdvanceAfterSuccess(
                    manual ? now : due, task.IntervalDays, task.Hour, task.Minute);
                _ctx.Log.Info(Loc.T("Log.Backup.CompletedWithNext", task.Name, trigger, result.Summary, task.NextDueLocal.Value.ToString("yyyy-MM-dd HH:mm")));
            }
            else
            {
                if (!IsLocalEndpoint(task.Source))
                    task.NextDueLocal = now.Add(RealtimeHostPoll);
                _ctx.Log.Info(Loc.T("Log.Backup.Completed", task.Name, trigger, result.Summary));
            }
            _ctx.PersistConfig();
            BackupProgress? last;
            lock (_progressGate)
                last = _history.FirstOrDefault(x => x.RunId == runId);
            PublishProgress(new BackupProgress
            {
                RunId = runId,
                TaskId = task.Id,
                TaskName = task.Name,
                Phase = "Done",
                Current = last?.Current ?? 1,
                Total = last?.Total ?? 1,
                StartedAtLocal = startedAt,
                EndedAtLocal = DateTime.Now
            });
        }
        catch (OperationCanceledException)
        {
            task.LastResult = $"[{trigger}] {Loc.T("Tray.Backup.Cancelled")}";
            _ctx.Log.Warn(Loc.T("Log.Backup.Cancelled", task.Name, trigger));
            if (IsPlanned(task))
            {
                var due = task.NextDueLocal ?? DateTime.Now;
                task.NextDueLocal = IntervalSchedule.AdvanceAfterSuccess(
                    due, task.IntervalDays, task.Hour, task.Minute);
            }
            _ctx.PersistConfig();
            BackupProgress? last;
            lock (_progressGate)
                last = _history.FirstOrDefault(x => x.RunId == runId);
            PublishProgress(new BackupProgress
            {
                RunId = runId,
                TaskId = task.Id,
                TaskName = task.Name,
                Phase = "Cancelled",
                Current = last?.Current ?? 0,
                Total = last?.Total ?? 0,
                StartedAtLocal = startedAt,
                EndedAtLocal = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            task.LastResult = $"[{trigger}] {ex.Message}";
            if (IsPlanned(task))
            {
                var due = task.NextDueLocal ?? DateTime.Now;
                task.NextDueLocal = IntervalSchedule.AdvanceAfterSuccess(
                    due, task.IntervalDays, task.Hour, task.Minute);
                _ctx.Log.Error(Loc.T("Log.Backup.FailedRescheduled", task.Name, trigger, ex.Message, task.NextDueLocal.Value.ToString("yyyy-MM-dd HH:mm")));
            }
            else
            {
                _ctx.Log.Error(Loc.T("Log.Backup.Failed", task.Name, trigger, ex.Message));
            }
            _ctx.PersistConfig();
            BackupProgress? last;
            lock (_progressGate)
                last = _history.FirstOrDefault(x => x.RunId == runId);
            PublishProgress(new BackupProgress
            {
                RunId = runId,
                TaskId = task.Id,
                TaskName = task.Name,
                Phase = "Failed",
                Current = last?.Current ?? 0,
                Total = last?.Total ?? 0,
                StartedAtLocal = startedAt,
                EndedAtLocal = DateTime.Now
            });
        }
        finally
        {
            lock (_cancelTokens)
            {
                if (_cancelTokens.TryGetValue(task.Id, out var cts) && ReferenceEquals(cts, userCts))
                    _cancelTokens.Remove(task.Id);
            }
            try { userCts.Dispose(); } catch { /* ignore */ }
            lock (_runningTasks) _runningTasks.Remove(task.Id);
            // 结束后不清空历史；仅刷新托盘提示。
            try { ProgressChanged?.Invoke(LatestProgress); } catch { /* ignore */ }
        }
    }

    private void PublishProgress(BackupProgress? progress)
    {
        lock (_progressGate)
        {
            _latestProgress = progress;
            if (progress is not null)
            {
                var old = _history.FirstOrDefault(x => x.RunId == progress.RunId);
                if (old is null)
                    _history.Add(CloneProgress(progress));
                else
                {
                    old.TaskId = progress.TaskId;
                    old.TaskName = progress.TaskName;
                    old.Phase = progress.Phase;
                    old.Current = progress.Current;
                    old.Total = progress.Total;
                    old.StartedAtLocal = progress.StartedAtLocal;
                    old.EndedAtLocal = progress.EndedAtLocal;
                }

                PruneHistoryLocked();
                if (progress.Phase is "Scanning" or "Done" or "Failed" or "Cancelled")
                    SaveHistoryLocked();
            }
        }
        try { ProgressChanged?.Invoke(progress); }
        catch { /* UI 订阅方异常不影响备份 */ }
    }

    private void LoadHistory()
    {
        lock (_progressGate)
        {
            _history.Clear();
            try
            {
                if (File.Exists(HistoryPath))
                {
                    var json = File.ReadAllText(HistoryPath);
                    var records = JsonSerializer.Deserialize<List<BackupProgress>>(json);
                    if (records is not null)
                        _history.AddRange(records.Where(x => !string.IsNullOrWhiteSpace(x.RunId)));
                }
            }
            catch
            {
                // 历史记录损坏不影响备份功能。
            }

            var now = DateTime.Now;
            foreach (var item in _history.Where(x => x.IsActive))
            {
                item.Phase = "Failed";
                item.EndedAtLocal = now;
            }
            PruneHistoryLocked();
            SaveHistoryLocked();
        }
    }

    private void PruneHistoryLocked()
    {
        var cutoff = DateTime.Now - HistoryRetention;
        _history.RemoveAll(x =>
            !x.IsActive && (x.EndedAtLocal ?? x.StartedAtLocal) < cutoff);
    }

    private void PruneExpiredHistory()
    {
        var changed = false;
        lock (_progressGate)
        {
            var count = _history.Count;
            PruneHistoryLocked();
            changed = count != _history.Count;
            if (changed)
                SaveHistoryLocked();
        }
        if (changed)
        {
            try { ProgressChanged?.Invoke(null); } catch { /* ignore */ }
        }
    }

    private void SaveHistoryLocked()
    {
        try
        {
            AppPaths.EnsureDataLayout();
            var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(HistoryPath, json);
        }
        catch
        {
            // 历史持久化失败不应中断备份。
        }
    }

    private static BackupProgress CloneProgress(BackupProgress p) => new()
    {
        RunId = p.RunId,
        TaskId = p.TaskId,
        TaskName = p.TaskName,
        Phase = p.Phase,
        Current = p.Current,
        Total = p.Total,
        StartedAtLocal = p.StartedAtLocal,
        EndedAtLocal = p.EndedAtLocal
    };

    /// <summary>IProgress 的同步实现，确保结束状态不会被排队中的旧进度覆盖。</summary>
    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
