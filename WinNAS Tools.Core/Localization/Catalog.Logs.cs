namespace WinNASTools.Core.Localization;

public static partial class Catalog
{
    private static void RegisterLogs()
    {
        RegisterLogHost();
        RegisterLogFeatures();
        RegisterLogMedia();
        RegisterLogBackup();
    }

    private static void RegisterLogHost()
    {
        R("Log.Config.LogRetentionSet", "日志保留天数已设为 {0} 天。", "日誌保留天數已設為 {0} 天。", "Log retention set to {0} day(s).");
        R("Log.Feature.InitFailed", "初始化 {0} 失败: {1}", "初始化 {0} 失敗: {1}", "Failed to initialize {0}: {1}");
        R("Log.Monitor.Started", "{0} 监控已启动。", "{0} 監控已啟動。", "{0} monitoring started.");
        R("Log.Monitor.Stopped", "{0} 监控已停止。", "{0} 監控已停止。", "{0} monitoring stopped.");
        R("Log.Leave.EnterAwayWithGrace", "离开：进入离开态（{0}），短时阻止归来 {1}s。", "離開：進入離開態（{0}），短時阻止歸來 {1}s。", "Leave: entered away ({0}); return grace {1}s.");
        R("Log.Leave.EnterAway", "离开：进入离开态（{0}）。", "離開：進入離開態（{0}）。", "Leave: entered away ({0}).");
        R("Log.LeaveNow.NotRunning", "一键离开：监控未启动。", "一鍵離開：監控未啟動。", "Leave now: monitoring is not running.");
        R("Log.LeaveNow.SkipWaitProtected", "一键离开：跳过等待（短时阻止归来未启用，已做最短输入保护）。", "一鍵離開：略過等待（短時阻止歸來未啟用，已做最短輸入保護）。", "Leave now: skipped wait (grace disabled; minimal input protection applied).");
        R("Log.LeaveNow.SkipWait", "一键离开：跳过等待。", "一鍵離開：略過等待。", "Leave now: skipped wait.");
        R("Log.LeaveNow.FeatureFailed", "{0} 一键离开失败: {1}", "{0} 一鍵離開失敗: {1}", "{0} leave now failed: {1}");
        R("Log.LeaveNow.Failed", "一键离开失败: {0}", "一鍵離開失敗: {0}", "Leave now failed: {0}");
        R("Log.Modules.Updated", "模块开关已更新：窗口={0} 电源={1} 音乐={2} 浏览器={3} 停止应用={4} 锁屏={5} 短时阻止归来={6} 打开链接={7} 打印机={8} 备份={9}", "模組開關已更新：視窗={0} 電源={1} 音樂={2} 瀏覽器={3} 停止應用={4} 鎖定={5} 短時阻止歸來={6} 開啟連結={7} 印表機={8} 備份={9}", "Modules updated: window={0} power={1} media={2} browser={3} stop-app={4} lock={5} leave-grace={6} url={7} printer={8} backup={9}");
        R("Log.Config.Saved", "配置已保存。", "設定已儲存。", "Settings saved.");
        R("Log.Leave.LockDetected", "离开：检测到锁屏，进入离开态。", "離開：偵測到鎖定螢幕，進入離開態。", "Leave: lock detected; entered away.");
        R("Log.Leave.ManualLockRun", "离开：手动锁屏，执行一轮离开任务。", "離開：手動鎖定，執行一輪離開任務。", "Leave: manual lock; running leave tasks.");
        R("Log.Leave.ManualLockFeatureFailed", "{0} 手动锁屏离开失败: {1}", "{0} 手動鎖定離開失敗: {1}", "{0} manual lock leave failed: {1}");
        R("Log.Leave.ManualLockFailed", "手动锁屏离开失败: {0}", "手動鎖定離開失敗: {0}", "Manual lock leave failed: {0}");
        R("Log.Return.ByUnlock", "归来，系统解锁。", "歸來，系統解鎖。", "Return, system unlocked.");
        R("Log.Return.ByActivity", "归来，检测到活动。", "歸來，偵測到活動。", "Return, activity detected.");
        R("Log.Return.UnlockFailed", "解锁归来失败: {0}", "解鎖歸來失敗: {0}", "Return after unlock failed: {0}");
        R("Log.Feature.Error", "{0}: {1}", "{0}: {1}", "{0}: {1}");
        R("Log.State.Transition", "状态: {0} → {1}", "狀態: {0} → {1}", "State: {0} → {1}");
        R("Log.State.Stopped", "Stopped", "Stopped", "Stopped");
        R("Log.State.Watching", "Watching", "Watching", "Watching");
        R("Log.Reason.LeaveNow", "一键离开", "一鍵離開", "Leave now");
        R("Log.Reason.Idle", "空闲 {0}s", "閒置 {0}s", "Idle {0}s");
        R("Log.Crash.UiException", "未处理 UI 异常: {0}", "未處理 UI 異常: {0}", "Unhandled UI exception: {0}");
        R("Log.Crash.Unhandled", "未处理异常: {0}", "未處理異常: {0}", "Unhandled exception: {0}");
        R("Log.Crash.UnobservedTask", "未观察任务异常: {0}", "未觀察任務異常: {0}", "Unobserved task exception: {0}");
        R("Log.Hotkey.LeaveChanged", "离开热键已改为：{0}", "離開快捷鍵已改為：{0}", "Leave hotkey changed to: {0}");
        R("Log.Hotkey.Registered", "热键已注册：{0} → 一键离开", "熱鍵已註冊：{0} → 一鍵離開", "Hotkey registered: {0} → leave now");
        R("Log.Hotkey.RegisterFailed", "热键注册失败（可能被占用）：{0}", "熱鍵註冊失敗（可能已被占用）：{0}", "Hotkey registration failed (may be in use): {0}");
        R("Log.Config.Exported", "配置已导出：{0}", "設定已匯出：{0}", "Config exported: {0}");
        R("Log.Config.Imported", "配置已导入：{0}，即将重启。", "設定已匯入：{0}，即將重新啟動。", "Config imported: {0}. Restarting.");
        R("Log.Ui.LockSecondsAdjusted", "自动锁屏时间已调整为 {0} 秒（对齐其它离开任务）。", "自動鎖定時間已調整為 {0} 秒（對齊其它離開任務）。", "Auto lock adjusted to {0}s (aligned with other leave tasks).");
        R("Log.Ui.PrinterTaskSaved", "已保存打印机任务「{0}」。", "已儲存印表機任務「{0}」。", "Saved printer task \"{0}\".");
        R("Log.Ui.PrinterManualSent", "打印机「{0}」：手动打印已发送 → {1}", "印表機「{0}」：手動列印已送出 → {1}", "Printer \"{0}\": manual print sent → {1}");
        R("Log.Ui.PrinterManualFailed", "手动打印失败: {0}", "手動列印失敗: {0}", "Manual print failed: {0}");
        R("Log.Ui.UrlTaskSaved", "已保存打开链接任务「{0}」。", "已儲存開啟連結任務「{0}」。", "Saved open-URL task \"{0}\".");
        R("Log.Ui.UrlManualFailed", "手动打开链接失败: {0}", "手動開啟連結失敗: {0}", "Manual open URL failed: {0}");
        R("Log.Ui.BackupTaskSaved", "已保存备份任务「{0}」。", "已儲存備份任務「{0}」。", "Saved backup task \"{0}\".");
        R("Log.Ui.BackupProgressUiFailed", "刷新备份进度 UI 失败: {0}", "重新整理備份進度 UI 失敗: {0}", "Failed to refresh backup progress UI: {0}");
        R("Log.Ui.AppSwitchTaskSaved", "已保存停止应用任务「{0}」。", "已儲存停止應用任務「{0}」。", "Saved stop-app task \"{0}\".");
        R("Log.Ui.HostSaved", "主机「{0}」已保存。", "主機「{0}」已儲存。", "Host \"{0}\" saved.");
        R("Log.Config.OpenLogFailed", "打开日志文件失败：{0}", "開啟日誌檔案失敗：{0}", "Failed to open log file: {0}");
        R("Log.Restart.LanguageChanged", "语言已切换为 {0}，即将重启。", "語言已切換為 {0}，即將重新啟動。", "Language switched to {0}. Restarting.");
    }

