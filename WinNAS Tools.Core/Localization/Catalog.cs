namespace WinNASTools.Core.Localization;

public static partial class Catalog
{
    private static int _registered;

    public static void EnsureRegistered()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _registered, 1, 0) != 0)
            return;
        RegisterLanguage();
        RegisterTray();
        RegisterStatus();
        RegisterCommon();
        RegisterFeatures();
        RegisterUi();
        RegisterMsgBox();
        RegisterLogs();
    }

    private static void R(string key, string zhCn, string zhTw, string en) =>
        Loc.Register(key, zhCn, zhTw, en);

    private static void RegisterLanguage()
    {
        R("Language.Title", "语言", "語言", "Language");
        R("Language.Auto", "跟随系统", "跟隨系統", "Follow system");
        R("Language.ZhCn", "简体中文", "简体中文", "Simplified Chinese");
        R("Language.ZhTw", "繁體中文", "繁體中文", "Traditional Chinese");
        R("Language.En", "English", "English", "English");
        R("Language.RestartHint", "切换语言后将重启程序生效。", "切換語言後將重新啟動程式以生效。", "Restart the app to apply the new language.");
    }

    private static void RegisterTray()
    {
        R("Tray.OpenPanel", "打开面板", "開啟面板", "Open panel");
        R("Tray.LeaveNow", "一键离开", "一鍵離開", "Leave now");
        R("Tray.PowerPreference", "电源偏好", "電源偏好", "Power preference");
        R("Tray.Modules", "模块开关", "模組開關", "Modules");
        R("Tray.LeaveHotkey", "离开快捷键…", "離開快捷鍵…", "Leave hotkey…");
        R("Tray.LogRetention", "日志保存天数…", "日誌保留天數…", "Log retention…");
        R("Tray.ExportConfig", "导出配置…", "匯出設定…", "Export config…");
        R("Tray.ImportConfig", "导入配置…", "匯入設定…", "Import config…");
        R("Tray.Autostart", "开机自启", "開機自啟", "Start at login");
        R("Tray.RestoreDefaults", "恢复默认", "恢復預設", "Restore defaults");
        R("Tray.Settings", "系统设置", "系統設定", "Settings");
        R("Tray.StopMonitoring", "停止监控", "停止監控", "Stop monitoring");
        R("Tray.StartMonitoring", "开始监控", "開始監控", "Start monitoring");
        R("Tray.Exit", "退出", "結束", "Exit");
        R("Tray.NoBackupHistory", "WinNAS Tools · 暂无备份记录", "WinNAS Tools · 暫無備份記錄", "WinNAS Tools · No backup history");
        R("Tray.Backup.DefaultName", "备份", "備份", "Backup");
        R("Tray.Backup.Scanning", "{0} · 扫描中", "{0} · 掃描中", "{0} · Scanning");
        R("Tray.Backup.RunningPercent", "{0} · {1}%", "{0} · {1}%", "{0} · {1}%");
        R("Tray.Backup.Running", "{0} · 执行中", "{0} · 執行中", "{0} · Running");
        R("Tray.Backup.Cancelling", "{0} · 取消中", "{0} · 取消中", "{0} · Cancelling");
        R("Tray.Backup.StatusLine", "{0} · {1} · {2}", "{0} · {1} · {2}", "{0} · {1} · {2}");
        R("Tray.Backup.NotRun", "{0} · 未运行", "{0} · 未執行", "{0} · Not run");
        R("Tray.Backup.Cancelled", "已取消", "已取消", "Cancelled");
        R("Tray.Backup.Success", "成功", "成功", "Success");
    }

    private static void RegisterStatus()
    {
        R("Status.Prefix", "状态：", "狀態：", "Status: ");
        R("Status.Monitoring", "监控中", "監控中", "Monitoring");
        R("Status.Stopped", "已停止", "已停止", "Stopped");
        R("Status.LockedAway", " | 锁屏离开", " | 鎖定離開", " | Locked away");
        R("Status.Away", " | 离开中", " | 離開中", " | Away");
        R("Status.Exempt", " | 豁免场景", " | 豁免場景", " | Exempt");
        R("Status.GraceActive", " | 短时阻止归来中", " | 短時阻止歸來中", " | Return grace active");
        R("Status.Full", "状态：{0}{1} | 空闲 {2}s{3}{4}", "狀態：{0}{1} | 閒置 {2}s{3}{4}", "Status: {0}{1} | Idle {2}s{3}{4}");
        R("Status.HotkeyLeave", "{0} 离开", "{0} 離開", "{0} leave");
        R("Status.Default", "状态：已停止", "狀態：已停止", "Status: Stopped");
    }

    private static void RegisterCommon()
    {
        R("Unit.Second", "秒", "秒", "s");
        R("Unit.Day", "天", "天", "days");
        R("Button.Save", "保存", "儲存", "Save");
        R("Button.Pause", "暂停", "暫停", "Pause");
        R("Button.Resume", "恢复", "恢復", "Resume");
        R("Button.Settings", "设置", "設定", "Settings");
        R("Button.Clear", "清除", "清除", "Clear");
        R("Button.New", "新建", "新增", "New");
        R("Button.Delete", "删除", "刪除", "Delete");
        R("Button.Browse", "浏览", "瀏覽", "Browse");
        R("Button.Ok", "确定", "確定", "OK");
        R("Button.Cancel", "取消", "取消", "Cancel");
        R("Button.Rename", "重命名", "重新命名", "Rename");
        R("Button.Host", "主机", "主機", "Host");
        R("Button.PrintNow", "立即打印", "立即列印", "Print now");
        R("Button.OpenNow", "立即打开", "立即開啟", "Open now");
        R("Button.RunNow", "立即执行", "立即執行", "Run now");
        R("Button.Running", "执行中…", "執行中…", "Running…");
        R("Button.ContinueEdit", "继续编辑", "繼續編輯", "Continue editing");
        R("Button.Return", "返回", "返回", "Back");
        R("Button.Discard", "丢弃", "捨棄", "Discard");
        R("Label.Name", "名称", "名稱", "Name");
        R("Label.Last", "上次", "上次", "Last");
        R("Label.Next", "下次", "下次", "Next");
        R("Label.Interval", "间隔", "間隔", "Interval");
        R("Label.Time", "时刻", "時刻", "Time");
        R("Label.Method", "方式", "方式", "Method");
        R("Label.Printer", "打印机", "印表機", "Printer");
        R("Label.Image", "图片", "圖片", "Image");
        R("Label.Link", "链接", "連結", "URL");
        R("Label.Browser", "浏览器", "瀏覽器", "Browser");
        R("Label.Close", "关闭", "關閉", "Close");
        R("Label.Wait", "等待", "等待", "Wait");
        R("Label.ProcessList", "进程列表", "處理序清單", "Process list");
        R("Label.SourceA", "源 A", "來源 A", "Source A");
        R("Label.TargetB", "目标 B", "目標 B", "Target B");
        R("Label.Conflict", "冲突", "衝突", "Conflict");
        R("Label.Trash", "回收站", "資源回收筒", "Trash");
        R("Label.Exclude", "排除", "排除", "Exclude");
        R("Label.Trigger", "触发", "觸發", "Trigger");
        R("Label.App", "应用", "應用程式", "App");
        R("Label.Path", "路径", "路徑", "Path");
        R("Label.Return", "归来", "歸來", "Return");
        R("Label.Idle", "空闲", "閒置", "Idle");
        R("Label.Local", "本机", "本機", "Local");
        R("Log.FilterNormal", "运行日志 · 普通", "執行日誌 · 一般", "Log · Info");
        R("Log.FilterError", "运行日志 · 错误", "執行日誌 · 錯誤", "Log · Errors");
        R("Log.OpenFile", "文件", "檔案", "File");
        R("Power.Performance", "性能", "效能", "Performance");
        R("Power.Balanced", "平衡", "平衡", "Balanced");
        R("Power.Manual", "手动", "手動", "Manual");
        R("Power.Saver", "节能", "省電", "Power saver");
        R("Power.Custom", "自定义", "自訂", "Custom");
        R("Power.Unknown", "未知", "未知", "Unknown");
        R("Power.PreferenceMode", "电源偏好模式", "電源偏好模式", "Preferred power plan");
        R("Media.ResumeOn", "开启", "開啟", "On");
        R("Media.ResumeOff", "不开启", "不開啟", "Off");
        R("Media.HotkeyLabel", "播放/暂停快捷键 （兜底控制，可留空）", "播放/暫停快捷鍵（備援控制，可留空）", "Play/pause hotkeys (optional fallback)");
        R("AppSwitch.SelectApp", "选择应用", "選擇應用程式", "Select app");
        R("AppSwitch.SpecifyPath", "指定路径", "指定路徑", "Specify path");
        R("AppSwitch.Restart", "重启", "重新啟動", "Restart");
        R("AppSwitch.NoRestart", "不重启", "不重新啟動", "Don't restart");
        R("AppSwitch.NormalList", "普通", "一般", "Normal");
        R("AppSwitch.ComplexList", "复杂", "進階", "Advanced");
        R("Url.DoNotClose", "不关闭", "不關閉", "Keep open");
        R("Url.AutoClose", "自动关闭", "自動關閉", "Auto close");
        R("Printer.Color", "彩色", "彩色", "Color");
        R("Printer.Grayscale", "灰度", "灰階", "Grayscale");
        R("Backup.Mode.Copy", "复制 A→B", "複製 A→B", "Copy A→B");
        R("Backup.Mode.Mirror", "镜像 A→B", "鏡像 A→B", "Mirror A→B");
        R("Backup.Mode.Sync", "同步 A↔B", "同步 A↔B", "Sync A↔B");
        R("Backup.Realtime", "实时备份", "即時備份", "Realtime backup");
        R("Backup.Planned", "计划备份", "排程備份", "Scheduled backup");
        R("Backup.Conflict.Skip", "跳过", "略過", "Skip");
        R("Backup.Conflict.NewerWins", "较新为准", "以較新者為準", "Newer wins");
        R("Backup.Conflict.PreferA", "以A为准", "以 A 為準", "Prefer A");
        R("Backup.Conflict.PreferB", "以B为准", "以 B 為準", "Prefer B");
        R("Backup.Conflict.Overwrite", "覆盖", "覆寫", "Overwrite");
        R("Backup.Conflict.OverwriteTrash", "覆盖+回收站", "覆寫+資源回收筒", "Overwrite + trash");
        R("List.NotScheduled", "未排期", "未排程", "Not scheduled");
        R("List.Realtime", "实时", "即時", "Realtime");
        R("List.PausedMark", "[停] ", "[停] ", "[Paused] ");
        R("Empty.None", "（空）", "（空）", "(empty)");
    }

    private static void RegisterFeatures()
    {
        R("Feature.Window", "自动隐藏窗口", "自動隱藏視窗", "Auto hide windows");
        R("Feature.Power", "自动电源模式", "自動電源模式", "Auto power plan");
        R("Feature.Media", "自动停止音乐", "自動停止音樂", "Auto stop music");
        R("Feature.Browser", "自动关闭浏览器", "自動關閉瀏覽器", "Auto close browser");
        R("Feature.AppSwitch", "自动停止应用", "自動停止應用程式", "Auto stop apps");
        R("Feature.Lock", "自动锁屏", "自動鎖定螢幕", "Auto lock screen");
        R("Feature.LeaveGrace", "离开短时阻止归来", "離開短時阻止歸來", "Short leave grace period");
        R("Feature.ManualLockLeave", "手动锁屏执行离开", "手動鎖定執行離開", "Run leave on manual lock");
        R("Feature.MediaResume", "自动恢复音乐", "自動恢復音樂", "Auto resume music");
        R("Feature.Printer", "打印机维护", "印表機維護", "Printer maintenance");
        R("Feature.UrlLauncher", "定时打开链接", "定時開啟連結", "Scheduled open URL");
        R("Feature.Backup", "文件备份", "檔案備份", "File backup");
        R("Feature.Window.Display", "自动窗口", "自動視窗", "Auto window");
        R("Feature.Power.Display", "自动电源", "自動電源", "Auto power");
        R("Feature.Media.Display", "自动音乐", "自動音樂", "Auto media");
        R("Tab.LeaveReturn", "离开/归来", "離開/歸來", "Leave / return");
        R("Tab.Schedule", "定时计划", "定時計畫", "Schedule");
        R("ModulePanel.Title", "模块开关", "模組開關", "Modules");
        R("ModulePanel.LeaveReturn", "离开/归来", "離開/歸來", "Leave / return");
        R("ModulePanel.Schedule", "定时计划", "定時計畫", "Schedule");
    }
}
