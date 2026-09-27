#if WINDOWS
using System.Net.Http;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using YinYanMusic.App.Services;

namespace YinYanMusic.App;

/// <summary>
/// Windows 端亚克力底图：与 Android 同一套算法（缩到 40×40 + 箱式模糊），
/// 差别只在解码与编码——这里走 WIC 的 BitmapDecoder / BitmapEncoder。
/// </summary>
public class WindowsAcrylicImageService : IAcrylicImageService
{
    private static readonly HttpClient Http = new();

    private readonly Dictionary<string, ImageSource?> _cache = new();

    public async Task<ImageSource?> CreateAsync(string? coverUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(coverUrl)) return null;

        // V2.13：字节来源统一走 ImageSourceFactory —— 本地歌（Windows 上是盘符路径）
        // 原先会被 ApiConfig.Absolute 拼成 http://host/C:/... 后 404，或者被"必须 http 开头"的
        // 守卫直接挡掉，于是本地歌永远没有底图。缓存键用原始地址（理由同 Android 侧）。
        if (_cache.TryGetValue(coverUrl, out var cached)) return cached;

        ImageSource? result = null;
        try
        {
            var bytes = await ImageSourceFactory.ReadBytesAsync(coverUrl, Http, ct);
            if (bytes is null || bytes.Length == 0) return null;

            var size = AcrylicDefaults.Size;
            var rgba = await DecodeSmallAsync(bytes, size, ct);
            if (rgba is null) return null;

            AcrylicBlur.Apply(rgba, size, size, AcrylicDefaults.BlurRadius);

            using var outStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
            encoder.SetPixelData(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight,
                (uint)size, (uint)size, 96, 96, rgba);
            await encoder.FlushAsync();

            outStream.Seek(0);
            var length = (uint)outStream.Size;
            var png = new byte[length];
            using (var reader = new DataReader(outStream.GetInputStreamAt(0)))
            {
                await reader.LoadAsync(length);
                reader.ReadBytes(png);
            }

            result = ImageSource.FromStream(() => new MemoryStream(png));
        }
        catch
        {
            return null;   // 取不到就退化成纯遮罩，不影响浮窗可用性
        }

        _cache[coverUrl] = result;
        return result;
    }

    private static async Task<byte[]?> DecodeSmallAsync(byte[] bytes, int size, CancellationToken ct)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
        }
        ct.ThrowIfCancellationRequested();
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        // Rgba8 + Straight（非预乘）：与本算法的按通道求平均一致
        var data = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Rgba8,
            BitmapAlphaMode.Straight,
            new BitmapTransform { ScaledWidth = (uint)size, ScaledHeight = (uint)size },
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);

        return data.DetachPixelData();
    }
}
#endif
