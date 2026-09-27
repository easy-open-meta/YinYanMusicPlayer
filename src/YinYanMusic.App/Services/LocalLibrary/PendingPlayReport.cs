using SQLite;

namespace YinYanMusic.App.Services.LocalLibrary;

/// <summary>
/// 离线播放待补报队列的一行（V2.7 只写不读，V2.11 消费）。
///
/// <para>为什么 V2.7 就要建这张表：缓存歌曲的播放**必须**上报（它是服务端曲库的歌，
/// 是统计与推荐的数据源），但离线时发不出去。如果等到 V2.11 再做，V2.7 上线到 V2.11
/// 之间的离线播放就永久丢了，而且"边缓存边丢数据"最难解释。所以本版只负责**留痕**：
/// 每一次"离线播放缓存歌"就往队列里写一条，V2.11 负责批量 flush。</para>
///
/// <para><b>幂等键是客户端 GUID</b>（<see cref="ClientReportKey"/>），不是时间桶——
/// 时间桶会把"同一分钟内切歌重播"误判成重复，也可能漏判跨分钟的重复上报
/// （见本地备忘 2026-09-18「V2.11 补报的存储与传输格式」）。</para>
/// </summary>
[Table("PendingPlayReport")]
public class PendingPlayReport
{
    /// <summary>客户端生成的 GUID（32 位无连字符）。服务端按它做唯一键幂等去重。</summary>
    [PrimaryKey]
    public string ClientReportKey { get; set; } = string.Empty;

    /// <summary>服务端 songId。补报时若返回 unknownSongs（歌已被后台删除），本地丢弃这条、不重试。</summary>
    [Indexed(Name = "IX_PendingPlayReport_SongId")]
    public long SongId { get; set; }

    /// <summary>
    /// 入队时**本地登录的账号 Id**（V2.11，3.11.6 已知问题修复）。flush 时只提交属于当前账号的行：
    /// A 离线攒的播放绝不能记到后来登录的 B 名下。老版本（V2.7–V2.10）写入的行此列为 0，
    /// 归属无法追溯，flush 时一并按"当前账号"处理（量级很小，且补报本来就是尽力而为）。
    /// </summary>
    public long UserId { get; set; }

    /// <summary>播放发生的时刻（UTC）。服务端统计口径用它，而不是"上报时刻"。</summary>
    public DateTime PlayedAtUtc { get; set; }

    /// <summary>播放起始位置（秒）。V2.7 的离线播放恒为 0；V2.10 起会带上真实进度。</summary>
    public int PositionSeconds { get; set; }

    /// <summary>入队时刻。只用于本地裁剪（队列过长时丢最旧的）与排查。</summary>
    public DateTime EnqueuedAtUtc { get; set; }
}
