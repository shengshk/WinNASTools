using WinNASTools.Core.Contracts;
using WinNASTools.Core.Native;

namespace WinNASTools.Core.Features;

/// <summary>
/// 自动锁屏：离开任务中偏后执行。锁屏后由宿主冻结空闲离开/归来，解锁才算归来。
/// </summary>
public sealed class LockFeature : IWinNASToolsFeature
{
    private IFeatureContext? _ctx;
    private bool _lockedByUs;

    public string Id => "lock";
    public string DisplayName => "自动锁屏";
    public bool IsEnabled { get; set; }

    public void Initialize(IFeatureContext context) => _ctx = context;

    public void OnIdleTick(IdleSnapshot snapshot)
    {
        if (_ctx is null || !IsEnabled) return;
        if (_ctx.CurrentState == AppState.Stopped) return;
        if (_ctx.IsIdleLeaveReturnFrozen) return;

        var cfg = _ctx.Config.Lock;
        if (cfg.LockAfterSeconds <= 0) return;
        if (_ctx.ShouldSkipAutoIdleTriggers) return;
        if (_ctx.IsWorkstationLocked) return;
        if (snapshot.IdleSeconds < cfg.LockAfterSeconds) return;

        _ctx.MarkAway($"空闲 {snapshot.IdleSeconds:N0}s");
        DoLock($"空闲 {snapshot.IdleSeconds:N0}s");
    }

    public void ForceTrigger()
    {
        if (_ctx is null || !IsEnabled) return;
        if (_ctx.IsWorkstationLocked) return;
        _ctx.MarkAway("一键离开");
        DoLock("一键离开");
    }

    public void OnStateChanged(AppState from, AppState to)
    {
        if (to == AppState.Stopped)
            _lockedByUs = false;
    }

    public void Dispose() => _lockedByUs = false;

    /// <summary>解锁后复位，便于下一轮再锁。</summary>
    public void OnUnlocked() => _lockedByUs = false;

    private void DoLock(string reason)
    {
        if (_ctx is null) return;
        if (_ctx.IsWorkstationLocked || _lockedByUs) return;

        try
        {
            if (!NativeMethods.LockWorkStation())
            {
                _ctx.Log.Error("锁屏失败：LockWorkStation 返回 false。");
                return;
            }

            _lockedByUs = true;
            _ctx.MarkAway(reason);
            _ctx.Log.Info($"锁屏：已锁定（{reason}）。");

            // 尽量避免锁屏把已熄屏又弄亮：锁后再请求关屏（已灭则无害）。
            try
            {
                NativeMethods.PostMessage(
                    NativeMethods.HwndBroadcast,
                    NativeMethods.WmSyscommand,
                    (IntPtr)NativeMethods.ScMonitorpower,
                    NativeMethods.MonitorPowerOff);
            }
            catch { /* ignore */ }
        }
        catch (Exception ex)
        {
            _ctx.Log.Error($"锁屏失败: {ex.Message}");
        }
    }
}
