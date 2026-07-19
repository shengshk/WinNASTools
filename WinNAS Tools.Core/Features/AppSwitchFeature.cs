using System.Diagnostics;
using WinNASTools.Core.Contracts;
using WinNASTools.Core.Native;

namespace WinNASTools.Core.Features;

/// <summary>
/// 自动停止应用：离开时若目标进程正在运行则停止并记录；归来时仅当「确由本程序停止」
/// 且开启归来重启才重新启动。离开时本来就没运行 → 不记录、归来也不启动。
/// 监控停止/程序退出不做任何恢复动作。
/// </summary>
public sealed class AppSwitchFeature : IWinNASToolsFeature
{
    private static readonly TimeSpan GracefulCloseTimeout = TimeSpan.FromSeconds(8);

    private IFeatureContext? _ctx;
    private readonly Dictionary<string, TaskState> _states = new();
    private int _busy;

    public string Id => "appswitch";
    public string DisplayName => "自动停止应用";
    public bool IsEnabled { get; set; }

    public void Initialize(IFeatureContext context) => _ctx = context;

    private sealed class TaskState
    {
        public bool Evaluated;        // 本轮离开是否已判定
        public bool WeStopped;        // 是否确由本程序停止（重启前提）
        public string? CapturedPath;  // 停止前记录的可执行路径，用于重启
    }

    public void OnIdleTick(IdleSnapshot snapshot)
    {
        if (_ctx is null || !IsEnabled) return;
        if (_ctx.CurrentState == AppState.Stopped) return;
        if (_ctx.IsIdleLeaveReturnFrozen) return;

        var idle = snapshot.IdleSeconds;
        var returned = _ctx.ShouldRunReturnActions;

        foreach (var task in _ctx.Config.AppSwitch.Tasks)
        {
            if (!task.Enabled) continue;
            var st = GetState(task.Id);

            if (returned)
            {
                if (st.Evaluated)
                {
                    if (st.WeStopped && task.RestartOnReturn)
                        Restart(task, st);
                    st.Evaluated = false;
                    st.WeStopped = false;
                    st.CapturedPath = null;
                }
                continue;
            }

            if (!st.Evaluated
                && !_ctx.ShouldSkipAutoIdleTriggers
                && task.StopAfterSeconds > 0
                && idle >= task.StopAfterSeconds)
            {
                _ctx.MarkAway($"空闲 {idle:N0}s");
                EvaluateStop(task, st, $"空闲 {idle:N0}s");
            }
        }
    }

    public void ForceTrigger()
    {
        if (_ctx is null || !IsEnabled) return;
        _ctx.MarkAway("一键离开");
        foreach (var task in _ctx.Config.AppSwitch.Tasks)
        {
            if (!task.Enabled) continue;
            var st = GetState(task.Id);
            if (st.Evaluated) continue;
            EvaluateStop(task, st, "一键离开");
        }
    }

    public void OnStateChanged(AppState from, AppState to)
    {
        // 监控停止时不做恢复，仅清理内部状态。
        if (to == AppState.Stopped)
            _states.Clear();
    }

    public void Dispose() => _states.Clear();

    private TaskState GetState(string id)
    {
        if (!_states.TryGetValue(id, out var st))
        {
            st = new TaskState();
            _states[id] = st;
        }
        return st;
    }