    private static void RegisterLogFeatures()
    {
        R("Log.Window.Restored", "窗口：检测到活动，已恢复。", "視窗：偵測到活動，已恢復。", "Window: activity detected; restored.");
        R("Log.Window.Hidden", "窗口：已隐藏 {0} 个（{1}）。", "視窗：已隱藏 {0} 個（{1}）。", "Window: hid {0} window(s) ({1}).");

        R("Log.Power.LeaveNowSaver", "电源：一键离开 → 节能", "電源：一鍵離開 → 省電", "Power: leave now → power saver");
        R("Log.Power.ReturnMismatch", "电源：归来后仍为「{0}」，期望「{1}」。", "電源：歸來後仍為「{0}」，期望「{1}」。", "Power: still \"{0}\" after return; expected \"{1}\".");
        R("Log.Power.SwitchNoPowercfg", "电源切换失败：无法启动 powercfg。", "電源切換失敗：無法啟動 powercfg。", "Power switch failed: could not start powercfg.");
        R("Log.Power.SwitchTimeout", "电源切换失败：powercfg 超时。", "電源切換失敗：powercfg 逾時。", "Power switch failed: powercfg timed out.");
        R("Log.Power.SwitchExitCode", "电源切换失败：powercfg 退出码 {0}。", "電源切換失敗：powercfg 結束碼 {0}。", "Power switch failed: powercfg exit code {0}.");
        R("Log.Power.Switched", "电源：→ {0}", "電源：→ {0}", "Power: → {0}");
        R("Log.Power.SwitchFailed", "电源切换失败: {0}", "電源切換失敗: {0}", "Power switch failed: {0}");

        R("Log.Media.WithReason", "音乐：{0}（{1}）。", "音樂：{0}（{1}）。", "Media: {0} ({1}).");
        R("Log.Media.Stopped", "音乐：已停止，方式={0}（{1}）。", "音樂：已停止，方式={0}（{1}）。", "Media: stopped via {0} ({1}).");
        R("Log.Media.Message", "音乐：{0}。", "音樂：{0}。", "Media: {0}.");

        R("Log.Browser.ProcessListEmpty", "浏览器：进程列表为空，跳过。", "瀏覽器：處理序清單為空，略過。", "Browser: process list empty; skipped.");
        R("Log.Browser.KillFailed", "浏览器：结束 {0} 失败: {1}", "瀏覽器：結束 {0} 失敗: {1}", "Browser: failed to end {0}: {1}");
        R("Log.Browser.NoMatch", "浏览器：未发现匹配进程（{0}）。", "瀏覽器：未發現符合的處理序（{0}）。", "Browser: no matching processes ({0}).");
        R("Log.Browser.ForceKillFailed", "浏览器：强制结束 PID {0} 失败: {1}", "瀏覽器：強制結束 PID {0} 失敗: {1}", "Browser: force kill PID {0} failed: {1}");
        R("Log.Browser.ClosedSummary", "浏览器：正常关闭 {0} 个进程，超时强制结束 {1} 个进程（{2}）。", "瀏覽器：正常關閉 {0} 個處理序，逾時強制結束 {1} 個處理序（{2}）。", "Browser: gracefully closed {0} process(es), force-killed {1} after timeout ({2}).");

        R("Log.Lock.FailedFalse", "锁屏失败：LockWorkStation 返回 false。", "鎖定失敗：LockWorkStation 回傳 false。", "Lock failed: LockWorkStation returned false.");
        R("Log.Lock.Done", "锁屏：已锁定（{0}）。", "鎖定：已鎖定（{0}）。", "Lock: workstation locked ({0}).");
        R("Log.Lock.Failed", "锁屏失败: {0}", "鎖定失敗: {0}", "Lock failed: {0}");

        R("Log.AppSwitch.NoProcessName", "停止应用「{0}」：未配置进程名，跳过。", "停止應用程式「{0}」：未設定處理序名稱，略過。", "Stop app \"{0}\": no process name configured; skipped.");
        R("Log.AppSwitch.EnumFailed", "停止应用「{0}」：枚举 {1} 失败: {2}", "停止應用程式「{0}」：列舉 {1} 失敗: {2}", "Stop app \"{0}\": failed to enumerate {1}: {2}");
        R("Log.AppSwitch.NotRunning", "停止应用「{0}」：目标未运行，归来不启动（{1}）。", "停止應用程式「{0}」：目標未執行，歸來不啟動（{1}）。", "Stop app \"{0}\": target not running; won't restart on return ({1}).");
        R("Log.AppSwitch.Stopped", "停止应用「{0}」：已停止 {1} 个关联进程（{2}）。", "停止應用程式「{0}」：已停止 {1} 個相關處理序（{2}）。", "Stop app \"{0}\": stopped {1} related process(es) ({2}).");
        R("Log.AppSwitch.NoLaunchPath", "停止应用「{0}」：未取得启动路径，归来将无法重启，请在任务中填写启动路径。", "停止應用程式「{0}」：未取得啟動路徑，歸來將無法重新啟動，請在任務中填寫啟動路徑。", "Stop app \"{0}\": no launch path captured; restart on return won't work. Set launch path in the task.");
        R("Log.AppSwitch.MainStillRunning", "停止应用「{0}」：仍有主进程未退出，不标记为已停止，归来不会重复启动。", "停止應用程式「{0}」：仍有主處理序未結束，不標記為已停止，歸來不會重複啟動。", "Stop app \"{0}\": main process still running; not marked stopped; won't restart on return.");
        R("Log.AppSwitch.ForceKillFailed", "停止应用「{0}」：强制结束 PID {1} 失败: {2}", "停止應用程式「{0}」：強制結束 PID {1} 失敗: {2}", "Stop app \"{0}\": force kill PID {1} failed: {2}");
        R("Log.AppSwitch.AlreadyRunning", "停止应用「{0}」：应用已在运行，跳过重复启动。", "停止應用程式「{0}」：應用程式已在執行，略過重複啟動。", "Stop app \"{0}\": already running; skipped restart.");
        R("Log.AppSwitch.NoPathSkipRestart", "停止应用「{0}」：无启动路径，跳过重启。", "停止應用程式「{0}」：無啟動路徑，略過重新啟動。", "Stop app \"{0}\": no launch path; skipped restart.");
        R("Log.AppSwitch.Restarted", "停止应用「{0}」：检测到活动，已重新启动。", "停止應用程式「{0}」：偵測到活動，已重新啟動。", "Stop app \"{0}\": activity detected; restarted.");
        R("Log.AppSwitch.RestartFailed", "停止应用「{0}」：重启失败: {1}", "停止應用程式「{0}」：重新啟動失敗: {1}", "Stop app \"{0}\": restart failed: {1}");

        R("Log.Printer.ScheduleError", "打印机维护调度异常: {0}", "印表機維護排程異常: {0}", "Printer maintenance schedule error: {0}");
        R("Log.Printer.MissedSkipped", "打印机「{0}」：错过计划已跳过 → 下次 {1}", "印表機「{0}」：錯過排程已略過 → 下次 {1}", "Printer \"{0}\": missed run skipped → next {1}");
        R("Log.Printer.Sent", "打印机「{0}」：已发送打印 → {1}，下次 {2}", "印表機「{0}」：已送出列印 → {1}，下次 {2}", "Printer \"{0}\": print sent → {1}, next {2}");
        R("Log.Printer.FailedRescheduled", "打印机「{0}」失败: {1}；已改期到 {2}", "印表機「{0}」失敗: {1}；已改期到 {2}", "Printer \"{0}\" failed: {1}; rescheduled to {2}");

        R("Log.Url.ScheduleError", "定时打开链接调度异常: {0}", "定時開啟連結排程異常: {0}", "Scheduled URL open error: {0}");
        R("Log.Url.MissedSkipped", "打开链接「{0}」：错过计划已跳过 → 下次 {1}", "開啟連結「{0}」：錯過排程已略過 → 下次 {1}", "Open URL \"{0}\": missed run skipped → next {1}");
        R("Log.Url.Success", "打开链接「{0}」：执行成功，下次 {1}", "開啟連結「{0}」：執行成功，下次 {1}", "Open URL \"{0}\": succeeded, next {1}");
        R("Log.Url.FailedRescheduled", "打开链接「{0}」失败: {1}；已改期到 {2}", "開啟連結「{0}」失敗: {1}；已改期到 {2}", "Open URL \"{0}\" failed: {1}; rescheduled to {2}");
        R("Log.Url.Opening", "打开链接「{0}」：{1}", "開啟連結「{0}」：{1}", "Open URL \"{0}\": {1}");
        R("Log.Url.NoBrowserProcess", "打开链接「{0}」：无法识别默认浏览器进程，跳过自动关闭。", "開啟連結「{0}」：無法識別預設瀏覽器處理序，略過自動關閉。", "Open URL \"{0}\": default browser process unknown; skipped auto close.");
        R("Log.Url.ClosedAfterWait", "打开链接「{0}」：等待 {1}s 后已关闭浏览器 {2}。", "開啟連結「{0}」：等待 {1}s 後已關閉瀏覽器 {2}。", "Open URL \"{0}\": closed browser {2} after {1}s.");
        R("Log.Url.AutoCloseFailed", "打开链接「{0}」自动关闭浏览器失败: {1}", "開啟連結「{0}」自動關閉瀏覽器失敗: {1}", "Open URL \"{0}\": auto close browser failed: {1}");
        R("Log.Url.InvalidUrlEx", "链接必须是有效的 HTTP/HTTPS 地址。", "連結必須是有效的 HTTP/HTTPS 位址。", "URL must be a valid HTTP/HTTPS address.");
    }

