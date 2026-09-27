using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

/// <summary>
/// 后台评论审核（V2.9）。与其它后台接口一样，只对 admin 角色开放（全局约束第 8 条）。
/// </summary>
[ApiController]
[Route("api/admin/comments")]
[Authorize(Roles = "admin")]
public class AdminCommentsController(ICommentService comments) : ControllerBase
{
    /// <summary>
    /// 评论列表（后台）。可按正文关键词、对象类型、隐藏状态筛。
    /// 与 App 的接口不同，这里**不做隐藏过滤** —— 审核就是要看到全量，包括已隐藏的。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminCommentDto>>> List(
        [FromQuery] string? keyword,
        [FromQuery] string? targetType,
        [FromQuery] bool? isHidden,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await comments.AdminListAsync(keyword, targetType, isHidden, page, pageSize);
        return result.Success && result.Data is not null
            ? Ok(result.Data)
            : BadRequest(new { message = result.Error });
    }

    /// <summary>隐藏 / 恢复。软隐藏，随时可恢复（TC-2.9-08/09）。</summary>
    [HttpPut("{id:long}/hidden")]
    public async Task<IActionResult> SetHidden(long id, [FromQuery] bool hidden)
        => MapResult(await comments.AdminSetHiddenAsync(id, hidden));

    /// <summary>物理删除（连回复与点赞一起）。隐藏解决不了的内容才用它。</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
        => MapResult(await comments.AdminDeleteAsync(id));

    private IActionResult MapResult(ServiceResult r) => r.Error switch
    {
        CommentService.CommentNotFound => NotFound(new { message = r.Error }),
        _ => r.Success ? NoContent() : BadRequest(new { message = r.Error })
    };
}
