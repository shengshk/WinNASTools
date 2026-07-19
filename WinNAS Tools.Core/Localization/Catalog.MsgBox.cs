namespace WinNASTools.Core.Localization;

public static partial class Catalog
{
    private static void RegisterMsgBox()
    {
        R("Msg.OpenLogFailed", "无法打开日志文件：{0}", "無法開啟日誌檔案：{0}", "Cannot open log file: {0}");
        R("Msg.HotkeyInvalid", "快捷键无效。", "快捷鍵無效。", "Invalid hotkey.");
        R("Msg.HotkeyRecordRequired", "请先按下有效的快捷键组合。", "請先按下有效的快捷鍵組合。", "Press a valid hotkey combination first.");
        R("Msg.HotkeyRegisterFailed", "热键「{0}」注册失败（可能被占用），已保持原快捷键。", "熱鍵「{0}」註冊失敗（可能已被占用），已保留原快捷鍵。", "Failed to register hotkey \"{0}\" (it may be in use). Kept the previous hotkey.");
        R("Msg.ExportFailed", "导出失败：{0}", "匯出失敗：{0}", "Export failed: {0}");
        R("Msg.ImportInvalid", "配置不合法，已取消导入。\n\n{0}", "設定不合法，已取消匯入。\n\n{0}", "Invalid config; import cancelled.\n\n{0}");
        R("Msg.ImportConfirm", "导入将覆盖当前配置并重启程序，是否继续？", "匯入將覆寫目前設定並重新啟動程式，是否繼續？", "Import will overwrite current settings and restart the app. Continue?");
        R("Msg.ImportFailed", "导入失败：{0}", "匯入失敗：{0}", "Import failed: {0}");
        R("Msg.LogRetentionInvalid", "请输入 1–3650 之间的整数。", "請輸入 1–3650 之間的整數。", "Enter an integer between 1 and 3650.");
        R("Msg.PrinterValidation", "请填写有效的名称、打印机和计划时间。", "請填寫有效的名稱、印表機與排程時間。", "Enter a valid name, printer, and schedule.");
        R("Msg.ImageNotFound", "图片文件不存在。", "圖片檔案不存在。", "Image file not found.");
        R("Msg.SelectPrinterFirst", "请先选择打印机。", "請先選擇印表機。", "Select a printer first.");
        R("Msg.PrintFailed", "打印失败", "列印失敗", "Print failed");
        R("Msg.OpenUrlFailed", "打开链接失败", "開啟連結失敗", "Open URL failed");
        R("Msg.BackupFailed", "备份失败", "備份失敗", "Backup failed");
        R("Msg.TaskNameRequired", "请输入任务名称。", "請輸入任務名稱。", "Enter a task name.");
        R("Msg.SelectAppRequired", "请从列表选择目标应用。", "請從清單選擇目標應用程式。", "Select a target app from the list.");
        R("Msg.AppPathRequired", "请填写或浏览 exe 路径。", "請填寫或瀏覽 exe 路徑。", "Enter or browse to an exe path.");
        R("Msg.AppPathExeOnly", "路径模式仅支持 .exe 文件。", "路徑模式僅支援 .exe 檔案。", "Path mode supports .exe files only.");
        R("Msg.AppNameFromPath", "无法从路径识别应用名。", "無法從路徑識別應用程式名稱。", "Cannot detect app name from path.");
        R("Msg.IdleSecondsInvalid", "空闲秒数必须为不小于 0 的整数。", "閒置秒數必須為不小於 0 的整數。", "Idle seconds must be an integer ≥ 0.");
        R("Msg.PathFileMissing", "路径文件不存在，仍要保存吗？", "路徑檔案不存在，仍要儲存嗎？", "Path file not found. Save anyway?");
        R("Msg.UrlRequired", "请输入有效的 HTTP/HTTPS 链接。", "請輸入有效的 HTTP/HTTPS 連結。", "Enter a valid HTTP/HTTPS URL.");
        R("Msg.BrowserNotFound", "浏览器程序不存在。", "瀏覽器程式不存在。", "Browser executable not found.");
        R("Msg.UrlCloseDelayInvalid", "关闭等待秒数必须为不小于 0 的整数。", "關閉等待秒數必須為不小於 0 的整數。", "Close delay must be an integer ≥ 0.");
        R("Msg.ScheduleInvalid", "计划时间必须为有效的间隔天数和时刻。", "排程時間必須為有效的間隔天數與時刻。", "Schedule must have valid interval days and time.");
        R("Msg.TrashPathRequired", "请填写回收站路径。", "請填寫資源回收筒路徑。", "Enter a trash folder path.");
        R("Msg.TrashPathAbsolute", "本机回收站路径必须是绝对路径。", "本機資源回收筒路徑必須是絕對路徑。", "Local trash path must be absolute.");
        R("Msg.LocalDirRequired", "{0}必须是已存在的本机绝对目录。", "{0}必須是已存在的本機絕對目錄。", "{0} must be an existing local absolute directory.");
        R("Msg.HostNotFound", "{0}所选主机不存在，请重新选择。", "{0}所選主機不存在，請重新選擇。", "Selected host for {0} not found. Choose again.");
        R("Msg.DirNotFound", "{0}目录不存在。", "{0}目錄不存在。", "{0} directory not found.");
        R("Msg.Config.FileNotFound", "配置文件不存在。", "設定檔案不存在。", "Config file not found.");
        R("Msg.Config.FileEmpty", "配置文件为空。", "設定檔案為空。", "Config file is empty.");
        R("Msg.Config.RootNotObject", "配置根节点必须是 JSON 对象。", "設定根節點必須是 JSON 物件。", "Config root must be a JSON object.");
        R("Msg.Config.ParseFailed", "无法解析为有效配置。", "無法解析為有效設定。", "Failed to parse a valid config.");
        R("Msg.Config.RetentionRange", "日志保留天数必须在 1～3650 之间。", "日誌保留天數必須在 1～3650 之間。", "Log retention days must be between 1 and 3650.");
        R("Msg.Config.GraceMin", "离开短时阻止归来秒数必须 ≥ 1。", "離開短時阻止歸來秒數必須 ≥ 1。", "Return grace seconds must be ≥ 1.");
        R("Msg.Config.UrlLauncherInvalid", "定时打开链接配置无效。", "定時開啟連結設定無效。", "Scheduled URL config is invalid.");
        R("Msg.Config.UrlScheduleInvalid", "打开链接任务「{0}」的计划时间无效。", "開啟連結任務「{0}」的排程時間無效。", "Open URL task \"{0}\" has an invalid schedule.");
        R("Msg.Config.UrlCloseDelayInvalid", "打开链接任务「{0}」的关闭等待时间不能小于 0。", "開啟連結任務「{0}」的關閉等待時間不能小於 0。", "Open URL task \"{0}\" close delay cannot be negative.");
        R("Msg.Config.UrlInvalid", "打开链接任务「{0}」的链接必须是有效的 HTTP/HTTPS 地址。", "開啟連結任務「{0}」的連結必須是有效的 HTTP/HTTPS 位址。", "Open URL task \"{0}\" URL must be a valid HTTP/HTTPS address.");
        R("Msg.Config.JsonInvalid", "JSON 无效：{0}", "JSON 無效：{0}", "Invalid JSON: {0}");
        R("Msg.Config.ReadFailed", "读取失败：{0}", "讀取失敗：{0}", "Failed to read: {0}");
    }
}
