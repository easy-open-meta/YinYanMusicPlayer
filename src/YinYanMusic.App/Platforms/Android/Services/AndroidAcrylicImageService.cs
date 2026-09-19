#if ANDROID
using System.Net.Http;
using Android.Graphics;
using YinYanMusic.App.Services;

namespace YinYanMusic.App;

/// <summary>
/// Android 端亚克力底图：下载封面 → 缩到 40×40 → 箱式模糊 → 编码成 PNG 交给 Image 铺底。
/// 缩放倍数极大（40 → 屏宽 1600px 左右），双线性放大本身就把残余细节抹平，
/// 所以这里只需要几趟箱式模糊，比高斯核便宜得多。
/// </summary>
public class AndroidAcrylicImageService : IAcrylicImageService
{
    private static readonly HttpClient Http = new();

    // 缓存：同封面反复开关浮窗时不重复下载与重算（浮窗最常被反复打开）
    private readonly Dictionary<string, ImageSource?> _cache = new();

    public async Task<ImageSource?> CreateAsync(string? coverUrl, CancellationToken ct = default)
    {
        var url = ApiConfig.Absolute(coverUrl);
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;

        if (_cache.TryGetValue(url, out var cached)) return cached;

        ImageSource? result = null;
        try
        {
            var bytes = await Http.GetByteArrayAsync(url, ct);
            if (bytes.Length == 0) return null;

            var argb = DecodeSmall(bytes);
            if (argb is null) return null;

            var size = AcrylicDefaults.Size;
            var rgba = ToRgba(argb);
            AcrylicBlur.Apply(rgba, size, size, AcrylicDefaults.BlurRadius);
            ToArgb(rgba, argb);

            using var bitmap = Bitmap.CreateBitmap(argb, size, size, Bitmap.Config.Argb8888!);
            if (bitmap is null) return null;

            using var ms = new MemoryStream();
            if (!bitmap.Compress(Bitmap.CompressFormat.Png!, 100, ms)) return null;

            var png = ms.ToArray();
            result = ImageSource.FromStream(() => new MemoryStream(png));
        }
        catch
        {
            return null;   // 取不到就退化成纯遮罩，不影响浮窗可用性
        }

        _cache[url] = result;
        return result;
    }

    private static int[]? DecodeSmall(byte[] bytes)
    {
        var size = AcrylicDefaults.Size;

        var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0) return null;

        var opts = new BitmapFactory.Options
        {
            InSampleSize = SampleSize(bounds.OutWidth, bounds.OutHeight, size),
            InPreferredConfig = Bitmap.Config.Argb8888
        };
        using var decoded = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, opts);
        if (decoded is null) return null;

        // 采样只保证「不小于 size」，再精确缩到 size×size
        using var scaled = Bitmap.CreateScaledBitmap(decoded, size, size, true);
        if (scaled is null) return null;

        var px = new int[size * size];
        scaled.GetPixels(px, 0, size, 0, 0, size, size);
        return px;
    }

    private static int SampleSize(int w, int h, int target)
    {
        var size = 1;
        while (w / (size * 2) >= target && h / (size * 2) >= target) size *= 2;
        return size;
    }

    private static byte[] ToRgba(int[] argb)
    {
        var rgba = new byte[argb.Length * 4];
        for (var i = 0; i < argb.Length; i++)
        {
            var c = argb[i];                      // 0xAARRGGBB
            rgba[i * 4] = (byte)((c >> 16) & 0xFF);
            rgba[i * 4 + 1] = (byte)((c >> 8) & 0xFF);
            rgba[i * 4 + 2] = (byte)(c & 0xFF);
            rgba[i * 4 + 3] = (byte)((c >> 24) & 0xFF);
        }
        return rgba;
    }

    private static void ToArgb(byte[] rgba, int[] argb)
    {
        for (var i = 0; i < argb.Length; i++)
        {
            argb[i] = (rgba[i * 4 + 3] << 24)
                      | (rgba[i * 4] << 16)
                      | (rgba[i * 4 + 1] << 8)
                      | rgba[i * 4 + 2];
        }
    }
}
#endif
