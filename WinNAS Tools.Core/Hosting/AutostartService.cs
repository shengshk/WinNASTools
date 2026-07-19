using Microsoft.Win32;
using WinNASTools.Core;

namespace WinNASTools.Core.Hosting;

public static class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static readonly string[] RegistryNames = [AppBranding.Name, AppBranding.LegacyExeName];

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            if (key is null) return false;
            foreach (var name in RegistryNames)
            {
                var val = key.GetValue(name) as string;
                if (!string.IsNullOrWhiteSpace(val))
                    return true;
            }
            return false;
        }
        catch { return false; }
    }

    public static void SetEnabled(bool enable, string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enable)
        {
            key.SetValue(AppBranding.Name, $"\"{executablePath}\"");
            key.DeleteValue(AppBranding.LegacyExeName, throwOnMissingValue: false);
        }
        else
        {
            foreach (var name in RegistryNames)
                key.DeleteValue(name, throwOnMissingValue: false);
        }
    }
}
