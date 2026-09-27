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

    // ── M3: 后台用户管理 ─────────────────────────────────────────────────────

    /// <summary>后台：分页用户列表。</summary>
    Task<PagedAdminUserResult> GetAdminUsersAsync(string? keyword, int page, int pageSize);

    /// <summary>后台：超管新建普通用户账号（无需引导码）。</summary>
    Task<ServiceResult<UserDto>> CreateAdminUserAsync(CreateAdminUserRequest req);

    /// <summary>后台：超管编辑用户资料。</summary>
    Task<ServiceResult<AdminUserDto>> UpdateAdminUserAsync(long id, UpdateAdminUserRequest req);

    /// <summary>后台：超管禁用/启用用户。</summary>
    Task<ServiceResult<bool>> SetDisabledAsync(long id, bool disabled);

    /// <summary>后台：超管重置用户密码。</summary>
    Task<ServiceResult<bool>> ResetPasswordAsync(long id, ResetPasswordRequest req);
}

public class UserService(MusicDbContext db, AudioMetadataService metadata) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> SearchAsync(string? keyword, int limit)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return [];
        var kw = LikePattern.Contains(keyword.Trim());
        return await db.Users.AsNoTracking()
            .Where(u => EF.Functions.ILike(u.UserName, kw, LikePattern.EscapeChar) || EF.Functions.ILike(u.DisplayName, kw, LikePattern.EscapeChar))
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
        var playlists = await db.Playlists.AsNoTracking()
            .Where(p => p.OwnerId == userId && !p.IsSystem)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PlaylistDto(
                p.Id, p.Name, p.Description, p.CoverUrl, p.CategoryId, p.Category != null ? p.Category.Name : null,
                p.OwnerId, p.Owner.DisplayName, p.Songs.Count, p.CollectedBy.Count, p.CreatedAt, p.IsSystem))
            .ToListAsync();
        // 没有封面的歌单回退取第一首歌的封面 —— 与搜索/精选/我的歌单列表同一套兜底
        // （用户详情页漏了这一步，歌单在 App 端显示成占位图标，用户实测反馈）
        await metadata.FillPlaylistCoversAsync(playlists);
        return playlists;
    }

    // ── M3 后台用户管理实现 ─────────────────────────────────────────────────

    public async Task<PagedAdminUserResult> GetAdminUsersAsync(string? keyword, int page, int pageSize)
    {
        var q = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pat = LikePattern.Contains(keyword);
            q = q.Where(u => EF.Functions.ILike(u.UserName, pat, LikePattern.EscapeChar) || EF.Functions.ILike(u.DisplayName, pat, LikePattern.EscapeChar));
        }

        var total = await q.CountAsync();
        var items = await q.OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new AdminUserDto(
                u.Id, u.UserName, u.DisplayName, u.Bio, u.AvatarUrl,
                u.Gender, u.CreatedAt, u.IsDisabled, u.Email))
            .ToListAsync();
        return new PagedAdminUserResult(items, total, page, pageSize);
    }

    public async Task<ServiceResult<UserDto>> CreateAdminUserAsync(CreateAdminUserRequest req)
    {
        if (await db.Users.AnyAsync(u => u.UserName == req.UserName.Trim()))
            return ServiceResult<UserDto>.Fail("用户名已存在。");
        var user = new Core.Entities.User
        {
            UserName = req.UserName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            DisplayName = req.DisplayName.Trim(),
            Gender = req.Gender ?? "保密",
            Role = "user",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return ServiceResult<UserDto>.Ok(new UserDto(user.Id, user.UserName, user.DisplayName,
            user.Bio, user.AvatarUrl, user.Gender, user.CreatedAt, user.Role));
    }

    public async Task<ServiceResult<AdminUserDto>> UpdateAdminUserAsync(long id, UpdateAdminUserRequest req)
    {
        var u = await db.Users.FindAsync(id);
        if (u is null) return ServiceResult<AdminUserDto>.Fail("用户不存在。");

        if (!string.IsNullOrWhiteSpace(req.DisplayName)) u.DisplayName = req.DisplayName.Trim();
        if (req.Bio is not null) u.Bio = req.Bio;
        if (req.Gender is not null) u.Gender = req.Gender;
        if (req.AvatarUrl is not null) u.AvatarUrl = req.AvatarUrl;

        await db.SaveChangesAsync();
        return ServiceResult<AdminUserDto>.Ok(new AdminUserDto(
            u.Id, u.UserName, u.DisplayName, u.Bio, u.AvatarUrl,
            u.Gender, u.CreatedAt, u.IsDisabled, u.Email));
    }

    public async Task<ServiceResult<bool>> SetDisabledAsync(long id, bool disabled)
    {
        var u = await db.Users.FindAsync(id);
        if (u is null) return ServiceResult<bool>.Fail("用户不存在。");
        // 禁止禁用自己
        // （这里没有 currentUser，调用方在 Controller 里判断）
        u.IsDisabled = disabled;
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> ResetPasswordAsync(long id, ResetPasswordRequest req)
    {
        var u = await db.Users.FindAsync(id);
        if (u is null) return ServiceResult<bool>.Fail("用户不存在。");
        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }
}
