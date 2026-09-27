namespace YinYanMusic.Core.Entities;

/// <summary>
/// 播放进度（V2.10，断点续播）。语义是"换设备从上次的位置接着听"，
/// **不做**一台播放另一台实时跟随（那需要长连接，量级翻倍）。
///
/// <para>每用户每歌一行（复合主键），后写覆盖先写 —— 不需要历史，只需要"最后一次听到哪"。</para>
/// <para>
/// 只有**服务端曲库里有 songId** 的歌才会有记录：本地音乐（V2.6）没有 songId，压根不上报；
/// 缓存歌（V2.7）有 songId，照常记录。
/// </para>
/// </summary>
public class PlaybackProgress
{
    public long UserId { get; set; }
    public User User { get; set; } = default!;

    public long SongId { get; set; }
    public Song Song { get; set; } = default!;

    /// <summary>播放位置（秒）。</summary>
    public double PositionSeconds { get; set; }

    /// <summary>
    /// 上报设备名（V2.12）。客户端取 <c>DeviceInfo.Name</c>（Android 是机型、Windows 是主机名），
    /// 展示在"继续播放"入口上，用户能看出"这份进度来自哪台设备"。截断到 64 字符防脏数据撑爆列宽。
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>最后一次上报的时间。取"最近一条"就按它排序；同秒内再按 SongId 兜底保证稳定。</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 离线播放补报（V2.11）：客户端离线听完缓存歌后，联网把 <see cref="PendingPlayReport"/>（App 本地 SQLite）
/// 批量补交上来。每行 = 一次真实播放，按 <see cref="ClientReportKey"/> 幂等 —— 重复提交原样忽略，**不重复计数**。
///
/// <para>为什么单独建表而不在 <see cref="Song.PlayCount"/> 上想办法：PlayCount 只是总数，
/// <see cref="UserId"/> 让"谁在什么时候听过"可回溯（将来"我的播放历史"直接查它）。</para>
/// </summary>
public class PlayReport
{
    /// <summary>客户端生成的幂等键（GUID）。服务端唯一索引，冲突即已收过。</summary>
    public string ClientReportKey { get; set; } = string.Empty;

    public long UserId { get; set; }
    public User User { get; set; } = default!;

    public long SongId { get; set; }
    public Song Song { get; set; } = default!;

    /// <summary>播放发生的时刻（UTC）。统计口径用它，而不是"上报时刻"。</summary>
    public DateTime PlayedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
