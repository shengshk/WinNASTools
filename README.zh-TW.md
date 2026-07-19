# WinNAS Tools

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md)

- Windows 是普通人 DIY NAS 的最好系統，不服就是你對；
- 本工具嘗試依自身使用習慣讓 WinNAS 更靈活，降低待機功耗；
- 日常 Windows 桌面也可使用；
- Windows 11 簡單測試通過，不排除有致命性 bug。

目前版本：**[v0.8.0](https://github.com/shengshk/WinNASTools/releases/tag/v0.8.0)**（早期可用，尚未視為 1.0）

## Windows 托盤小工具

- 閒置 / 歸來：自動藏顯視窗、切電源、停開音樂、關瀏覽器、鎖定螢幕；
- 另含噴墨印表機維護與簡易檔案備份；可選定時開啟連結。

## 寫在前面

作者是業餘人員，專案多靠 Cursor 輔助開發，精力有限，以個人自用為主，因此：

- 功能類需求、改造建議請自行 **Fork**，本倉庫暫不擴充功能；
- 歡迎提交**明顯 Bug** 的修復 PR；提 PR 時盡量依下列格式說明，方便核對：

```markdown
## 問題截圖
（介面 / 日誌，能看清現象即可）
## 重現步驟
1. …
2. …
## 修復說明
- 原因：…
- 改動：…
## 校驗通過
- [ ] 已按步驟自測，問題消失
- [ ] 僅修 Bug，未改功能行為
```

## 功能概覽

### 離開 / 歸來

- **自動隱藏視窗**：閒置到點最小化一般視窗；歸來恢復（本程式自身保持托盤）
- **自動電源模式**：閒置切省電，歸來恢復效能 / 平衡（可選手動不切換）
- **自動停止音樂**：階梯暫停（系統鍵 → 自訂播放/暫停快捷鍵 → 系統靜音備援），歸來依生效方式恢復
- **自動關閉瀏覽器** / **自動停止應用** / **自動鎖定螢幕**（鎖定後會請求關閉顯示器）
- **手動鎖定執行離開**：Win+L 等鎖定時可選先跑一輪離開任務
- **離開短時阻止歸來**：一鍵 / 自動離開共用；結束後仍需連續約 2 秒活動才判定歸來

### 定時計畫

- **印表機維護**：依間隔 + 時刻噴墨測試頁；錯過過久則略過不補打
- **檔案備份**：複製 / 鏡像 / 同步；本機或 SMB / WebDAV 主機；排程或即時
- **定時開啟連結**：依排程以指定瀏覽器開啟 URL，可選延遲關閉

### 托盤與熱鍵

- 左鍵開啟面板；右鍵：一鍵離開、系統設定（模組 / 熱鍵 / 日誌天數 / **語言** / 開機自啟等）
- 介面與日誌支援 **简体中文 / 繁體中文 / English**（預設跟隨系統；切換語言後自動重新啟動）
- 預設熱鍵：**Ctrl+Alt+Shift+L**（可改）

### 截圖

![WinNAS Tools 介面概覽](docs/overview.jpg)

## 快速開始

### 執行發布包

1. 從 [Releases](https://github.com/shengshk/WinNASTools/releases) 下載最新 `WinNASTools.exe`（目前 [v0.8.0](https://github.com/shengshk/WinNASTools/releases/tag/v0.8.0)）
2. 放到任意目錄執行；首次會在 exe 同級建立 `data/`
3. 托盤圖示出現後即可使用

可攜目錄：

```
某資料夾/
  WinNASTools.exe
  data/                 # 首次執行自動建立
    WinNasToolsConfig.json
    winnas-tools.log
    assets/
```

### 從原始碼編譯（Windows x64）

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
cd "WinNAS Tools"
.\publish-singlefile.bat
```

產物：`publish\WinNASTools.exe`（自包含單檔，約 70MB）。`publish\data` 不會被腳本覆蓋。

也可用：

```powershell
dotnet publish ".\WinNAS Tools.App\WinNAS Tools.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ".\publish"
```

### 設定範例

倉庫內 `WinNasToolsConfig.example.json` 為結構參考。  
**請勿提交**本機真實設定：其中可能含加密後的主機密碼與本機路徑。

語言欄位（`Ui.Language`）：`auto` | `zh-CN` | `zh-TW` | `en`。

## 目錄

```
WinNAS Tools.sln
WinNAS Tools.App/          WPF 托盤與介面
WinNAS Tools.Core/         離開引擎、功能模組、備份
docs/                      截圖等
publish-singlefile.bat     一鍵發布
WinNasToolsConfig.example.json
LICENSE                    MIT
```

## 風險提示 / 免責聲明

1. 本專案僅供學習、研究與個人使用；作者不對正確性、完整性或適用性作任何保證，請自行判斷並遵守所在地法律法規。
2. 使用、修改或傳播本專案的任何內容，即視為已閱讀並接受本聲明；因使用引起的任何直接或間接後果（含資料損失、誤關處理序、列印耗材、隱私等），均由使用者自行承擔，與作者無關。
3. 離開任務可能關閉瀏覽器 / 停止處理序 / 切換電源 / 鎖定螢幕，請先確認設定，重要工作請先儲存。
4. 備份模組涉及網路憑證時，密碼僅以本機 DPAPI 等形式保護，**開源倉庫不得包含真實設定與憑證**。
5. 作者保留隨時修改或補充本聲明的權利，恕不另行通知。

## License

[MIT](LICENSE)
