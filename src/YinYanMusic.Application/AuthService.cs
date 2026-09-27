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
    /// <summary>后台管理登录：必须是 <c>admin</c> 角色，且令牌有效期更短（8 小时）。</summary>
    Task<ServiceResult<AuthResponse>> AdminLoginAsync(LoginRequest req);
    /// <summary>后台注册页是否还能创建超管（= 系统里还没有 admin）。</summary>
    Task<bool> CanCreateAdminAsync();
    Task<UserDto?> GetProfileAsync(long userId);
    Task<UserDto?> UpdateProfileAsync(long userId, UpdateProfileRequest req);

    // ── V2.5 安全中心 ────────────────────────────────────────────────────────

    /// <summary>
    /// 修改密码：校验旧密码 → BCrypt 重哈希 → <c>TokenVersion += 1</c>。
    /// 版本号自增会让所有已签发的旧 token 立即失效（"改了密码别人还能用"的窗口关掉）。
    /// </summary>
    Task<ServiceResult> ChangePasswordAsync(long userId, ChangePasswordRequest req);

    /// <summary>发送邮箱验证码。SMTP 未配置时返回失败 + 明确提示（不抛异常）。</summary>
    Task<ServiceResult<SendEmailCodeResult>> SendEmailCodeAsync(long userId, SendEmailCodeRequest req);

    /// <summary>绑定邮箱：校验验证码 → 查重 → 写入 <c>User.Email</c>。</summary>
    Task<ServiceResult<UserDto>> BindEmailAsync(long userId, BindEmailRequest req);

    /// <summary>
    /// 上传头像（V2.5 调整）：客户端提交 base64 图片，校验大小/类型后
    /// 以 data URI 存进 <c>Users.AvatarUrl</c>。
    /// </summary>
    Task<ServiceResult<UserDto>> UpdateAvatarAsync(long userId, UpdateAvatarRequest req);

    /// <summary>邮箱绑定功能是否可用（SMTP 已配置）。客户端据此决定是否显示入口。</summary>
    bool IsEmailBindingAvailable { get; }
}

