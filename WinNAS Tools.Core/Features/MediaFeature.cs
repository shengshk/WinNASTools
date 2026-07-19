using WinNASTools.Core.Contracts;
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
    public string DisplayName => "自动音乐";
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
            _ctx.MarkAway($"空闲 {idle:N0}s");
            EvaluateStop($"空闲 {idle:N0}s");
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
        _ctx.MarkAway("一键离开");
        EvaluateStop("一键离开");
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
            msg => _ctx?.Log.Info($"音乐：{msg}（{reason}）。"));

        _stopMethod = method;
        if (method == MediaController.StopMethod.None)
            return;

        _ctx?.Log.Info($"音乐：已停止，方式={MediaController.MethodLabel(method)}（{reason}）。");
    }

    private void DoResume()
    {
        var method = _stopMethod;
        _ctx?.SuppressUserActivity(TimeSpan.FromSeconds(3));
        MediaController.TryResume(
            method,
            _ctx?.Config.Media.PlayPauseHotkeys,
            msg => _ctx?.Log.Info($"音乐：{msg}。"));
    }
}
