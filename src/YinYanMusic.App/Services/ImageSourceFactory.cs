using YinYanMusic.Core;

namespace YinYanMusic.App.Services;

/// <summary>
/// 把「可能是后端相对路径 / 本地绝对路径 / <c>file://</c> / <c>content://</c> / <c>data:</c>」
/// 的字符串统一转成 <see cref="ImageSource"/>。
///
/// <para><b>为什么要有这个类</b>：XAML 侧本来有 <c>AbsoluteUrlConverter</c>，
/// 但代码后置侧（播放页长按封面预览、保存封面）又各自写了一套，
/// 结果本地曲库上线后真机踩坑 —— 预览用 <c>ImageSource.FromUri(new Uri(path))</c>，
/// 遇到 <c>/data/user/0/&lt;pkg&gt;/files/local-covers/x.jpg</c> 直接解析失败，预览一片空白。
/// 现在两边共用同一份判断，不会再出现"列表能显示、预览却是空白"这种不一致。</para>
/// </summary>
public static class ImageSourceFactory
{
    /// <summary>
    /// 转成 <see cref="ImageSource"/>；无法识别时返回 null（调用方自行降级）。
    /// </summary>
    public static ImageSource? From(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // data URI（V2.5 头像存库格式）
        if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = DecodeDataUri(raw);
            return bytes is null ? null : ImageSource.FromStream(() => new MemoryStream(bytes));
        }

        // 本地文件判定统一走 Core/MediaAddress（V2.13），这里不再自己维护一套前缀规则
        if (MediaAddress.IsLocalFile(raw))
        {
            try
            {
                // content:// 要走 ContentResolver 拿流；FromFile 只认文件系统路径
                if (raw.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
                    return FromContentUri(raw);

                return ImageSource.FromFile(MediaAddress.LocalPathOf(raw));
            }
            catch { return null; }
        }

        var url = ApiConfig.Absolute(raw);
        if (string.IsNullOrEmpty(url)) return null;
        try
        {
#if ANDROID
            // V2.16 全局公平内存机制：Android 上所有网络图片统一走 LruCache（按可用内存分配），
            // 同一张封面/头像只下载一次，缓存命中时直接复用原始字节，避免重复网络请求。
            var cache = ServiceHelper.GetService<AndroidBitmapCache>();
            if (cache is not null)
                return new StreamImageSource { Stream = token => LoadCachedBytesAsync(url, cache, token) };
#endif
            return ImageSource.FromUri(new Uri(url));
        }
        catch { return null; }
    }

#if ANDROID
    private static readonly HttpClient Http = new();

    private static async Task<Stream> LoadCachedBytesAsync(string url, AndroidBitmapCache cache, CancellationToken token)
    {
        var cached = cache.GetBytes(url);
        if (cached is not null) return new MemoryStream(cached);

        var bytes = await Http.GetByteArrayAsync(url, token);
        cache.PutBytes(url, bytes);
        return new MemoryStream(bytes);
    }
#endif

    /// <summary>
    /// 读出原始字节。给"保存封面到相册"、通知栏封面、亚克力底图、主题色取色这些
    /// "只要字节、不要 ImageSource"的场景用 —— 它们不能只处理 http，
    /// 本地歌的封面是磁盘上的文件、Android 上还可能是 <c>content://</c>。
    ///
    /// <para><b>V2.13 起建议所有这类调用都走本方法</b>：地址形态的判断只有这一处，
    /// 之前亚克力底图/取色各自写了一遍"先 ApiConfig.Absolute 再要求结果以 http 开头"，
    /// 于是本地歌永远取不到封面（拼出假 URL 后 404，或被那道守卫直接挡掉）。</para>
    /// </summary>
    /// <param name="raw">任意形态的地址：http(s) / <c>data:</c> / <c>file://</c> / <c>content://</c> / 盘符 / 裸绝对路径。</param>
    /// <param name="http">
    /// 外部传入的 HttpClient。**进程级复用的调用方应当传自己的静态实例** ——
    /// 不传的话每次都要 new 一个（新连接池、远程封面会多一次 TLS 握手）。
    /// 只由本方法负责释放它自己 new 出来的那个，传进来的实例不动。
    /// </param>
    public static async Task<byte[]?> ReadBytesAsync(string? raw, HttpClient? http = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return DecodeDataUri(raw);

        if (raw.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            return await ReadContentUriBytesAsync(raw);

        // 本地文件判定统一走 Core/MediaAddress（V2.13）
        if (MediaAddress.IsLocalFile(raw))
        {
            try { return await File.ReadAllBytesAsync(MediaAddress.LocalPathOf(raw), ct); }
            catch { return null; }
        }

        var ownsClient = http is null;
        var client = http ?? new HttpClient();
        try
        {
            return await client.GetByteArrayAsync(ApiConfig.Absolute(raw), ct);
        }
        catch { return null; }
        finally
        {
            if (ownsClient) client.Dispose();
        }
    }

    private static byte[]? DecodeDataUri(string dataUri)
    {
        try
        {
            var comma = dataUri.IndexOf(',');
            if (comma < 0) return null;
            // 注意限定 System.Convert：本命名空间下没有同名方法，但保持与旧代码一致的显式写法
            return System.Convert.FromBase64String(dataUri[(comma + 1)..]);
        }
        catch { return null; }
    }

#if ANDROID
    /// <summary>
    /// Android 的 <c>content://</c> 图片：先经 ContentResolver 拿流再交给 FromStream。
    /// <see cref="ImageSource.FromFile(string)"/> 只认文件系统路径，喂 content URI 会加载失败。
    /// </summary>
    private static ImageSource? FromContentUri(string contentUri)
    {
        var bytes = ReadContentUriBytes(contentUri);
        return bytes is null ? null : ImageSource.FromStream(() => new MemoryStream(bytes));
    }

    private static byte[]? ReadContentUriBytes(string contentUri)
    {
        try
        {
            var context = global::Android.App.Application.Context;
            using var parsed = global::Android.Net.Uri.Parse(contentUri);
            if (parsed is null) return null;

            using var input = context.ContentResolver?.OpenInputStream(parsed);
            if (input is null) return null;

            using var ms = new MemoryStream();
            input.CopyTo(ms);
            return ms.ToArray();
        }
        catch { return null; }
    }

    private static Task<byte[]?> ReadContentUriBytesAsync(string contentUri) =>
        Task.FromResult(ReadContentUriBytes(contentUri));
#else
    // 非 Android 平台不会有 content:// URI（走到这里说明数据被跨端串了），一律当不可用。
    private static ImageSource? FromContentUri(string contentUri) => null;
    private static Task<byte[]?> ReadContentUriBytesAsync(string contentUri) => Task.FromResult<byte[]?>(null);
#endif
}
