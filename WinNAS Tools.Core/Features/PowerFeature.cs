using System.Diagnostics;
using System.Text.RegularExpressions;
using WinNASTools.Core.Contracts;
using WinNASTools.Core.Localization;

namespace WinNASTools.Core.Features;

public sealed class PowerFeature : IWinNASToolsFeature
{
    private static readonly Guid Balanced = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid PowerSaver = Guid.Parse("a1841308-3541-4fab-bc81-f71556f20b4a");
    private static readonly Guid HighPerformance = Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

    private IFeatureContext? _ctx;
    private string _currentLabel = Loc.T("Power.Unknown");
    private bool _inSaver;

    public string Id => "power";
    public string DisplayName => Loc.T("Feature.Power.Display");
    public bool IsEnabled { get; set; } = true;

    public void Initialize(IFeatureContext context)
    {
        _ctx = context;
        RefreshActivePlan();
        ApplyPreferredPlan();
        _inSaver = false;
    }

    public void OnIdleTick(IdleSnapshot snapshot)
    {
        if (_ctx is null || !IsEnabled) return;
        if (_ctx.CurrentState == AppState.Stopped) return;
        if (_ctx.IsIdleLeaveReturnFrozen) return;

        var cfg = _ctx.Config.Power;
        if (string.Equals(cfg.Mode, "Manual", StringComparison.OrdinalIgnoreCase))
            return;

        // 归来边沿：不依赖 _inSaver 标志（可能被其它路径清掉），强制对齐偏好计划。
        if (_ctx.ShouldRunReturnActions)
        {
            RestorePreferredFromAway();
            return;
        }

        var idle = snapshot.IdleSeconds;
        if (!_inSaver
            && !_ctx.ShouldSkipAutoIdleTriggers
            && cfg.SaverAfterSeconds > 0
            && idle >= cfg.SaverAfterSeconds)
        {
            _ctx.MarkAway(Loc.T("Log.Reason.Idle", idle.ToString("N0")));
            EnterSaver();
        }
    }

    public void ForceTrigger()
    {
        if (_ctx is null || !IsEnabled) return;
        if (string.Equals(_ctx.Config.Power.Mode, "Manual", StringComparison.OrdinalIgnoreCase))
            return;
        if (_inSaver) return;
        _ctx.MarkAway(Loc.T("Log.Reason.LeaveNow"));
        EnterSaver(force: true);
    }

    public void OnStateChanged(AppState from, AppState to)
    {
        if (to == AppState.Stopped)
        {
            // 停止监控时恢复用户偏好电源，而不是强行切平衡。
            RestorePreferredFromAway();
        }
        else if (to == AppState.Watching && from == AppState.Stopped)
        {
            RestorePreferredFromAway();
        }
    }

    public void Dispose()
    {
        if (_inSaver)
            RestorePreferredFromAway();
    }

    /// <summary>配置里的电源偏好变更后：核对当前计划，不一致则立刻切到偏好。</summary>
    public void ApplyPreferenceNow()
    {
        if (_ctx is null || !IsEnabled) return;
        if (string.Equals(_ctx.Config.Power.Mode, "Manual", StringComparison.OrdinalIgnoreCase))
            return;

        RestorePreferredFromAway();
    }

    private void EnterSaver(bool force = false)
    {
        if (SetPlan(Loc.T("Power.Saver"), PowerSaver))
        {
            _inSaver = true;
            if (force) _ctx?.Log.Info(Loc.T("Log.Power.LeaveNowSaver"));
        }
    }

    private void RestorePreferredFromAway()
    {
        if (_ctx is null) return;
        RefreshActivePlan();
        ApplyPreferredPlan();
        // 以系统实际方案为准：只有已回到偏好才清离开节能标记。
        RefreshActivePlan();
        var mode = _ctx.Config.Power.Mode;
        var preferred = string.Equals(mode, "Performance", StringComparison.OrdinalIgnoreCase) ? Loc.T("Power.Performance")
            : string.Equals(mode, "Balanced", StringComparison.OrdinalIgnoreCase) ? Loc.T("Power.Balanced")
            : null;
        _inSaver = preferred is null || _currentLabel != preferred;
        if (_inSaver)
            _ctx.Log.Warn(Loc.T("Log.Power.ReturnMismatch", _currentLabel, preferred));
    }

    private void ApplyPreferredPlan()
    {
        if (_ctx is null) return;
        var mode = _ctx.Config.Power.Mode;
        if (string.Equals(mode, "Performance", StringComparison.OrdinalIgnoreCase))
            SetPlan(Loc.T("Power.Performance"), HighPerformance);
        else if (string.Equals(mode, "Balanced", StringComparison.OrdinalIgnoreCase))
            SetPlan(Loc.T("Power.Balanced"), Balanced);
    }

    private bool SetPlan(string label, Guid guid)
    {
        // 以系统真实方案为准，避免缓存标签导致该切不切。
        RefreshActivePlan();
        if (_currentLabel == label) return true;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = $"/setactive {guid}",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var p = Process.Start(psi);
            if (p is null)
            {
                _ctx?.Log.Error(Loc.T("Log.Power.SwitchNoPowercfg"));
                return false;
            }

            if (!p.WaitForExit(3000))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
                _ctx?.Log.Error(Loc.T("Log.Power.SwitchTimeout"));
                return false;
            }

            if (p.ExitCode != 0)
            {
                _ctx?.Log.Error(Loc.T("Log.Power.SwitchExitCode", p.ExitCode));
                return false;
            }

            _currentLabel = label;
            _ctx?.Log.Info(Loc.T("Log.Power.Switched", label));
            return true;
        }
        catch (Exception ex)
        {
            _ctx?.Log.Error(Loc.T("Log.Power.SwitchFailed", ex.Message));
            return false;
        }
    }

    private void RefreshActivePlan()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "/getactivescheme",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var p = Process.Start(psi);
            if (p is null) return;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);

            var match = Regex.Match(output, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
            if (!match.Success) return;
            var g = Guid.Parse(match.Groups[1].Value);
            _currentLabel = g == HighPerformance ? Loc.T("Power.Performance")
                : g == PowerSaver ? Loc.T("Power.Saver")
                : g == Balanced ? Loc.T("Power.Balanced")
                : Loc.T("Power.Custom");
        }
        catch
        {
            _currentLabel = Loc.T("Power.Unknown");
        }
    }
}
