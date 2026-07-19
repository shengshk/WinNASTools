using Microsoft.Win32;

namespace WinNASTools.Core.Engine;

/// <summary>监听工作站锁屏/解锁，驱动离开态冻结与归来边沿。</summary>
public sealed class SessionLockMonitor : IDisposable
{
    private readonly Action _onLocked;
    private readonly Action _onUnlocked;
    private bool _subscribed;

    public SessionLockMonitor(Action onLocked, Action onUnlocked)
    {
        _onLocked = onLocked;
        _onUnlocked = onUnlocked;
    }

    public void Start()
    {
        if (_subscribed) return;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        _subscribed = true;
    }

    public void Dispose()
    {
        if (!_subscribed) return;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _subscribed = false;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        try
        {
            if (e.Reason == SessionSwitchReason.SessionLock)
                _onLocked();
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
                _onUnlocked();
        }
        catch
        {
            /* ignore */
        }
    }
}
