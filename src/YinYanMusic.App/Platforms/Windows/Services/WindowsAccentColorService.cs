#if WINDOWS
using System.Net.Http;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using YinYanMusic.App.Services;

namespace YinYanMusic.App;

/// <summary>Windows 端取色：下载封面 → BitmapDecoder 缩到 48×48 → 交给共享算法算主色。</summary>
public class WindowsAccentColorService : IAccentColorService
{
    private static readonly HttpClient Http = new();

    private const uint TargetSize = 48;

    public async Task<Color?> ExtractAsync(string? imageUrl, CancellationToken ct = default)
    {
        var url = ApiConfig.Absolute(imageUrl);
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;

        try
        {
            var bytes = await Http.GetByteArrayAsync(url, ct);
            if (bytes.Length == 0) return null;

            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
            }
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            // Straight（非预乘）alpha：预乘过的像素在半透明区域会让色相偏掉
            var data = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Rgba8,
                BitmapAlphaMode.Straight,
                new BitmapTransform { ScaledWidth = TargetSize, ScaledHeight = TargetSize },
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            var pixels = data.DetachPixelData();     // 已按 Rgba8 排列，可直接交给算法
            return AccentColorCalculator.FromPixels(pixels, (int)TargetSize, (int)TargetSize);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Windows 端模糊：暂不支持。
/// WinUI 3 的背景模糊要组合 CompositionBrush + Win2D 的 GaussianBlurEffect，
/// 得额外引入 Microsoft.Graphics.Win2D 依赖，改动面比 Android 大得多。
/// 这里先返回 false，播放页会保留「未模糊但已着色」的兜底外观。
/// </summary>
public class WindowsBlurService : IBlurService
{
    public bool Apply(VisualElement view, float radius) => false;

    public void Clear(VisualElement view) { }
}
#endif
