using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

/// <summary>
/// 后台消息推送（V2.15）。与其它后台接口一样只对 admin 角色开放（全局约束第 8 条）。
/// </summary>
[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = "admin")]
public class AdminNotificationsController(INotificationService notifications, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>
    /// 发送通知。范围：全部 / 部分 / 单个用户（<c>targetType</c> = all / partial / single）。
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AdminNotificationDto>> Send(SendNotificationRequest req)
    {
        var result = await notifications.AdminSendAsync(currentUser.RequireUserId(), req);
        if (result.Success && result.Data is not null) return Ok(result.Data);

        return result.Error switch
        {
            NotificationService.InvalidTarget => BadRequest(new { message = result.Error }),
            NotificationService.EmptyRecipients => BadRequest(new { message = result.Error }),
            NotificationService.EmptyTitle => BadRequest(new { message = result.Error }),
            NotificationService.EmptyContent => BadRequest(new { message = result.Error }),
            NotificationService.TitleTooLong => BadRequest(new { message = result.Error }),
            NotificationService.ContentTooLong => BadRequest(new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }

    /// <summary>已发送列表（分页，按时间倒序）。可按标题/正文关键词筛。</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminNotificationDto>>> List(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await notifications.AdminListAsync(keyword, page, pageSize);
        return result.Success && result.Data is not null
            ? Ok(result.Data)
            : BadRequest(new { message = result.Error });
    }
}
