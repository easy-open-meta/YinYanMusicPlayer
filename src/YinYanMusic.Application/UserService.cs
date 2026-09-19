using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface IUserService
{
    /// <summary>按用户名/昵称模糊搜索用户（搜索页"用户"结果区）。</summary>
    Task<IReadOnlyList<UserDto>> SearchAsync(string? keyword, int limit);

    /// <summary>用户详情页资料：基础信息 + 粉丝/关注(关注的歌手)/歌单 统计 + 观看者是否已关注。</summary>
    Task<UserProfileDto?> GetProfileAsync(long userId, long? viewerId);

    /// <summary>该用户的公开歌单（排除 IsSystem 的个人系统歌单，如"我喜欢的音乐"）。</summary>
    Task<IReadOnlyList<PlaylistDto>> GetPublicPlaylistsAsync(long userId);
}

public class UserService(MusicDbContext db) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> SearchAsync(string? keyword, int limit)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return [];
        var kw = LikePattern.Contains(keyword.Trim());
        return await db.Users.AsNoTracking()
            .Where(u => EF.Functions.ILike(u.UserName, kw) || EF.Functions.ILike(u.DisplayName, kw))
            .OrderBy(u => u.Id)
            .Take(limit)
            .Select(u => new UserDto(u.Id, u.UserName, u.DisplayName, u.Bio, u.AvatarUrl, u.Gender, u.CreatedAt))
            .ToListAsync();
    }

    public async Task<UserProfileDto?> GetProfileAsync(long userId, long? viewerId)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (u is null) return null;

        // "粉丝" = 用户互关 Follow 表（谁关注了 TA）；"关注" = TA 关注的歌手数（与本 App"我的"页口径一致）
        var followers = await db.Follows.CountAsync(f => f.FolloweeId == userId);
        var following = await db.ArtistFollows.CountAsync(f => f.UserId == userId);
        var playlists = await db.Playlists.CountAsync(p => p.OwnerId == userId && !p.IsSystem);
        var isFollowing = viewerId.HasValue && await db.Follows
            .AnyAsync(f => f.FollowerId == viewerId && f.FolloweeId == userId);

        return new UserProfileDto(u.Id, u.UserName, u.DisplayName, u.Bio, u.AvatarUrl, u.Gender,
            followers, following, playlists, isFollowing);
    }

    public async Task<IReadOnlyList<PlaylistDto>> GetPublicPlaylistsAsync(long userId)
    {
        return await db.Playlists.AsNoTracking()
            .Where(p => p.OwnerId == userId && !p.IsSystem)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PlaylistDto(
                p.Id, p.Name, p.Description, p.CoverUrl, p.CategoryId, p.Category != null ? p.Category.Name : null,
                p.OwnerId, p.Owner.DisplayName, p.Songs.Count, p.CollectedBy.Count, p.CreatedAt, p.IsSystem))
            .ToListAsync();
    }
}
