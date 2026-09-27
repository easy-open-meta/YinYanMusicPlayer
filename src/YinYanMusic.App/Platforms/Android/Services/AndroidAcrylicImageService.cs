#if ANDROID
using System.Net.Http;
using Android.Graphics;
using YinYanMusic.App.Services;

namespace YinYanMusic.App;

/// <summary>
/// Android 端亚克力底图：下载封面 → 缩到 40×40 → 箱式模糊 → 编码成 PNG 交给 Image 铺底。
/// 缩放倍数极大（40 → 屏宽 1600px 左右），双线性放大本身就把残余细节抹平，
/// 所以这里只需要几趟箱式模糊，比高斯核便宜得多。
/// 处理结果按 URL 缓存在 <see cref="AndroidBitmapCache"/>（官方 LruCache，按可用内存分配），
/// 同封面反复开关浮窗时不重复下载与重算。
/// </summary>
public class AndroidAcrylicImageService : IAcrylicImageService
{
    private static readonly HttpClient Http = new();

    private readonly AndroidBitmapCache _bitmapCache;

    public AndroidAcrylicImageService(AndroidBitmapCache bitmapCache)
    {
        _bitmapCache = bitmapCache;
    }

    public async Task<ImageSource?> CreateAsync(string? coverUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(coverUrl)) return null;

        // V2.13：字节来源统一走 ImageSourceFactory.ReadBytesAsync。
        // 这里原先写的是「ApiConfig.Absolute(coverUrl) 然后要求结果以 http 开头」——
        // 本地歌的封面是磁盘路径 / content://，前者会被拼成假 URL 后 404，后者直接被那道守卫挡掉，
        // 结果是本地歌永远没有亚克力底图（静默降级成纯遮罩，所以一直没被发现）。
        // 现在 http / data: / file:// / content:// / 盘符 / 裸路径都能出字节。
        // 缓存键改用**原始地址**：同一张封面在"绝对 URL"与"本地路径"两种形态下语义不同，
        // 用原始串做键最省心，也不会在改服务器地址后命中旧缓存。
        if (_bitmapCache.GetBitmap(coverUrl) is { } cached)
            return BitmapToImageSource(cached);

        Bitmap? bitmap = null;
        try
        {
            var bytes = await ImageSourceFactory.ReadBytesAsync(coverUrl, Http, ct);
            if (bytes is null || bytes.Length == 0) return null;

            var argb = DecodeSmall(bytes);
            if (argb is null) return null;

            var size = AcrylicDefaults.Size;
            var rgba = ToRgba(argb);
            AcrylicBlur.Apply(rgba, size, size, AcrylicDefaults.BlurRadius);
            ToArgb(rgba, argb);

            bitmap = Bitmap.CreateBitmap(argb, size, size, Bitmap.Config.Argb8888!);
            if (bitmap is null) return null;
        }
        catch
        {
            return null;   // 取不到就退化成纯遮罩，不影响浮窗可用性
        }

        _bitmapCache.PutBitmap(coverUrl, bitmap);
        return BitmapToImageSource(bitmap);
    }

    private static ImageSource? BitmapToImageSource(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        if (!bitmap.Compress(Bitmap.CompressFormat.Png!, 100, ms)) return null;
        var png = ms.ToArray();
        return ImageSource.FromStream(() => new MemoryStream(png));
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