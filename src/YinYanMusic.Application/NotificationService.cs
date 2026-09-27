using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface INotificationService
{
    /// <summary>自己的通知分页：未读在前，再按创建时间倒序。</summary>
    Task<ServiceResult<PagedResult<NotificationDto>>> ListAsync(long userId, int page, int pageSize);

    /// <summary>未读数（角标 / 初始值）。</summary>
    Task<ServiceResult<int>> UnreadCountAsync(long userId);

    /// <summary>标记单条已读。非本人接收行 → NotFound 语义。</summary>
    Task<ServiceResult> MarkReadAsync(long userId, long notificationId);

    /// <summary>一键全部已读，返回本次标已读的条数。</summary>
    Task<ServiceResult<int>> MarkAllReadAsync(long userId);

    /// <summary>后台发送通知（全部 / 部分 / 单个）。</summary>
    Task<ServiceResult<AdminNotificationDto>> AdminSendAsync(long adminId, SendNotificationRequest req);

    /// <summary>后台已发送列表分页。</summary>
    Task<ServiceResult<PagedResult<AdminNotificationDto>>> AdminListAsync(string? keyword, int page, int pageSize);
}

/// <summary>
/// 实时推送抽象：服务层不直接依赖 SignalR，单测可换成 no-op / 收集桩。
/// </summary>
public interface INotificationBroadcaster
{
    Task PushNotificationAsync(long userId, NotificationDto notification, int unreadCount);
    Task PushUnreadCountAsync(long userId, int unreadCount);
}

/// <summary>默认空实现：未注册 SignalR 时发送不炸，只是没有实时通道。</summary>
public sealed class NoopNotificationBroadcaster : INotificationBroadcaster
{
    public Task PushNotificationAsync(long userId, NotificationDto notification, int unreadCount) => Task.CompletedTask;
    public Task PushUnreadCountAsync(long userId, int unreadCount) => Task.CompletedTask;
}

/// <summary>
/// 站内通知服务（V2.15）。发送时按目标范围物化 <see cref="NotificationRecipient"/> 行，
/// 再经 <see cref="INotificationBroadcaster"/> 推给在线用户；读路径只查自己的接收行。
/// </summary>
public class NotificationService(MusicDbContext db, INotificationBroadcaster broadcaster) : INotificationService
{
    public const string NotificationNotFound = "通知不存在。";
    public const string InvalidTarget = "目标范围不正确。";
    public const string EmptyRecipients = "请至少选择一个接收用户。";
    public const string TitleTooLong = "标题不能超过 100 字。";
    public const string ContentTooLong = "正文不能超过 500 字。";
    public const string EmptyTitle = "标题不能为空。";
    public const string EmptyContent = "正文不能为空。";

    public const int MaxTitleLength = 100;
    public const int MaxContentLength = 500;
    private const int MaxPageSize = 100;

    public async Task<ServiceResult<PagedResult<NotificationDto>>> ListAsync(long userId, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.NotificationRecipients
            .Where(r => r.UserId == userId);

        var total = await query.CountAsync();

        // 未读在前（0/1 排序），再按创建时间倒序、Id 兜底保证翻页稳定。
        var items = await query
            .OrderBy(r => r.IsRead ? 1 : 0)
            .ThenByDescending(r => r.Notification.CreatedAtUtc)
            .ThenByDescending(r => r.NotificationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new NotificationDto(
                r.Notification.Id,
                r.Notification.Title,
                r.Notification.Content,
                r.IsRead,
                r.Notification.RelatedSongId,
                r.Notification.RelatedPlaylistId,
                r.Notification.CreatedAtUtc,
                r.ReadAtUtc,
                r.Notification.RelatedAlbumId))
            .ToListAsync();

        return ServiceResult<PagedResult<NotificationDto>>.Ok(
            new PagedResult<NotificationDto>(items, total, page, pageSize));
    }

