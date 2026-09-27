namespace YinYanMusic.Core.Dtos;

/// <summary>
/// 上报播放进度（V2.10）。客户端在暂停、切歌、退出 App 以及播放中每 30 秒各报一次。
/// <see cref="DeviceName"/>（V2.12）标识上报设备，展示在"继续播放"入口上。
/// </summary>
public record PlaybackProgressRequest(long SongId, double PositionSeconds, string? DeviceName = null);

/// <summary>
/// 最近一次播放（V2.10）。带上完整 <see cref="Song"/> 是为了让"继续播放"卡片能直接起播 ——
/// 客户端播放需要整张 <see cref="SongDto"/>（音频地址、时长、封面、歌手都在里面），
/// 只给 songId 的话点一下还要再查一次歌。<see cref="DeviceName"/>（V2.12）告诉用户这份进度来自哪台设备。
/// </summary>
public record PlaybackProgressDto(long SongId, double PositionSeconds, DateTime UpdatedAtUtc, SongDto Song, string? DeviceName = null);

/// <summary>
/// 离线播放补报（V2.11）：一条 = 一次离线播放。<see cref="ClientKey"/> 是客户端生成的幂等键，
/// 服务端按它去重（重复提交不重复计数）。
/// </summary>
public record PlayReportRequest(long SongId, string ClientKey, DateTime PlayedAtUtc, double PositionSeconds);

/// <summary>离线播放补报的批量请求体。</summary>
public record PlayReportsBatchRequest(IReadOnlyList<PlayReportRequest> Items);

/// <summary>
/// 补报结果：<see cref="Accepted"/> 本次新收下的条数（这些才 +1 播放数）；
/// <see cref="Duplicated"/> 服务端已收过（幂等命中）；<see cref="UnknownSongs"/> 曲库里已不存在的 songId
/// （后台删过）。客户端对三类结果统一从本地队列删除、不重试。
/// </summary>
public record PlayReportsAck(int Accepted, int Duplicated, IReadOnlyList<long> UnknownSongs);
