using WinNASTools.Core.Contracts;
using WinNASTools.Core.Localization;
using WinNASTools.Core.Services;

namespace WinNASTools.Core.Features;

/// <summary>
/// 自动音乐：离开时阶梯暂停（系统键 → 用户快捷键 → 静音），
/// 归来时仅用当时生效的方式恢复。
/// </summary>
public sealed class MediaFeature : IWinNASToolsFeature
{
    private IFeatureContext? _ctx;
    private MediaController.StopMethod _stopMethod;
    private bool _stopEvaluated;

    public string Id => "media";
    public string DisplayName => Loc.T("Feature.Media.Display");
    public bool IsEnabled { get; set; } = true;

    public void Initialize(IFeatureContext context) => _ctx = context;

    public void OnIdleTick(IdleSnapshot snapshot)
    {
        if (_ctx is null || !IsEnabled) return;
        if (_ctx.CurrentState == AppState.Stopped) return;
        if (_ctx.IsIdleLeaveReturnFrozen) return;

        var cfg = _ctx.Config.Media;
        var idle = snapshot.IdleSeconds;

        if (!_stopEvaluated
            && !_ctx.ShouldSkipAutoIdleTriggers
            && cfg.StopAfterSeconds > 0
            && idle >= cfg.StopAfterSeconds)
        {
            var reason = Loc.T("Log.Reason.Idle", idle.ToString("N0"));
            _ctx.MarkAway(reason);
            EvaluateStop(reason);
            return;
        }

        if (_stopEvaluated && _ctx.ShouldRunReturnActions)
        {
            if (_stopMethod != MediaController.StopMethod.None && cfg.AutoResume)
                DoResume();
            _stopMethod = MediaController.StopMethod.None;
            _stopEvaluated = false;
        }
    }

    public void ForceTrigger()
    {
        if (_ctx is null || !IsEnabled) return;
        if (_stopEvaluated) return;
        var reason = Loc.T("Log.Reason.LeaveNow");
        _ctx.MarkAway(reason);
        EvaluateStop(reason);
    }

    public void OnStateChanged(AppState from, AppState to)
    {
        if (to == AppState.Stopped)
        {
            _stopMethod = MediaController.StopMethod.None;
            _stopEvaluated = false;
        }
    }

    public void Dispose()
    {
        _stopMethod = MediaController.StopMethod.None;
        _stopEvaluated = false;
    }

    private void EvaluateStop(string reason)
    {
        _stopEvaluated = true;
        _ctx?.SuppressUserActivity(TimeSpan.FromSeconds(6));

        var method = MediaController.TryStop(
            _ctx?.Config.Media.PlayPauseHotkeys,
            msg => _ctx?.Log.Info(Loc.T("Log.Media.WithReason", msg, reason)));

        _stopMethod = method;
        if (method == MediaController.StopMethod.None)
            return;

        _ctx?.Log.Info(Loc.T("Log.Media.Stopped", MediaController.MethodLabel(method), reason));
    }

    private void DoResume()
    {
        var method = _stopMethod;
        _ctx?.SuppressUserActivity(TimeSpan.FromSeconds(3));
        MediaController.TryResume(
            method,
            _ctx?.Config.Media.PlayPauseHotkeys,
            msg => _ctx?.Log.Info(Loc.T("Log.Media.Message", msg)));
    }
}
