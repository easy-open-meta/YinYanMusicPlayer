namespace YinYanMusic.Core.Dtos;

/// <summary>
/// 后台仪表盘统计（V2.16）：曲库计数、总播放、分区分布、最近新增歌曲、Top 歌手。
/// </summary>
public record DashboardStatsDto(
    long Songs,
    long Albums,
    long Artists,
    long Playlists,
    long Users,
    long TotalPlays,
    IReadOnlyList<CategoryDistDto> CategoryDistribution,
    IReadOnlyList<RecentSongDto> RecentSongs,
    IReadOnlyList<TopArtistDto> TopArtists);

/// <summary>分区歌曲分布。<paramref name="CategoryId"/> 为 null 表示「未分类」歌曲。</summary>
public record CategoryDistDto(int? CategoryId, string? CategoryName, long SongCount);

/// <summary>最近新增歌曲（列表页用的精简字段）。</summary>
public record RecentSongDto(
    long Id,
    string Title,
    string ArtistName,
    string? AlbumName,
    string? CategoryName,
    int DurationSeconds,
    long PlayCount,
    DateTime CreatedAt);

/// <summary>Top 歌手：<paramref name="SongCount"/> 按关联表统计（联合创作在每位歌手名下都算），
/// <paramref name="TotalPlays"/> 是名下歌曲累计播放量之和。</summary>
public record TopArtistDto(long Id, string Name, long SongCount, long TotalPlays);