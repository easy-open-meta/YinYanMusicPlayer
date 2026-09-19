using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;

namespace YinYanMusic.Api.Controllers;

/// <summary>
/// 后台管理端点。⚠️ 需要 <c>Role=admin</c> 的 JWT
/// （角色来自 <c>Users.Role</c> 列，登录时写进 token 的 <c>ClaimTypes.Role</c>）。
/// 之前这三个端点是**完全裸奔**的：谁能访问到端口谁就能导入/删歌，故补上鉴权。
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController(IAdminService admin) : ControllerBase
{
    [HttpPost("import")]
    public async Task<ActionResult> ImportFromDirectory([FromQuery] string? dir = null)
    {
        var (total, imported, error) = await admin.ImportAsync(dir);
        if (error is not null) return BadRequest(new { message = error });
        return Ok(new { total, imported, skipped = total - imported });
    }

    [HttpDelete("seed-songs")]
    public async Task<ActionResult> DeleteSeedSongs()
        => Ok(new { deleted = await admin.DeleteSeedSongsAsync() });

    [HttpPost("scan-durations")]
    public async Task<ActionResult> ScanDurations()
    {
        var (total, updated, error) = await admin.ScanDurationsAsync();
        if (error is not null) return BadRequest(new { message = error });
        return Ok(new { total, updated });
    }
}
