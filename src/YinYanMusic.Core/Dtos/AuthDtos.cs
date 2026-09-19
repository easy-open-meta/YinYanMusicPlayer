namespace YinYanMusic.Core.Dtos;

public record RegisterRequest(string UserName, string Password, string DisplayName, string? Gender, DateOnly? Birthday);

public record LoginRequest(string UserName, string Password);

public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserDto User);

public record UserDto(long Id, string UserName, string DisplayName, string? Bio, string? AvatarUrl, string Gender, DateTime CreatedAt);

/// <summary>用户详情页资料：基础信息 + 粉丝/关注/歌单统计 + 观看者是否已关注（未登录时恒 false）。</summary>
public record UserProfileDto(
    long Id,
    string UserName,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string Gender,
    long FollowersCount,
    long FollowingCount,
    long PlaylistCount,
    bool IsFollowing);

public record UpdateProfileRequest(string? DisplayName, string? Bio, string? Gender, string? AvatarUrl);