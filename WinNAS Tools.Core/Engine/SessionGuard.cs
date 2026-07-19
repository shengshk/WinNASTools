using WinNASTools.Core.Native;

namespace WinNASTools.Core.Engine;

/// <summary>检测不应自动空闲触发的场景（一键离开仍可用）。</summary>
public static class SessionGuard
{
    public static bool ShouldSkipAutoIdleTriggers()
    {
        try
        {
            if (NativeMethods.IsRemoteSession())
                return true;
            if (NativeMethods.IsForegroundFullscreen())
                return true;
        }
        catch
        {
            /* ignore probe failures */
        }

        return false;
    }
}
