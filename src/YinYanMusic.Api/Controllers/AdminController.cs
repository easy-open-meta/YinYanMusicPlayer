using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;

namespace YinYanMusic.Api.Controllers;

[ApiController]
[Route("api/admin")]
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