    private static void RegisterLogMedia()
    {
        R("Log.Media.ProbeFailedSkip", "探测失败，保守跳过", "偵測失敗，保守略過", "Probe failed; skipped conservatively");
        R("Log.Media.NoPlaybackSkip", "当前无音频播放，跳过", "目前無音訊播放，略過", "No audio playing; skipped");
        R("Log.Media.SessionSilentSkip", "有会话但无声，跳过", "有工作階段但無聲，略過", "Session active but silent; skipped");
        R("Log.Media.AlreadyMutedGiveUp", "仍有声，但系统已静音，放弃", "仍有聲，但系統已靜音，放棄", "Still audible but system muted; gave up");
        R("Log.Media.MuteFallback", "前序方式无效，已系统静音兜底", "前序方式無效，已以系統靜音備援", "Previous methods failed; muted system as fallback");
        R("Log.Media.AllFailed", "全部方式失败（含静音）", "全部方式失敗（含靜音）", "All methods failed (including mute)");
        R("Log.Media.ResumePlay", "已用系统 Play 恢复（离开时为 {0}）", "已用系統 Play 恢復（離開時為 {0}）", "Resumed with system Play (left via {0})");
        R("Log.Media.ResumeMediaKey", "已用多媒体播放/暂停键恢复", "已用多媒體播放/暫停鍵恢復", "Resumed with media play/pause key");
        R("Log.Media.HotkeyResumeFailed", "快捷键恢复失败，已放弃（{0}）", "快捷鍵恢復失敗，已放棄（{0}）", "Hotkey resume failed; gave up ({0})");
        R("Log.Media.HotkeyResumed", "已用快捷键{0}（{1}）恢复", "已用快捷鍵{0}（{1}）恢復", "Resumed with hotkey {0} ({1})");
        R("Log.Media.UnmuteOk", "已取消系统静音（离开时静音兜底；不强制恢复播放）", "已取消系統靜音（離開時靜音備援；不強制恢復播放）", "System unmuted (left muted; playback not forced)");
        R("Log.Media.UnmuteFailed", "取消系统静音失败，已放弃", "取消系統靜音失敗，已放棄", "Failed to unmute; gave up");
        R("Log.Media.ActionSendFailed", "{0} 发送失败：{1}", "{0} 傳送失敗：{1}", "{0} send failed: {1}");
        R("Log.Media.ActionEffective", "{0} 生效", "{0} 生效", "{0} worked");
        R("Log.Media.ActionEffectiveSession", "{0} 生效（峰值不可用，按会话判断）", "{0} 生效（峰值不可用，依工作階段判斷）", "{0} worked (peak unavailable; judged by session)");
        R("Log.Media.ActionIneffective", "{0} 无效，继续", "{0} 無效，繼續", "{0} ineffective; continuing");
        R("Log.Media.ActionSendFailedContinue", "{0} 发送失败，继续", "{0} 傳送失敗，繼續", "{0} send failed; continuing");
        R("Log.Media.Label.SystemPause", "系统 Pause", "系統 Pause", "System Pause");
        R("Log.Media.Label.SystemStop", "系统 Stop", "系統 Stop", "System Stop");
        R("Log.Media.Label.MediaKey", "多媒体播放/暂停键", "多媒體播放/暫停鍵", "Media play/pause key");
        R("Log.Media.Label.Hotkey", "快捷键{0}（{1}）", "快捷鍵{0}（{1}）", "Hotkey {0} ({1})");
        R("Log.Media.Method.MediaKey", "多媒体键", "多媒體鍵", "Media key");
        R("Log.Media.Method.Hotkey1", "快捷键1", "快捷鍵1", "Hotkey 1");
        R("Log.Media.Method.Hotkey2", "快捷键2", "快捷鍵2", "Hotkey 2");
        R("Log.Media.Method.Hotkey3", "快捷键3", "快捷鍵3", "Hotkey 3");
        R("Log.Media.Method.Mute", "系统静音", "系統靜音", "System mute");
        R("Log.Media.Method.None", "无", "無", "None");
        R("Log.Media.NotConfigured", "未配置", "未設定", "Not configured");
    }

