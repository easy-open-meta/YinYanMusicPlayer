using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var result = await auth.RegisterAsync(req);
        if (result.Success) return Ok(result.Data);

        // 「超管已存在 / 引导码不对」不是参数问题，是权限问题 → 403
        if (result.Error is "管理员已存在。" or "引导码不正确。")
            return StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error });

        return BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// 后台注册页能否创建超管（匿名）。只有 <c>source=admin</c> 有意义；
    /// App 端注册常驻开放，不需要查这个。
    /// </summary>
    [HttpGet("registration-open")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> RegistrationOpen([FromQuery] string? source = "admin")
    {
        var open = string.Equals(source, "admin", StringComparison.OrdinalIgnoreCase)
            && await auth.CanCreateAdminAsync();
        return Ok(new { open });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var result = await auth.LoginAsync(req);
        return result.Success ? Ok(result.Data) : Unauthorized(new { message = result.Error });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await auth.GetProfileAsync(currentUser.RequireUserId());
        return user is null ? NotFound() : Ok(user);
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateProfileRequest req)
    {
        var user = await auth.UpdateProfileAsync(currentUser.RequireUserId(), req);
        return user is null ? NotFound() : Ok(user);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // V2.5 安全中心
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 修改密码。成功后 <c>TokenVersion</c> 自增，所有旧 token 立即失效
    /// —— 客户端需要引导用户用新密码重新登录。
    /// </summary>
    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        var result = await auth.ChangePasswordAsync(currentUser.RequireUserId(), req);
        return result.Success ? NoContent() : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// 发送邮箱验证码。SMTP 未配置时返回 200 + <c>sent=false</c> + 提示文案
    /// （不用 4xx：这不是客户端错误，前端按"功能不可用"处理即可）。
    /// </summary>
    [Authorize]
    [HttpPost("email/code")]
    public async Task<ActionResult<SendEmailCodeResult>> SendEmailCode(SendEmailCodeRequest req)
    {
        var result = await auth.SendEmailCodeAsync(currentUser.RequireUserId(), req);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }

    /// <summary>绑定邮箱：提交邮箱 + 验证码。</summary>
    [Authorize]
    [HttpPut("email")]
    public async Task<ActionResult<UserDto>> BindEmail(BindEmailRequest req)
    {
        var result = await auth.BindEmailAsync(currentUser.RequireUserId(), req);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// 上传头像（V2.5 调整）：客户端提交 base64 图片，服务端校验后以 data URI
    /// 存进 <c>Users.AvatarUrl</c>。
    /// <para>
    /// 用 base64 而非 multipart 文件：头像要跟着用户资料一起返回（列表页、用户页都要），
    /// 存成 data URI 后客户端一个 Image Source 就能直接显示，不用再发一次静态资源请求。
    /// </para>
    /// </summary>
    [Authorize]
    [HttpPut("me/avatar")]
    [RequestSizeLimit(4_000_000)]   // 2MB 图片的 base64 约 2.7MB，留点余量
    public async Task<ActionResult<UserDto>> UpdateAvatar(UpdateAvatarRequest req)
    {
        var result = await auth.UpdateAvatarAsync(currentUser.RequireUserId(), req);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// 安全中心能力探测（匿名可读）：客户端据此决定是否显示邮箱绑定入口。
    /// 放在 auth 下、允许匿名，避免未登录时也要带 token 才能判断。
    /// </summary>
    [HttpGet("capabilities")]
    [AllowAnonymous]
    public ActionResult<object> Capabilities()
        => Ok(new { emailBinding = auth.IsEmailBindingAvailable });
}
