using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

/// <summary>
/// 后台仪表盘数据统计（V2.16）。只读聚合，供 Dashboard 页展示。
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
        => Ok(await dashboard.GetStatsAsync());
}