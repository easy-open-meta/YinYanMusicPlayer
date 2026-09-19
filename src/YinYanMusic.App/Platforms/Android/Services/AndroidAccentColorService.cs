#if ANDROID
using System.Net.Http;
using Android.Graphics;
using Android.OS;
using YinYanMusic.App.Services;
// Android.Graphics 里也有一个 Color，会和 MAUI 的撞车；这里明确要的是 MAUI 的
using Color = Microsoft.Maui.Graphics.Color;

namespace YinYanMusic.App;

/// <summary>Android 端取色：下载封面 → 降采样解码 → 交给共享算法算主色。</summary>
public class AndroidAccentColorService : IAccentColorService
{
    // 进程级复用：封面是同一批图反复切，别每次新建连接池
    private static readonly HttpClient Http = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    // 只解到这么大就够了：算主色不需要原图，48×48 已经足够且几乎不耗时
    private const int TargetSize = 48;

    public async Task<Color?> ExtractAsync(string? imageUrl, CancellationToken ct = default)
    {
        var url = ApiConfig.Absolute(imageUrl);
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;

        try
        {
            var bytes = await Http.GetByteArrayAsync(url, ct);
            if (bytes.Length == 0) return null;

            var rgba = DecodeToRgba(bytes);
            return rgba is null ? null : AccentColorCalculator.FromPixels(rgba.Value.Rgba, rgba.Value.W, rgba.Value.H);
        }
        catch
        {
            return null;   // 取色失败不该影响播放页渲染，交给调用方降级
        }
    }

    private static (byte[] Rgba, int W, int H)? DecodeToRgba(byte[] bytes)
    {
        var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0) return null;

        var opts = new BitmapFactory.Options
        {
            InSampleSize = SampleSize(bounds.OutWidth, bounds.OutHeight, TargetSize),
            InPreferredConfig = Bitmap.Config.Argb8888
        };
        using var bmp = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, opts);
        if (bmp is null) return null;

        var w = bmp.Width;
        var h = bmp.Height;
        var argb = new int[w * h];
        bmp.GetPixels(argb, 0, w, 0, 0, w, h);

        var rgba = new byte[w * h * 4];
        for (var i = 0; i < argb.Length; i++)
        {
            var c = argb[i];                       // 0xAARRGGBB
            rgba[i * 4 + 0] = (byte)((c >> 16) & 0xFF);
            rgba[i * 4 + 1] = (byte)((c >> 8) & 0xFF);
            rgba[i * 4 + 2] = (byte)(c & 0xFF);
            rgba[i * 4 + 3] = (byte)((c >> 24) & 0xFF);
        }
        return (rgba, w, h);
    }

    private static int SampleSize(int w, int h, int target)
    {
        var size = 1;
        while (w / (size * 2) >= target && h / (size * 2) >= target) size *= 2;
        return size;
    }
}

/// <summary>Android 端模糊：Android 12（API 31）起用 RenderEffect，以下不支持。</summary>
public class AndroidBlurService : IBlurService
{
    public bool Apply(VisualElement view, float radius)
    {
        if (view.Handler?.PlatformView is not Android.Views.View native) return false;
        if (Build.VERSION.SdkInt < BuildVersionCodes.S) return false;   // API 31 以下没有 RenderEffect

        var density = native.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        var px = Math.Max(1f, radius * density);
        native.SetRenderEffect(RenderEffect.CreateBlurEffect(px, px, Shader.TileMode.Clamp));
        return true;
    }

    public void Clear(VisualElement view)
    {
        if (view.Handler?.PlatformView is Android.Views.View native)
            native.SetRenderEffect(null);
    }
}
#endif
