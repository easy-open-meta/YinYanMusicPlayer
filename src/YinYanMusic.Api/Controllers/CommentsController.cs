using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

/// <summary>
/// 评论接口（V2.9）。列表公开可读，其余动作要求登录。
/// </summary>
[ApiController]
[Route("api/comments")]
public class CommentsController(ICommentService comments, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>
    /// 评论列表：主评论分页，每条带一层回复。
    /// <para>
    /// 未登录也能读（<see cref="ICurrentUserService.UserId"/> 为 null）。此时被后台隐藏的评论
    /// 一律不返回；登录后作者仍能看到自己被隐藏的那条（TC-2.9-08）。
    /// </para>
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CommentDto>>> List(
        [FromQuery] string targetType,
        [FromQuery] long targetId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await comments.ListAsync(targetType, targetId, currentUser.UserId, page, pageSize);
        // 只有"类型不对"会失败 —— 目标不存在是空列表，不是错误
        return result.Success && result.Data is not null
            ? Ok(result.Data)
            : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// 某条主评论下**全部回复**的扁平分页：列表接口每条主评论只带回 200 条子孙，
    /// 超出的部分由客户端用这个接口"展开全部"（分页追加）。
    /// <para>
    /// 返回项一律 <c>Replies</c> 为空 —— 它们是扁平的，但带 <c>ParentId</c> 与 <c>Depth</c>，
    /// 且按 Id 升序（父节点一定先出现），调用方扫一遍就能自建层级。
    /// </para>
    /// </summary>
    [HttpGet("{id:long}/replies")]
    public async Task<ActionResult<PagedResult<CommentDto>>> ListReplies(
        long id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var result = await comments.ListThreadRepliesAsync(id, currentUser.UserId, page, pageSize);
        return result.Success && result.Data is not null
            ? Ok(result.Data)
            : NotFound(new { message = result.Error });
    }

    /// <summary>
    /// 可见评论**总数（主评论 + 回复）**：App 的「更多」菜单要用它拼「查看评论：(N)」，
    /// 面板标题也用它。与列表接口的 <c>total</c>（只数主评论、回复跟着主评论分页）不是一个口径。
    /// </summary>
    [HttpGet("count")]
    public async Task<ActionResult> Count([FromQuery] string targetType, [FromQuery] long targetId)
    {
        var result = await comments.CountAsync(targetType, targetId, currentUser.UserId);
        return result.Success
            ? Ok(new { count = result.Data })
            : BadRequest(new { message = result.Error });
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(CreateCommentRequest req)
    {
        var result = await comments.CreateAsync(currentUser.RequireUserId(), req);
        if (result.Success) return Ok(result.Data);

        return result.Error switch
        {
            CommentService.TargetNotFound => NotFound(new { message = result.Error }),
            CommentService.CommentNotFound => NotFound(new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }

    [Authorize]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
        => MapResult(await comments.DeleteAsync(currentUser.RequireUserId(), id));

    [Authorize]
    [HttpPost("{id:long}/like")]
    public async Task<IActionResult> Like(long id)
        => MapLike(await comments.LikeAsync(currentUser.RequireUserId(), id));

    [Authorize]
    [HttpDelete("{id:long}/like")]
    public async Task<IActionResult> Unlike(long id)
        => MapLike(await comments.UnlikeAsync(currentUser.RequireUserId(), id));

    private IActionResult MapResult(ServiceResult r) => r.Error switch
    {
        CommentService.CommentNotFound => NotFound(new { message = r.Error }),
        CommentService.NoPermission => Forbid(),
        _ => r.Success ? NoContent() : BadRequest(new { message = r.Error })
    };

    private IActionResult MapLike(ServiceResult<CommentLikeState> r) => r.Error switch
    {
        CommentService.CommentNotFound => NotFound(new { message = r.Error }),
        _ => r.Success ? Ok(r.Data) : BadRequest(new { message = r.Error })
    };
}
