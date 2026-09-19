using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface IPlaylistService
{
    Task<PagedResult<PlaylistDto>> SearchAsync(string? keyword, int? categoryId, long? ownerId, int page, int pageSize);
    Task<PlaylistDetailDto?> GetAsync(long id, long? userId);
    Task<PlaylistDto> CreateAsync(long userId, CreatePlaylistRequest req);
    Task<ServiceResult> UpdateAsync(long userId, long id, UpdatePlaylistRequest req);
    Task<ServiceResult> DeleteAsync(long userId, long id);
    Task<ServiceResult> AddSongsAsync(long userId, long id, AddSongsToPlaylistRequest req);
    Task<ServiceResult> RemoveSongAsync(long userId, long id, long songId);
    Task<ServiceResult> CollectAsync(long userId, long id);
    Task<ServiceResult> UncollectAsync(long userId, long id);
    Task<IReadOnlyList<PlaylistDto>> GetCollectedAsync(long userId);
    Task<IReadOnlyList<long>> GetPlaylistsContainingSongAsync(long userId, long songId);
}

public class PlaylistService(MusicDbContext db, AudioMetadataService metadata) : IPlaylistService
{
    public async Task<PagedResult<PlaylistDto>> SearchAsync(string? keyword, int? categoryId, long? ownerId, int page, int pageSize)
    {
        var q = db.Playlists.AsNoTracking().AsQueryable();
        // 系统歌单（如“我喜欢的音乐”）个人可见，不出现在公开的搜索/精选列表里
        q = q.Where(p => !p.IsSystem);
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(p => EF.Functions.ILike(p.Name, LikePattern.Contains(keyword)));
        if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId);
        if (ownerId.HasValue) q = q.Where(p => p.OwnerId == ownerId);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new PlaylistDto(
                p.Id, p.Name, p.Description, p.CoverUrl, p.CategoryId, p.Category != null ? p.Category.Name : null,
                p.OwnerId, p.Owner.DisplayName, p.Songs.Count, p.CollectedBy.Count, p.CreatedAt, p.IsSystem))
            .ToListAsync();
        // 没有封面的歌单回退取第一首歌的封面
        await metadata.FillPlaylistCoversAsync(items);
        return new PagedResult<PlaylistDto>(items, total, page, pageSize);
    }

    public async Task<PlaylistDetailDto?> GetAsync(long id, long? userId)
    {
        var playlist = await db.Playlists.AsNoTracking()
            .Include(p => p.Owner).Include(p => p.Category)
            .Include(p => p.Songs).ThenInclude(ps => ps.Song).ThenInclude(s => s.Artist)
            .Include(p => p.Songs).ThenInclude(ps => ps.Song).ThenInclude(s => s.Album)
            .Include(p => p.CollectedBy)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (playlist is null) return null;

        var songDtos = playlist.Songs
            .OrderByDescending(ps => ps.AddedAt).ThenByDescending(ps => ps.Position)
            .Select(ps => ps.Song.ToDto()).ToList();
        await metadata.FillDurationsAsync(songDtos);
        await metadata.FillCoversAsync(songDtos);
        // ATL 解析失败（时长未知，FillDurations 探测后仍为 0）的歌曲不渲染进歌单列表
        songDtos.RemoveAll(s => s.DurationSeconds <= 0);
        // 歌单详情头图：无封面时回退取第一首有封面的歌
        var playlistCover = playlist.CoverUrl
            ?? songDtos.FirstOrDefault(s => !string.IsNullOrEmpty(s.CoverUrl))?.CoverUrl;
        return new PlaylistDetailDto(
            playlist.Id, playlist.Name, playlist.Description, playlistCover,
            playlist.OwnerId, playlist.Owner.DisplayName,
            userId == playlist.OwnerId,
            userId.HasValue && playlist.CollectedBy.Any(c => c.UserId == userId),
            songDtos,
            playlist.CategoryId, playlist.Category?.Name, playlist.IsSystem);
    }

    public async Task<PlaylistDto> CreateAsync(long userId, CreatePlaylistRequest req)
    {
        var playlist = new Playlist
        {
            OwnerId = userId,
            Name = req.Name.Trim(),
            Description = req.Description,
            CategoryId = req.CategoryId
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync();
        var owner = await db.Users.FindAsync(userId);
        return new PlaylistDto(playlist.Id, playlist.Name, playlist.Description, playlist.CoverUrl,
            playlist.CategoryId, null, userId, owner!.DisplayName, 0, 0, playlist.CreatedAt);
    }

    public async Task<ServiceResult> UpdateAsync(long userId, long id, UpdatePlaylistRequest req)
    {
        var playlist = await db.Playlists.FindAsync(id);
        if (playlist is null) return ServiceResult.Fail("歌单不存在。");
        if (playlist.OwnerId != userId) return ServiceResult.Fail("无权操作。");
        // 系统歌单（“我喜欢的音乐”）由注册流程生成、与 LikedSongs 绑定，不允许改名/换标签
        if (playlist.IsSystem) return ServiceResult.Fail("系统歌单不可修改。");

        if (!string.IsNullOrWhiteSpace(req.Name)) playlist.Name = req.Name.Trim();
        if (req.Description is not null) playlist.Description = req.Description.Trim();
        if (req.CoverUrl is not null) playlist.CoverUrl = req.CoverUrl;
        if (req.ClearCategory) playlist.CategoryId = null;
        else if (req.CategoryId.HasValue) playlist.CategoryId = req.CategoryId;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(long userId, long id)
    {
        var playlist = await db.Playlists.FindAsync(id);
        if (playlist is null) return ServiceResult.Fail("歌单不存在。");
        if (playlist.OwnerId != userId) return ServiceResult.Fail("无权操作。");
        // 兜底：前端已隐藏“我喜欢的音乐”的删除入口，服务端再拦一道，避免绕过 UI 直接调接口删掉
        if (playlist.IsSystem) return ServiceResult.Fail("系统歌单不可删除。");
        db.Playlists.Remove(playlist);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> AddSongsAsync(long userId, long id, AddSongsToPlaylistRequest req)
    {
        var playlist = await db.Playlists.Include(p => p.Songs).FirstOrDefaultAsync(p => p.Id == id);
        if (playlist is null) return ServiceResult.Fail("歌单不存在。");
        if (playlist.OwnerId != userId) return ServiceResult.Fail("无权操作。");

        var existing = playlist.Songs.Select(ps => ps.SongId).ToHashSet();
        var position = playlist.Songs.Count == 0 ? 0 : playlist.Songs.Max(ps => ps.Position);
        foreach (var songId in req.SongIds.Distinct())
        {
            if (existing.Contains(songId)) continue;
            if (!await db.Songs.AnyAsync(s => s.Id == songId)) continue;
            playlist.Songs.Add(new PlaylistSong { PlaylistId = id, SongId = songId, Position = ++position });
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemoveSongAsync(long userId, long id, long songId)
    {
        var playlist = await db.Playlists.FindAsync(id);
        if (playlist is null) return ServiceResult.Fail("歌单不存在。");
        if (playlist.OwnerId != userId) return ServiceResult.Fail("无权操作。");
        await db.PlaylistSongs.Where(ps => ps.PlaylistId == id && ps.SongId == songId).ExecuteDeleteAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CollectAsync(long userId, long id)
    {
        if (!await db.Playlists.AnyAsync(p => p.Id == id)) return ServiceResult.Fail("歌单不存在。");
        if (await db.PlaylistCollections.AnyAsync(c => c.UserId == userId && c.PlaylistId == id))
            return ServiceResult.Ok();
        db.PlaylistCollections.Add(new PlaylistCollection { UserId = userId, PlaylistId = id });
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UncollectAsync(long userId, long id)
    {
        var count = await db.PlaylistCollections.Where(c => c.UserId == userId && c.PlaylistId == id).ExecuteDeleteAsync();
        return count > 0 ? ServiceResult.Ok() : ServiceResult.Fail("未收藏该歌单。");
    }

    public async Task<IReadOnlyList<PlaylistDto>> GetCollectedAsync(long userId)
    {
        var items = await db.PlaylistCollections.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.Playlist)
            .Select(p => new PlaylistDto(
                p.Id, p.Name, p.Description, p.CoverUrl, p.CategoryId, p.Category != null ? p.Category.Name : null,
                p.OwnerId, p.Owner.DisplayName, p.Songs.Count, p.CollectedBy.Count, p.CreatedAt))
            .ToListAsync();
        await metadata.FillPlaylistCoversAsync(items);
        return items;
    }

    public async Task<IReadOnlyList<long>> GetPlaylistsContainingSongAsync(long userId, long songId)
    {
        return await db.PlaylistSongs.AsNoTracking()
            .Where(ps => ps.Playlist.OwnerId == userId && ps.SongId == songId)
            .Select(ps => ps.PlaylistId)
            .ToListAsync();
    }
}