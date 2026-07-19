using WinNASTools.Core.Contracts;
using WinNASTools.Core.Native;

namespace WinNASTools.Core.Features;

public sealed class WindowFeature : IWinNASToolsFeature
{
    private IFeatureContext? _ctx;
    private readonly List<(IntPtr Hwnd, bool WasMaximized)> _hidden = new();
    private bool _isHidden;
    private DateTime _hiddenAt = DateTime.MinValue;

    public string Id => "window";
    public string DisplayName => "自动窗口";
    public bool IsEnabled { get; set; } = true;

    public void Initialize(IFeatureContext context) => _ctx = context;

    public void OnIdleTick(IdleSnapshot snapshot)
    {
        if (_ctx is null || !IsEnabled) return;
        if (_ctx.CurrentState == AppState.Stopped) return;
        if (_ctx.IsIdleLeaveReturnFrozen) return;

        var cfg = _ctx.Config.Window;
        var idle = snapshot.IdleSeconds;

        if (!_isHidden
            && !_ctx.ShouldSkipAutoIdleTriggers
            && cfg.HideAfterSeconds > 0
            && idle >= cfg.HideAfterSeconds)
        {
            _ctx.MarkAway($"空闲 {idle:N0}s");
            DoHide(idle);
            return;
        }

        if (_isHidden && _ctx.ShouldRunReturnActions)
        {
            RestoreWindows();
            _isHidden = false;
            _ctx.Log.Info("窗口：检测到活动，已恢复。");
        }
    }

    public void ForceTrigger()
    {
        if (_ctx is null || !IsEnabled) return;
        if (_isHidden) return;
        _ctx.MarkAway("一键离开");
        DoHide(force: true);
    }

    public void OnStateChanged(AppState from, AppState to)
    {
        if (to == AppState.Stopped && _isHidden)
        {
            RestoreWindows();
            _isHidden = false;
        }
    }

    public void Dispose()
    {
        if (_isHidden)
        {
            RestoreWindows();
            _isHidden = false;
        }
    }

    private void DoHide(double idle = 0, bool force = false)
    {
        HideWindows();
        _isHidden = true;
        _hiddenAt = DateTime.UtcNow;
        var tip = force ? "一键离开" : $"空闲 {idle:N0}s";
        _ctx?.Log.Info($"窗口：已隐藏 {_hidden.Count} 个（{tip}）。");
    }

    // 桌面与任务栏（含多屏副任务栏）的窗口类名，永远不隐藏。
    private static readonly HashSet<string> ShellClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd",           // 主任务栏
        "Shell_SecondaryTrayWnd",  // 副屏任务栏
        "Progman",                 // 桌面
        "WorkerW"                  // 桌面壁纸层
    };

    private void HideWindows()
    {
        _hidden.Clear();
        var shell = NativeMethods.GetShellWindow();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (hWnd == shell) return true;
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;
            if (NativeMethods.IsIconic(hWnd)) return true;
            if (NativeMethods.GetWindowTextLength(hWnd) == 0) return true;

            var ex = NativeMethods.GetWindowLong(hWnd, NativeMethods.GwlExstyle);
            if ((ex & NativeMethods.WsExToolwindow) != 0) return true;

            // 仅排除桌面/任务栏；其余全部隐藏（含本程序自身窗口）。
            if (ShellClassNames.Contains(NativeMethods.GetWindowClassName(hWnd))) return true;

            var maximized = NativeMethods.IsZoomed(hWnd);
            if (NativeMethods.ShowWindow(hWnd, NativeMethods.SwMinimize))
                _hidden.Add((hWnd, maximized));

            return true;
        }, IntPtr.Zero);
    }

    private void RestoreWindows()
    {
        // 藏窗时本进程会被最小化，随后通常已进托盘 Hide。
        // 归来时不要再 Restore 本进程，否则会把设置面板强制弹到前台。
        var selfPid = (uint)Environment.ProcessId;
        foreach (var (hWnd, wasMaximized) in _hidden)
        {
            try
            {
                NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
                if (pid == selfPid) continue;

                NativeMethods.ShowWindow(
                    hWnd,
                    wasMaximized ? NativeMethods.SwShowMaximized : NativeMethods.SwRestore);
            }
            catch { /* window may be gone */ }
        }
        _hidden.Clear();
    }
}
