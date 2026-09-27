using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

/// <summary>
/// 站内通知（V2.15，用户侧）。全部要求登录 JWT：未登录 401（TC-2.15-07）。
/// 非本人接收行标记已读 → 404（不泄露他人通知是否存在）。
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(INotificationService notifications, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>分页拉取自己的通知（未读在前）。</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await notifications.ListAsync(currentUser.RequireUserId(), page, pageSize);
        return result.Success && result.Data is not null
            ? Ok(result.Data)
            : BadRequest(new { message = result.Error });
    }

    /// <summary>未读数（角标轮询 / 初始值）。</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount()
    {
        // ServiceResult<int>.Data 是 int（失败时为 0），不能按 int? 解 .Value
        var result = await notifications.UnreadCountAsync(currentUser.RequireUserId());
        return result.Success
            ? Ok(new UnreadCountDto(result.Data))
            : BadRequest(new { message = result.Error });
    }

    /// <summary>标记单条已读。非本人接收行 → 404。</summary>
    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var result = await notifications.MarkReadAsync(currentUser.RequireUserId(), id);
        if (result.Success) return NoContent();
        if (result.Error == NotificationService.NotificationNotFound)
            return NotFound(new { message = result.Error });
        return BadRequest(new { message = result.Error });
    }

    /// <summary>一键全部已读。返回本次标已读的条数。</summary>
    [HttpPost("read-all")]
    public async Task<ActionResult<UnreadCountDto>> MarkAllRead()
    {
        var result = await notifications.MarkAllReadAsync(currentUser.RequireUserId());
        if (result.Success) return Ok(new UnreadCountDto(0));
        return BadRequest(new { message = result.Error });
    }
}
