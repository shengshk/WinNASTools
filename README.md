# WinNAS Tools

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md)

- Windows 是普通人 DIY NAS 的最好系统，不服就是你对；
- 本工具尝试根据自身使用习惯让 WinNAS 更灵活，降低待机功耗；
- 当然日常 Windows 也可使用；
- Windows 11 简单测试通过，不排除有致命性 bug。

当前版本：**[v0.8.0](https://github.com/shengshk/WinNASTools/releases/tag/v0.8.0)**（早期可用，尚未视为 1.0）

## Windows 托盘小工具

- 空闲 / 归来：自动藏显窗、切电源、停开音乐、关浏览器、锁屏；
- 另含喷墨打印机维护与简易文件备份；可选定时打开链接。

## 写在前面

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

## 功能概览

### 离开 / 归来

- **自动隐藏窗口**：空闲到点最小化普通窗口；归来恢复（本程序自身保持托盘）
- **自动电源模式**：空闲切节能，归来恢复性能 / 平衡（可选手动不切换）
- **自动停止音乐**：阶梯暂停（系统键 → 自定义播放/暂停快捷键 → 系统静音兜底），归来按生效方式恢复
- **自动关闭浏览器** / **自动停止应用** / **自动锁屏**（锁屏后会请求关显示器）
- **手动锁屏执行离开**：Win+L 等锁屏时可选先跑一轮离开任务
- **离开短时阻止归来**：一键 / 自动离开共用；结束后仍需连续约 2 秒活动才判定归来

### 定时计划

- **打印机维护**：按间隔 + 时刻喷墨测试页；错过过久则跳过不补打
- **文件备份**：复制 / 镜像 / 同步；本机或 SMB / WebDAV 主机；计划或实时
- **定时打开链接**：按计划用指定浏览器打开 URL，可选延时关闭

### 托盘与热键

- 左键打开面板；右键：一键离开、系统设置（模块 / 热键 / 日志天数 / 语言 / 开机自启等）
- 界面与日志支持 **简体中文 / 繁體中文 / English**（默认跟随系统；切换语言后自动重启）
- 默认热键：**Ctrl+Alt+Shift+L**（可改）

### 截图

![WinNAS Tools 界面概览](docs/overview.jpg)

## 快速开始

### 运行发布包

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

### 从源码编译（Windows x64）

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

### 配置示例

仓库内 `WinNasToolsConfig.example.json` 为结构参考。  
**请勿提交**本机真实配置：其中可能含加密后的主机密码与本机路径。

语言字段（`Ui.Language`）：`auto` | `zh-CN` | `zh-TW` | `en`。

## 目录

```
WinNAS Tools.sln
WinNAS Tools.App/          WPF 托盘与界面
WinNAS Tools.Core/         离开引擎、功能模块、备份
docs/                      截图等
publish-singlefile.bat     一键发布
WinNasToolsConfig.example.json
LICENSE                    MIT
```

## 风险提示 / 免责声明

1. 本项目仅供学习、研究与个人使用；作者不对准确性、完整性或适用性作任何保证，请自行判断并遵守所在地法律法规。
2. 使用、修改或传播本项目的任何内容，即视为已阅读并接受本声明；因使用引起的任何直接或间接后果（含数据损失、误关进程、打印耗材、隐私等），均由使用者自行承担，与作者无关。
3. 离开任务可能关闭浏览器 / 停止进程 / 切换电源 / 锁屏，请先确认配置，重要工作请先保存。
4. 备份模块涉及网络凭据时，密码仅以本机 DPAPI 等形式保护，**开源仓库不得包含真实配置与凭据**。
5. 作者保留随时修改或补充本声明的权利，恕不另行通知。

## License

[MIT](LICENSE)