    public async Task<ServiceResult<int>> UnreadCountAsync(long userId)
    {
        var count = await db.NotificationRecipients
            .CountAsync(r => r.UserId == userId && !r.IsRead);
        return ServiceResult<int>.Ok(count);
    }

    public async Task<ServiceResult> MarkReadAsync(long userId, long notificationId)
    {
        var row = await db.NotificationRecipients
            .FirstOrDefaultAsync(r => r.NotificationId == notificationId && r.UserId == userId);
        if (row is null) return ServiceResult.Fail(NotificationNotFound);

        if (!row.IsRead)
        {
            row.IsRead = true;
            row.ReadAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var unread = await UnreadCountAsync(userId);
        await broadcaster.PushUnreadCountAsync(userId, unread.Success ? unread.Data : 0);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<int>> MarkAllReadAsync(long userId)
    {
        var rows = await db.NotificationRecipients
            .Where(r => r.UserId == userId && !r.IsRead)
            .ToListAsync();

        if (rows.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var row in rows)
            {
                row.IsRead = true;
                row.ReadAtUtc = now;
            }
            await db.SaveChangesAsync();
        }

        await broadcaster.PushUnreadCountAsync(userId, 0);
        return ServiceResult<int>.Ok(rows.Count);
    }

    public async Task<ServiceResult<AdminNotificationDto>> AdminSendAsync(long adminId, SendNotificationRequest req)
    {
        var title = (req.Title ?? string.Empty).Trim();
        var content = (req.Content ?? string.Empty).Trim();
        if (title.Length == 0) return ServiceResult<AdminNotificationDto>.Fail(EmptyTitle);
        if (content.Length == 0) return ServiceResult<AdminNotificationDto>.Fail(EmptyContent);
        if (title.Length > MaxTitleLength) return ServiceResult<AdminNotificationDto>.Fail(TitleTooLong);
        if (content.Length > MaxContentLength) return ServiceResult<AdminNotificationDto>.Fail(ContentTooLong);
        if (!NotificationTargets.IsValid(req.TargetType))
            return ServiceResult<AdminNotificationDto>.Fail(InvalidTarget);

        List<long> targetIds;
        if (req.TargetType == NotificationTargets.All)
        {
            // 初版：发送时按当前用户列表批量物化接收行（设计 3.15.3）。
            targetIds = await db.Users
                .Where(u => !u.IsDisabled)
                .Select(u => u.Id)
                .ToListAsync();
            if (targetIds.Count == 0)
                return ServiceResult<AdminNotificationDto>.Fail(EmptyRecipients);
        }
        else
        {
            var raw = (req.RecipientUserIds ?? []).Distinct().Where(id => id > 0).ToList();
            // single 只接受恰好一个目标（partial 至少一个）
            if (req.TargetType == NotificationTargets.Single && raw.Count != 1)
                return ServiceResult<AdminNotificationDto>.Fail(
                    req.TargetType == NotificationTargets.Single && raw.Count > 1
                        ? "单个推送只能选择一个接收用户。"
                        : EmptyRecipients);
            if (raw.Count == 0)
                return ServiceResult<AdminNotificationDto>.Fail(EmptyRecipients);

            // 只保留仍存在且未停用的账号，避免给幽灵 Id 建行。
            targetIds = await db.Users
                .Where(u => raw.Contains(u.Id) && !u.IsDisabled)
                .Select(u => u.Id)
                .ToListAsync();
            if (targetIds.Count == 0)
                return ServiceResult<AdminNotificationDto>.Fail(EmptyRecipients);
        }

        var notification = new Notification
        {
            Title = title,
            Content = content,
            TargetType = req.TargetType,
            RecipientUserIds = req.TargetType == NotificationTargets.All
                ? null
                : JsonSerializer.Serialize(targetIds.OrderBy(x => x).ToList()),
            RelatedSongId = req.RelatedSongId,
            RelatedPlaylistId = req.RelatedPlaylistId,
            RelatedAlbumId = req.RelatedAlbumId,
            Status = NotificationStatuses.Sending,
            CreatedById = adminId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        foreach (var userId in targetIds)
        {
            notification.Recipients.Add(new NotificationRecipient { UserId = userId });
        }

        db.Notifications.Add(notification);

        try
        {
            await db.SaveChangesAsync();
            notification.Status = NotificationStatuses.Completed;
            notification.SentAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch
        {
            notification.Status = NotificationStatuses.Failed;
            try { await db.SaveChangesAsync(); } catch { /* 最后一次尝试：失败态写不进去也不能再抛 */ }
            return ServiceResult<AdminNotificationDto>.Fail("发送失败，请稍后重试。");
        }

        // 在线用户实时推送（离线的下次拉列表 / 未读数即可补齐）
        var dto = new NotificationDto(
            notification.Id, notification.Title, notification.Content, IsRead: false,
            notification.RelatedSongId, notification.RelatedPlaylistId, notification.CreatedAtUtc,
            null, notification.RelatedAlbumId);

        foreach (var userId in targetIds)
        {
            var unread = await UnreadCountAsync(userId);
            await broadcaster.PushNotificationAsync(userId, dto, unread.Success ? unread.Data : 0);
        }

        // 发送人展示名：用 DisplayName（后台列表也走同一口径）；
        // 账号已被删时退化成 Id 字符串，总比空着好判断。
        var adminName = await db.Users
            .Where(u => u.Id == adminId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync();

        var adminDto = ToAdminDto(notification, adminName,
            recipientCount: targetIds.Count, readCount: 0);
        return ServiceResult<AdminNotificationDto>.Ok(adminDto);
    }

    public async Task<ServiceResult<PagedResult<AdminNotificationDto>>> AdminListAsync(string? keyword, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Notifications.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            // 搜索一律 ILike + 显式转义符（全局约束第 3 条 / LikePattern.EscapeChar）
            query = query.Where(n =>
                EF.Functions.ILike(n.Title, LikePattern.Contains(kw), LikePattern.EscapeChar) ||
                EF.Functions.ILike(n.Content, LikePattern.Contains(kw), LikePattern.EscapeChar));
        }

        var total = await query.CountAsync();

        var rows = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new AdminListRow(
                n,
                n.Recipients.Count,
                n.Recipients.Count(r => r.IsRead),
                db.Users
                    .Where(u => u.Id == n.CreatedById)
                    .Select(u => u.DisplayName)
                    .FirstOrDefault()))
            .ToListAsync();

        var items = rows
            .Select(r => ToAdminDto(r.Notification, r.CreatedByName, r.RecipientCount, r.ReadCount))
            .ToList();

        return ServiceResult<PagedResult<AdminNotificationDto>>.Ok(
            new PagedResult<AdminNotificationDto>(items, total, page, pageSize));
    }

    private sealed record AdminListRow(Notification Notification, int RecipientCount, int ReadCount, string? CreatedByName);

    private static AdminNotificationDto ToAdminDto(
        Notification n, string? createdByName, int? recipientCount = null, int? readCount = null)
    {
        var ids = string.IsNullOrWhiteSpace(n.RecipientUserIds)
            ? []
            : JsonSerializer.Deserialize<List<long>>(n.RecipientUserIds) ?? [];

        return new AdminNotificationDto(
            n.Id, n.Title, n.Content, n.TargetType, ids,
            n.RelatedSongId, n.RelatedPlaylistId, n.RelatedAlbumId, n.Status,
            n.CreatedById,
            // 发送人已被删（无 FK，属预期情况）→ 退化成 Id 字符串，避免列表出现空白列
            string.IsNullOrWhiteSpace(createdByName)
                ? n.CreatedById.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : createdByName,
            n.CreatedAtUtc, n.SentAtUtc,
            recipientCount ?? n.Recipients.Count,
            readCount ?? n.Recipients.Count(r => r.IsRead));
    }
}
