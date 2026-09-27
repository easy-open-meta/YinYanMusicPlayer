using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync();
}

/// <summary>
/// 后台仪表盘统计。全部是只读聚合查询，一次请求返回仪表盘所需的全部数字，
/// 前端不用为每张卡片单独发请求。
/// </summary>
public class DashboardService(MusicDbContext db) : IDashboardService
{
    public async Task<DashboardStatsDto> GetStatsAsync()
    {
        var songCount = await db.Songs.CountAsync();
        var albumCount = await db.Albums.CountAsync();
        var artistCount = await db.Artists.CountAsync();
        var playlistCount = await db.Playlists.CountAsync();
        var userCount = await db.Users.CountAsync();
        // 没有播放历史表，总播放 = 全部歌曲累计播放量之和（空表聚合返回 0）。
        var totalPlays = await db.Songs.SumAsync(s => s.PlayCount);

        // 分区歌曲分布：按 CategoryId 分组，null = 未分类。
        var grouped = await db.Songs
            .GroupBy(s => s.CategoryId)
            .Select(g => new { CategoryId = g.Key, SongCount = g.Count() })
            .ToListAsync();
        var categoryNames = await db.Categories
            .ToDictionaryAsync(c => (int?)c.Id, c => c.Name);
        var distribution = grouped
            .Select(g => new CategoryDistDto(
                g.CategoryId,
                g.CategoryId.HasValue ? categoryNames.GetValueOrDefault(g.CategoryId) : "未分类",
                g.SongCount))
            .OrderByDescending(g => g.SongCount)
            .ToList();

        var recentSongs = await db.Songs.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Take(8)
            .Select(s => new RecentSongDto(
                s.Id,
                s.Title,
                s.Artist.Name,
                s.Album != null ? s.Album.Name : null,
                s.Category != null ? s.Category.Name : null,
                s.DurationSeconds,
                s.PlayCount,
                s.CreatedAt))
            .ToListAsync();

        // Top 歌手：走关联表（联合创作的歌在每位歌手名下都算），按累计播放量排序。
        var topArtists = await db.SongArtists.AsNoTracking()
            .GroupBy(sa => new { sa.ArtistId, sa.Artist.Name })
            .OrderByDescending(g => g.Sum(sa => sa.Song.PlayCount))
            .ThenByDescending(g => g.Count())
            .Take(8)
            .Select(g => new TopArtistDto(g.Key.ArtistId, g.Key.Name, g.Count(), g.Sum(sa => sa.Song.PlayCount)))
            .ToListAsync();

        return new DashboardStatsDto(
            songCount, albumCount, artistCount, playlistCount, userCount, totalPlays,
            distribution, recentSongs, topArtists);
    }
}