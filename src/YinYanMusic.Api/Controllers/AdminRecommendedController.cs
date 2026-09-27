using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

/// <summary>
/// 后台「推荐位管理」（V2.12）。与其它后台接口一样只对 admin 角色开放（全局约束第 8 条），
/// 路由前缀 <c>api/admin/</c> 与 AdminCommentsController 一致。
/// </summary>
[ApiController]
[Route("api/admin/recommended")]
[Authorize(Roles = "admin")]
public class AdminRecommendedController(IRecommendService recommend) : ControllerBase
{
    /// <summary>
    /// 全部歌单（含从未被推荐的），带各自的人工干预状态。后台不过滤系统歌单 ——
    /// 审核视角要看全量，包括"我喜欢的音乐"这种永远不会被推荐的。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminRecommendedDto>>> List(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await recommend.AdminListAsync(keyword, page, pageSize));

    /// <summary>
    /// 设置推荐位：置顶<paramref name="req"/>的 SortOrder、加权 Weight、下线 IsHidden 一次性提交。
    /// 歌单还没被干预过就新建一行（幂等：重复提交是覆盖，不会插出第二行 —— 主键即 PlaylistId）。
    /// </summary>
    [HttpPut("{playlistId:long}")]
    public async Task<IActionResult> Set(long playlistId, UpdateRecommendedRequest req)
        => MapResult(await recommend.AdminSetAsync(playlistId, req));

    /// <summary>移除人工干预，该歌单回到纯规则排序。注意与"下线"是两回事：下线保留干预行。</summary>
    [HttpDelete("{playlistId:long}")]
    public async Task<IActionResult> Remove(long playlistId)
        => MapResult(await recommend.AdminRemoveAsync(playlistId));

    private IActionResult MapResult(ServiceResult r) => r.Error switch
    {
        RecommendService.PlaylistNotFound => NotFound(new { message = r.Error }),
        RecommendService.NoOverride => NotFound(new { message = r.Error }),
        _ => r.Success ? NoContent() : BadRequest(new { message = r.Error })
    };
}
