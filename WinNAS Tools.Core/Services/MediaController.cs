using WinNASTools.Core.Native;

namespace WinNASTools.Core.Services;

/// <summary>离开时阶梯停止媒体；归来时仅用「当时生效的那一招」恢复。</summary>
public static class MediaController
{
    public enum StopMethod
    {
        None = 0,
        Pause = 1,
        Stop = 2,
        MediaKey = 3,
        Hotkey1 = 4,
        Hotkey2 = 5,
        Hotkey3 = 6,
        Mute = 7
    }

    private const int SettleMs = 400;

    /// <summary>
    /// 有播放才动作：Pause → Stop → 多媒体键 → 用户快捷键1～3 → 系统静音。
    /// 每步后复测峰值；静音为最终兜底（不依赖峰值确认）。
    /// </summary>
    public static StopMethod TryStop(IReadOnlyList<string?>? playPauseHotkeys, Action<string>? log = null)
    {
        var playing = AudioSessionProbe.IsAnyAudioPlaying();
        if (playing != true)
        {
            log?.Invoke(playing is null ? "探测失败，保守跳过" : "当前无音频播放，跳过");
            return StopMethod.None;
        }

        // 再确认确实在出声，避免“会话 Active 但已静音/无声”误动作。
        if (SystemAudioControl.IsOutputAudible() == false)
        {
            log?.Invoke("有会话但无声，跳过");
            return StopMethod.None;
        }

        if (TryAction("系统 Pause", NativeMethods.MediaPause, log))
            return StopMethod.Pause;

        if (TryAction("系统 Stop", NativeMethods.MediaStop, log))
            return StopMethod.Stop;

        if (TryAction("多媒体播放/暂停键", NativeMethods.SendMediaPlayPauseKey, log))
            return StopMethod.MediaKey;

        var keys = NormalizeHotkeys(playPauseHotkeys);
        for (var i = 0; i < keys.Count; i++)
        {
            var spec = keys[i];
            if (string.IsNullOrWhiteSpace(spec)) continue;
            var method = i switch
            {
                0 => StopMethod.Hotkey1,
                1 => StopMethod.Hotkey2,
                _ => StopMethod.Hotkey3
            };
            var label = $"快捷键{i + 1}（{spec}）";
            if (TryAction(label, () => HotkeySpec.TrySend(spec), log))
                return method;
        }

        // 静音兜底：峰值在静音后也会变 0，但即便探测失败也执行。
        var wasMuted = SystemAudioControl.GetMute();
        if (wasMuted == true)
        {
            log?.Invoke("仍有声，但系统已静音，放弃");
            return StopMethod.None;
        }

        if (SystemAudioControl.TrySetMute(true))
        {
            log?.Invoke("前序方式无效，已系统静音兜底");
            return StopMethod.Mute;
        }

        log?.Invoke("全部方式失败（含静音）");
        return StopMethod.None;
    }

    /// <summary>按离开时生效方式对称恢复；失败则放弃，不继续盲试其它手段。</summary>
    public static void TryResume(StopMethod method, IReadOnlyList<string?>? playPauseHotkeys, Action<string>? log = null)
    {
        if (method == StopMethod.None) return;

        switch (method)
        {
            case StopMethod.Pause:
            case StopMethod.Stop:
                NativeMethods.MediaPlay();
                log?.Invoke($"已用系统 Play 恢复（离开时为 {MethodLabel(method)}）");
                break;

            case StopMethod.MediaKey:
                NativeMethods.SendMediaPlayPauseKey();
                log?.Invoke("已用多媒体播放/暂停键恢复");
                break;

            case StopMethod.Hotkey1:
            case StopMethod.Hotkey2:
            case StopMethod.Hotkey3:
            {
                var keys = NormalizeHotkeys(playPauseHotkeys);
                var index = method - StopMethod.Hotkey1;
                var spec = index >= 0 && index < keys.Count ? keys[index] : null;
                if (string.IsNullOrWhiteSpace(spec) || !HotkeySpec.TrySend(spec))
                {
                    log?.Invoke($"快捷键恢复失败，已放弃（{spec ?? "未配置"}）");
                    return;
                }
                log?.Invoke($"已用快捷键{index + 1}（{spec}）恢复");
                break;
            }

            case StopMethod.Mute:
                if (SystemAudioControl.TrySetMute(false))
                    log?.Invoke("已取消系统静音（离开时静音兜底；不强制恢复播放）");
                else
                    log?.Invoke("取消系统静音失败，已放弃");
                break;
        }
    }

    public static string MethodLabel(StopMethod method) => method switch
    {
        StopMethod.Pause => "Pause",
        StopMethod.Stop => "Stop",
        StopMethod.MediaKey => "多媒体键",
        StopMethod.Hotkey1 => "快捷键1",
        StopMethod.Hotkey2 => "快捷键2",
        StopMethod.Hotkey3 => "快捷键3",
        StopMethod.Mute => "系统静音",
        _ => "无"
    };

    private static bool TryAction(string label, Action action, Action<string>? log)
    {
        try { action(); }
        catch (Exception ex)
        {
            log?.Invoke($"{label} 发送失败：{ex.Message}");
            return false;
        }

        Thread.Sleep(SettleMs);
        var audible = SystemAudioControl.IsOutputAudible();
        if (audible == false)
        {
            log?.Invoke($"{label} 生效");
            return true;
        }

        // 峰值探测失败时，再用会话状态兜一次（偏保守：仍有声则继续下一招）
        if (audible is null && AudioSessionProbe.IsAnyAudioPlaying() == false)
        {
            log?.Invoke($"{label} 生效（峰值不可用，按会话判断）");
            return true;
        }

        log?.Invoke($"{label} 无效，继续");
        return false;
    }

    private static bool TryAction(string label, Func<bool> action, Action<string>? log)
    {
        bool ok;
        try { ok = action(); }
        catch (Exception ex)
        {
            log?.Invoke($"{label} 发送失败：{ex.Message}");
            return false;
        }

        if (!ok)
        {
            log?.Invoke($"{label} 发送失败，继续");
            return false;
        }

        Thread.Sleep(SettleMs);
        var audible = SystemAudioControl.IsOutputAudible();
        if (audible == false)
        {
            log?.Invoke($"{label} 生效");
            return true;
        }

        if (audible is null && AudioSessionProbe.IsAnyAudioPlaying() == false)
        {
            log?.Invoke($"{label} 生效（峰值不可用，按会话判断）");
            return true;
        }

        log?.Invoke($"{label} 无效，继续");
        return false;
    }

    private static List<string> NormalizeHotkeys(IReadOnlyList<string?>? hotkeys)
    {
        var list = new List<string>(3);
        if (hotkeys is null) return list;
        foreach (var h in hotkeys)
        {
            if (list.Count >= 3) break;
            var t = h?.Trim() ?? "";
            if (t.Length == 0) continue;
            if (!HotkeySpec.TryParse(t, out _, out _, out var display)) continue;
            list.Add(display);
        }
        return list;
    }
}
