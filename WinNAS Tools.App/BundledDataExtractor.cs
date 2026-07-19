using System.IO;
using System.Reflection;
using WinNASTools.Core;

namespace WinNASTools.App;

/// <summary>首次运行将内嵌资源解包到 exe 同级 data/。</summary>
public static class BundledDataExtractor
{
    private static readonly (string Resource, string Target)[] Files =
    [
        ("bundled.assets.inkjet-nozzle-test.tif", AppPaths.DefaultPrinterTestImagePath)
    ];

    public static void EnsureRuntimeData()
    {
        AppPaths.EnsureDataLayout();
        var asm = Assembly.GetExecutingAssembly();
        foreach (var (resource, target) in Files)
            ExtractIfMissing(asm, resource, target);
    }

    private static void ExtractIfMissing(Assembly asm, string resourceName, string targetPath)
    {
        if (File.Exists(targetPath)) return;

        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null) return;

        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var fs = File.Create(targetPath);
        stream.CopyTo(fs);
    }
}
