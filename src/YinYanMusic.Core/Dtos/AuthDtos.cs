namespace YinYanMusic.Core.Dtos;

/// <summary>
/// 注册请求。<paramref name="Source"/> 区分两条通道（见设计文档 4.1）：
/// <list type="bullet">
///   <item><c>"app"</c>（默认，MAUI 用户端）→ 始终可用，注册出普通用户 <c>role=user</c>；</item>
///   <item><c>"admin"</c>（后台管理页 <c>/register</c>）→ 仅当系统还没有超管时可用，
///         且必须带正确的 <paramref name="BootstrapCode"/>，注册出超级管理员 <c>role=admin</c>。</item>
/// </list>
/// ⚠️ Source 由客户端上报，<b>可伪造，只是护栏不是安全边界</b>；真正的门槛是引导码（只存在服务器磁盘上）。
/// </summary>
public record RegisterRequest(
    string UserName,
    string Password,
    string DisplayName,
    string? Gender,
    DateOnly? Birthday,
    string? Source = null,
    string? BootstrapCode = null);

public record LoginRequest(string UserName, string Password);

public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserDto User);

/// <summary><paramref name="Role"/>：<c>user</c> 普通用户 / <c>admin</c> 超级管理员。
/// 默认 <c>user</c> 是为了让老客户端（未传该字段时）行为不变。</summary>
public record UserDto(
    long Id,
    string UserName,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string Gender,
    DateTime CreatedAt,
    string Role = "user",
    /// <summary>已绑定邮箱（V2.5）。未绑定时为 null。</summary>
    string? Email = null);

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

// ── V2.5: 安全中心 ──────────────────────────────────────────────────────────

/// <summary>修改密码：需校验旧密码。</summary>
public record ChangePasswordRequest(string OldPassword, string NewPassword);

/// <summary>发送邮箱验证码：<paramref name="Email"/> 是待绑定的邮箱。</summary>
public record SendEmailCodeRequest(string Email);

/// <summary>绑定邮箱：提交邮箱 + 收到的验证码。</summary>
public record BindEmailRequest(string Email, string Code);

/// <summary>
/// 邮箱验证码发送结果。<paramref name="DevCode"/> 仅在未配置 SMTP 时返回，
/// 方便开发环境自测；生产（SMTP 已配置）恒为 null。
/// </summary>
public record SendEmailCodeResult(bool Sent, string? Message, string? DevCode = null);

/// <summary>
/// 头像上传（V2.5 调整）：客户端把图片转成 base64 提交，服务端校验后
/// 以 data URI 形式存进 <c>Users.AvatarUrl</c>。
/// </summary>
/// <param name="ImageBase64">
/// 图片数据。接受两种形式：完整 data URI（<c>data:image/png;base64,xxxx</c>）
/// 或裸 base64 串（此时按 <paramref name="ContentType"/> 补前缀）。
/// </param>
/// <param name="ContentType">裸 base64 时的 MIME 类型，如 <c>image/png</c>。</param>
public record UpdateAvatarRequest(string ImageBase64, string? ContentType = null);

// ── M3: 后台用户管理 ────────────────────────────────────────────────────────

/// <summary>后台用户列表项（分页）。</summary>
public record AdminUserDto(
    long Id,
    string UserName,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string Gender,
    DateTime CreatedAt,
    bool IsDisabled,
    /// <summary>已绑定邮箱（V2.5）。后台用户列表用它核对绑定情况。</summary>
    string? Email = null);

public record PagedAdminUserResult(IReadOnlyList<AdminUserDto> Items, int Total, int Page, int PageSize);

/// <summary>超管新建普通用户账号（后台专用，无需引导码）。</summary>
public record CreateAdminUserRequest(string UserName, string Password, string DisplayName, string? Gender);

/// <summary>超管编辑用户资料。</summary>
public record UpdateAdminUserRequest(string? DisplayName, string? Bio, string? Gender, string? AvatarUrl);

/// <summary>超管重置用户密码。</summary>
public record ResetPasswordRequest(string NewPassword);