public class AuthService(MusicDbContext db, ITokenService tokens,
                         IBootstrapCodeService bootstrap, AuthOptions options,
                         IEmailSender email) : IAuthService
{
    private const string AdminRole = "admin";
    private const string UserRole = "user";

    /// <summary>后台令牌有效期：8 小时（App 端仍用配置的 7 天）。</summary>
    private static readonly TimeSpan AdminTokenLifetime = TimeSpan.FromHours(8);

    // ── V2.5 邮箱验证码参数 ──────────────────────────────────────────────────
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CodeRateWindow = TimeSpan.FromMinutes(5);
    private const int MaxCodesPerWindow = 3;

    public bool IsEmailBindingAvailable => email.IsConfigured;

    public async Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest req)
    {
        var userName = req.UserName.Trim();
        if (userName.Length < 3 || userName.Length > 32)
            return ServiceResult<AuthResponse>.Fail("用户名长度须在 3-32 个字符之间。");
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return ServiceResult<AuthResponse>.Fail("密码长度至少 6 位。");

        if (await db.Users.AnyAsync(u => u.UserName == userName))
            return ServiceResult<AuthResponse>.Fail("该用户名已被注册。");

        var fromAdmin = string.Equals(req.Source, AdminRole, StringComparison.OrdinalIgnoreCase);

        if (fromAdmin)
        {
            // ⚠️ 判据是「有没有 admin」而不是「有没有用户」：
            // 否则别人先在 App 注册一个普通账号，后台注册页就永远关死、超管再也建不出来。
            if (await db.Users.AnyAsync(u => u.Role == AdminRole))
                return ServiceResult<AuthResponse>.Fail("管理员已存在。");
            if (!bootstrap.Verify(req.BootstrapCode))
                return ServiceResult<AuthResponse>.Fail("引导码不正确。");
        }
        else if (!options.AllowAppRegistration)
        {
            return ServiceResult<AuthResponse>.Fail("注册已关闭，请联系管理员。");
        }

        var user = new User
        {
            UserName = userName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? userName : req.DisplayName.Trim(),
            Gender = string.IsNullOrWhiteSpace(req.Gender) ? "保密" : req.Gender!,
            Birthday = req.Birthday,
            Role = fromAdmin ? AdminRole : UserRole
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

        if (fromAdmin) bootstrap.Consume();     // 一次性：超管建成即销毁引导码

        // 后台注册出来的令牌要跟着后台走（8 小时）——它是给浏览器用的，不能给 7 天的 App 令牌
        var (token, expires) = tokens.CreateToken(user, fromAdmin ? AdminTokenLifetime : null);
        return ServiceResult<AuthResponse>.Ok(new AuthResponse(token, expires, ToDto(user)));
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == req.UserName.Trim());
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return ServiceResult<AuthResponse>.Fail("用户名或密码错误。");
        if (user.IsDisabled)
            return ServiceResult<AuthResponse>.Fail("该账号已被停用。");

        var (token, expires) = tokens.CreateToken(user);
        return ServiceResult<AuthResponse>.Ok(new AuthResponse(token, expires, ToDto(user)));
    }

    public async Task<ServiceResult<AuthResponse>> AdminLoginAsync(LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == req.UserName.Trim());
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return ServiceResult<AuthResponse>.Fail("用户名或密码错误。");
        if (user.IsDisabled)
            return ServiceResult<AuthResponse>.Fail("该账号已被停用。");
        if (!string.Equals(user.Role, AdminRole, StringComparison.Ordinal))
            return ServiceResult<AuthResponse>.Fail("该账号不是管理员。");

        var (token, expires) = tokens.CreateToken(user, AdminTokenLifetime);
        return ServiceResult<AuthResponse>.Ok(new AuthResponse(token, expires, ToDto(user)));
    }

    public async Task<bool> CanCreateAdminAsync()
        => !await db.Users.AnyAsync(u => u.Role == AdminRole);

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

    // ══════════════════════════════════════════════════════════════════════════
    // V2.5 安全中心
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<ServiceResult> ChangePasswordAsync(long userId, ChangePasswordRequest req)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return ServiceResult.Fail("账号不存在。");

        if (string.IsNullOrWhiteSpace(req.OldPassword))
            return ServiceResult.Fail("请输入当前密码。");
        if (!BCrypt.Net.BCrypt.Verify(req.OldPassword, user.PasswordHash))
            return ServiceResult.Fail("当前密码不正确。");
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
            return ServiceResult.Fail("新密码长度至少 6 位。");
        if (string.Equals(req.OldPassword, req.NewPassword, StringComparison.Ordinal))
            return ServiceResult.Fail("新密码不能与当前密码相同。");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        // 关键：版本号自增 → 所有已签发 token 的 tv 声明与库中值不再匹配 → 立即失效。
        // 无状态 JWT 下这是代价最小的"改密踢下线"方案，不需要维护黑名单。
        user.TokenVersion += 1;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<SendEmailCodeResult>> SendEmailCodeAsync(long userId, SendEmailCodeRequest req)
    {
        var target = req.Email?.Trim() ?? string.Empty;
        if (!IsValidEmail(target))
            return ServiceResult<SendEmailCodeResult>.Fail("邮箱格式不正确。");

        var user = await db.Users.FindAsync(userId);
        if (user is null) return ServiceResult<SendEmailCodeResult>.Fail("账号不存在。");

        // 邮箱已被他人占用：提前拦掉，别等用户输完验证码才说
        if (await db.Users.AnyAsync(u => u.Email == target && u.Id != userId))
            return ServiceResult<SendEmailCodeResult>.Fail("该邮箱已被其他账号绑定。");

        // 限流：5 分钟内最多 3 次
        var since = DateTime.UtcNow - CodeRateWindow;
        var recent = await db.EmailVerifications
            .CountAsync(v => v.UserId == userId && v.CreatedAt >= since);
        if (recent >= MaxCodesPerWindow)
            return ServiceResult<SendEmailCodeResult>.Fail("验证码发送过于频繁，请 5 分钟后再试。");

        // 未配置 SMTP：不生成码、不发信，直接回明确提示（客户端据此隐藏入口）
        if (!email.IsConfigured)
            return ServiceResult<SendEmailCodeResult>.Ok(
                new SendEmailCodeResult(false, "服务器未配置邮件服务，暂无法发送验证码。"));

        var code = Random.Shared.Next(100000, 1000000).ToString();
        db.EmailVerifications.Add(new EmailVerification
        {
            UserId = userId,
            Email = target,
            Code = code,
            ExpiresAt = DateTime.UtcNow + CodeLifetime
        });
        await db.SaveChangesAsync();

        var body = $"""
            你的音言音乐邮箱验证码是：{code}

            有效期 10 分钟，请勿转发给他人。
            如果这不是你本人的操作，忽略本邮件即可。
            """;
        var send = await email.SendAsync(target, "音言音乐 · 邮箱验证码", body);
        if (!send.Success)
            return ServiceResult<SendEmailCodeResult>.Ok(new SendEmailCodeResult(false, send.Error));

        return ServiceResult<SendEmailCodeResult>.Ok(new SendEmailCodeResult(true, "验证码已发送，请查收邮件。"));
    }

    public async Task<ServiceResult<UserDto>> BindEmailAsync(long userId, BindEmailRequest req)
    {
        var target = req.Email?.Trim() ?? string.Empty;
        if (!IsValidEmail(target))
            return ServiceResult<UserDto>.Fail("邮箱格式不正确。");
        if (string.IsNullOrWhiteSpace(req.Code))
            return ServiceResult<UserDto>.Fail("请输入验证码。");

        var user = await db.Users.FindAsync(userId);
        if (user is null) return ServiceResult<UserDto>.Fail("账号不存在。");

        if (await db.Users.AnyAsync(u => u.Email == target && u.Id != userId))
            return ServiceResult<UserDto>.Fail("该邮箱已被其他账号绑定。");

        // 取该邮箱最近一条未使用、未过期的码
        var record = await db.EmailVerifications
            .Where(v => v.UserId == userId && v.Email == target && v.UsedAt == null)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync();
        if (record is null)
            return ServiceResult<UserDto>.Fail("请先获取验证码。");
        if (record.ExpiresAt < DateTime.UtcNow)
            return ServiceResult<UserDto>.Fail("验证码已过期，请重新获取。");
        if (!string.Equals(record.Code, req.Code.Trim(), StringComparison.Ordinal))
            return ServiceResult<UserDto>.Fail("验证码不正确。");

        record.UsedAt = DateTime.UtcNow;   // 一次性，防重放
        user.Email = target;
        await db.SaveChangesAsync();
        return ServiceResult<UserDto>.Ok(ToDto(user));
    }

    // ── 头像（V2.5 调整）：base64 存库 ────────────────────────────────────────
    // 规则（MIME 白名单 + 解码后体积上限）统一放在 ImageDataUri ——
    // 专辑封面与歌手头像用的是同一套，别在这里另立一份。

    public async Task<ServiceResult<UserDto>> UpdateAvatarAsync(long userId, UpdateAvatarRequest req)
    {
        var raw = req.ImageBase64?.Trim() ?? string.Empty;
        if (raw.Length == 0) return ServiceResult<UserDto>.Fail("请选择图片。");

        // 解析出 MIME 与纯 base64 数据。支持两种入参：
        //   data:image/png;base64,AAAA…（完整 data URI）
        //   AAAA…（裸 base64，此时必须带 ContentType）
        string mime;
        string base64;
        var comma = raw.IndexOf(',');
        if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
        {
            var header = raw[5..comma];                      // image/png;base64
            var semi = header.IndexOf(';');
            mime = (semi > 0 ? header[..semi] : header).Trim().ToLowerInvariant();
            base64 = raw[(comma + 1)..];
        }
        else
        {
            mime = (req.ContentType ?? "image/png").Trim().ToLowerInvariant();
            base64 = raw;
        }

        if (!ImageDataUri.AllowedMimeTypes.Contains(mime))
            return ServiceResult<UserDto>.Fail($"不支持的图片格式（{mime}），请用 PNG / JPG / WebP / GIF。");

        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException) { return ServiceResult<UserDto>.Fail("图片数据格式不正确。"); }

        // 体积校验用「解码后的真实字节数」而不是 base64 字符串长度 ——
        // base64 会膨胀约 1/3，按字符串长度判会把 1.5MB 的图误判成超限。
        if (bytes.Length > ImageDataUri.MaxDecodedBytes)
            return ServiceResult<UserDto>.Fail($"图片过大（{bytes.Length / 1024.0 / 1024.0:F1}MB），请压缩到 {ImageDataUri.MaxDecodedBytes / 1024 / 1024}MB 以内。");
        if (bytes.Length < 64)
            return ServiceResult<UserDto>.Fail("图片内容无效。");

        var user = await db.Users.FindAsync(userId);
        if (user is null) return ServiceResult<UserDto>.Fail("账号不存在。");

        // 存 data URI：客户端 <Image Source> 可直接用，无需再下载；
        // 同时保持 AvatarUrl 字段语义不变（它本来就存 URL/路径）。
        user.AvatarUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        await db.SaveChangesAsync();
        return ServiceResult<UserDto>.Ok(ToDto(user));
    }

    /// <summary>轻量邮箱格式校验（不做 RFC 全量解析，够拦住常见笔误即可）。</summary>
    private static bool IsValidEmail(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length > 256) return false;
        var at = s.IndexOf('@');
        if (at <= 0 || at != s.LastIndexOf('@')) return false;
        var domain = s[(at + 1)..];
        // 域名必须含点、点不在首尾、且没有空格
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.')
               && !s.Any(char.IsWhiteSpace);
    }

    private static UserDto ToDto(User u) =>
        new(u.Id, u.UserName, u.DisplayName, u.Bio, u.AvatarUrl, u.Gender, u.CreatedAt,
            string.IsNullOrWhiteSpace(u.Role) ? UserRole : u.Role, u.Email);
}