namespace YinYanMusic.Core.Entities;

public class User
{
    public long Id { get; set; }
    public string UserName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string Gender { get; set; } = "保密";
    public DateOnly? Birthday { get; set; }

    /// <summary>
    /// 角色：<c>user</c>（默认）或 <c>admin</c>。用于后台管理接口的鉴权
    /// （<c>[Authorize(Roles = "admin")]</c>，token 里必须有 <c>ClaimTypes.Role</c> 才生效）。
    /// 老库由 Program.cs 启动时的幂等 SQL 补列，默认 'user'；
    /// 把某人提成管理员：<c>UPDATE "Users" SET "Role"='admin' WHERE "UserName"='xxx';</c> 然后**重新登录**。
    /// </summary>
    public string Role { get; set; } = "user";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Playlist> Playlists { get; set; } = [];
    public ICollection<LikedSong> LikedSongs { get; set; } = [];
    public ICollection<PlaylistCollection> PlaylistCollections { get; set; } = [];
    public ICollection<Follow> Following { get; set; } = [];
    public ICollection<Follow> Followers { get; set; } = [];
}