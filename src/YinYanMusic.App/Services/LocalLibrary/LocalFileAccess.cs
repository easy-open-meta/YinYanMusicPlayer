using YinYanMusic.Core;

namespace YinYanMusic.App.Services.LocalLibrary;

/// <summary>
/// 本地文件的"存在性"判断（V2.6）。两端路径形态不同，这里收口成一个入口：
///
/// <list type="bullet">
/// <item>Windows / 普通绝对路径：直接 <see cref="File.Exists(string)"/>。</item>
/// <item>Android <c>content://</c> URI：**不能用 File.Exists**（它不是文件系统路径）。
/// 走 ContentResolver 查询，能拿到游标且行数 &gt; 0 即视为存在。</item>
/// <item><c>file://</c> 前缀：剥掉前缀再判断。</item>
/// </list>
///
/// 刻意不做"每次判断都开一次 ContentResolver"之外的缓存：扫描时对几百条记录各查一次
/// 在 Android 上是毫秒级开销，换来的正确性（文件被删立刻反映）更重要。
/// </summary>
public static class LocalFileAccess
{
    /// <summary>
    /// 把各种形态的路径规范化成可直接喂给 <see cref="File.Exists(string)"/> 或播放器的形式。
    ///
    /// <para>V2.13：实现改为委托 <see cref="MediaAddress.LocalPathOf"/> ——
    /// 这里原先自己写了一份 `file://` 前缀剥离，与 ApiConfig / ImageSourceFactory 里那份是同一个逻辑的第三份复制，
    /// 也就带着同一个缺陷：Windows 的 <c>file:///C:/x.mp3</c> 会被剥成 <c>/C:/x.mp3</c>，
    /// 交给 <see cref="File.Exists(string)"/> 必然为 false（本地歌会被误判成"文件已失效"）。
    /// 现在路径规范化只剩一份实现，两端的差异只在 <see cref="MediaAddress"/> 里注释的那一个斜杠上。</para>
    /// </summary>
    public static string Normalize(string rawPath) =>
        string.IsNullOrWhiteSpace(rawPath) ? string.Empty : MediaAddress.LocalPathOf(rawPath);

    /// <summary>该路径指向的音频是否仍可访问。</summary>
    public static bool Exists(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return false;

        if (rawPath.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            return ContentUriExists(rawPath);

        var path = Normalize(rawPath);
        try { return File.Exists(path); }
        catch { return false; }
    }

#if ANDROID
    /// <summary>
    /// Android 10+ 起 <c>_data</c> 路径在分区存储下未必可读，<c>content://</c> URI 才是权威入口。
    ///
    /// <para>两类 URI 都要支持：</para>
    /// <list type="bullet">
    /// <item><b>MediaStore</b>（<c>content://media/external/audio/media/{id}</c>）：
    /// 直接查游标即可，快。</item>
    /// <item><b>SAF</b>（<c>content://com.android.externalstorage.documents/tree/...</c>）：
    /// 这类 URI 不在 MediaStore 的 provider 里，查游标会抛异常；
    /// 改用 <c>OpenInputStream</c> 试读一下（能拿到流就说明还在且仍有授权）。</item>
    /// </list>
    /// </summary>
    private static bool ContentUriExists(string uri)
    {
        try
        {
            var context = global::Android.App.Application.Context;
            using var parsed = global::Android.Net.Uri.Parse(uri);
            if (parsed is null) return false;

            // SAF document URI：查游标不适用，直接试读
            if (uri.Contains("documents", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = context.ContentResolver?.OpenInputStream(parsed);
                return stream is not null;
            }

            using var cursor = context.ContentResolver?.Query(
                parsed, [global::Android.Provider.MediaStore.Audio.Media.InterfaceConsts.Id],
                null, null, null);
            if (cursor is not null && cursor.MoveToFirst()) return true;

            // MediaStore 查不到（可能已被移出媒体库但文件仍在）→ 退回试读，避免误判为失效
            using var fallback = context.ContentResolver?.OpenInputStream(parsed);
            return fallback is not null;
        }
        catch
        {
            // 查询异常（权限被撤、Provider 不存在）按"不可访问"处理，
            // 让上层走"文件已失效"的提示分支，而不是崩在播放里。
            return false;
        }
    }
#else
    // 非 Android 平台不会有 content:// URI（走到这里说明数据被跨端串了），一律判为不可访问。
    private static bool ContentUriExists(string uri) => false;
#endif

    /// <summary>
    /// 把路径转成 MediaElement 能吃的 <see cref="Uri"/>。
    /// 绝对文件路径要补 <c>file://</c> 前缀，否则 <see cref="Uri"/> 会当成相对路径解析失败。
    /// </summary>
    public static Uri? ToPlayableUri(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return null;

        if (rawPath.StartsWith("content://", StringComparison.OrdinalIgnoreCase) ||
            rawPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            rawPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            rawPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(rawPath, UriKind.Absolute, out var direct) ? direct : null;
        }

        var path = Normalize(rawPath);
        try
        {
            return new Uri(path);   // 绝对文件路径 → 构造函数自动补 file://
        }
        catch
        {
            return null;
        }
    }
}
