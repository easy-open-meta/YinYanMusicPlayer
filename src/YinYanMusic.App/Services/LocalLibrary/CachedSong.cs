using SQLite;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Services.LocalLibrary;

/// <summary>
/// 已缓存的**在线歌曲**（V2.7）。一行 = 服务端曲库里的一首歌，音频文件已下载到本机。
///
/// <para>与 <see cref="LocalSong"/> 的本质区别：<b>它有 songId</b>，
/// 服务端知道它存在，离线播放的次数与进度要补报（V2.11）。
/// 所以这张表是「缓存索引」，不是曲库——文件被删/被清就是一行都不该留，
/// 也不该在这里做任何"用户自有元数据"的编辑。</para>
///
/// <para>冗余若干展示字段（Title / Artist / CoverUrl …）是为了**离线也能把界面画出来**：
/// 缓存管理页在无网时必须能列出歌名、歌手、封面，否则用户只能看到一串 songId。</para>
/// </summary>
[Table("CachedSong")]
public class CachedSong
{
    /// <summary>服务端 songId。天然唯一，直接当主键——同一首歌不会缓存两份。</summary>
    [PrimaryKey]
    public long SongId { get; set; }

    /// <summary>音频文件在本机的绝对路径（不含 <c>file://</c> 前缀）。</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>文件字节数。容量统计只看这一列，不去遍历目录（见设计文档 §3.7.3）。</summary>
    public long SizeBytes { get; set; }

    public DateTime CachedAtUtc { get; set; }

    /// <summary>最近一次播放时间。清理时按它排序给用户参考（本版不做 LRU 自动淘汰）。</summary>
    public DateTime? LastAccessUtc { get; set; }

    // ── 冗余展示字段（离线时列表/播放页要用） ────────────────────────────────

    public string Title { get; set; } = string.Empty;
    public string? ArtistName { get; set; }
    /// <summary>
    /// 全部歌手名，用 <c>" / "</c> 连接（联合创作才非空；单歌手 / V2.7 时候缓存进来的老行都是 NULL）。
    /// <para>
    /// 存成一行而不是列表：SQLite 这一层没有列表类型，冗余展示字段本来就是一列一个问题。
    /// 老缓存行没有这一列时退回 <see cref="ArtistName"/>（界面上只是少了合作者，不会空白）。
    /// </para>
    /// </summary>
    public string? ArtistsText { get; set; }
    public string? AlbumName { get; set; }
    /// <summary>服务端封面地址（相对路径原样存）。离线时加载失败是预期内的，UI 回退占位图。</summary>
    public string? CoverUrl { get; set; }
    /// <summary>服务端的音频相对地址。**缓存管理页播放时用它拼回远程地址**（有网优先远程）。</summary>
    public string AudioUrl { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }

    // ── 非持久化辅助字段 ───────────────────────────────────────────────────

    /// <summary>文件已丢失（被手工删除/清理工具扫掉）→ 置位，UI 给出提示。</summary>
    [Ignore]
    public bool IsMissing { get; set; }

    /// <summary>人类可读的文件大小（列表右侧直接绑它，XAML 不需要再挂转换器）。</summary>
    [Ignore]
    public string SizeText => FormatSize(SizeBytes);

    /// <summary>
    /// 界面上那一行歌手（列表直接绑它）：有冗余的全部歌手就用它，否则退回主歌手。
    /// 与 <see cref="SongDto.ArtistsDisplay"/> 同口径，所以同一首歌在缓存页和在线列表里长得一样。
    /// </summary>
    [Ignore]
    public string ArtistsDisplay =>
        string.IsNullOrEmpty(ArtistsText) ? ArtistName ?? string.Empty : ArtistsText;

    /// <summary>缓存时间文案，如「09-22 12:30 缓存」。</summary>
    [Ignore]
    public string CachedAtText => $"{CachedAtUtc.ToLocalTime():MM-dd HH:mm} 缓存";

    /// <summary>字节数 → 人类可读（B / KB / MB / GB，保留一位小数）。</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024d;
        if (kb < 1024) return $"{kb:F0} KB";
        double mb = kb / 1024d;
        if (mb < 1024) return $"{mb:F1} MB";
        return $"{mb / 1024d:F2} GB";
    }

    // ── 映射：CachedSong ⇄ SongDto ─────────────────────────────────────────

    /// <summary>
    /// 转成 UI 通用的 <see cref="SongDto"/>，让缓存歌直接复用现有列表模板、播放页与队列。
    ///
    /// <para><b>Id 用正的 songId</b>（与在线歌同一套 ID 空间，因为它就是在线歌），
    /// <b>AudioUrl 保留服务端相对地址</b>——播放时由 PlayerService 按"有网优先远程、
    /// 无网走缓存"决定真正的音源（TC-2.7-03 / TC-2.7-02）。</para>
    /// </summary>
    public SongDto ToDto() => new()
    {
        Id = SongId,
        Title = string.IsNullOrWhiteSpace(Title) ? $"歌曲 {SongId}" : Title,
        ArtistId = 0,                       // 缓存索引里不存歌手 Id，长按菜单的"歌手跳转"因此不显示
        ArtistName = ArtistName ?? string.Empty,
        // 拆回列表：离线播放时播放页的长按复制仍要能逐个人列出歌手。
        // 索引里**没存歌手 ID**（当初缓存只冗余了展示字段），所以这里给的 Id 是 0：
        // 名字照常显示，但"点歌手进详情页 / 关注"这类要 ID 的动作在离线缓存上不提供 —— 反正也没网打不开歌手页。
        // 老缓存行（V2.7 及更早）没有 ArtistsText → null → 各界面自动退回 ArtistName。
        Artists = string.IsNullOrEmpty(ArtistsText)
            ? null
            : [.. ArtistsText.Split(" / ", StringSplitOptions.RemoveEmptyEntries)
                    .Select(name => new SongArtistRef(0, name))],
        AlbumId = null,
        AlbumName = AlbumName,
        CoverUrl = CoverUrl,
        AudioUrl = AudioUrl,
        LyricUrl = null,
        DurationSeconds = DurationSeconds,
        PlayCount = 0,
        IsLiked = false,
        IsLocal = false,                    // 关键：它是**在线歌**，离线上报要走补报而不是本地计数
    };
}
