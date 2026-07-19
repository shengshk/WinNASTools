using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WinNASTools.Core.Contracts;
using WinNASTools.Core.Native;
using WinNASTools.Core.Services;
using Timer = System.Threading.Timer;

namespace WinNASTools.Core.Features;

/// <summary>按间隔天数和时刻打开 HTTP/HTTPS 链接，可在等待后关闭该浏览器的全部进程。</summary>
public sealed class UrlLauncherFeature : IWinNASToolsFeature
{
    private static readonly TimeSpan GracefulCloseTimeout = TimeSpan.FromSeconds(8);
    private IFeatureContext? _ctx;
    private Timer? _timer;
    private readonly object _gate = new();
    private bool _runningCheck;
    private CancellationTokenSource? _disposeCts;

    public string Id => "url-launcher";
    public string DisplayName => "定时打开链接";
    public bool IsEnabled { get; set; } = true;

    public void Initialize(IFeatureContext context)
    {
        _ctx = context;
        _disposeCts?.Dispose();
        _disposeCts = new CancellationTokenSource();
        EnsureTaskDefaults();
        _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));
    }

    public void OnIdleTick(IdleSnapshot snapshot) { }
    public void ForceTrigger() { }
    public void OnStateChanged(AppState from, AppState to) { }

    public void RefreshSchedule()
    {
        if (_ctx is null) return;
        EnsureTaskDefaults();
        _ctx.PersistConfig();
    }

    public async Task RunTaskNowAsync(string taskId)
    {
        if (_ctx is null) return;
        var task = _ctx.Config.UrlLauncher.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return;

        await ExecuteTaskAsync(task).ConfigureAwait(false);
        task.LastRunLocal = DateTime.Now;
        task.LastResult = "手动打开成功";
        _ctx.PersistConfig();
    }

    private void EnsureTaskDefaults()
    {
        if (_ctx is null) return;
        var now = DateTime.Now;
        var changed = false;
        foreach (var task in _ctx.Config.UrlLauncher.Tasks)
        {
            if (task.NextDueLocal is not null) continue;
            task.NextDueLocal = IntervalSchedule.ComputeInitialNextDue(
                task.IntervalDays, task.Hour, task.Minute, now);
            changed = true;
        }
        if (changed) _ctx.PersistConfig();
    }

    private void Tick()
    {
        if (_ctx is null || !IsEnabled || !_ctx.Config.UrlLauncher.Enabled) return;
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
            _ctx?.Log.Error($"定时打开链接调度异常: {ex.Message}");
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
        var dirty = false;

        foreach (var task in _ctx.Config.UrlLauncher.Tasks)
        {
            if (!task.Enabled || string.IsNullOrWhiteSpace(task.Url)) continue;
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
                    _ctx.Log.Info($"打开链接「{task.Name}」：错过计划已跳过 → 下次 {due:yyyy-MM-dd HH:mm}");
                }
                continue;
            }

            try
            {
                await ExecuteTaskAsync(task).ConfigureAwait(false);
                task.LastRunLocal = now;
                task.LastResult = "成功";
                task.NextDueLocal = IntervalSchedule.AdvanceAfterSuccess(
                    due, task.IntervalDays, task.Hour, task.Minute);
                dirty = true;
                _ctx.Log.Info($"打开链接「{task.Name}」：执行成功，下次 {task.NextDueLocal:yyyy-MM-dd HH:mm}");
            }
            catch (Exception ex)
            {
                task.LastResult = ex.Message;
                task.NextDueLocal = IntervalSchedule.AdvanceAfterSuccess(
                    due, task.IntervalDays, task.Hour, task.Minute);
                dirty = true;
                _ctx.Log.Error($"打开链接「{task.Name}」失败: {ex.Message}；已改期到 {task.NextDueLocal:yyyy-MM-dd HH:mm}");
            }
        }

        if (dirty) _ctx.PersistConfig();
    }

    private Task ExecuteTaskAsync(UrlLauncherTaskConfig task)
    {
        if (_ctx is null) return Task.CompletedTask;
        if (!Uri.TryCreate(task.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("链接必须是有效的 HTTP/HTTPS 地址。");

        var configuredPath = task.BrowserPath?.Trim() ?? "";
        var browserPath = string.IsNullOrEmpty(configuredPath)
            ? TryResolveDefaultBrowserPath(uri.Scheme)
            : configuredPath;

        Process? started;
        if (string.IsNullOrEmpty(configuredPath))
        {
            started = Process.Start(new ProcessStartInfo
            {
                FileName = task.Url,
                UseShellExecute = true
            });
        }
        else
        {
            if (!File.Exists(configuredPath))
                throw new FileNotFoundException("浏览器程序不存在。", configuredPath);
            var psi = new ProcessStartInfo
            {
                FileName = configuredPath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(configuredPath) ?? ""
            };
            psi.ArgumentList.Add(task.Url);
            started = Process.Start(psi);
        }

        string? processName = null;
        if (!string.IsNullOrWhiteSpace(browserPath))
            processName = Path.GetFileNameWithoutExtension(browserPath);
        if (string.IsNullOrWhiteSpace(processName) && started is not null)
        {
            try { processName = started.ProcessName; }
            catch { /* shell broker may exit immediately */ }
        }
        started?.Dispose();

        _ctx.Log.Info($"打开链接「{task.Name}」：{task.Url}");
        if (task.AutoCloseBrowser)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                _ctx.Log.Warn($"打开链接「{task.Name}」：无法识别默认浏览器进程，跳过自动关闭。");
            }
            else
            {
                _ = CloseAfterDelayAsync(
                    task.Name,
                    processName,
                    Math.Max(0, task.CloseDelaySeconds),
                    _disposeCts?.Token ?? CancellationToken.None);
            }
        }
        return Task.CompletedTask;
    }

    private async Task CloseAfterDelayAsync(
        string taskName,
        string processName,
        int delaySeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
            await CloseAllByNameAsync(processName, cancellationToken).ConfigureAwait(false);
            _ctx?.Log.Info($"打开链接「{taskName}」：等待 {delaySeconds}s 后已关闭浏览器 {processName}。");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _ctx?.Log.Error($"打开链接「{taskName}」自动关闭浏览器失败: {ex.Message}");
        }
    }

    private static async Task CloseAllByNameAsync(string processName, CancellationToken cancellationToken)
    {
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0) return;
        try
        {
            var pids = processes.Select(p => p.Id).ToHashSet();
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
                if (pids.Contains((int)pid))
                    NativeMethods.PostMessage(hWnd, NativeMethods.WmClose, IntPtr.Zero, IntPtr.Zero);
                return true;
            }, IntPtr.Zero);

            var deadline = DateTime.UtcNow + GracefulCloseTimeout;
            while (DateTime.UtcNow < deadline && processes.Any(IsStillRunning))
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }

            foreach (var process in processes)
            {
                if (!IsStillRunning(process)) continue;
                process.Kill(entireProcessTree: true);
            }
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static bool IsStillRunning(Process process)
    {
        try { return !process.HasExited; }
        catch { return false; }
    }

    private static string? TryResolveDefaultBrowserPath(string scheme)
    {
        var length = 1024u;
        var buffer = new StringBuilder((int)length);
        var result = AssocQueryString(0, 2, scheme, null, buffer, ref length);
        return result == 0 && buffer.Length > 0 ? buffer.ToString() : null;
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(
        uint flags,
        uint str,
        string pszAssoc,
        string? pszExtra,
        StringBuilder pszOut,
        ref uint pcchOut);

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        _disposeCts?.Cancel();
        _disposeCts?.Dispose();
        _disposeCts = null;
    }
}
