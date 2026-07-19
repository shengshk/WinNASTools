@echo off
cd /d "%~dp0"
taskkill /im WinNASTools.exe /f >nul 2>&1

REM 只替换程序文件，保留 publish\data（配置、日志、备份状态）
if not exist publish mkdir publish
if exist "publish\WinNASTools.exe" del /f /q "publish\WinNASTools.exe" >nul 2>&1
if exist "publish\WinNASTools.pdb" del /f /q "publish\WinNASTools.pdb" >nul 2>&1

dotnet publish "WinNAS Tools.App\WinNAS Tools.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false -o ".\publish" --source https://api.nuget.org/v3/index.json
if errorlevel 1 exit /b 1
echo.
echo Published: %~dp0publish\WinNASTools.exe
echo Data kept: %~dp0publish\data\
dir "%~dp0publish\WinNASTools.exe"
