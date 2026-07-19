namespace WinNASTools.Core;

/// <summary>
/// 便携目录：exe 同级的 data/（配置、日志、内置资源）。
/// </summary>
public static class AppPaths
{
    /// <summary>程序根目录（WinNASTools.exe 所在目录；单文件发布时 data 写在 exe 旁）。</summary>
    public static string RootDirectory
    {
        get
        {
            var exeDir = string.IsNullOrWhiteSpace(Environment.ProcessPath)
                ? null
                : Path.GetDirectoryName(Environment.ProcessPath);
            if (!string.IsNullOrWhiteSpace(exeDir))
                return exeDir;

            var baseDir = AppContext.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrEmpty(baseDir)
                ? Environment.CurrentDirectory
                : baseDir;
        }
    }

    public static string DataDirectory => Path.Combine(RootDirectory, "data");
    public static string AssetsDirectory => Path.Combine(DataDirectory, "assets");
    public static string ConfigPath => Path.Combine(DataDirectory, "WinNasToolsConfig.json");
    public static string LegacyConfigPath => Path.Combine(DataDirectory, "config.json");
    public static string LogPath => Path.Combine(DataDirectory, "winnas-tools.log");
    public static string LegacyLogPath => Path.Combine(DataDirectory, "winnas-tools.log");

    /// <summary>内置喷墨维护测试图（相对 data 的稳定文件名）。</summary>
    public static string DefaultPrinterTestImagePath =>
        Path.Combine(AssetsDirectory, "inkjet-nozzle-test.tif");

    public static void EnsureDataLayout()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(AssetsDirectory);
        TryMigrateLegacyConfig();
        TryMigrateLegacyLog();
    }

    private static void TryMigrateLegacyConfig()
    {
        try
        {
            if (File.Exists(ConfigPath)) return;
            if (File.Exists(LegacyConfigPath))
                File.Move(LegacyConfigPath, ConfigPath);
        }
        catch { /* ignore */ }
    }

    private static void TryMigrateLegacyLog()
    {
        try
        {
            if (File.Exists(LegacyLogPath) && !File.Exists(LogPath))
                File.Copy(LegacyLogPath, LogPath, overwrite: false);
        }
        catch { /* ignore */ }
    }
}
