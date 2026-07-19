using WinNASTools.Core.Contracts;
using WinNASTools.Core.Services;
using Timer = System.Threading.Timer;

namespace WinNASTools.Core.Features;

/// <summary>打印机维护：定时打印测试图，与离开/空闲无关。</summary>
public sealed class PrinterMaintenanceFeature : IWinNASToolsFeature
{
    private IFeatureContext? _ctx;
    private Timer? _timer;
    private readonly object _gate = new();
    private bool _runningCheck;

    public string Id => "printer";
    public string DisplayName => "打印机维护";
    public bool IsEnabled { get; set; } = true;

    public void Initialize(IFeatureContext context)
    {
        _ctx = context;
        EnsureTaskDefaults();
        _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
    }

    public void OnIdleTick(IdleSnapshot snapshot) { /* 时钟驱动，不依赖空闲 */ }

    public void ForceTrigger() { /* 一键离开不触发打印 */ }

    public void OnStateChanged(AppState from, AppState to) { }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>配置重载后校正 NextDue。</summary>
    public void RefreshSchedule()
    {
        if (_ctx is null) return;
        EnsureTaskDefaults();
        _ctx.PersistConfig();
    }

    private void EnsureTaskDefaults()
    {
        if (_ctx is null) return;
        var now = DateTime.Now;
        var changed = false;
        foreach (var task in _ctx.Config.Printer.Tasks)
        {
            if (task.NextDueLocal is null)
            {
                task.NextDueLocal = IntervalSchedule.ComputeInitialNextDue(
                    task.IntervalDays, task.Hour, task.Minute, now);
                changed = true;
            }
        }
        if (changed)
            _ctx.PersistConfig();
    }

    private void Tick()
    {
        if (_ctx is null || !IsEnabled) return;
        if (!_ctx.Config.Printer.Enabled) return;

        lock (_gate)
        {
            if (_runningCheck) return;
            _runningCheck = true;
        }

        _ = RunDueTasksGuardedAsync();
    }

    private async Task RunDueTasksGuardedAsync()
    {
        try
        {
            await RunDueTasksAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _ctx?.Log.Error($"打印机维护调度异常: {ex.Message}");
        }
        finally
        {
            lock (_gate) _runningCheck = false;
        }
    }

    private async Task RunDueTasksAsync()
    {
        if (_ctx is null) return;
        var now = DateTime.Now;
        var cfg = _ctx.Config;
        var dirty = false;

        foreach (var task in cfg.Printer.Tasks)
        {
            if (!task.Enabled) continue;
            if (string.IsNullOrWhiteSpace(task.PrinterName)) continue;

            task.NextDueLocal ??= IntervalSchedule.ComputeInitialNextDue(
                task.IntervalDays, task.Hour, task.Minute, now);

            var due = task.NextDueLocal.Value;
            if (!IntervalSchedule.TryEvaluate(
                    ref due, now, task.IntervalDays, task.Hour, task.Minute, out var skipped))
            {
                if (skipped)
                {
                    task.NextDueLocal = due;
                    task.LastResult = $"已跳过错过的计划，下次 {due:yyyy-MM-dd HH:mm}";
                    dirty = true;
                    _ctx.Log.Info($"打印机「{task.Name}」：错过计划已跳过 → 下次 {due:yyyy-MM-dd HH:mm}");
                }
                continue;
            }

            try
            {
                var path = PrinterService.ResolveImagePath(task.ImagePath);
                var color = !string.Equals(task.ColorMode, "Grayscale", StringComparison.OrdinalIgnoreCase);
                await PrinterService.PrintImageAsync(task.PrinterName, path, color)
                    .ConfigureAwait(false);

                task.LastRunLocal = now;
                task.LastResult = "成功";
                task.NextDueLocal = IntervalSchedule.AdvanceAfterSuccess(
                    due, task.IntervalDays, task.Hour, task.Minute);
                dirty = true;
                _ctx.Log.Info($"打印机「{task.Name}」：已发送打印 → {task.PrinterName}，下次 {task.NextDueLocal:yyyy-MM-dd HH:mm}");
            }
            catch (Exception ex)
            {
                task.LastResult = ex.Message;
                // 失败也推进，避免每 30 秒狂打；用户可手动改 NextDue
                task.NextDueLocal = IntervalSchedule.AdvanceAfterSuccess(
                    due, task.IntervalDays, task.Hour, task.Minute);
                dirty = true;
                _ctx.Log.Error($"打印机「{task.Name}」失败: {ex.Message}；已改期到 {task.NextDueLocal:yyyy-MM-dd HH:mm}");
            }
        }

        if (dirty)
        {
            _ctx.PersistConfig();
            // 通知 UI 刷新（若有）
        }
    }
}
