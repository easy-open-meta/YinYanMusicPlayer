using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

/// <summary>
/// 推荐歌单专区（V2.12）。
///
/// <para>**刻意不加 <c>[Authorize]</c>**：发现页的推荐专区在未登录时也要能看
/// （未登录时 <c>for-you</c> 退化热播，见 RecommendService），
/// 让游客点进来看得到东西，是这一版做"分发入口"的前提。</para>
/// </summary>
[ApiController]
[Route("api/recommend")]
public class RecommendController(IRecommendService recommend, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>
    /// <paramref name="scene"/>：hot / collected / new / for-you / zone（未知值落到 hot）。
    /// <paramref name="categoryId"/>：仅 <c>zone</c> 场景使用，缺失时返回空列表。
    /// 登录态只影响 for-you 的个性化程度 —— 带了 token 就按口味加权，没带就按热播。
    /// </summary>
    [HttpGet("playlists")]
    public async Task<ActionResult<PagedResult<PlaylistDto>>> Playlists(
        [FromQuery] string? scene,
        [FromQuery] int? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await recommend.GetPlaylistsAsync(scene, categoryId, currentUser.UserId, page, pageSize));
}
