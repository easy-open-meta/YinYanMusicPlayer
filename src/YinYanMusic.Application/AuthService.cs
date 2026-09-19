using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface IAuthService
{
    Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest req);
    Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest req);
    Task<UserDto?> GetProfileAsync(long userId);
    Task<UserDto?> UpdateProfileAsync(long userId, UpdateProfileRequest req);
}

public class AuthService(MusicDbContext db, ITokenService tokens) : IAuthService
{
    public async Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest req)
    {
        var userName = req.UserName.Trim();
        if (userName.Length < 3 || userName.Length > 32)
            return ServiceResult<AuthResponse>.Fail("用户名长度须在 3-32 个字符之间。");
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return ServiceResult<AuthResponse>.Fail("密码长度至少 6 位。");

        if (await db.Users.AnyAsync(u => u.UserName == userName))
            return ServiceResult<AuthResponse>.Fail("该用户名已被注册。");

        var user = new User
        {
            UserName = userName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? userName : req.DisplayName.Trim(),
            Gender = string.IsNullOrWhiteSpace(req.Gender) ? "保密" : req.Gender!,
            Birthday = req.Birthday
        };
        db.Users.Add(user);

        db.Playlists.Add(new Playlist
        {
            Owner = user,
            Name = "我喜欢的音乐",
            Description = "所有喜欢的歌曲都会自动收藏在这里。",
            IsSystem = true
        });

        await db.SaveChangesAsync();
        var (token, expires) = tokens.CreateToken(user);
        return ServiceResult<AuthResponse>.Ok(new AuthResponse(token, expires, ToDto(user)));
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == req.UserName.Trim());
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return ServiceResult<AuthResponse>.Fail("用户名或密码错误。");

        var (token, expires) = tokens.CreateToken(user);
        return ServiceResult<AuthResponse>.Ok(new AuthResponse(token, expires, ToDto(user)));
    }

    public async Task<UserDto?> GetProfileAsync(long userId)
    {
        var user = await db.Users.FindAsync(userId);
        return user is null ? null : ToDto(user);
    }

    public async Task<UserDto?> UpdateProfileAsync(long userId, UpdateProfileRequest req)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return null;

        if (!string.IsNullOrWhiteSpace(req.DisplayName)) user.DisplayName = req.DisplayName!.Trim();
        if (req.Bio is not null) user.Bio = req.Bio.Trim();
        if (!string.IsNullOrWhiteSpace(req.Gender)) user.Gender = req.Gender!;
        if (!string.IsNullOrWhiteSpace(req.AvatarUrl)) user.AvatarUrl = req.AvatarUrl;
        await db.SaveChangesAsync();
        return ToDto(user);
    }

    private static UserDto ToDto(User u) => new(u.Id, u.UserName, u.DisplayName, u.Bio, u.AvatarUrl, u.Gender, u.CreatedAt);
}