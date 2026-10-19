using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HeartRateMonitor.App;

/// <summary>
/// 窗口 / 任务栏 / 托盘图标。
/// 图标以清单资源（EmbeddedResource）方式内嵌，直接通过程序集资源流解码，
/// 不使用 pack:// URI —— 后者在单文件发布（PublishSingleFile）下可能解析失败。
/// </summary>
internal static class AppIcon
{
    private const string ResourceName = "HeartRateMonitor.App.Assets.app.ico";

    private static bool _loaded;
    private static ImageSource? _image;
    private static string? _iconFile;

    /// <summary>
    /// 获取应用图标；加载失败时返回 <c>null</c>（此时窗口会退回使用 exe 内嵌图标）。
    /// </summary>
    internal static ImageSource? Image
    {
        get
        {
            if (_loaded) return _image;
            _loaded = true;

            try
            {
                using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName);
                if (stream is not null)
                {
                    var decoder = new IconBitmapDecoder(
                        stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);

                    // 取最大的一帧(256×256), 由系统按需缩放, 避免任务栏图标被放大而模糊
                    BitmapFrame? largest = null;
                    foreach (var frame in decoder.Frames)
                    {
                        if (largest is null || frame.PixelWidth > largest.PixelWidth)
                            largest = frame;
                    }

                    _image = largest;
                }
            }
            catch
            {
                _image = null;
            }

            return _image;
        }
    }

    /// <summary>
    /// 把内嵌 .ico 释放到本地临时文件，供 Win32 托盘 API（LoadImage）使用。
    /// 仍然兼容单文件发布（不依赖磁盘上的文件路径）。
    /// </summary>
    internal static string? EnsureTrayIconFile()
    {
        if (_iconFile is not null && File.Exists(_iconFile)) return _iconFile;

        try
        {
            using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName);
            if (stream is null) return null;

            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HeartRateMonitor");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, "tray.ico");
            using (var file = File.Create(path))
            {
                stream.CopyTo(file);
            }

            _iconFile = path;
            return path;
        }
        catch
        {
            return null;
        }
    }
}
