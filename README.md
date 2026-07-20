# WinNAS Tools

<div align="center">

[📝 简体中文](#zh-cn) | [📖 繁體中文](#zh-tw) | [🌐 English](#en)

</div>

---

<a id="zh-cn"></a>

## 📝 简体中文

- Windows 是普通人 DIY NAS 的最好系统，不服就是你对；
- 本工具尝试根据自身使用习惯让 WinNAS 更灵活，降低待机功耗；
- 当然日常 Windows 也可使用；
- Windows 11 简单测试通过，不排除有致命性 bug。

当前版本：**[v0.8.0](https://github.com/shengshk/WinNASTools/releases/tag/v0.8.0)**（早期可用，尚未视为 1.0）

### Windows 托盘小工具

- 空闲 / 归来：自动藏显窗、切电源、停开音乐、关浏览器、锁屏；
- 另含喷墨打印机维护与简易文件备份；可选定时打开链接。

### 写在前面

作者是业余人员，项目全靠 Cursor 辅助开发，精力有限，本项目以个人自用为主，所以：

- 功能类需求、改造建议请自行 **Fork**，本仓库暂不扩展功能；
- 欢迎提交**明显 Bug** 的修复 PR；提 PR 时尽量按下面格式说明，方便核对：

```markdown
## 问题截图
（界面 / 日志，能看清现象即可）
## 复现步骤
1. …
2. …
## 修复说明
- 原因：…
- 改动：…
## 校验通过
- [ ] 已按步骤自测，问题消失
- [ ] 仅修 Bug，未改功能行为
```

### 功能概览

#### 离开 / 归来

- **自动隐藏窗口**：空闲到点最小化普通窗口；归来恢复（本程序自身保持托盘）
- **自动电源模式**：空闲切节能，归来恢复性能 / 平衡（可选手动不切换）
- **自动停止音乐**：阶梯暂停（系统键 → 自定义播放/暂停快捷键 → 系统静音兜底），归来按生效方式恢复
- **自动关闭浏览器** / **自动停止应用** / **自动锁屏**（锁屏后会请求关显示器）
- **手动锁屏执行离开**：Win+L 等锁屏时可选先跑一轮离开任务
- **离开短时阻止归来**：一键 / 自动离开共用；结束后仍需连续约 2 秒活动才判定归来

#### 定时计划

- **打印机维护**：按间隔 + 时刻喷墨测试页；错过过久则跳过不补打
- **文件备份**：复制 / 镜像 / 同步；本机或 SMB / WebDAV 主机；计划或实时
- **定时打开链接**：按计划用指定浏览器打开 URL，可选延时关闭

#### 托盘与热键

- 左键打开面板；右键：一键离开、系统设置（模块 / 热键 / 日志天数 / 语言 / 开机自启等）
- 界面与日志支持 **简体中文 / 繁體中文 / English**（默认跟随系统；切换语言后自动重启）
- 默认热键：**Ctrl+Alt+Shift+L**（可改）

#### 截图

![WinNAS Tools 界面概览](docs/overview.jpg)

### 快速开始

#### 运行发布包

1. 从 [Releases](https://github.com/shengshk/WinNASTools/releases) 下载最新 `WinNASTools.exe`（当前 [v0.8.0](https://github.com/shengshk/WinNASTools/releases/tag/v0.8.0)）
2. 放到任意目录运行；首次会在 exe 同级创建 `data/`
3. 托盘图标出现后即可使用

便携目录：

```
某文件夹/
  WinNASTools.exe
  data/                 # 首次运行自动创建
    WinNasToolsConfig.json
    winnas-tools.log
    assets/
```

#### 从源码编译（Windows x64）

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
cd "WinNAS Tools"
.\publish-singlefile.bat
```

产物：`publish\WinNASTools.exe`（自包含单文件，约 70MB）。`publish\data` 不会被脚本覆盖。

也可用：

```powershell
dotnet publish ".\WinNAS Tools.App\WinNAS Tools.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ".\publish"
```

#### 配置示例

仓库内 `WinNasToolsConfig.example.json` 为结构参考。  
**请勿提交**本机真实配置：其中可能含加密后的主机密码与本机路径。

语言字段（`Ui.Language`）：`auto` | `zh-CN` | `zh-TW` | `en`。

### 目录

```
WinNAS Tools.sln
WinNAS Tools.App/          WPF 托盘与界面
WinNAS Tools.Core/         离开引擎、功能模块、备份
docs/                      截图等
publish-singlefile.bat     一键发布
WinNasToolsConfig.example.json
LICENSE                    MIT
```

### 风险提示 / 免责声明

1. 本项目仅供学习、研究与个人使用；作者不对准确性、完整性或适用性作任何保证，请自行判断并遵守所在地法律法规。
2. 使用、修改或传播本项目的任何内容，即视为已阅读并接受本声明；因使用引起的任何直接或间接后果（含数据损失、误关进程、打印耗材、隐私等），均由使用者自行承担，与作者无关。
3. 离开任务可能关闭浏览器 / 停止进程 / 切换电源 / 锁屏，请先确认配置，重要工作请先保存。
4. 备份模块涉及网络凭据时，密码仅以本机 DPAPI 等形式保护，**开源仓库不得包含真实配置与凭据**。
5. 作者保留随时修改或补充本声明的权利，恕不另行通知。

### License

[MIT](LICENSE)

<p align="right"><a href="#">⬆ 回顶部</a></p>

---

<a id="zh-tw"></a>

## 📖 繁體中文

- Windows 是普通人 DIY NAS 的最好系統，不服就是你對；
- 本工具嘗試依自身使用習慣讓 WinNAS 更靈活，降低待機功耗；
- 日常 Windows 桌面也可使用；
- Windows 11 簡單測試通過，不排除有致命性 bug。

目前版本：**[v0.8.0](https://github.com/shengshk/WinNASTools/releases/tag/v0.8.0)**（早期可用，尚未視為 1.0）

### Windows 托盤小工具

- 閒置 / 歸來：自動藏顯視窗、切電源、停開音樂、關瀏覽器、鎖定螢幕；
- 另含噴墨印表機維護與簡易檔案備份；可選定時開啟連結。

### 寫在前面

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

### 功能概覽

#### 離開 / 歸來

- **自動隱藏視窗**：閒置到點最小化一般視窗；歸來恢復（本程式自身保持托盤）
- **自動電源模式**：閒置切省電，歸來恢復效能 / 平衡（可選手動不切換）
- **自動停止音樂**：階梯暫停（系統鍵 → 自訂播放/暫停快捷鍵 → 系統靜音備援），歸來依生效方式恢復
- **自動關閉瀏覽器** / **自動停止應用** / **自動鎖定螢幕**（鎖定後會請求關閉顯示器）
- **手動鎖定執行離開**：Win+L 等鎖定時可選先跑一輪離開任務
- **離開短時阻止歸來**：一鍵 / 自動離開共用；結束後仍需連續約 2 秒活動才判定歸來

#### 定時計畫

- **印表機維護**：依間隔 + 時刻噴墨測試頁；錯過過久則略過不補打
- **檔案備份**：複製 / 鏡像 / 同步；本機或 SMB / WebDAV 主機；排程或即時
- **定時開啟連結**：依排程以指定瀏覽器開啟 URL，可選延遲關閉

#### 托盤與熱鍵

- 左鍵開啟面板；右鍵：一鍵離開、系統設定（模組 / 熱鍵 / 日誌天數 / **語言** / 開機自啟等）
- 介面與日誌支援 **简体中文 / 繁體中文 / English**（預設跟隨系統；切換語言後自動重新啟動）
- 預設熱鍵：**Ctrl+Alt+Shift+L**（可改）

#### 截圖

![WinNAS Tools 介面概覽](docs/overview.jpg)

### 快速開始

#### 執行發布包

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

#### 從原始碼編譯（Windows x64）

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

#### 設定範例

倉庫內 `WinNasToolsConfig.example.json` 為結構參考。  
**請勿提交**本機真實設定：其中可能含加密後的主機密碼與本機路徑。

語言欄位（`Ui.Language`）：`auto` | `zh-CN` | `zh-TW` | `en`。

### 目錄

```
WinNAS Tools.sln
WinNAS Tools.App/          WPF 托盤與介面
WinNAS Tools.Core/         離開引擎、功能模組、備份
docs/                      截圖等
publish-singlefile.bat     一鍵發布
WinNasToolsConfig.example.json
LICENSE                    MIT
```

### 風險提示 / 免責聲明

1. 本專案僅供學習、研究與個人使用；作者不對正確性、完整性或適用性作任何保證，請自行判斷並遵守所在地法律法規。
2. 使用、修改或傳播本專案的任何內容，即視為已閱讀並接受本聲明；因使用引起的任何直接或間接後果（含資料損失、誤關處理序、列印耗材、隱私等），均由使用者自行承擔，與作者無關。
3. 離開任務可能關閉瀏覽器 / 停止處理序 / 切換電源 / 鎖定螢幕，請先確認設定，重要工作請先儲存。
4. 備份模組涉及網路憑證時，密碼僅以本機 DPAPI 等形式保護，**開源倉庫不得包含真實設定與憑證**。
5. 作者保留隨時修改或補充本聲明的權利，恕不另行通知。

### License

[MIT](LICENSE)

<p align="right"><a href="#">⬆ 回頂部</a></p>

---

<a id="en"></a>

## 🌐 English

- Windows is a great OS for DIY NAS — feel free to disagree;
- This tool tries to make a WinNAS more flexible and lower idle power draw, based on personal habits;
- Fine for everyday Windows desktops too;
- Lightly tested on Windows 11; serious bugs may still exist.

Current release: **[v0.8.0](https://github.com/shengshk/WinNASTools/releases/tag/v0.8.0)** (early usable; not yet 1.0)

### Windows tray utility

- Idle / return: auto hide/show windows, switch power plan, stop/resume music, close browsers, lock screen;
- Also inkjet printer maintenance and simple file backup; optional scheduled URL open.

### Before you open an issue

The author is an amateur; the project is built mainly with Cursor, spare-time, for personal use:

- Feature requests / large redesigns: please **Fork**; this repo will not expand scope for now;
- Bug-fix PRs for **clear bugs** are welcome. Prefer this format:

```markdown
## Screenshots
(UI / logs that show the issue)
## Steps to reproduce
1. …
2. …
## Fix notes
- Cause: …
- Change: …
## Verified
- [ ] Reproduced fix locally
- [ ] Bug fix only; no intentional behavior change
```

### Features

#### Leave / return

- **Auto hide windows**: minimize normal windows after idle; restore on return (this app stays in the tray)
- **Auto power plan**: power saver on leave; restore Performance / Balanced on return (or Manual = no switch)
- **Auto stop music**: stepped pause (system keys → custom play/pause hotkeys → system mute fallback); resume via the method that worked
- **Auto close browser** / **Auto stop apps** / **Auto lock** (lock also requests display off)
- **Run leave on manual lock**: optional leave pass on Win+L etc.
- **Short leave grace**: shared by one-click and auto leave; then ~2 consecutive seconds of activity before return

#### Schedule

- **Printer maintenance**: inkjet test page by interval + time of day; skip if missed too long
- **File backup**: copy / mirror / sync; local or SMB / WebDAV hosts; planned or realtime
- **Scheduled open URL**: open a URL in a chosen browser on schedule; optional delayed close

#### Tray & hotkeys

- Left-click opens the panel; right-click: leave now, settings (modules / hotkey / log retention / **language** / autostart, etc.)
- UI and logs: **Simplified Chinese / Traditional Chinese / English** (default follows system; changing language restarts the app)
- Default leave hotkey: **Ctrl+Alt+Shift+L** (editable)

#### Screenshot

![WinNAS Tools overview](docs/overview.jpg)

### Quick start

#### Run the release build

1. Download `WinNASTools.exe` from [Releases](https://github.com/shengshk/WinNASTools/releases) (current [v0.8.0](https://github.com/shengshk/WinNASTools/releases/tag/v0.8.0))
2. Run from any folder; first launch creates `data/` next to the exe
3. Use the tray icon

Portable layout:

```
SomeFolder/
  WinNASTools.exe
  data/                 # created on first run
    WinNasToolsConfig.json
    winnas-tools.log
    assets/
```

#### Build from source (Windows x64)

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
cd "WinNAS Tools"
.\publish-singlefile.bat
```

Output: `publish\WinNASTools.exe` (self-contained single file, ~70MB). `publish\data` is not overwritten by the script.

Or:

```powershell
dotnet publish ".\WinNAS Tools.App\WinNAS Tools.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ".\publish"
```

#### Config sample

See `WinNasToolsConfig.example.json` for structure.  
**Do not commit** real local configs (may contain DPAPI-protected host passwords and paths).

Language (`Ui.Language`): `auto` | `zh-CN` | `zh-TW` | `en`.

### Layout

```
WinNAS Tools.sln
WinNAS Tools.App/          WPF tray & UI
WinNAS Tools.Core/         leave engine, features, backup
docs/                      screenshots, etc.
publish-singlefile.bat
WinNasToolsConfig.example.json
LICENSE                    MIT
```

### Disclaimer

1. For learning, research, and personal use only; no warranty of accuracy, completeness, or fitness. Follow local laws.
2. By using, modifying, or distributing this project you accept this disclaimer; any consequences (data loss, killed processes, ink use, privacy, etc.) are your own responsibility.
3. Leave actions may close browsers / stop processes / switch power / lock the screen — review settings and save work first.
4. Backup host passwords are protected locally (e.g. DPAPI); **never put real credentials in the public repo**.
5. This disclaimer may be updated without notice.

### License

[MIT](LICENSE)

<p align="right"><a href="#">⬆ Back to top</a></p>
