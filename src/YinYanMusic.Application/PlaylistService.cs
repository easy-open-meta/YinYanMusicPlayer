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
    /// <summary>歌单内搜索歌曲（V2.4）：关键词命中标题/歌手/专辑，空关键词返回全部。</summary>
    Task<PagedResult<SongDto>> SearchSongsAsync(long playlistId, string? keyword, int page, int pageSize);
    Task<PlaylistDto> CreateAsync(long userId, CreatePlaylistRequest req);
    Task<ServiceResult> UpdateAsync(long userId, long id, UpdatePlaylistRequest req);
    Task<ServiceResult> DeleteAsync(long userId, long id);
    /// <summary>超管强行删除任意歌单（不受 OwnerId 限制）。系统歌单也拦一道。</summary>
    Task<ServiceResult> AdminDeleteAsync(long id);
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
        {
            // 与歌曲页同一套语义：歌单名 / 创建者名 任一命中即可。
            // 创建者字段用 Owner.DisplayName —— 与列表里展示的 OwnerName 是同一个值，
            // 保证"搜到的和看到的对得上"。
            var pat = LikePattern.Contains(keyword);
            q = q.Where(p => EF.Functions.ILike(p.Name, pat, LikePattern.EscapeChar) || EF.Functions.ILike(p.Owner.DisplayName, pat, LikePattern.EscapeChar));
        }
        if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId);
        if (ownerId.HasValue) q = q.Where(p => p.OwnerId == ownerId);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new PlaylistDto(
                p.Id, p.Name, p.Description, p.CoverUrl, p.CategoryId, p.Category != null ? p.Category.Name : null,
                p.OwnerId, p.Owner.DisplayName, p.Songs.Count, p.CollectedBy.Count, p.CreatedAt, p.IsSystem,
                // 播放次数：歌单内全部歌曲 PlayCount 之和（V2.4）。用子查询在库端聚合，
                // 不做冗余列 —— 增删歌曲、删歌后统计会自动跟着变，无需维护。
                p.Songs.Sum(ps => (long?)ps.Song.PlayCount) ?? 0))
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
            // 联合创作：ToDto() 的 Artists 走关联表，不 Include 就只剩主歌手（从歌单播放时选不出其他歌手）
            .Include(p => p.Songs).ThenInclude(ps => ps.Song).ThenInclude(s => s.SongArtists).ThenInclude(sa => sa.Artist)
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
        // 播放次数：歌单内全部歌曲 PlayCount 之和（V2.4）。
        // 注意这里基于已过滤掉「时长未知」的 songDtos 计算 —— 与列表页口径略有差异：
        // 列表页用库端 SUM 含全部关联歌曲，详情页只算真正展示出来的歌。
        // 这样用户看到的数字与列表里的歌对得上，不会出现"看不见的歌也计入"的困惑。
        var playCount = songDtos.Sum(s => (long)s.PlayCount);
        return new PlaylistDetailDto(
            playlist.Id, playlist.Name, playlist.Description, playlistCover,
            playlist.OwnerId, playlist.Owner.DisplayName,
            userId == playlist.OwnerId,
            userId.HasValue && playlist.CollectedBy.Any(c => c.UserId == userId),
            songDtos,
            playlist.CategoryId, playlist.Category?.Name, playlist.IsSystem,
            playCount);
    }

    /// <summary>
    /// 歌单内搜索歌曲（V2.4）。关键词命中标题 / 歌手名 / 专辑名，走 ILike（大小写不敏感）。
    /// 空关键词返回歌单全部歌曲。分页在库端完成。
    /// </summary>
    public async Task<PagedResult<SongDto>> SearchSongsAsync(long playlistId, string? keyword, int page, int pageSize)
    {
        // 从 Songs 侧查、用 Any 关联歌单：这样能直接 Include Artist/Album。
        // ⚠️ 不能写 db.PlaylistSongs.Select(ps => ps.Song).Include(...) ——
        // EF Core 禁止在 Select 投影之后再 Include（会抛 InvalidOperationException）。
        var q = db.Songs.AsNoTracking()
            .Where(s => s.PlaylistSongs.Any(ps => ps.PlaylistId == playlistId));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pat = LikePattern.Contains(keyword);
            // 与全局搜索一致：PostgreSQL 的 LIKE 区分大小写，必须用 ILike
            // 歌手名命中任一关联歌手（含联合创作者）；Artist.Name 那条是兜底
            q = q.Where(s => EF.Functions.ILike(s.Title, pat, LikePattern.EscapeChar)
                          || EF.Functions.ILike(s.Artist.Name, pat, LikePattern.EscapeChar)
                          || s.SongArtists.Any(sa => EF.Functions.ILike(sa.Artist.Name, pat, LikePattern.EscapeChar))
                          || (s.Album != null && EF.Functions.ILike(s.Album.Name, pat, LikePattern.EscapeChar)));
        }

        var total = await q.CountAsync();
        // 必须 Include Artist/Album：SongDto.ToDto() 从导航属性取 ArtistName/AlbumName，
        // 不 Include 这两个字段会全是空字符串（列表里显示不出歌手/专辑）。
        var songs = await q.OrderBy(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(s => s.Artist)
            .Include(s => s.Album)
            .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)   // 同上：Artists 走关联表
            .ToListAsync();
        var items = songs.Select(s => s.ToDto()).ToList();
        await metadata.FillDurationsAsync(items);
        await metadata.FillCoversAsync(items);
        return new PagedResult<SongDto>(items, total, page, pageSize);
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
        if (req.CoverUrl is not null)
        {
            // 封面也允许存 base64（data URI），过与专辑封面/头像同一道校验：
            // 格式白名单 + 解码后 ≤2MB。歌单列表是公开接口，不设闸的话一个超大 base64
            // 就能把列表响应撑爆。
            if (ImageDataUri.Validate(req.CoverUrl) is string coverError)
                return ServiceResult.Fail(coverError);
            playlist.CoverUrl = req.CoverUrl;
        }
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
        // 兜底：前端已隐藏"我喜欢的音乐"的删除入口，服务端再拦一道，避免绕过 UI 直接调接口删掉
        if (playlist.IsSystem) return ServiceResult.Fail("系统歌单不可删除。");
        // 评论没有外键可依（TargetId 是多态的，见 CommentTargets），必须显式清（TC-2.9-10）
        await db.Comments.Where(c => c.TargetType == CommentTargets.Playlist && c.TargetId == id).ExecuteDeleteAsync();
        db.Playlists.Remove(playlist);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> AdminDeleteAsync(long id)
    {
        var playlist = await db.Playlists.FindAsync(id);
        if (playlist is null) return ServiceResult.Fail("歌单不存在。");
        if (playlist.IsSystem) return ServiceResult.Fail("系统歌单不可删除。");
        await db.Comments.Where(c => c.TargetType == CommentTargets.Playlist && c.TargetId == id).ExecuteDeleteAsync();
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