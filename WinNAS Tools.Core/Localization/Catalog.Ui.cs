namespace WinNASTools.Core.Localization;

public static partial class Catalog
{
    private static void RegisterUi()
    {
        R("Dialog.LeaveHotkey.Title", "离开快捷键", "離開快捷鍵", "Leave hotkey");
        R("Dialog.LeaveHotkey.Prompt", "在此窗口按下新的快捷键组合：", "在此視窗按下新的快捷鍵組合：", "Press the new hotkey combination in this window:");
        R("Dialog.LeaveHotkey.Hint", "需包含 Ctrl/Alt/Shift 之一", "需包含 Ctrl/Alt/Shift 之一", "Must include Ctrl, Alt, or Shift");
        R("Dialog.LeaveHotkey.RecordTitle", "播放/暂停快捷键 {0}", "播放/暫停快捷鍵 {0}", "Play/pause hotkey {0}");
        R("Dialog.LogRetention.Title", "日志保存天数", "日誌保留天數", "Log retention");
        R("Dialog.LogRetention.Description", "超过指定天数的后台日志将被自动丢弃。", "超過指定天數的後台日誌將自動捨棄。", "Background logs older than the specified days will be discarded.");
        R("Dialog.LogRetention.Label", "保留天数（1–3650）：", "保留天數（1–3650）：", "Retention days (1–3650):");
        R("Dialog.RenameTask.Title", "重命名任务", "重新命名任務", "Rename task");
        R("Dialog.RenameTask.Label", "名称：", "名稱：", "Name:");
        R("Dialog.ExportConfig.Title", "导出配置", "匯出設定", "Export config");
        R("Dialog.ImportConfig.Title", "导入配置", "匯入設定", "Import config");
        R("Dialog.FileFilter.Config", "WinNAS Tools 配置|*.json|所有文件|*.*", "WinNAS Tools 設定|*.json|所有檔案|*.*", "WinNAS Tools config|*.json|All files|*.*");
        R("Dialog.PickImage.Title", "选择打印图片", "選擇列印圖片", "Select print image");
        R("Dialog.PickImage.Filter", "图片|*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*", "圖片|*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.bmp|所有檔案|*.*", "Images|*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.bmp|All files|*.*");
        R("Dialog.PickBrowser.Title", "选择浏览器程序", "選擇瀏覽器程式", "Select browser");
        R("Dialog.PickBrowser.Filter", "浏览器程序|*.exe|所有文件|*.*", "瀏覽器程式|*.exe|所有檔案|*.*", "Browser|*.exe|All files|*.*");
        R("Dialog.PickApp.Title", "选择应用程序", "選擇應用程式", "Select application");
        R("Dialog.PickApp.Filter", "应用程序|*.exe|所有文件|*.*", "應用程式|*.exe|所有檔案|*.*", "Applications|*.exe|All files|*.*");
        R("Hotkey.NotSet", "（未设置）", "（未設定）", "(not set)");
        R("Hotkey.ClearedHint", "已清除，点确定保存", "已清除，點確定儲存", "Cleared — click OK to save");
        R("Draft.Single", "有 1 个{0}任务草稿未保存（{1}）。\n\n继续编辑：跳转到该任务\n丢弃：丢弃草稿并退出", "有 1 個{0}任務草稿未儲存（{1}）。\n\n繼續編輯：跳轉到該任務\n捨棄：捨棄草稿並結束", "1 unsaved {0} task draft ({1}).\n\nContinue editing: go to the task\nDiscard: discard draft and exit");
        R("Draft.Multiple.Header", "当前有未保存草稿：\n", "目前有未儲存草稿：\n", "Unsaved drafts:\n");
        R("Draft.Multiple.Line", "{0} 个{1}任务草稿", "{0} 個{1}任務草稿", "{0} {1} task draft(s)");
        R("Draft.Multiple.Footer", "\n\n返回：取消本次退出\n丢弃：丢弃全部草稿并退出", "\n\n返回：取消本次結束\n捨棄：捨棄全部草稿並結束", "\n\nBack: cancel exit\nDiscard: discard all drafts and exit");
        R("Draft.Category.AppSwitch", "停止应用", "停止應用程式", "stop app");
        R("Draft.Category.Printer", "打印机", "印表機", "printer");
        R("Draft.Category.Url", "打开链接", "開啟連結", "open URL");
        R("Draft.Category.Backup", "备份", "備份", "backup");
        R("Backup.HostNotConfigured", "（请先配置主机）", "（請先設定主機）", "(Configure a host first)");
        R("Backup.RootDirectory", "（根目录）", "（根目錄）", "(root)");
        R("Backup.HostUnreachable", "（无法连接主机）", "（無法連線主機）", "(Cannot reach host)");
        R("Backup.UseHostRoot", "留空：使用主机根目录", "留空：使用主機根目錄", "Empty: use host root");
        R("BackupProgress.Cancel", "取消", "取消", "Cancel");
        R("BackupProgress.Success", "成功", "成功", "Success");
        R("BackupProgress.Failed", "失败", "失敗", "Failed");
        R("BackupProgress.Cancelled", "已取消", "已取消", "Cancelled");
        R("BackupProgress.InProgress", "进行中", "進行中", "In progress");
        R("BackupProgress.Scanning", "备份「{0}」扫描中…", "備份「{0}」掃描中…", "Backup \"{0}\" scanning…");
        R("BackupProgress.Cancelling", "备份「{0}」取消中…", "備份「{0}」取消中…", "Backup \"{0}\" cancelling…");
        R("BackupProgress.RunningPercent", "备份「{0}」{1}%（{2}/{3}）", "備份「{0}」{1}%（{2}/{3}）", "Backup \"{0}\" {1}% ({2}/{3})");
        R("BackupProgress.Running", "备份「{0}」处理中…", "備份「{0}」處理中…", "Backup \"{0}\" running…");
        R("BackupProgress.Done", "备份「{0}」成功", "備份「{0}」成功", "Backup \"{0}\" succeeded");
        R("BackupProgress.Fail", "备份「{0}」失败", "備份「{0}」失敗", "Backup \"{0}\" failed");
        R("BackupProgress.CancelledFull", "备份「{0}」已取消", "備份「{0}」已取消", "Backup \"{0}\" cancelled");
        R("BackupProgress.Named", "备份「{0}」", "備份「{0}」", "Backup \"{0}\"");
        R("BackupProgress.Header", "{0} · {1}", "{0} · {1}", "{0} · {1}");
    }
}
