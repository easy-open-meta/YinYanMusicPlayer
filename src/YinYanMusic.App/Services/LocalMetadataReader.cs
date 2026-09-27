using YinYanMusic.App.Services.LocalLibrary;

namespace YinYanMusic.App.Services;

/// <summary>
/// 单个本地文件的元数据解析结果。
/// </summary>
public record LocalTrackMetadata(
    string Title,
    string? ArtistName,
    string? AlbumName,
    int DurationSeconds,
    /// <summary>抽出的内嵌封面落盘路径；没有内嵌封面时为 null。</summary>
    string? CoverPath);

/// <summary>
/// 读本地音频文件的标签与内嵌封面（V2.6），底层用 ATL（<c>z440.atl.core</c>）。
///
/// <para><b>为什么用 ATL</b>：2026-09-23 起全项目只保留这一个音频元数据库 —— 服务端的
/// <c>AudioTagReader</c> / <c>Mp3DurationReader</c> 与 Scanner 用的都是它，客户端跟着用才能保证
/// "同一个文件在服务端曲库和本地曲库里显示的标题/歌手/专辑完全一致"。切换前拿全库 81 个文件对拍过
/// TagLib 与 ATL：标题/专辑/年份/封面零差异，只有多值歌手字段的语义不同（见 <see cref="FirstValue"/>）。</para>
///
/// <para>时长同样由 ATL **逐帧解析**后向下取整（与服务端 <c>Mp3DurationReader</c> 同一口径）。
/// ⚠️ 注意这与 TagLib 的 <c>Properties.Duration</c> 不同：那是估算值，ATL 解析不出的流会给 0，
/// 而 0 时长的歌会被 <c>PlayerService</c> 跳过（与服务端曲库同一规则）。</para>
/// </summary>
public static class LocalMetadataReader
{
    /// <summary>Android 侧读 <c>content://</c> 时先落到这个临时文件再解析。</summary>
    private static string TempDir => Path.Combine(FileSystem.CacheDirectory, "local-meta");

    /// <summary>
    /// 解析一个音频文件的元数据。
    /// <paramref name="filePath"/> 可以是绝对路径，也可以是 <c>content://</c> URI（Android）。
    /// </summary>
    /// <returns>解析失败（文件损坏/格式不支持）返回 null，由调用方计入 Failed，不阻断整体扫描。</returns>
    public static LocalTrackMetadata? TryRead(string filePath, string fallbackTitle)
    {
        try
        {
            var track = new ATL.Track(filePath);

            var title = FirstNonEmpty(track.Title?.Trim(), fallbackTitle);
            var artist = FirstNonEmpty(FirstValue(track.Artist), FirstValue(track.AlbumArtist));
            var album = track.Album?.Trim();

            // 与播放页进度条同口径：ATL 逐帧解析 + 向下取整
            var duration = track.DurationMs > 0 ? (int)(track.DurationMs / 1000.0) : 0;
            if (duration < 0) duration = 0;

            var coverPath = TryExtractCover(track, filePath);

            return new LocalTrackMetadata(title, artist, album, duration, coverPath);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 把内嵌封面抽成独立图片文件（AppData/local-covers）。
    /// 不把字节存进 SQLite：封面动辄几百 KB，塞进库会让列表查询明显变慢。
    /// </summary>
    private static string? TryExtractCover(ATL.Track track, string sourceFilePath)
    {
        if (track.EmbeddedPictures is not { Count: > 0 } pictures) return null;
        if (pictures[0].PictureData is not { Length: > 0 } bytes) return null;

        try
        {
            Directory.CreateDirectory(LocalLibraryStore.CoverDirectory);

            // 用"源文件路径的哈希"命名：同一文件重复扫描不会反复生成新封面，
            // 也不会因为标题里的非法字符（/ : * ? 等）导致写文件失败。
            var hash = Convert.ToHexString(
                System.Security.Cryptography.SHA1.HashData(
                    System.Text.Encoding.UTF8.GetBytes(sourceFilePath)))[..16];
            var ext = NormalizeImageExtension(pictures[0].MimeType);
            var coverPath = Path.Combine(LocalLibraryStore.CoverDirectory, $"{hash}{ext}");

            // 已存在说明这个文件的封面抽过了，直接复用
            if (File.Exists(coverPath)) return coverPath;

            File.WriteAllBytes(coverPath, bytes);
            return coverPath;
        }
        catch
        {
            // 封面抽取失败不影响入库（列表回退到占位图标）
            return null;
        }
    }

    /// <summary>MIME → 扩展名。标签里的 MimeType 有时是空的，兜底 jpg。</summary>
    private static string NormalizeImageExtension(string? mimeType) => mimeType?.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        _ => ".jpg",
    };

    private static string FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a! : (b ?? string.Empty);

    /// <summary>
    /// 多值标签取第一个值（ATL 用 <c>;</c> 连接多值，如 <c>"Aimer;EGOIST"</c>）。
    /// ⚠️ 别改成返回整串：会把 <c>"Aimer;EGOIST"</c> 当成一个新歌手，
    /// 与迁移前（TagLib <c>FirstPerformer</c>）以及服务端 <c>AudioTagReader</c> 的口径都不一致。
    /// </summary>
    private static string? FirstValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var first = raw.Split(';')[0].Trim();
        return string.IsNullOrWhiteSpace(first) ? null : first;
    }

