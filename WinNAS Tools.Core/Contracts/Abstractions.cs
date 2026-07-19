namespace WinNASTools.Core.Contracts;

/// <summary>引擎运行态（与各功能内部计时无关）。</summary>
public enum AppState
{
    Stopped,
    Watching
}

public readonly record struct IdleSnapshot(double IdleSeconds, DateTime UtcNow);

public interface IWinNASToolsFeature
{
    string Id { get; }
    string DisplayName { get; }
    bool IsEnabled { get; set; }

    void Initialize(IFeatureContext context);
    void OnIdleTick(IdleSnapshot snapshot);
    void OnStateChanged(AppState from, AppState to);

    /// <summary>一键离开：跳过等待，立刻执行本模块「到期动作」。</summary>
    void ForceTrigger();

    void Dispose();
}

public interface IFeatureContext
{
    global::WinNASTools.Core.AppConfig Config { get; }
    IFeatureBus Bus { get; }
    ILogger Log { get; }
    void RequestState(AppState state);
    AppState CurrentState { get; }

    /// <summary>程序自身动作 / 一键离开归来豁免期内，不计为「用户回来了」。</summary>
    void SuppressUserActivity(TimeSpan duration);
    bool IsUserActivitySuppressed { get; }

    /// <summary>全屏或远程桌面时，跳过空闲自动触发（一键离开仍可用）。</summary>
    bool ShouldSkipAutoIdleTriggers { get; }

    /// <summary>业务离开态（含锁屏强制离开）。</summary>
    bool IsAway { get; }

    /// <summary>工作站已锁定（锁屏界面）。</summary>
    bool IsWorkstationLocked { get; }

    /// <summary>锁屏期间冻结空闲离开/归来，避免循环。</summary>
    bool IsIdleLeaveReturnFrozen { get; }

    /// <summary>本拍是否应执行归来恢复（解锁边沿，或未锁屏时的键鼠活动）。</summary>
    bool ShouldRunReturnActions { get; }

    /// <summary>标记已进入离开（空闲任务/一键离开/手动锁屏跑离开时调用）。</summary>
    void MarkAway(string reason);

    /// <summary>串行写回配置，避免后台调度与 UI 保存互相覆盖。</summary>
    void PersistConfig();
}

public interface IFeatureBus
{
    void Publish<TEvent>(TEvent evt);
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);
}

public interface ILogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}
