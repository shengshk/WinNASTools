@echo off
cd /d "%~dp0"
dotnet publish ".\WinNAS Tools.App\WinNAS Tools.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false --source https://api.nuget.org/v3/index.json -o ".\publish"
echo.
echo Publish done. Expected: only WinNASTools.exe in .\publish
dir ".\publish"
pause
