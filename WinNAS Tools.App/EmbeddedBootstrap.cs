using System.IO;
using System.Reflection;
using WinNASTools.Core;

namespace WinNASTools.App;

/// <summary>首次运行将内嵌默认资源解包到 exe 同级 data/。</summary>
internal static class EmbeddedBootstrap
{
    private static readonly (string ResourceName, string RelativePath)[] Files =
    [
        ("embedded.assets.inkjet-nozzle-test.tif", "assets/inkjet-nozzle-test.tif"),
    ];

    public static void EnsureDataFiles()
    {
        AppPaths.EnsureDataLayout();
        var asm = Assembly.GetExecutingAssembly();

        foreach (var (resource, relative) in Files)
        {
            var dest = Path.Combine(AppPaths.DataDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(dest)) continue;

            using var stream = asm.GetManifestResourceStream(resource);
            if (stream is null) continue;

            var dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var fs = File.Create(dest);
            stream.CopyTo(fs);
        }
    }
}
