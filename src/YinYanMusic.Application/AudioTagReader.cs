using ATL;

namespace YinYanMusic.Application;

/// <summary>
/// 音频标签读取：**全项目唯一的实现**，底层用 ATL（<c>z440.atl.core</c>）。
/// <para>
/// 2026-09-23 从 TagLib 切到 ATL（只保留一个音频元数据库）。切换前拿全库 81 个文件逐首对拍过：
/// 标题 / 专辑 / 年份 / 内嵌封面**零差异**（封面逐字节相同，18 个文件两边都没有封面），
/// 只有歌手字段有语义差别 ——
/// ATL 的 <c>Artist</c> 返回**完整的多值字段**（多个歌手用 <c>;</c> 连接），
/// TagLib 的 <c>FirstPerformer</c> 只给第一个。若直接用整串，<c>"milet;Aimer;幾田りら"</c>
/// 会成为一个**新歌手**、把库里已有的 <c>milet</c> 劈成两半，所以这里取第一个（见 <see cref="FirstValue"/>）。
/// </para>
/// <para>
/// 时长**不在这里**（见 <see cref="Mp3DurationReader"/>）：时长只走 ATL 的逐帧解析 + 向下取整，
/// 这是全链路统一的约定，别用标签里的时长字段代替。
/// </para>
/// </summary>
public static class AudioTagReader
{
    /// <summary>读到的标签。没有任何标签时各字段为 null/默认值（不抛异常）。</summary>
    /// <param name="Title">标题；标签里没有则 null（调用方一般回退到文件名）。</param>
    /// <param name="Artists">
    /// **全部**歌手，已按多值分隔符拆好（至少一个元素；标签没有歌手时是 "未知艺术家"）。
    /// 第一个是主歌手（与迁移前的单歌手行为一致），其余是联合创作者。
    /// </param>
    /// <param name="Album">专辑；没有则 null。</param>
    /// <param name="Year">发行年份；没有或离谱（0 / ≥3000）则 null。</param>
    /// <param name="Cover">内嵌封面原始字节；没有则 null。</param>
    /// <param name="CoverExtension">封面扩展名（按 MIME 推断，默认 .jpg）。</param>
    public sealed record Tags(
        string? Title,
        IReadOnlyList<string> Artists,
        string? Album,
        int? Year,
        byte[]? Cover,
        string CoverExtension)
    {
        /// <summary>主歌手（列表显示、专辑归属用它）。</summary>
        public string Artist => Artists[0];
    }

    /// <summary>读全部标签。文件损坏 / 不支持的格式返回 null（调用方据此拒绝入库或降级）。</summary>
    /// <remarks>
    /// ⚠️ ATL 还有 <c>Date</c> / <c>PublishingDate</c> / <c>OriginalReleaseDate</c> 三个日期属性，
    /// **不要拿它们当发行日期**：缺失时它们返回 <c>0001-01-01</c> 而不是 null（全库实测 81 个文件
    /// 一个不落都有"值"，大部分是公元 1 年），而真有年份的文件它们也只是 <c>&lt;年&gt;-01-01</c>。
    /// 也就是说标签里能拿到的精度就是"年"，所以这里只认 <see cref="ATL.Track.Year"/>。
    /// 另外该属性对**没有扩展名**的文件同样可用（ATL 靠内容嗅探，TagLib 靠扩展名会直接失败）。
    /// </remarks>
    public static Tags? TryRead(string filePath)
    {
        try
        {
            var track = new ATL.Track(filePath);

            var title = Blank(track.Title) ? null : track.Title!.Trim();
            var artists = SplitArtists(track.Artist, track.AlbumArtist);
            var album = Blank(track.Album) ? null : track.Album!.Trim();
            var year = track.Year is > 0 and < 3000 ? track.Year : null;

            byte[]? cover = null;
            var ext = ".jpg";
            if (track.EmbeddedPictures is { Count: > 0 } pics
                && pics[0].PictureData is { Length: > 0 } data)
            {
                cover = data;
                // ATL 给的是 MIME（TagLib 那边也一样是 MIME）→ 映射成扩展名，落盘文件名要用
                ext = (pics[0].MimeType ?? string.Empty).ToLowerInvariant() switch
                {
                    var m when m.Contains("png") => ".png",
                    var m when m.Contains("webp") => ".webp",
                    _ => ".jpg"
                };
            }

            return new Tags(title, artists, album, year, cover, ext);
        }
        catch
        {
            // 与迁移前一致：坏文件由调用方当作"读取失败"处理（扫描时计入 failed 并跳过）
            return null;
        }
    }

    /// <summary>只取发行年份（不需要封面字节时用它，省一次图片解码）。失败/没有年份返回 null。</summary>
    public static int? TryReadYear(string filePath)
    {
        try
        {
            var year = new ATL.Track(filePath).Year;
            return year is > 0 and < 3000 ? year : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 把多值歌手字段拆成多个歌手。
    /// <para>
    /// 分隔符只认 <c>;</c> 和中文顿号 <c>、</c>：前者是 ATL 归一后的多值分隔符（实测
    /// <c>"Aimer;EGOIST"</c>、<c>"milet;Aimer;幾田りら"</c>），后者是中文标签里实际出现的写法
    /// （实测 <c>"陈小春、陈国坤、李灿琛…"</c>）。
    /// ⚠️ **不要按 <c>/</c> 拆**：乐队名里带斜杠很常见（AC/DC），会把一个歌手劈成两个。
    /// </para>
    /// <para>顺带做去重（忽略大小写，避免 "feat." 之类重复）、去空、限长（最多 10 位，防脏数据）。</para>
    /// <para>
    /// ⚠️ <paramref name="albumArtist"/> 只在**主歌手字段为空时**才用（与迁移前的
    /// <c>FirstPerformer ?? FirstAlbumArtist</c> 一致）。两者都取会把同一批人算两遍 ——
    /// 实测某文件 <c>Artist="Aimer;EGOIST"</c>、<c>AlbumArtist="Aimer/EGOIST"</c>（ID3v2.3 用 / 分隔），
    /// 都取就会多出一个叫 <c>"Aimer/EGOIST"</c> 的假歌手。也不要把 <c>/</c> 当分隔符：
    /// 乐队名里带斜杠很常见（AC/DC），会把一个歌手劈成两个。
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> SplitArtists(string? artist, string? albumArtist = null)
    {
        var names = Split(artist);
        if (names.Count == 0) names = Split(albumArtist);
        return names.Count > 0 ? names : ["未知艺术家"];
    }

    private static List<string> Split(string? raw)
    {
        var result = new List<string>();
        if (Blank(raw)) return result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in raw!.Split([';', '、'], StringSplitOptions.RemoveEmptyEntries))
        {
            var name = part.Trim();
            if (name.Length == 0 || !seen.Add(name)) continue;
            result.Add(name);
            if (result.Count >= 10) break;   // 限长：防脏标签造出几十个歌手
        }
        return result;
    }

    /// <summary>
    /// 歌手名里是否含多值分隔符（<c>;</c> 或 <c>、</c>）—— 用来判断一条**已存的**歌手名
    /// 是不是"多个人挤在一行"的老数据（见 DirectoryScanService 的存量修复）。
    /// </summary>
    public static bool HasArtistSeparator(string? name) =>
        !Blank(name) && name!.IndexOfAny([';', '、']) >= 0;

    private static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
}