    private static void RegisterLogBackup()
    {
        R("Log.Backup.ScheduleError", "备份调度异常: {0}", "備份排程異常: {0}", "Backup schedule error: {0}");
        R("Log.Backup.MissedSkipped", "备份「{0}」：错过计划已跳过 → 下次 {1}", "備份「{0}」：錯過排程已略過 → 下次 {1}", "Backup \"{0}\": missed run skipped → next {1}");
        R("Log.Backup.WatchOverflow", "备份「{0}」实时监控缓冲溢出，重建监视：{1}", "備份「{0}」即時監控緩衝溢位，重建監視：{1}", "Backup \"{0}\": realtime watch buffer overflow; rebuilding: {1}");
        R("Log.Backup.WatchStartFailed", "备份「{0}」实时监控启动失败: {1}", "備份「{0}」即時監控啟動失敗: {1}", "Backup \"{0}\": failed to start realtime watch: {1}");
        R("Log.Backup.AlreadyRunning", "备份「{0}」：已在运行，跳过（已重新排队）。", "備份「{0}」：已在執行，略過（已重新排隊）。", "Backup \"{0}\": already running; skipped (requeued).");
        R("Log.Backup.Started", "备份「{0}」[{1}]：开始", "備份「{0}」[{1}]：開始", "Backup \"{0}\" [{1}]: started");
        R("Log.Backup.CompletedWithNext", "备份「{0}」[{1}]完成：{2}；下次 {3}", "備份「{0}」[{1}]完成：{2}；下次 {3}", "Backup \"{0}\" [{1}] done: {2}; next {3}");
        R("Log.Backup.Completed", "备份「{0}」[{1}]完成：{2}", "備份「{0}」[{1}]完成：{2}", "Backup \"{0}\" [{1}] done: {2}");
        R("Log.Backup.Cancelled", "备份「{0}」[{1}]：已取消", "備份「{0}」[{1}]：已取消", "Backup \"{0}\" [{1}]: cancelled");
        R("Log.Backup.FailedRescheduled", "备份「{0}」[{1}]失败: {2}；已改期到 {3}", "備份「{0}」[{1}]失敗: {2}；已改期到 {3}", "Backup \"{0}\" [{1}] failed: {2}; rescheduled to {3}");
        R("Log.Backup.Failed", "备份「{0}」[{1}]失败: {2}", "備份「{0}」[{1}]失敗: {2}", "Backup \"{0}\" [{1}] failed: {2}");
        R("Log.Backup.Scanning", "备份「{0}」：扫描 {1} → {2}（{3}）", "備份「{0}」：掃描 {1} → {2}（{3}）", "Backup \"{0}\": scanning {1} → {2} ({3})");
        R("Log.Backup.TrashFailedSkip", "备份：回收失败，已跳过覆盖 {0}", "備份：回收失敗，已略過覆寫 {0}", "Backup: trash failed; skipped overwrite {0}");
        R("Log.Backup.ConflictSkip", "备份冲突跳过：{0}", "備份衝突略過：{0}", "Backup conflict skipped: {0}");
        R("Log.Backup.CopyFailedRetry", "复制失败({0}/{1}) {2}: {3}", "複製失敗({0}/{1}) {2}: {3}", "Copy failed ({0}/{1}) {2}: {3}");
        R("Log.Backup.TrashFailedRetry", "回收失败({0}/{1}) {2}: {3}", "回收失敗({0}/{1}) {2}: {3}", "Trash failed ({0}/{1}) {2}: {3}");
        R("Log.Backup.DeleteFailedRetry", "删除失败({0}/{1}) {2}: {3}", "刪除失敗({0}/{1}) {2}: {3}", "Delete failed ({0}/{1}) {2}: {3}");
        R("Log.Backup.Summary", "复制{0} 更新{1} 删除{2} 跳过{3} 冲突{4} 失败{5}", "複製{0} 更新{1} 刪除{2} 略過{3} 衝突{4} 失敗{5}", "Copied {0} Updated {1} Deleted {2} Skipped {3} Conflicts {4} Failed {5}");
        R("Log.Backup.Trigger.Manual", "手动备份", "手動備份", "Manual backup");
        R("Log.Backup.Trigger.Realtime", "实时备份", "即時備份", "Realtime backup");
        R("Log.Backup.Trigger.Planned", "计划备份", "排程備份", "Scheduled backup");
        R("Log.Backup.Mode.Copy", "Copy", "Copy", "Copy");
        R("Log.Backup.Mode.Mirror", "Mirror", "Mirror", "Mirror");
        R("Log.Backup.Mode.Sync", "Sync", "Sync", "Sync");
    }
}
