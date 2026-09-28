using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Api.Jobs;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

[ApiController]
[Route("api/admin")]
// ⚠️ 这一组以前**完全没有鉴权**：能扫服务器任意目录（import）、能删数据（seed-songs），
// 而 API 绑 0.0.0.0 + CORS 全开 → 局域网任何人可调用。现在只有超管可用（登录除外）。
[Authorize(Roles = "admin")]
public class AdminController(
    IAdminService admin,
    IAuthService auth,
    IUserService users,
    ICurrentUserService currentUser,
    IScanRunner runner,
    IScanGate scanGate,
    DirectoryScanJob scanJob,
    ScanOptions scanOptions,
    ScanState scanState) : ControllerBase
{
    /// <summary>后台管理员登录（匿名）。校验凭据 + role=admin，令牌有效期 7 天。</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var result = await auth.AdminLoginAsync(req);
        return result.Success ? Ok(result.Data) : Unauthorized(new { message = result.Error });
    }

    /// <summary>
    /// 手动导入：**与定时扫描完全同一条路径**（同一个 <see cref="IScanRunner"/>、同一套解析规则：
    /// 读 ATL 标签 + 校验时长 + 递归 + 按相对路径去重），只是触发方式不同、可以指定目录。
    /// <para>
    /// 以前这里是"按文件名拆「歌手 - 歌名」"的另一套实现，结果同一个目录用不同入口导入会得到
    /// 不同质量的记录（无专辑、无发行年份、时长恒为 0、坏文件照收）。现已收口。
    /// </para>
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult> ImportFromDirectory([FromQuery] string? dir = null, CancellationToken ct = default)
    {
        var outcome = await runner.TryRunAsync("manual", ct, dir);

        if (outcome.Status == ScanRunStatus.Busy)
            return Conflict(new { message = "已有扫描或导入任务在进行中，请稍后再试。" });

        if (outcome.Status == ScanRunStatus.Failed)
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = $"导入未能执行：{outcome.Error}" });

        var snapshot = outcome.Snapshot;
        if (snapshot is null) return Accepted(new { message = "导入已取消。" });

        // 目录不存在之类仍是 400（与收口前一致），成功则回显各项计数
        if (snapshot.Error is not null) return BadRequest(new { message = snapshot.Error });

        return Ok(new
        {
            total = snapshot.Total,
            imported = snapshot.Imported,
            skipped = snapshot.Skipped,
            updated = snapshot.Updated,
            failed = snapshot.Failed,
            durationMs = snapshot.DurationMs
        });
    }

    [HttpDelete("seed-songs")]
    public async Task<ActionResult> DeleteSeedSongs()
        => Ok(new { deleted = await admin.DeleteSeedSongsAsync() });

    /// <summary>扫描时长。同样过 <see cref="IScanGate"/>（它也是整表读写 + 逐文件解析的重活）。</summary>
    [HttpPost("scan-durations")]
    public async Task<ActionResult> ScanDurations(CancellationToken ct = default)
    {
        if (!await scanGate.TryEnterAsync("扫描时长", ct))
            return Conflict(new { message = "已有扫描或导入任务在进行中，请稍后再试。" });

        try
        {
            var (total, updated, error) = await admin.ScanDurationsAsync();
            if (error is not null) return BadRequest(new { message = error });
            return Ok(new { total, updated });
        }
        finally
        {
            scanGate.Release();
        }
    }

    // ── V2.8: 定时扫描目录入库 ──────────────────────────────────────────────

    /// <summary>
    /// 定时扫描任务的状态：是否启用、间隔、下次运行时间、上一轮结果。
    /// 状态存在进程内存里，API 重启后归零（原因见 <see cref="ScanState"/> 注释）。
    /// </summary>
    [HttpGet("scan/status")]
    public ActionResult GetScanStatus()
    {
        var last = scanState.Current;
        return Ok(new
        {
            enabled = scanState.Enabled,
            intervalMinutes = scanState.IntervalMinutes,
            isRunning = scanState.IsRunning,
            nextRunAtUtc = scanState.NextRunAtUtc,
            runCount = scanState.RunCount,
            lastRunAtUtc = last.FinishedAtUtc,
            lastStartedAtUtc = last.StartedAtUtc,
            lastTrigger = last.Trigger,
            lastSucceeded = last.StartedAtUtc is null ? (bool?)null : last.Succeeded,
            lastDurationMs = last.DurationMs,
            lastTotal = last.Total,
            lastImported = last.Imported,
            lastUpdated = last.Updated,
            lastFailed = last.Failed,
            lastSkipped = last.Skipped,
            lastError = last.Error
        });
    }

    /// <summary>
    /// 运行时开关定时器（后台「定时扫描」卡片上的开关）。
    /// ⚠️ 只改**进程内存**：重启后回到配置文件的值。间隔 &gt; 0 才算启用；
    /// 配置里间隔是 0 时打开开关，用默认 60 分钟兜底（否则"打开了却不跑"更费解）。
    /// </summary>
    [HttpPut("scan/enabled")]
    public ActionResult SetScanEnabled([FromQuery] bool enabled)
    {
        const int FallbackIntervalMinutes = 60;
        var interval = scanOptions.ScanIntervalMinutes > 0 ? scanOptions.ScanIntervalMinutes : FallbackIntervalMinutes;

        scanState.Configure(enabled, enabled ? interval : scanState.IntervalMinutes);
        scanJob.RequestReschedule();

        return Ok(new
        {
            enabled = scanState.Enabled,
            intervalMinutes = scanState.IntervalMinutes,
            nextRunAtUtc = scanState.NextRunAtUtc
        });
    }

    /// <summary>
    /// 立即跑一轮目录扫描。与定时任务共用 <see cref="IScanRunner"/> 的执行权，
    /// 已有扫描在跑时返回 409（单实例保护），不排队。
    /// </summary>
    [HttpPost("scan/run")]
    public async Task<ActionResult> RunScanNow(CancellationToken ct)
    {
        var outcome = await runner.TryRunAsync("manual", ct);

        if (outcome.Status == ScanRunStatus.Busy)
            return Conflict(new { message = "上一轮扫描尚未结束，请稍后再试。" });

        if (outcome.Status == ScanRunStatus.Failed)
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = $"扫描未能执行：{outcome.Error}" });

        var snapshot = outcome.Snapshot;
        if (snapshot is null) return Accepted(new { message = "扫描已取消。" });

        return Ok(new
        {
            succeeded = snapshot.Succeeded,
            total = snapshot.Total,
            imported = snapshot.Imported,
            updated = snapshot.Updated,
            failed = snapshot.Failed,
            skipped = snapshot.Skipped,
            durationMs = snapshot.DurationMs,
            error = snapshot.Error
        });
    }

    // ── M3: 用户管理 ─────────────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<ActionResult<PagedAdminUserResult>> GetUsers(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await users.GetAdminUsersAsync(keyword, page, pageSize));

    [HttpPost("users")]
    public async Task<ActionResult<UserDto>> CreateUser(CreateAdminUserRequest req)
    {
        var result = await users.CreateAdminUserAsync(req);
        return result.Success
            ? Ok(result.Data)
            : Conflict(new { message = result.Error });
    }

    [HttpPut("users/{id:long}")]
    public async Task<ActionResult<AdminUserDto>> UpdateUser(long id, UpdateAdminUserRequest req)
    {
        var result = await users.UpdateAdminUserAsync(id, req);
        return result.Success
            ? Ok(result.Data)
            : NotFound(new { message = result.Error });
    }

    /// <summary>PUT /api/admin/users/{id}/disabled  传 ?disabled=true 禁用，false 启用。</summary>
    [HttpPut("users/{id:long}/disabled")]
    public async Task<IActionResult> SetDisabled(long id, [FromQuery] bool disabled)
    {
        // 禁止超管禁用自己（避免误操作把自己锁出）
        if (currentUser.UserId == id)
            return BadRequest(new { message = "不能禁用自己。" });
        var result = await users.SetDisabledAsync(id, disabled);
        return result.Success
            ? NoContent()
            : NotFound(new { message = result.Error });
    }

    [HttpPut("users/{id:long}/password")]
    public async Task<IActionResult> ResetPassword(long id, ResetPasswordRequest req)
    {
        var result = await users.ResetPasswordAsync(id, req);
        return result.Success
            ? NoContent()
            : NotFound(new { message = result.Error });
    }
}
