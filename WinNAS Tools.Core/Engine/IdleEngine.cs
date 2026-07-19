using WinNASTools.Core.Contracts;
using WinNASTools.Core.Native;
using Timer = System.Threading.Timer;

namespace WinNASTools.Core.Engine;

/// <summary>
/// 唯一心跳：输出「有效空闲秒数」。
/// Suppress 窗口内的输入永久忽略（不在豁免结束后补录），
/// 这样离开热键/误触不会在短时阻止归来结束后瞬间触发归来。
/// </summary>
public sealed class IdleEngine : IDisposable
{
    private readonly object _gate = new();
    private Timer? _timer;
    private AppConfig _config;
    private bool _running;
    private DateTime _lastRealActivityUtc = DateTime.UtcNow;
    private DateTime _suppressUntilUtc = DateTime.MinValue;
    /// <summary>此时间点之前的系统输入一律忽略（含阻止归来期内的热键/误触）。</summary>
    private DateTime _ignoreInputBeforeUtc = DateTime.MinValue;

    public AppState State { get; private set; } = AppState.Stopped;
    public double LastIdleSeconds { get; private set; }
    public bool IsUserActivitySuppressed
    {
        get { lock (_gate) return DateTime.UtcNow < _suppressUntilUtc; }
    }

    public event Action<IdleSnapshot>? IdleTick;
    public event Action<AppState, AppState>? StateChanged;

    public IdleEngine(AppConfig config) => _config = config;

    public void UpdateConfig(AppConfig config)
    {
        lock (_gate)
        {
            _config = config;
            if (_running && _timer is not null)
            {
                var interval = Math.Max(200, _config.PollIntervalMs);
                _timer.Change(interval, interval);
            }
        }
    }

    public void SuppressUserActivity(TimeSpan duration)
    {
        lock (_gate)
        {
            var until = DateTime.UtcNow + duration;
            if (until > _suppressUntilUtc)
                _suppressUntilUtc = until;
            if (_suppressUntilUtc > _ignoreInputBeforeUtc)
                _ignoreInputBeforeUtc = _suppressUntilUtc;
        }
    }

    /// <summary>与 Tick / LeaveNow 串行执行，避免 hide/restore 交错。</summary>
    public void RunExclusive(Action action)
    {
        lock (_gate) action();
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_running) return;
            _running = true;
            SyncLastRealActivityFromSystem();
            SetState(AppState.Watching);
            var interval = Math.Max(200, _config.PollIntervalMs);
            _timer = new Timer(_ => Tick(), null, 0, interval);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_running) return;
            _running = false;
            _timer?.Dispose();
            _timer = null;
            SetState(AppState.Stopped);
        }
    }

    public void RequestState(AppState state)
    {
        lock (_gate)
        {
            if (!_running && state != AppState.Stopped)
                return;
            SetState(state);
        }
    }

    private void Tick()
    {
        IdleSnapshot snap;
        lock (_gate)
        {
            if (!_running) return;

            var now = DateTime.UtcNow;
            var suppressed = now < _suppressUntilUtc;

            // 探测失败：不更新活动时刻，避免被当成「刚有人操作」。
            if (NativeMethods.TryGetIdleTime(out var idleSpan) && !suppressed)
            {
                var raw = idleSpan.TotalSeconds;
                var systemActivityAt = now - TimeSpan.FromSeconds(raw);
                if (systemActivityAt > _ignoreInputBeforeUtc
                    && systemActivityAt > _lastRealActivityUtc)
                    _lastRealActivityUtc = systemActivityAt;
            }

            var effective = Math.Max(0, (now - _lastRealActivityUtc).TotalSeconds);
            LastIdleSeconds = effective;
            snap = new IdleSnapshot(effective, now);
        }

        try { IdleTick?.Invoke(snap); }
        catch { /* feature errors isolated */ }
    }

    private void SyncLastRealActivityFromSystem()
    {
        if (NativeMethods.TryGetIdleTime(out var idle))
            _lastRealActivityUtc = DateTime.UtcNow - idle;
        else
            _lastRealActivityUtc = DateTime.UtcNow;
    }

    private void SetState(AppState next)
    {
        if (State == next) return;
        var prev = State;
        State = next;
        try { StateChanged?.Invoke(prev, next); }
        catch { /* ignore */ }
    }

    public void Dispose() => Stop();
}
