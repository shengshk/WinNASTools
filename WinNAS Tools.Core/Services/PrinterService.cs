using System.Drawing;
using System.Drawing.Printing;
using WinNASTools.Core;

namespace WinNASTools.Core.Services;

public static class PrinterService
{
    public static readonly TimeSpan DefaultPrintTimeout = TimeSpan.FromSeconds(60);

    public static IReadOnlyList<string> GetInstalledPrinters()
    {
        var list = new List<string>();
        foreach (string name in PrinterSettings.InstalledPrinters)
            list.Add(name);
        return list;
    }

    public static string ResolveImagePath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        var builtIn = AppPaths.DefaultPrinterTestImagePath;
        if (File.Exists(builtIn))
            return builtIn;

        throw new FileNotFoundException("找不到打印图片（自定义路径与内置测试图均不可用）。");
    }

    public static void PrintImage(string printerName, string imagePath, bool color)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new InvalidOperationException("未指定打印机。");

        var printers = GetInstalledPrinters();
        if (!printers.Any(p => string.Equals(p, printerName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"打印机不存在或未安装：{printerName}");

        using var image = Image.FromFile(imagePath);
        Image printImage = image;
        Bitmap? grayBitmap = null;
        try
        {
            if (!color)
            {
                grayBitmap = ToGrayscale(image);
                printImage = grayBitmap;
            }

            using var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = printerName;
            doc.PrinterSettings.Copies = 1;
            doc.DefaultPageSettings.Color = color;
            // 几乎无边距；纸张跟随打印机默认
            doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            doc.OriginAtMargins = false;
            // 按图片方向自动横/竖打，避免横向图被塞进纵向纸
            doc.DefaultPageSettings.Landscape = printImage.Width >= printImage.Height;
            doc.DocumentName = $"{AppBranding.Name} 打印机维护";

            doc.PrintPage += (_, e) =>
            {
                if (e.Graphics is null) return;

                // 使用英寸坐标系，避免打印机 DpiX(如 360) 与 PageBounds(1/100 英寸) 混用导致画得很小
                e.Graphics.PageUnit = GraphicsUnit.Inch;
                float pageW = e.MarginBounds.Width / 100f;
                float pageH = e.MarginBounds.Height / 100f;
                if (pageW <= 0 || pageH <= 0)
                {
                    pageW = e.PageBounds.Width / 100f;
                    pageH = e.PageBounds.Height / 100f;
                }

                float imgW = printImage.Width / printImage.HorizontalResolution;
                float imgH = printImage.Height / printImage.VerticalResolution;
                if (imgW <= 0 || imgH <= 0)
                {
                    imgW = printImage.Width / 96f;
                    imgH = printImage.Height / 96f;
                }

                float scale = Math.Min(pageW / imgW, pageH / imgH);
                float w = imgW * scale;
                float h = imgH * scale;
                float x = (pageW - w) / 2f;
                float y = (pageH - h) / 2f;
                e.Graphics.DrawImage(printImage, x, y, w, h);
                e.HasMorePages = false;
            };

            doc.Print();
        }
        finally
        {
            grayBitmap?.Dispose();
        }
    }

    /// <summary>
    /// 在线程池执行同步打印；超时后调用方恢复调度。
    /// Windows 打印 API 不支持安全中断正在进行的 Print()，因此这里只停止等待，不删除打印队列。
    /// </summary>
    public static async Task PrintImageAsync(
        string printerName,
        string imagePath,
        bool color,
        TimeSpan? timeout = null)
    {
        var printTask = Task.Run(() => PrintImage(printerName, imagePath, color));
        var limit = timeout ?? DefaultPrintTimeout;
        var completed = await Task.WhenAny(printTask, Task.Delay(limit)).ConfigureAwait(false);
        if (completed != printTask)
        {
            // 观察后台任务的最终异常，避免成为未观察异常。
            _ = printTask.ContinueWith(
                t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            throw new TimeoutException($"打印提交超过 {limit.TotalSeconds:N0} 秒，调度已停止等待。");
        }

        await printTask.ConfigureAwait(false);
    }

    private static Bitmap ToGrayscale(Image source)
    {
        var bmp = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        bmp.SetResolution(source.HorizontalResolution, source.VerticalResolution);
        using var g = Graphics.FromImage(bmp);
        var matrix = new System.Drawing.Imaging.ColorMatrix(new[]
        {
            new[] { 0.299f, 0.299f, 0.299f, 0f, 0f },
            new[] { 0.587f, 0.587f, 0.587f, 0f, 0f },
            new[] { 0.114f, 0.114f, 0.114f, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f }
        });
        using var attrs = new System.Drawing.Imaging.ImageAttributes();
        attrs.SetColorMatrix(matrix);
        g.DrawImage(source, new Rectangle(0, 0, bmp.Width, bmp.Height),
            0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
        return bmp;
    }
}
