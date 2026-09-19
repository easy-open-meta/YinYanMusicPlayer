using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface IMeService
{
    Task<object> GetOverviewAsync(long userId);
    Task<IReadOnlyList<PlaylistDto>> GetMyPlaylistsAsync(long userId);
    Task<IReadOnlyList<ArtistDto>> GetFollowedArtistsAsync(long userId);
    Task<IReadOnlyList<UserDto>> GetFollowedUsersAsync(long userId);
    Task<IReadOnlyList<UserDto>> GetFollowersAsync(long userId);
    Task<ServiceResult> FollowAsync(long userId, long targetUserId);
    Task<ServiceResult> UnfollowAsync(long userId, long targetUserId);
    Task<IReadOnlyList<long>> GetFollowedArtistIdsAsync(long userId);
    Task<ServiceResult> FollowArtistAsync(long userId, long artistId);
    Task<ServiceResult> UnfollowArtistAsync(long userId, long artistId);
}

public class MeService(MusicDbContext db, AudioMetadataService metadata) : IMeService
{
    public async Task<object> GetOverviewAsync(long userId)
    {
        var liked = await db.LikedSongs.CountAsync(l => l.UserId == userId);
        var myPlaylists = await db.Playlists.CountAsync(p => p.OwnerId == userId);
        var collected = await db.PlaylistCollections.CountAsync(c => c.UserId == userId);
        var following = await db.Follows.CountAsync(f => f.FollowerId == userId);
        var followers = await db.Follows.CountAsync(f => f.FolloweeId == userId);
        // “关注”在该 App 里指“我关注的歌手”（ArtistFollow 表），不是用户之间的 Follow。
        var artistFollowing = await db.ArtistFollows.CountAsync(f => f.UserId == userId);
        return new { liked, myPlaylists, collected, following, followers, artistFollowing };
    }

    public async Task<IReadOnlyList<PlaylistDto>> GetMyPlaylistsAsync(long userId)
    {
        var items = await db.Playlists.AsNoTracking()
            .Where(p => p.OwnerId == userId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PlaylistDto(
                p.Id, p.Name, p.Description, p.CoverUrl, p.CategoryId, p.Category != null ? p.Category.Name : null,
                p.OwnerId, p.Owner.DisplayName, p.Songs.Count, p.CollectedBy.Count, p.CreatedAt, p.IsSystem))
            .ToListAsync();
        await metadata.FillPlaylistCoversAsync(items);
        return items;
    }

    /// <summary>当前用户关注的歌手完整信息（“我的”页关注列表）。</summary>
    public async Task<IReadOnlyList<ArtistDto>> GetFollowedArtistsAsync(long userId)
    {
        return await db.ArtistFollows.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.Artist)
            .Select(a => new ArtistDto(a.Id, a.Name, a.Region, a.Kind, a.AvatarUrl, a.Bio, 0, 0, 0))
            .ToListAsync();
    }

    /// <summary>当前用户关注的用户列表（用户互关 Follow 表；"我的"页关注列表的用户区）。</summary>
    public async Task<IReadOnlyList<UserDto>> GetFollowedUsersAsync(long userId)
    {
        return await db.Follows.AsNoTracking()
            .Where(f => f.FollowerId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.Followee)
            .Select(u => new UserDto(u.Id, u.UserName, u.DisplayName, u.Bio, u.AvatarUrl, u.Gender, u.CreatedAt))
            .ToListAsync();
    }

    /// <summary>关注当前用户的粉丝列表（用户互关 Follow 表；本 App 暂无关注用户的入口，通常为空）。</summary>
    public async Task<IReadOnlyList<UserDto>> GetFollowersAsync(long userId)
    {
        return await db.Follows.AsNoTracking()
            .Where(f => f.FolloweeId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.Follower)
            .Select(u => new UserDto(u.Id, u.UserName, u.DisplayName, u.Bio, u.AvatarUrl, u.Gender, u.CreatedAt))
            .ToListAsync();
    }

    public async Task<ServiceResult> FollowAsync(long userId, long targetUserId)
    {
        if (userId == targetUserId) return ServiceResult.Fail("不能关注自己。");
        if (!await db.Users.AnyAsync(u => u.Id == targetUserId)) return ServiceResult.Fail("用户不存在。");
        if (await db.Follows.AnyAsync(f => f.FollowerId == userId && f.FolloweeId == targetUserId))
            return ServiceResult.Ok();
        db.Follows.Add(new Follow { FollowerId = userId, FolloweeId = targetUserId });
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UnfollowAsync(long userId, long targetUserId)
    {
        var count = await db.Follows.Where(f => f.FollowerId == userId && f.FolloweeId == targetUserId).ExecuteDeleteAsync();
        return count > 0 ? ServiceResult.Ok() : ServiceResult.Fail("未关注该用户。");
    }

    public async Task<IReadOnlyList<long>> GetFollowedArtistIdsAsync(long userId)
    {
        return await db.ArtistFollows.AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => f.ArtistId)
            .ToListAsync();
    }

    public async Task<ServiceResult> FollowArtistAsync(long userId, long artistId)
    {
        if (!await db.Artists.AnyAsync(a => a.Id == artistId)) return ServiceResult.Fail("歌手不存在。");
        if (await db.ArtistFollows.AnyAsync(f => f.UserId == userId && f.ArtistId == artistId))
            return ServiceResult.Ok();
        db.ArtistFollows.Add(new ArtistFollow { UserId = userId, ArtistId = artistId });
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UnfollowArtistAsync(long userId, long artistId)
    {
        var count = await db.ArtistFollows.Where(f => f.UserId == userId && f.ArtistId == artistId).ExecuteDeleteAsync();
        return count > 0 ? ServiceResult.Ok() : ServiceResult.Fail("未关注该歌手。");
    }
}