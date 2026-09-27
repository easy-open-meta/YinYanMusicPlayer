namespace YinYanMusic.Core.Entities;

public class User
{
    public long Id { get; set; }
    public string UserName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string DisplayName { get; set; } = default!;

    /// <summary>
    /// 角色：<c>"user"</c> 普通用户（App 自助注册）/ <c>"admin"</c> 超级管理员（后台注册页产出，全系统唯一）。
    /// ⚠️ 判断"能不能建超管"必须看<b>有没有 admin</b>，不能看"有没有用户"——
    /// 否则别人先在 App 注册一个账号，后台注册页就永远关死、超管再也建不出来。
    /// </summary>
    public string Role { get; set; } = "user";

    /// <summary>停用（软删除）。被停用的账号不能登录。</summary>
    public bool IsDisabled { get; set; }

    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string Gender { get; set; } = "保密";
    public DateOnly? Birthday { get; set; }

    /// <summary>
    /// 绑定邮箱（V2.5）。未绑定时为 null。
    /// 全局唯一：同一邮箱不能绑到多个账号（绑定前会查重）。
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 令牌版本（V2.5）。签发 token 时写进 <c>tv</c> 声明，鉴权时与库中值比对，
    /// 不一致即判失效。改密时 +1 —— 这样"改了密码但旧 token 还能用"的窗口就被关掉了，
    /// 无需维护黑名单。
    /// </summary>
    public int TokenVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Playlist> Playlists { get; set; } = [];
    public ICollection<LikedSong> LikedSongs { get; set; } = [];
    public ICollection<PlaylistCollection> PlaylistCollections { get; set; } = [];
    public ICollection<Follow> Following { get; set; } = [];
    public ICollection<Follow> Followers { get; set; } = [];
    public ICollection<EmailVerification> EmailVerifications { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    /// <summary>该用户收到的站内通知接收行（V2.15）。账号删除时级联清掉。</summary>
    public ICollection<NotificationRecipient> NotificationRecipients { get; set; } = [];
}

/// <summary>
/// 邮箱验证码（V2.5）。落库而不是放内存缓存 —— Docker / 安装包形态下 API 重启很频繁，
/// 内存里的码一重启就没了，用户会莫名其妙"验证码失效"。
/// </summary>
public class EmailVerification
{
    public long Id { get; set; }

    public long UserId { get; set; }
    public User User { get; set; } = default!;

    /// <summary>要绑定的邮箱（也用于"同一邮箱已绑他人"的预校验）。</summary>
    public string Email { get; set; } = default!;

    /// <summary>6 位数字验证码。明文存储 —— 它本身是短时效一次性凭据，
    /// 且校验只比对字符串；哈希化对这类场景没有实际收益。</summary>
    public string Code { get; set; } = default!;

    /// <summary>过期时间（签发后 10 分钟）。</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>已使用时间；非 null 表示该码已用过（一次性，防止重放）。</summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>签发时间。用于"5 分钟内最多发 3 次"的限流统计。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}