    /// <summary>
    /// Android：把 <c>content://</c> URI 复制到缓存目录，返回本地临时文件路径。
    /// ATL 只吃文件路径（读不了 ContentProvider 流）。
    /// 调用方负责在用完后删除（见 <see cref="TryReadContentUri"/>）。
    /// </summary>
    /// <param name="contentUri">MediaStore 或 SAF 的 content:// URI。</param>
    /// <param name="displayName">
    /// 文件名（含扩展名），**必须传**。MediaStore 的 <c>LastPathSegment</c> 是纯数字 ID
    /// （<c>.../audio/media/1000006134</c>），取不到扩展名 → 临时文件没有后缀。
    /// 旧实现（TagLib）按扩展名判格式，没后缀会直接抛异常 → **整个元数据读取失败、封面全丢**
    /// （真机实测踩到：元数据看着正常是因为 MediaStore 兜底了，但封面一直是空的）；
    /// ATL 靠内容嗅探、没后缀也能读，这里继续传它是留一层保险。
    /// </param>
    public static string? TryMaterializeContentUri(string contentUri, string? displayName = null)
    {
#if ANDROID
        try
        {
            var context = global::Android.App.Application.Context;
            using var parsed = global::Android.Net.Uri.Parse(contentUri);
            if (parsed is null) return null;

            Directory.CreateDirectory(TempDir);

            // 扩展名来源优先级：调用方给的 displayName > URI 末段（SAF 的 document URI 末段带文件名）
            var ext = Path.GetExtension(displayName ?? string.Empty);
            if (string.IsNullOrEmpty(ext))
                ext = Path.GetExtension(parsed.LastPathSegment ?? string.Empty);

            var temp = Path.Combine(TempDir, $"{Guid.NewGuid():N}{ext}");

            using var input = context.ContentResolver?.OpenInputStream(parsed);
            if (input is null) return null;
            using (var output = File.Create(temp))
            {
                input.CopyTo(output);
            }
            return temp;
        }
        catch
        {
            return null;
        }
#else
        return null;
#endif
    }

    /// <summary>解析 <c>content://</c> URI：先落地成临时文件，解析完立即删掉临时文件。</summary>
    public static LocalTrackMetadata? TryReadContentUri(string contentUri, string fallbackTitle, string? displayName = null)
    {
        var temp = TryMaterializeContentUri(contentUri, displayName);
        if (temp is null) return null;

        try
        {
            var meta = TryRead(temp, fallbackTitle);
            if (meta is null) return null;

            // 封面已经在 TryRead 里抽到正式目录了，这里只把临时音频文件清掉
            return meta;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }
}
