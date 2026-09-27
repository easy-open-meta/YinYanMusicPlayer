using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Hubs;

/// <summary>
/// 通知实时通道（V2.15）。路径 <c>/hubs/notifications</c>，JWT 走
/// <c>?access_token=</c> 查询串（SignalR 标准做法，见 Program.cs 的 OnMessageReceived）。
/// </summary>
[Authorize]
public class NotificationsHub(INotificationService notifications) : Hub
{
    /// <summary>当前连接对应的用户 Id（由 JWT NameIdentifier 声明给出）。</summary>
    private long UserId
    {
        get
        {
            var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Context.User?.FindFirstValue("sub");
            if (long.TryParse(raw, out var id)) return id;
            throw new HubException("未登录或令牌无效。");
        }
    }

    /// <summary>
    /// 客户端连上后主动同步一次未读数。断线窗口里丢掉的实时事件无法重放，
    /// 列表与未读数以 REST / 本方法为准（TC-2.15-08 补齐遗漏）。
    /// </summary>
    public async Task<int> GetUnreadCount()
    {
        var result = await notifications.UnreadCountAsync(UserId);
        return result.Success ? result.Data : 0;
    }
}

/// <summary>
/// 基于 <see cref="IHubContext{T}"/> 的广播实现：按 UserId 分组投递
/// （连接建立时把 <c>ClaimTypes.NameIdentifier</c> 映射到 Hub 用户，<c>Clients.User</c> 才有效）。
/// </summary>
public class SignalRNotificationBroadcaster(IHubContext<NotificationsHub> hub) : INotificationBroadcaster
{
    public const string NotificationReceived = "NotificationReceived";
    public const string UnreadCountChanged = "UnreadCountChanged";

    public Task PushNotificationAsync(long userId, NotificationDto notification, int unreadCount)
    {
        var payload = new NotificationPush(notification, unreadCount);
        return hub.Clients.User(userId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .SendAsync(NotificationReceived, payload, CancellationToken.None);
    }

    public Task PushUnreadCountAsync(long userId, int unreadCount)
    {
        return hub.Clients.User(userId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .SendAsync(UnreadCountChanged, unreadCount, CancellationToken.None);
    }

    /// <summary>SignalR 推给 App 的载荷：新通知本体 + 当前未读数（角标一次到位）。</summary>
    public record NotificationPush(NotificationDto Notification, int UnreadCount);
}
