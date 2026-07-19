using System.Diagnostics;
using WinNASTools.Core.Contracts;
using WinNASTools.Core.Native;

namespace WinNASTools.Core.Features;

/// <summary>自动关闭浏览器：结束指定进程；无「恢复」。</summary>
public sealed class BrowserFeature : IWinNASToolsFeature
{
    private static readonly TimeSpan GracefulCloseTimeout = TimeSpan.FromSeconds(8);
    private IFeatureContext? _ctx;
    private bool _closedByUs;
    private int _closing;

    public string Id => "browser";
    public string DisplayName => "自动关闭浏览器";
    public bool IsEnabled { get; set; }

    public void Initialize(IFeatureContext context) => _ctx = context;

    public void OnIdleTick(IdleSnapshot snapshot)
    {
        if (_ctx is null || !IsEnabled) return;
        if (_ctx.CurrentState == AppState.Stopped) return;
        if (_ctx.IsIdleLeaveReturnFrozen) return;

        var cfg = _ctx.Config.Browser;
        var idle = snapshot.IdleSeconds;

        if (!_closedByUs
            && !_ctx.ShouldSkipAutoIdleTriggers
            && cfg.CloseAfterSeconds > 0
            && idle >= cfg.CloseAfterSeconds)
        {
            _ctx.MarkAway($"空闲 {idle:N0}s");
            DoClose($"空闲 {idle:N0}s");
            return;
        }

        // 用户回来后复位，便于下次空闲再触发
        if (_closedByUs && _ctx.ShouldRunReturnActions)
            _closedByUs = false;
    }

    public void ForceTrigger()
    {
        if (_ctx is null || !IsEnabled) return;
        if (_closedByUs) return;
        _ctx.MarkAway("一键离开");
        DoClose("一键离开");
    }

    public void OnStateChanged(AppState from, AppState to)
    {
        if (to == AppState.Stopped)
            _closedByUs = false;
    }

    public void Dispose() => _closedByUs = false;

    private void DoClose(string reason)
    {
        if (_ctx is null) return;
        if (Interlocked.Exchange(ref _closing, 1) != 0) return;

        try
        {
        var names = ParseNames(_ctx.Config.Browser.ProcessNames);
        if (names.Count == 0)
        {
            _ctx.Log.Warn("浏览器：进程列表为空，跳过。");
            return;
        }

        var processes = new Dictionary<int, Process>();
        foreach (var name in names)
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    if (!processes.TryAdd(p.Id, p))
                        p.Dispose();
                }
            }
            catch (Exception ex)
            {
                _ctx.Log.Error($"浏览器：结束 {name} 失败: {ex.Message}");
            }
        }

        if (processes.Count == 0)
        {
            _ctx.Log.Info($"浏览器：未发现匹配进程（{reason}）。");
            return;
        }

        var matchingPids = processes.Keys.ToHashSet();
        var closeRequests = 0;
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;
            if (NativeMethods.GetWindowTextLength(hWnd) == 0) return true;
            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            if (!matchingPids.Contains((int)pid)) return true;

            if (NativeMethods.PostMessage(
                    hWnd, NativeMethods.WmClose, IntPtr.Zero, IntPtr.Zero))
                closeRequests++;
            return true;
        }, IntPtr.Zero);

        // 有主窗口才等待浏览器正常保存会话并退出；纯后台残留无需空等。
        if (closeRequests > 0)
        {
            var deadline = DateTime.UtcNow + GracefulCloseTimeout;
            while (DateTime.UtcNow < deadline && processes.Values.Any(IsStillRunning))
                Thread.Sleep(200);
        }

        var graceful = processes.Values.Count(p => !IsStillRunning(p));
        var forced = 0;
        foreach (var process in processes.Values)
        {
            try
            {
                if (!IsStillRunning(process)) continue;
                process.Kill(entireProcessTree: true);
                forced++;
            }
            catch (Exception ex)
            {
                _ctx.Log.Warn($"浏览器：强制结束 PID {process.Id} 失败: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        if (graceful > 0 || forced > 0)
            _closedByUs = true;
        _ctx.Log.Info(
            $"浏览器：正常关闭 {graceful} 个进程，超时强制结束 {forced} 个进程（{reason}）。");
        }
        finally
        {
            Interlocked.Exchange(ref _closing, 0);
        }
    }

    private static bool IsStillRunning(Process process)
    {
        try { return !process.HasExited; }
        catch { return false; }
    }

    private static List<string> ParseNames(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        return raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s[..^4] : s)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