    private void EvaluateStop(AppSwitchTaskConfig task, TaskState st, string reason)
    {
        st.Evaluated = true;
        st.WeStopped = false;
        st.CapturedPath = null;

        if (_ctx is null) return;
        if (Interlocked.Exchange(ref _busy, 1) != 0) return;

        try
        {
            var names = ParseNames(task.ProcessName);
            if (names.Count == 0)
            {
                _ctx.Log.Warn($"停止应用「{task.Name}」：未配置进程名，跳过。");
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
                    _ctx.Log.Error($"停止应用「{task.Name}」：枚举 {name} 失败: {ex.Message}");
                }
            }

            if (processes.Count == 0)
            {
                _ctx.Log.Info($"停止应用「{task.Name}」：目标未运行，归来不启动（{reason}）。");
                return;
            }

            var rootPids = processes.Keys.ToHashSet();

            // 一个应用可能包含不同名称的子进程。停止前先快照整棵进程树，
            // 这样主进程响应 WM_CLOSE 后退出，也不会遗漏随后脱离的辅助进程。
            var relatedPids = NativeMethods.GetDescendantProcessIds(processes.Keys);
            foreach (var pid in relatedPids)
            {
                if (processes.ContainsKey(pid)) continue;
                try
                {
                    processes.Add(pid, Process.GetProcessById(pid));
                }
                catch { /* 快照后进程已退出 */ }
            }

            // 停止前记录启动路径：优先用户显式配置，否则读取运行进程路径。
            st.CapturedPath = ResolveLaunchPath(task, processes.Values);

            var closed = GracefulClose(processes.Values, task.Name);
            var allRootsStopped = rootPids.All(pid =>
                !processes.TryGetValue(pid, out var process) || !IsStillRunning(process));
            if (closed && allRootsStopped)
            {
                st.WeStopped = true;
                _ctx.Log.Info($"停止应用「{task.Name}」：已停止 {processes.Count} 个关联进程（{reason}）。");
                if (task.RestartOnReturn && string.IsNullOrWhiteSpace(st.CapturedPath))
                    _ctx.Log.Warn($"停止应用「{task.Name}」：未取得启动路径，归来将无法重启，请在任务中填写启动路径。");
            }
            else if (!allRootsStopped)
            {
                _ctx.Log.Warn($"停止应用「{task.Name}」：仍有主进程未退出，不标记为已停止，归来不会重复启动。");
            }

            foreach (var p in processes.Values)
                p.Dispose();
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private string? ResolveLaunchPath(AppSwitchTaskConfig task, IEnumerable<Process> processes)
    {
        if (!string.IsNullOrWhiteSpace(task.LaunchPath))
            return task.LaunchPath.Trim();

        foreach (var p in processes)
        {
            try
            {
                var file = p.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(file))
                    return file;
            }
            catch { /* 受保护/提权进程读不到，忽略 */ }
        }
        return null;
    }

    /// <summary>优雅关闭：先向可见主窗口发 WM_CLOSE 等待，超时再强制结束。返回是否有进程被关闭。</summary>
    private bool GracefulClose(IReadOnlyCollection<Process> processes, string taskName)
    {
        if (_ctx is null || processes.Count == 0) return false;

        var matchingPids = processes.Select(p => p.Id).ToHashSet();
        var closeRequests = 0;
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;
            if (NativeMethods.GetWindowTextLength(hWnd) == 0) return true;
            NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
            if (!matchingPids.Contains((int)pid)) return true;
            if (NativeMethods.PostMessage(hWnd, NativeMethods.WmClose, IntPtr.Zero, IntPtr.Zero))
                closeRequests++;
            return true;
        }, IntPtr.Zero);

        if (closeRequests > 0)
        {
            var deadline = DateTime.UtcNow + GracefulCloseTimeout;
            while (DateTime.UtcNow < deadline && processes.Any(IsStillRunning))
                Thread.Sleep(200);
        }

        var affected = processes.Count(p => !IsStillRunning(p));
        foreach (var process in processes)
        {
            try
            {
                if (!IsStillRunning(process)) continue;
                process.Kill(entireProcessTree: true);
                affected++;
            }
            catch (Exception ex)
            {
                _ctx.Log.Warn($"停止应用「{taskName}」：强制结束 PID {process.Id} 失败: {ex.Message}");
            }
        }
        return affected > 0;
    }

    private void Restart(AppSwitchTaskConfig task, TaskState st)
    {
        if (_ctx is null) return;
        if (IsConfiguredAppRunning(task.ProcessName))
        {
            _ctx.Log.Info($"停止应用「{task.Name}」：应用已在运行，跳过重复启动。");
            return;
        }

        var path = !string.IsNullOrWhiteSpace(task.LaunchPath) ? task.LaunchPath.Trim() : st.CapturedPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            _ctx.Log.Warn($"停止应用「{task.Name}」：无启动路径，跳过重启。");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            };
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    psi.WorkingDirectory = dir;
            }
            catch { /* 快捷方式等无目录，忽略 */ }

            Process.Start(psi);
            _ctx.Log.Info($"停止应用「{task.Name}」：检测到活动，已重新启动。");
        }
        catch (Exception ex)
        {
            _ctx.Log.Error($"停止应用「{task.Name}」：重启失败: {ex.Message}");
        }
    }

    private static bool IsConfiguredAppRunning(string? processName)
    {
        foreach (var name in ParseNames(processName))
        {
            Process[] matches;
            try { matches = Process.GetProcessesByName(name); }
            catch { continue; }

            if (matches.Length > 0)
            {
                foreach (var process in matches) process.Dispose();
                return true;
            }
        }
        return false;
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
