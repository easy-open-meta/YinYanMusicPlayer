using SQLite;

namespace YinYanMusic.App.Services.LocalLibrary;

/// <summary>缓存历史上的事件类型（以字符串入库，便于将来扩展且库内直接可读）。</summary>
public static class CacheHistoryEvent
{
    /// <summary>下载完成、已落到本机。</summary>
    public const string Completed = "completed";
    /// <summary>用户手动移除了某一首的缓存。</summary>
    public const string Removed = "removed";
    /// <summary>用户清空了全部缓存（一条汇总记录）。</summary>
    public const string Cleared = "cleared";
    /// <summary>下载失败（重试用尽）。</summary>
    public const string Failed = "failed";
    /// <summary>「播完自动缓存」因为超过上限被跳过。</summary>
    public const string SkippedByLimit = "skipped-limit";
}

/// <summary>
/// 缓存历史的一条记录（V2.7 增强：缓存管理页的「缓存历史」标签页）。
///
/// <para>与 <see cref="CachedSong"/> 的区别：那张表是**当前索引**（删了就没了），
/// 这张表是**发生过什么的日志** —— 缓存过又被移除/清空、下载失败、自动缓存被上限跳过，
/// 都会留一条。用户据此回答"我明明缓存过怎么没了"这类问题，
/// 也能看清"哪些歌因为超上限一直没缓存上"。</para>
///
/// <para>刻意不存文件路径：历史里没有"可播放的文件"这回事，存了反而误导。</para>
/// </summary>
[Table("CacheHistory")]
public class CacheHistoryEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>服务端 songId。清空缓存那条汇总记录为 0。</summary>
    [Indexed(Name = "IX_CacheHistory_SongId")]
    public long SongId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? ArtistName { get; set; }
    public string? CoverUrl { get; set; }

    /// <summary>事件发生时刻（UTC）。</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>事件类型，取 <see cref="CacheHistoryEvent"/> 的常量。</summary>
    public string Event { get; set; } = CacheHistoryEvent.Completed;

    /// <summary>涉及的文件大小（完成=文件大小；移除=被删掉的大小；其余为 0）。</summary>
    public long SizeBytes { get; set; }

    // ── 展示用计算属性（不持久化） ──────────────────────────────────────────

    [Ignore]
    public string AtText => AtUtc.ToLocalTime().ToString("MM-dd HH:mm");

    [Ignore]
    public string EventText => Event switch
    {
        CacheHistoryEvent.Completed => "已缓存",
        CacheHistoryEvent.Removed => "已移除",
        CacheHistoryEvent.Cleared => "已清空",
        CacheHistoryEvent.Failed => "下载失败",
        CacheHistoryEvent.SkippedByLimit => "超上限跳过",
        _ => Event,
    };

    /// <summary>事件色：完成=绿、移除/清空=灰、失败=危险色、超上限=提醒色。</summary>
    [Ignore]
    public string EventColorHex => Event switch
    {
        CacheHistoryEvent.Completed => "#16A34A",
        CacheHistoryEvent.Failed => "#E5484D",
        CacheHistoryEvent.SkippedByLimit => "#F59E0B",
        _ => "#8A8AA3",
    };

    /// <summary>
    /// 事件色（直接给 XAML 绑 <c>TextColor</c> 用）。
    /// MAUI 不会把 string 自动转成 <see cref="Color"/>，绑定字符串会静默失败（文字变默认色），
    /// 所以在这里给出强类型版本。
    /// </summary>
    [Ignore]
    public Color EventColor => Color.FromArgb(EventColorHex);

    [Ignore]
    public string SizeText => SizeBytes > 0 ? CachedSong.FormatSize(SizeBytes) : string.Empty;

    [Ignore]
    public bool HasArtist => !string.IsNullOrWhiteSpace(ArtistName);

    /// <summary>
    /// 「艺术家 · 时间」副标题 —— 与「缓存完成」列表同一口径（缓存管理页三个标签页样式统一）。
    /// 没有艺术家时只显示时间。
    /// </summary>
    [Ignore]
    public string SubtitleText => HasArtist ? $"{ArtistName} · {AtText}" : AtText;
}
